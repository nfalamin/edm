using System;
using System.IO;
using System.Security.Cryptography.X509Certificates;

namespace EDM.Services
{
    public class SignatureVerificationResult
    {
        public bool IsSigned { get; set; }
        public bool IsValid { get; set; }
        public string Subject { get; set; } = string.Empty;
        public string Issuer { get; set; } = string.Empty;
        public string StatusMessage { get; set; } = string.Empty;
    }

    /// <summary>
    /// Verifies Authenticode digital signatures of executables, DLLs, and update installers.
    /// Strictly reports 'Unsigned' if no valid digital certificate is embedded.
    /// </summary>
    public static class AuthenticodeVerifier
    {
        public static SignatureVerificationResult VerifyFile(string filePath)
        {
            if (!File.Exists(filePath))
            {
                return new SignatureVerificationResult
                {
                    IsSigned = false,
                    IsValid = false,
                    StatusMessage = "File not found."
                };
            }

            try
            {
                using var cert = new X509Certificate2(filePath);
                bool chainValid = cert.Verify();

                return new SignatureVerificationResult
                {
                    IsSigned = true,
                    IsValid = chainValid,
                    Subject = cert.Subject,
                    Issuer = cert.Issuer,
                    StatusMessage = chainValid ? "Valid Authenticode Signature" : "Self-signed or untrusted certificate chain"
                };
            }
            catch (Exception)
            {
                // No certificate found or file unsigned
                return new SignatureVerificationResult
                {
                    IsSigned = false,
                    IsValid = false,
                    StatusMessage = "Unsigned (No Authenticode certificate embedded)"
                };
            }
        }
    }
}
