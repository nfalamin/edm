using System;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace EDM.Services
{
    public class ExtractionResult
    {
        public bool IsSuccess { get; set; }
        public string ExtractedFolderPath { get; set; } = string.Empty;
        public int ExtractedFileCount { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public class AutoExtractorAndStreamService : IDisposable
    {
        private HttpListener? _streamListener;
        private readonly int _streamPort = 45825;
        private readonly CancellationTokenSource _cts = new();

        public bool IsExtractionPermitted { get; set; } = false;
        public bool DeleteArchiveAfterExtraction { get; set; } = false;

        public AutoExtractorAndStreamService(bool isExtractionPermitted = false, bool startStreamServer = false)
        {
            IsExtractionPermitted = isExtractionPermitted;

            if (startStreamServer)
            {
                try
                {
                    _streamListener = new HttpListener();
                    _streamListener.Prefixes.Add($"http://127.0.0.1:{_streamPort}/edm-stream/");
                    _streamListener.Start();
                    Task.Run(() => HandleStreamingRequestsAsync(_cts.Token));
                }
                catch
                {
                    // Fallback gracefully
                }
            }
        }

        public bool IsCompressedArchive(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath)) return false;
            var ext = Path.GetExtension(filePath).ToLowerInvariant();
            return ext is ".zip" or ".tar" or ".gz" or ".tgz";
        }

        public async Task<ExtractionResult> TryExtractArchiveAsync(
            string archivePath, 
            string? destinationFolder = null, 
            bool overridePermission = false, 
            CancellationToken ct = default)
        {
            var result = new ExtractionResult();

            // Strict permission safeguard
            if (!IsExtractionPermitted && !overridePermission)
            {
                result.IsSuccess = false;
                result.ErrorMessage = "Auto-extraction is disabled by user permissions.";
                return result;
            }

            if (!File.Exists(archivePath) || !IsCompressedArchive(archivePath))
            {
                result.IsSuccess = false;
                result.ErrorMessage = "Valid compressed archive file not found.";
                return result;
            }

            try
            {
                var targetDir = destinationFolder ?? Path.Combine(
                    Path.GetDirectoryName(archivePath) ?? string.Empty,
                    Path.GetFileNameWithoutExtension(archivePath) + "_extracted"
                );

                if (!Directory.Exists(targetDir))
                {
                    Directory.CreateDirectory(targetDir);
                }

                var fullTargetDir = Path.GetFullPath(targetDir);
                string safeTargetPrefix = fullTargetDir.EndsWith(Path.DirectorySeparatorChar.ToString()) 
                    ? fullTargetDir 
                    : fullTargetDir + Path.DirectorySeparatorChar;

                int count = 0;

                // Secure ZIP extraction with ZipSlip mitigation
                using (var zip = ZipFile.OpenRead(archivePath))
                {
                    foreach (var entry in zip.Entries)
                    {
                        if (string.IsNullOrEmpty(entry.Name)) continue; // skip directory entry

                        var destinationFilePath = Path.GetFullPath(Path.Combine(fullTargetDir, entry.FullName));

                        // ZipSlip check: prevent path traversal attacks (e.g. ../../windows/system32)
                        if (!destinationFilePath.StartsWith(safeTargetPrefix, StringComparison.OrdinalIgnoreCase))
                        {
                            throw new InvalidOperationException($"ZipSlip path traversal attempt detected in entry: {entry.FullName}");
                        }

                        var parent = Path.GetDirectoryName(destinationFilePath);
                        if (!string.IsNullOrEmpty(parent) && !Directory.Exists(parent))
                        {
                            Directory.CreateDirectory(parent);
                        }

                        entry.ExtractToFile(destinationFilePath, overwrite: true);
                        count++;
                    }
                }

                result.IsSuccess = true;
                result.ExtractedFolderPath = fullTargetDir;
                result.ExtractedFileCount = count;

                if (DeleteArchiveAfterExtraction)
                {
                    try { File.Delete(archivePath); } catch { }
                }
            }
            catch (Exception ex)
            {
                result.IsSuccess = false;
                result.ErrorMessage = ex.Message;
            }

            return await Task.FromResult(result);
        }

        public string GetStreamingUrlForPartialFile(string partialFilePath)
        {
            return $"http://127.0.0.1:{_streamPort}/edm-stream/play?file={Uri.EscapeDataString(partialFilePath)}";
        }

        private static bool IsAllowedStreamingFile(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath)) return false;
            string ext = Path.GetExtension(filePath).ToLowerInvariant();
            return ext is ".mp4" or ".mkv" or ".webm" or ".avi" or ".mov" or ".ts" or ".mp3" or ".aac" or ".m4a" or ".flac" or ".part" or ".tmp";
        }

        private async Task HandleStreamingRequestsAsync(CancellationToken ct)
        {
            if (_streamListener == null) return;

            while (!ct.IsCancellationRequested && _streamListener.IsListening)
            {
                try
                {
                    var ctx = await _streamListener.GetContextAsync().ConfigureAwait(false);
                    var req = ctx.Request;
                    var resp = ctx.Response;

                    var fileParam = req.QueryString["file"];
                    if (!string.IsNullOrEmpty(fileParam) && IsAllowedStreamingFile(fileParam))
                    {
                        // Open with FileShare.ReadWrite so active downloading writer is not blocked
                        using var fs = new FileStream(fileParam, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                        resp.ContentType = "video/mp4";
                        resp.ContentLength64 = fs.Length;
                        resp.StatusCode = 200;
                        await fs.CopyToAsync(resp.OutputStream, ct).ConfigureAwait(false);
                        resp.OutputStream.Close();
                        continue;
                    }

                    resp.StatusCode = 404;
                    resp.Close();
                }
                catch (OperationCanceledException) { break; }
                catch { }
            }
        }

        public void Dispose()
        {
            _cts.Cancel();
            try { _streamListener?.Stop(); _streamListener?.Close(); } catch { }
            _cts.Dispose();
        }
    }
}
