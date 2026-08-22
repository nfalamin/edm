using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using EDM.Models;
using EDM.NativeMessaging;

namespace EDM.Services
{
    public enum IngestionSource
    {
        BrowserExtension = 1,
        ClipboardMonitor = 2,
        DragAndDrop = 3,
        CommandLine = 4,
        BatchFile = 5,
        DropBasket = 6,
        Manual = 7,
        RemoteDashboard = 8,
        NativeMessaging = 9,
        NativeHost = 9,
        Direct = 10
    }

    public class DownloadRequest
    {
        public string RequestId { get; set; } = Guid.NewGuid().ToString("N");
        public IngestionSource Source { get; set; }
        public string Url { get; set; } = string.Empty;
        public string DestinationDirectory { get; set; } = string.Empty;
        public string? TargetDirectory { get => DestinationDirectory; set => DestinationDirectory = value ?? string.Empty; }
        public string? TargetCategory { get; set; }
        public string? ContentType { get; set; }
        public string? SuggestedFileName { get; set; }
        public string? TargetQueueId { get; set; } = "default";
        public QueuePriority Priority { get; set; } = QueuePriority.Normal;
        public bool SilentMode { get; set; } = false;
        public bool ExitAfterDownload { get; set; } = false;
        public string? Referrer { get; set; }
        public string? Cookies { get; set; }
        public Dictionary<string, string> CustomHeaders { get; set; } = new();
    }

    public class CommandLineParseResult
    {
        public int ExitCode { get; set; } = 0; // 0=Success, 1=InvalidArgs, 2=SecurityRejected, 3=Error
        public List<DownloadRequest> Requests { get; set; } = new();
        public bool IsHelpOrVersion { get; set; } = false;
        public string OutputMessage { get; set; } = string.Empty;
    }

    /// <summary>
    /// Unified download ingestion layer handling Browser, Clipboard, Drag-Drop, Batch, and CLI inputs
    /// through a standardized, zero-trust sanitized pipeline.
    /// </summary>
    public class UniversalDownloadIngestionService
    {
        private static readonly Regex UrlRegex = new(
            @"https?:\/\/(www\.)?[-a-zA-Z0-9@:%._\+~#=]{1,256}\.[a-zA-Z0-9()]{1,6}\b([-a-zA-Z0-9()@:%_\+.~#?&//=]*)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private readonly HashSet<string> _recentIngestedUrls = new(StringComparer.OrdinalIgnoreCase);
        private readonly object _lock = new();

        public List<DownloadRequest> IngestFromClipboard(string clipboardText, string defaultDownloadDir)
        {
            var results = new List<DownloadRequest>();
            if (string.IsNullOrWhiteSpace(clipboardText)) return results;

            var extracted = ClipboardMonitorService.ExtractDownloadableUrls(clipboardText);
            foreach (var url in extracted)
            {
                if (!SecuritySanitizer.IsAllowedUrlScheme(url)) continue;

                lock (_lock)
                {
                    if (_recentIngestedUrls.Contains(url)) continue; // Duplicate suppression
                    _recentIngestedUrls.Add(url);
                }

                string rawName = string.Empty;
                try
                {
                    rawName = Path.GetFileName(new Uri(url).AbsolutePath);
                }
                catch { }

                results.Add(new DownloadRequest
                {
                    Source = IngestionSource.ClipboardMonitor,
                    Url = url,
                    DestinationDirectory = defaultDownloadDir,
                    SuggestedFileName = SecuritySanitizer.SanitizeFileName(rawName)
                });
            }

            return results;
        }

        public DownloadRequest? IngestFromBrowserHandoff(IpcHandoffPayload payload, string defaultDownloadDir)
        {
            if (payload == null || string.IsNullOrWhiteSpace(payload.Url)) return null;

            string url = payload.Url.Trim();
            if (!SecuritySanitizer.IsAllowedUrlScheme(url)) return null;

            lock (_lock)
            {
                if (_recentIngestedUrls.Contains(url)) return null; // Cross-source duplicate suppression
                _recentIngestedUrls.Add(url);
            }

            string rawName = !string.IsNullOrWhiteSpace(payload.Filename)
                ? payload.Filename
                : (!string.IsNullOrWhiteSpace(payload.Title) ? payload.Title : string.Empty);

            if (string.IsNullOrWhiteSpace(rawName))
            {
                try { rawName = Path.GetFileName(new Uri(url).AbsolutePath); } catch { }
            }

            string sanitizedFileName = SecuritySanitizer.SanitizeFileName(rawName);

            var req = new DownloadRequest
            {
                Source = IngestionSource.BrowserExtension,
                Url = url,
                DestinationDirectory = defaultDownloadDir,
                SuggestedFileName = sanitizedFileName,
                Referrer = payload.Referer ?? payload.PageUrl,
                Cookies = payload.Cookies
            };

            if (!string.IsNullOrWhiteSpace(payload.AuthHeader))
            {
                req.CustomHeaders["Authorization"] = payload.AuthHeader;
            }
            if (!string.IsNullOrWhiteSpace(payload.UserAgent))
            {
                req.CustomHeaders["User-Agent"] = payload.UserAgent;
            }

            return req;
        }

        public List<DownloadRequest> IngestFromDragDrop(string[] droppedData, string defaultDownloadDir)
        {
            var results = new List<DownloadRequest>();
            if (droppedData == null || droppedData.Length == 0) return results;

            foreach (var item in droppedData)
            {
                if (string.IsNullOrWhiteSpace(item)) continue;

                if (File.Exists(item))
                {
                    // Batch text file containing URLs
                    if (item.EndsWith(".txt", StringComparison.OrdinalIgnoreCase) || item.EndsWith(".edm", StringComparison.OrdinalIgnoreCase))
                    {
                        var lines = File.ReadAllLines(item);
                        foreach (var line in lines)
                        {
                            var trimmed = line.Trim();
                            if (SecuritySanitizer.IsAllowedUrlScheme(trimmed))
                            {
                                results.Add(new DownloadRequest
                                {
                                    Source = IngestionSource.BatchFile,
                                    Url = trimmed,
                                    DestinationDirectory = defaultDownloadDir,
                                    SuggestedFileName = SecuritySanitizer.SanitizeFileName(Path.GetFileName(new Uri(trimmed).AbsolutePath))
                                });
                            }
                        }
                    }
                }
                else if (SecuritySanitizer.IsAllowedUrlScheme(item))
                {
                    results.Add(new DownloadRequest
                    {
                        Source = IngestionSource.DragAndDrop,
                        Url = item,
                        DestinationDirectory = defaultDownloadDir,
                        SuggestedFileName = SecuritySanitizer.SanitizeFileName(Path.GetFileName(new Uri(item).AbsolutePath))
                    });
                }
            }

            return results;
        }

        public CommandLineParseResult IngestFromCommandLine(string[] args, string defaultDownloadDir)
        {
            var result = new CommandLineParseResult();
            if (args == null || args.Length == 0) return result;

            string? url = null;
            string destDir = defaultDownloadDir;
            string? filename = null;
            string queueId = "default";
            bool silent = false;
            bool exitAfter = false;
            string? batchFile = null;

            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];
                if (arg.Equals("--help", StringComparison.OrdinalIgnoreCase) || arg.Equals("-h", StringComparison.OrdinalIgnoreCase))
                {
                    result.IsHelpOrVersion = true;
                    result.OutputMessage = "EDM CLI: edm.exe --url <URL> [--out <DIR>] [--filename <NAME>] [--queue <NAME>] [--silent] [--exit] [--batch <FILE>]";
                    return result;
                }

                if ((arg.Equals("--url", StringComparison.OrdinalIgnoreCase) || arg.Equals("-u", StringComparison.OrdinalIgnoreCase)) && i + 1 < args.Length)
                {
                    url = args[++i];
                }
                else if ((arg.Equals("--out", StringComparison.OrdinalIgnoreCase) || arg.Equals("-o", StringComparison.OrdinalIgnoreCase)) && i + 1 < args.Length)
                {
                    destDir = args[++i];
                }
                else if ((arg.Equals("--filename", StringComparison.OrdinalIgnoreCase) || arg.Equals("-f", StringComparison.OrdinalIgnoreCase)) && i + 1 < args.Length)
                {
                    filename = args[++i];
                }
                else if (arg.Equals("--queue", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    queueId = args[++i];
                }
                else if (arg.Equals("--batch", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    batchFile = args[++i];
                }
                else if (arg.Equals("--silent", StringComparison.OrdinalIgnoreCase) || arg.Equals("-s", StringComparison.OrdinalIgnoreCase))
                {
                    silent = true;
                }
                else if (arg.Equals("--exit", StringComparison.OrdinalIgnoreCase) || arg.Equals("-x", StringComparison.OrdinalIgnoreCase))
                {
                    exitAfter = true;
                }
                else if (SecuritySanitizer.IsAllowedUrlScheme(arg) && url == null)
                {
                    url = arg;
                }
            }

            if (!string.IsNullOrEmpty(batchFile) && File.Exists(batchFile))
            {
                var batchReqs = IngestFromDragDrop(new[] { batchFile }, destDir);
                result.Requests.AddRange(batchReqs);
                return result;
            }

            if (string.IsNullOrEmpty(url))
            {
                result.ExitCode = 1;
                result.OutputMessage = "Error: Missing required --url parameter.";
                return result;
            }

            if (!SecuritySanitizer.IsAllowedUrlScheme(url))
            {
                result.ExitCode = 2;
                result.OutputMessage = $"Security Error: URL scheme for '{url}' is not permitted.";
                return result;
            }

            result.Requests.Add(new DownloadRequest
            {
                Source = IngestionSource.CommandLine,
                Url = url,
                DestinationDirectory = destDir,
                SuggestedFileName = !string.IsNullOrEmpty(filename) ? SecuritySanitizer.SanitizeFileName(filename) : null,
                TargetQueueId = queueId,
                SilentMode = silent,
                ExitAfterDownload = exitAfter
            });

            return result;
        }
    }
}
