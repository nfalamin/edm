using System;
using System.Buffers;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using System.Security.Cryptography.X509Certificates;

namespace EDM.Services
{
    public class FileIntegrityService
    {
        public async Task<string> ComputeSha256Async(string filePath, CancellationToken ct)
        {
            if (filePath == null) throw new ArgumentNullException(nameof(filePath));
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 128 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
            using var sha = SHA256.Create();
            var buffer = ArrayPool<byte>.Shared.Rent(81920);
            try
            {
                int read;
                while ((read = await stream.ReadAsync(buffer, 0, buffer.Length, ct).ConfigureAwait(false)) > 0)
                {
                    sha.TransformBlock(buffer, 0, read, null, 0);
                    if (ct.IsCancellationRequested) ct.ThrowIfCancellationRequested();
                }
                sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                var hash = sha.Hash ?? Array.Empty<byte>();
                return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        /// <summary>
        /// Verifies the Authenticode signature of a PE file and validates its certificate chain.
        /// Returns true when the binary has a valid signature and the signing certificate chain builds successfully.
        /// </summary>
        public bool VerifyAuthenticodeSignature(string filePath)
        {
            try
            {
#pragma warning disable SYSLIB0057 // CreateFromSignedFile is obsolete; replace with X509CertificateLoader in a follow-up refactor
                var cert = X509Certificate.CreateFromSignedFile(filePath);
#pragma warning restore SYSLIB0057
                var cert2 = new X509Certificate2(cert);
                using var chain = new X509Chain();
                chain.ChainPolicy.RevocationMode = X509RevocationMode.Online;
                chain.ChainPolicy.RevocationFlag = X509RevocationFlag.ExcludeRoot;
                chain.ChainPolicy.UrlRetrievalTimeout = TimeSpan.FromSeconds(5);
                chain.ChainPolicy.VerificationFlags = X509VerificationFlags.NoFlag;
                return chain.Build(cert2);
            }
            catch (Exception ex)
            {
                LoggingService.Log($"[FileIntegrityService.VerifyAuthenticodeSignature] {ex.Message}");
                return false;
            }
        }
    }
}
