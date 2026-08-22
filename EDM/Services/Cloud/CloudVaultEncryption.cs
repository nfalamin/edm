using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace EDM.Services.Cloud
{
    /// <summary>
    /// Zero-Knowledge Client-Side AES-256-GCM Vault Encryption Engine.
    /// Ensures all cloud-synced download histories, custom rules, and site credentials
    /// are encrypted locally before transmission, guaranteeing EDM servers cannot inspect raw data.
    /// </summary>
    public static class CloudVaultEncryption
    {
        private const int KeySizeBytes = 32; // 256-bit
        private const int NonceSizeBytes = 12; // 96-bit for AES-GCM
        private const int TagSizeBytes = 16; // 128-bit authentication tag
        private const int SaltSizeBytes = 16;
        private const int Pbkdf2Iterations = 100_000;

        public static byte[] Encrypt(byte[] plainBytes, string passphrase)
        {
            if (plainBytes == null) throw new ArgumentNullException(nameof(plainBytes));
            if (string.IsNullOrEmpty(passphrase)) throw new ArgumentException("Passphrase cannot be empty", nameof(passphrase));

            byte[] salt = new byte[SaltSizeBytes];
            RandomNumberGenerator.Fill(salt);

            byte[] key = DeriveKey(passphrase, salt);
            byte[] nonce = new byte[NonceSizeBytes];
            RandomNumberGenerator.Fill(nonce);

            byte[] ciphertext = new byte[plainBytes.Length];
            byte[] tag = new byte[TagSizeBytes];

            using (var aesGcm = new AesGcm(key, TagSizeBytes))
            {
                aesGcm.Encrypt(nonce, plainBytes, ciphertext, tag);
            }

            // Output format: [Salt 16B] + [Nonce 12B] + [Tag 16B] + [Ciphertext NB]
            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms);
            bw.Write(salt);
            bw.Write(nonce);
            bw.Write(tag);
            bw.Write(ciphertext);

            return ms.ToArray();
        }

        public static byte[] Decrypt(byte[] encryptedPayload, string passphrase)
        {
            if (encryptedPayload == null) throw new ArgumentNullException(nameof(encryptedPayload));
            if (string.IsNullOrEmpty(passphrase)) throw new ArgumentException("Passphrase cannot be empty", nameof(passphrase));

            if (encryptedPayload.Length < SaltSizeBytes + NonceSizeBytes + TagSizeBytes)
            {
                throw new CryptographicException("Corrupted or invalid encrypted payload.");
            }

            using var ms = new MemoryStream(encryptedPayload);
            using var br = new BinaryReader(ms);

            byte[] salt = br.ReadBytes(SaltSizeBytes);
            byte[] nonce = br.ReadBytes(NonceSizeBytes);
            byte[] tag = br.ReadBytes(TagSizeBytes);
            int cipherLength = (int)(ms.Length - ms.Position);
            byte[] ciphertext = br.ReadBytes(cipherLength);

            byte[] key = DeriveKey(passphrase, salt);
            byte[] decrypted = new byte[cipherLength];

            using (var aesGcm = new AesGcm(key, TagSizeBytes))
            {
                aesGcm.Decrypt(nonce, ciphertext, tag, decrypted);
            }

            return decrypted;
        }

        public static string EncryptString(string plainText, string passphrase)
        {
            byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
            byte[] enc = Encrypt(plainBytes, passphrase);
            return Convert.ToBase64String(enc);
        }

        public static string DecryptString(string base64Payload, string passphrase)
        {
            byte[] enc = Convert.FromBase64String(base64Payload);
            byte[] dec = Decrypt(enc, passphrase);
            return Encoding.UTF8.GetString(dec);
        }

        private static byte[] DeriveKey(string passphrase, byte[] salt)
        {
            return Rfc2898DeriveBytes.Pbkdf2(
                Encoding.UTF8.GetBytes(passphrase),
                salt,
                Pbkdf2Iterations,
                HashAlgorithmName.SHA256,
                KeySizeBytes);
        }
    }
}
