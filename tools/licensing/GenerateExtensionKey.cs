using System;
using System.Security.Cryptography;
using System.Text;
using System.Linq;
using System.Collections.Generic;

class Program
{
    static void Main()
    {
        // Generate RSA keypair (2048-bit)
        var rsa = new RSACryptoServiceProvider(2048);

        // Get XML format and manually extract key
        string xmlKey = rsa.ToXmlString(false);  // false = public key only
        Console.WriteLine("=== Chrome Extension RSA Key Configuration ===\n");
        Console.WriteLine("Public Key XML:");
        Console.WriteLine(xmlKey);
        Console.WriteLine("\n" + new string('-', 60) + "\n");

        // Export public key to DER format for ID calculation
        RSAParameters rsaParams = rsa.ExportParameters(false);
        byte[] publicKeyBytes = ExportRSAPublicKeyDER(rsaParams);
        string publicKeyBase64 = Convert.ToBase64String(publicKeyBytes);

        Console.WriteLine("Public Key (Base64 DER for manifest.json 'key'):");
        Console.WriteLine(publicKeyBase64);
        Console.WriteLine("\n" + new string('-', 60) + "\n");

        // Calculate Chrome extension ID (SHA256 hash of public key)
        using (var sha256 = SHA256.Create())
        {
            byte[] hash = sha256.ComputeHash(publicKeyBytes);

            // Chrome maps each nibble to a-p (0-15 -> a-p)
            StringBuilder extensionId = new StringBuilder();
            for (int i = 0; i < 16; i++)  // 16 bytes, each produces 2 chars
            {
                byte b = hash[i];
                extensionId.Append((char)('a' + ((b >> 4) & 0xF)));  // High nibble
                extensionId.Append((char)('a' + (b & 0xF)));          // Low nibble
            }

            string chromeExtensionId = extensionId.ToString();
            Console.WriteLine("Derived Chrome Extension ID:");
            Console.WriteLine(chromeExtensionId);
            Console.WriteLine("\nAllowedOrigin for native messaging (in installer):");
            Console.WriteLine($"chrome-extension://{chromeExtensionId}/");
        }

        rsa.Dispose();
    }

    // Encode RSA public key to DER format (SubjectPublicKeyInfo)
    static byte[] ExportRSAPublicKeyDER(RSAParameters rsaParams)
    {
        // This is a simplified DER encoding for RSA public key
        // Format: SEQUENCE { SEQUENCE { OID, NULL }, BIT STRING { SEQUENCE { INTEGER, INTEGER } } }

        byte[] modulusBytes = rsaParams.Modulus;
        byte[] exponentBytes = rsaParams.Exponent;

        // Ensure high bit is not set (add 0x00 if needed for positive integer encoding)
        if ((modulusBytes[0] & 0x80) != 0)
            modulusBytes = new byte[1] { 0x00 }.Concat(modulusBytes).ToArray();
        if ((exponentBytes[0] & 0x80) != 0)
            exponentBytes = new byte[1] { 0x00 }.Concat(exponentBytes).ToArray();

        // Encode SEQUENCE of modulus and exponent
        byte[] keySequence = EncodeSequence(
            EncodeInteger(modulusBytes),
            EncodeInteger(exponentBytes)
        );

        // Encode BIT STRING of the sequence
        byte[] bitString = new byte[1] { 0x00 }.Concat(keySequence).ToArray();
        byte[] encodedBitString = EncodeDER(0x03, bitString);

        // Encode algorithm identifier (1.2.840.113549.1.1.1 = RSA encryption)
        byte[] algorithmIdentifier = EncodeSequence(
            EncodeDER(0x06, new byte[] { 0x2a, 0x86, 0x48, 0x86, 0xf7, 0x0d, 0x01, 0x01, 0x01 }),
            EncodeDER(0x05, new byte[] { })  // NULL
        );

        // Encode the final SubjectPublicKeyInfo
        byte[] subjectPublicKeyInfo = EncodeSequence(
            algorithmIdentifier,
            encodedBitString
        );

        return subjectPublicKeyInfo;
    }

    static byte[] EncodeSequence(params byte[][] items)
    {
        int length = items.Sum(x => x.Length);
        byte[] result = EncodeDER(0x30, items.SelectMany(x => x).ToArray());
        return result;
    }

    static byte[] EncodeInteger(byte[] value)
    {
        // Ensure leading zero if high bit is set
        if ((value[0] & 0x80) != 0)
            value = new byte[1] { 0x00 }.Concat(value).ToArray();
        return EncodeDER(0x02, value);
    }

    static byte[] EncodeDER(byte tag, byte[] value)
    {
        byte[] lengthBytes;
        if (value.Length < 128)
        {
            lengthBytes = new byte[] { (byte)value.Length };
        }
        else
        {
            // Long form: 0x81 for 1 byte length, 0x82 for 2 bytes, etc.
            byte[] lenBytes = BitConverter.GetBytes(value.Length);
            Array.Reverse(lenBytes);  // Big-endian
            lengthBytes = new byte[1] { (byte)(0x80 | lenBytes.Length) }.Concat(lenBytes).ToArray();
        }

        return new byte[] { tag }.Concat(lengthBytes).Concat(value).ToArray();
    }
}
