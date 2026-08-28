using System;
using System.Collections.Generic;
using EDM.Services;

namespace EDM.Models
{
    /// <summary>
    /// Lifecycle states for pending external download requests awaiting user confirmation.
    /// State transitions are strictly forward-only:
    /// Pending -> Displayed -> Approved (Terminal)
    /// Terminal states: Approved, Rejected, Expired, Cancelled, Failed.
    /// </summary>
    public enum PendingConfirmationStatus
    {
        Pending = 0,
        Displayed = 1,
        Approved = 2,
        Rejected = 3,
        Expired = 4,
        Cancelled = 5,
        Failed = 6
    }

    /// <summary>
    /// Thread-safe model representing an external download request held in the confirmation queue.
    /// Each incoming request receives an independent UUID/GUID to prevent overwriting.
    /// </summary>
    public class PendingDownloadRequest
    {
        public Guid PendingRequestId { get; set; } = Guid.NewGuid();
        public IngestionSource Source { get; set; } = IngestionSource.BrowserExtension;
        public string Url { get; set; } = string.Empty;
        public string? SuggestedFileName { get; set; }
        public string? Title { get; set; }
        public string? Referrer { get; set; }
        public string? Cookies { get; set; }
        public string? UserAgent { get; set; }
        public string? AuthHeader { get; set; }
        public string? Quality { get; set; }
        public string? Format { get; set; }
        public string? VideoUrl { get; set; }
        public string? AudioUrl { get; set; }
        public long? EstimatedSizeBytes { get; set; }
        public bool RequiresFfmpegMerge { get; set; }
        public string? DestinationDirectory { get; set; }
        public string? TargetCategory { get; set; }
        public string? TargetQueueId { get; set; } = "default";
        public string? DownloadIdentity { get; set; }

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime ExpiresAtUtc { get; set; } = DateTime.UtcNow.AddMinutes(10);
        public PendingConfirmationStatus Status { get; set; } = PendingConfirmationStatus.Pending;
        public string? RejectionReason { get; set; }
        public DateTime? DecisionTimeUtc { get; set; }

        /// <summary>
        /// Indicates if the request has reached a final, immutable state.
        /// </summary>
        public bool IsTerminal => Status is PendingConfirmationStatus.Approved 
                                      or PendingConfirmationStatus.Rejected 
                                      or PendingConfirmationStatus.Expired 
                                      or PendingConfirmationStatus.Cancelled 
                                      or PendingConfirmationStatus.Failed;

        /// <summary>
        /// Checks whether the request has exceeded its configured expiration time.
        /// </summary>
        public bool IsExpired(DateTime? nowUtc = null) => 
            (nowUtc ?? DateTime.UtcNow) > ExpiresAtUtc && Status != PendingConfirmationStatus.Approved;

        /// <summary>
        /// User-friendly display name for UI rendering.
        /// </summary>
        public string DisplayName
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(SuggestedFileName)) return SuggestedFileName;
                if (!string.IsNullOrWhiteSpace(Title)) return Title;
                try
                {
                    if (Uri.TryCreate(Url, UriKind.Absolute, out var uri))
                    {
                        string fn = System.IO.Path.GetFileName(uri.LocalPath);
                        if (!string.IsNullOrWhiteSpace(fn)) return fn;
                    }
                }
                catch { }
                return Url;
            }
        }

        /// <summary>
        /// Formatted human-readable estimated file size.
        /// </summary>
        public string FormattedSize => EstimatedSizeBytes.HasValue && EstimatedSizeBytes.Value > 0
            ? $"{EstimatedSizeBytes.Value / (1024.0 * 1024.0):F1} MB"
            : "Size Detecting...";

        /// <summary>
        /// Formatted source badge text.
        /// </summary>
        public string SourceBadge => Source switch
        {
            IngestionSource.BrowserExtension => "Browser Extension",
            IngestionSource.ClipboardMonitor => "Clipboard Copy",
            IngestionSource.NativeMessaging => "Native Messaging",
            IngestionSource.CommandLine => "Command Line",
            IngestionSource.RemoteDashboard => "Remote Control",
            _ => Source.ToString()
        };
    }

    /// <summary>
    /// Event arguments for pending request lifecycle events.
    /// </summary>
    public class PendingRequestEventArgs : EventArgs
    {
        public PendingDownloadRequest Request { get; }
        public PendingConfirmationStatus PreviousStatus { get; }
        public PendingConfirmationStatus NewStatus { get; }

        public PendingRequestEventArgs(PendingDownloadRequest request, PendingConfirmationStatus previousStatus, PendingConfirmationStatus newStatus)
        {
            Request = request;
            PreviousStatus = previousStatus;
            NewStatus = newStatus;
        }
    }
}
