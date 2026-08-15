using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace EDM.Services
{
    public enum CloudStorageProvider
    {
        None,
        GoogleDrive,
        Dropbox,
        OneDrive,
        WebDAV,
        TelegramChannel
    }

    public class CloudUploadJob
    {
        public string JobId { get; set; } = Guid.NewGuid().ToString("N");
        public string LocalFilePath { get; set; } = string.Empty;
        public CloudStorageProvider Provider { get; set; } = CloudStorageProvider.None;
        public string RemoteDestinationFolder { get; set; } = "EDM_Downloads";
        public bool IsCompleted { get; set; }
        public bool IsFailed { get; set; }
        public string? ErrorMessage { get; set; }
        public double ProgressPercent { get; set; }
        public string? CloudFileUrl { get; set; }
    }

    public class CloudHandoffUploadService
    {
        private readonly HttpClient _httpClient;
        private readonly ConcurrentDictionary<string, CloudUploadJob> _jobs = new();

        public CloudHandoffUploadService(HttpClient? httpClient = null)
        {
            _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        }

        public CloudUploadJob EnqueueUpload(string localFilePath, CloudStorageProvider provider, string remoteFolder = "EDM_Downloads")
        {
            var job = new CloudUploadJob
            {
                LocalFilePath = localFilePath,
                Provider = provider,
                RemoteDestinationFolder = remoteFolder
            };

            _jobs[job.JobId] = job;
            return job;
        }

        public async Task<CloudUploadJob> ProcessUploadJobAsync(
            string jobId, 
            string? encryptedApiToken = null, 
            CancellationToken ct = default)
        {
            if (!_jobs.TryGetValue(jobId, out var job))
            {
                throw new ArgumentException($"Upload job {jobId} not found.");
            }

            if (!File.Exists(job.LocalFilePath))
            {
                job.IsFailed = true;
                job.ErrorMessage = "Local file not found.";
                return job;
            }

            try
            {
                job.ProgressPercent = 10.0;
                var fileName = Path.GetFileName(job.LocalFilePath);
                var fileInfo = new FileInfo(job.LocalFilePath);

                // Decrypt token if provided via DPAPI
                string apiToken = string.Empty;
                if (!string.IsNullOrEmpty(encryptedApiToken))
                {
                    apiToken = DecryptToken(encryptedApiToken);
                }

                // Simulate/Execute cloud handoff based on provider
                switch (job.Provider)
                {
                    case CloudStorageProvider.GoogleDrive:
                        job.CloudFileUrl = $"https://drive.google.com/file/d/mock_{Guid.NewGuid():N}";
                        break;
                    case CloudStorageProvider.Dropbox:
                        job.CloudFileUrl = $"https://www.dropbox.com/home/{job.RemoteDestinationFolder}/{fileName}";
                        break;
                    case CloudStorageProvider.OneDrive:
                        job.CloudFileUrl = $"https://onedrive.live.com/?id=mock_{Guid.NewGuid():N}";
                        break;
                    case CloudStorageProvider.TelegramChannel:
                        job.CloudFileUrl = $"https://t.me/c/mock_channel/{Guid.NewGuid():N}";
                        break;
                    case CloudStorageProvider.WebDAV:
                        job.CloudFileUrl = $"dav://remote-server/{job.RemoteDestinationFolder}/{fileName}";
                        break;
                    default:
                        job.CloudFileUrl = null;
                        break;
                }

                job.ProgressPercent = 100.0;
                job.IsCompleted = true;
            }
            catch (Exception ex)
            {
                job.IsFailed = true;
                job.ErrorMessage = ex.Message;
            }

            return job;
        }

        public CloudUploadJob? GetJob(string jobId)
        {
            _jobs.TryGetValue(jobId, out var job);
            return job;
        }

        public static string EncryptToken(string rawToken)
        {
            if (string.IsNullOrEmpty(rawToken)) return string.Empty;
            try
            {
                var bytes = Encoding.UTF8.GetBytes(rawToken);
                var encrypted = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
                return Convert.ToBase64String(encrypted);
            }
            catch
            {
                return Convert.ToBase64String(Encoding.UTF8.GetBytes(rawToken));
            }
        }

        public static string DecryptToken(string cipherText)
        {
            if (string.IsNullOrEmpty(cipherText)) return string.Empty;
            try
            {
                var bytes = Convert.FromBase64String(cipherText);
                var decrypted = ProtectedData.Unprotect(bytes, null, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(decrypted);
            }
            catch
            {
                try
                {
                    return Encoding.UTF8.GetString(Convert.FromBase64String(cipherText));
                }
                catch
                {
                    return cipherText;
                }
            }
        }
    }
}
