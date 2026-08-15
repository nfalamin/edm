#!/usr/bin/env dotnet-script
#r "nuget: System.Security.Cryptography.Cng"

using System;
using System.Security.Cryptography;
using System.Text;
using System.Linq;

// Generate RSA keypair
using (var rsa = new RSACng(2048))
{
    // Export public key in DER format
    byte[] publicKeyDer = rsa.ExportSubjectPublicKeyInfo();
    string publicKeyBase64 = Convert.ToBase64String(publicKeyDer);

    Console.WriteLine("=== Chrome Extension RSA Key ===");
    Console.WriteLine("Public Key (for manifest.json 'key' field):");
    Console.WriteLine(publicKeyBase64);
    Console.WriteLine();

    // Export private key for backup (if needed)
    byte[] privateKeyDer = rsa.ExportPkcs8PrivateKey();
    string privateKeyBase64 = Convert.ToBase64String(privateKeyDer);
    Console.WriteLine("Private Key (KEEP SECURE - for signing updates):");
    Console.WriteLine(privateKeyBase64);
    Console.WriteLine();

    // Calculate extension ID based on Chrome's algorithm
    // Chrome extension ID = base16(sha256(publicKey)) first 16 chars, mapped to a-p
    using (var sha256 = SHA256.Create())
    {
        byte[] hash = sha256.ComputeHash(publicKeyDer);
        // Chrome extension ID uses a-p (0-15) encoding of the hash
        StringBuilder extensionId = new StringBuilder();
        for (int i = 0; i < 32; i++)  // 32 chars = 16 bytes * 2
        {
            int nibble = (hash[i / 2] >> ((i % 2 == 0) ? 4 : 0)) & 0xF;
            extensionId.Append((char)('a' + nibble));
        }

        string chromeExtensionId = extensionId.ToString().Substring(0, 32);
        Console.WriteLine("Derived Extension ID:");
        Console.WriteLine(chromeExtensionId);
        Console.WriteLine();
        Console.WriteLine("AllowedOrigin for native messaging:");
        Console.WriteLine($"chrome-extension://{chromeExtensionId}/");
    }
}
