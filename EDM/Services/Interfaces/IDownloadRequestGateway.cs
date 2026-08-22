using System;
using System.Threading;
using System.Threading.Tasks;
using EDM.Models;

namespace EDM.Services.Interfaces
{
    public enum DownloadSubmissionStatus
    {
        Accepted = 1,
        Duplicate = 2,
        Invalid = 3,
        SecurityRejected = 4,
        Disabled = 5,
        Failed = 6
    }

    public class DownloadSubmissionResult
    {
        public bool IsSuccess { get; set; }
        public DownloadSubmissionStatus Status { get; set; }
        public string? DownloadId { get; set; }
        public string? Message { get; set; }
        public DownloadItem? Item { get; set; }

        public static DownloadSubmissionResult Succeeded(DownloadItem item, string? downloadId = null) => new()
        {
            IsSuccess = true,
            Status = DownloadSubmissionStatus.Accepted,
            DownloadId = downloadId ?? item.Id.ToString("N"),
            Item = item,
            Message = "Download request accepted and enqueued successfully."
        };

        public static DownloadSubmissionResult Duplicate(string message = "Download request already active or queued.") => new()
        {
            IsSuccess = false,
            Status = DownloadSubmissionStatus.Duplicate,
            Message = message
        };

        public static DownloadSubmissionResult Invalid(string message) => new()
        {
            IsSuccess = false,
            Status = DownloadSubmissionStatus.Invalid,
            Message = message
        };

        public static DownloadSubmissionResult SecurityRejected(string message) => new()
        {
            IsSuccess = false,
            Status = DownloadSubmissionStatus.SecurityRejected,
            Message = message
        };

        public static DownloadSubmissionResult Disabled(string message = "The requested ingestion source is disabled in settings.") => new()
        {
            IsSuccess = false,
            Status = DownloadSubmissionStatus.Disabled,
            Message = message
        };

        public static DownloadSubmissionResult Failed(string message) => new()
        {
            IsSuccess = false,
            Status = DownloadSubmissionStatus.Failed,
            Message = message
        };
    }

    /// <summary>
    /// Authoritative Unified Download Request Gateway Interface.
    /// Centralizes validation, security sanitization, deterministic identity resolution,
    /// atomic concurrency-safe deduplication, and queue dispatch.
    /// </summary>
    public interface IDownloadRequestGateway
    {
        Task<DownloadSubmissionResult> SubmitRequestAsync(DownloadRequest request, CancellationToken ct = default);
        bool IsDuplicate(string url, string? downloadIdentity = null);
        void ResetDeduplicationCache();
    }
}
