using System;
using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;

namespace EDM.ControlPlane.Api.Services
{
    public interface IPasswordHasher
    {
        string HashPassword(string password);
        bool VerifyPassword(string password, string hashedPassword);
    }

    /// <summary>
    /// Modern Argon2id Password Hasher providing enterprise-grade security against GPU/ASIC attacks.
    /// Format: $argon2id$v=19$m=65536,t=3,p=4$<salt>$<hash>
    /// </summary>
    public class Argon2idPasswordHasher : IPasswordHasher
    {
        private const int SaltSize = 16;      // 128 bits
        private const int HashSize = 32;      // 256 bits
        private const int DegreeOfParallelism = 4; // 4 threads
        private const int MemorySize = 65536; // 64 MB
        private const int Iterations = 3;

        public string HashPassword(string password)
        {
            if (string.IsNullOrEmpty(password)) throw new ArgumentNullException(nameof(password));

            byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
            byte[] passwordBytes = Encoding.UTF8.GetBytes(password);

            using var argon2 = new Argon2id(passwordBytes)
            {
                Salt = salt,
                DegreeOfParallelism = DegreeOfParallelism,
                MemorySize = MemorySize,
                Iterations = Iterations
            };

            byte[] hash = argon2.GetBytes(HashSize);

            string saltB64 = Convert.ToBase64String(salt);
            string hashB64 = Convert.ToBase64String(hash);

            return $"$argon2id$v=19$m={MemorySize},t={Iterations},p={DegreeOfParallelism}${saltB64}${hashB64}";
        }

        public bool VerifyPassword(string password, string hashedPassword)
        {
            if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(hashedPassword)) return false;

            try
            {
                var parts = hashedPassword.Split('$');
                if (parts.Length != 6 || parts[1] != "argon2id") return false;

                byte[] salt = Convert.FromBase64String(parts[4]);
                byte[] expectedHash = Convert.FromBase64String(parts[5]);
                byte[] passwordBytes = Encoding.UTF8.GetBytes(password);

                using var argon2 = new Argon2id(passwordBytes)
                {
                    Salt = salt,
                    DegreeOfParallelism = DegreeOfParallelism,
                    MemorySize = MemorySize,
                    Iterations = Iterations
                };

                byte[] actualHash = argon2.GetBytes(expectedHash.Length);

                return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
            }
            catch
            {
                return false;
            }
        }
    }
}
