using System;
using System.Collections.Generic;
using EDM.Models;

namespace EDM.Services.Interfaces
{
    /// <summary>
    /// Contract for the thread-safe Pending Download Confirmation Queue Subsystem.
    /// Manages the lifecycle, atomic approvals, rejections, expirations, and UI events
    /// for external download requests awaiting user confirmation.
    /// </summary>
    public interface IPendingConfirmationQueueService
    {
        int PendingCount { get; }
        event EventHandler<PendingRequestEventArgs>? RequestEnqueued;
        event EventHandler<PendingRequestEventArgs>? RequestStateChanged;

        PendingDownloadRequest EnqueueRequest(
            string url,
            IngestionSource source,
            string? suggestedFileName = null,
            string? title = null,
            string? referrer = null,
            string? cookies = null,
            string? userAgent = null,
            string? authHeader = null,
            string? quality = null,
            string? format = null,
            string? videoUrl = null,
            string? audioUrl = null,
            long? estimatedSizeBytes = null,
            bool requiresFfmpegMerge = false,
            string? destinationDirectory = null,
            TimeSpan? expiration = null);

        bool TryGetRequest(Guid requestId, out PendingDownloadRequest? request);
        IReadOnlyList<PendingDownloadRequest> GetPendingRequests();
        IReadOnlyList<PendingDownloadRequest> GetAllRequests();

        bool MarkAsDisplayed(Guid requestId);
        bool TryApprove(Guid requestId, out PendingDownloadRequest? request);
        bool TryReject(Guid requestId, string? reason = null);
        bool TryCancel(Guid requestId);
        int ExpireOldRequests(DateTime? nowUtc = null);
        void ClearTerminalRequests();
    }
}
