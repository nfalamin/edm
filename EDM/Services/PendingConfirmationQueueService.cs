using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EDM.Models;
using EDM.Services.Interfaces;

namespace EDM.Services
{
    /// <summary>
    /// Production-grade thread-safe implementation of the Pending Download Confirmation Queue.
    /// Manages the full lifecycle of externally captured download requests awaiting user confirmation.
    /// Guarantees:
    /// - Zero-Trust gating: External triggers are held in Pending state until explicit user approval.
    /// - Strict forward-only state transitions.
    /// - Atomic approval preventing double-execution or race conditions.
    /// - Independent UUID identity per request (preventing URL overwrite).
    /// - Configurable expiration with background sweep.
    /// - Comprehensive audit logging with zero secret leakage.
    /// </summary>
    public sealed class PendingConfirmationQueueService : IPendingConfirmationQueueService, IDisposable
    {
        private static readonly Lazy<PendingConfirmationQueueService> _lazyInstance =
            new(() => new PendingConfirmationQueueService());
        public static PendingConfirmationQueueService Instance => _lazyInstance.Value;

        private readonly ConcurrentDictionary<Guid, PendingDownloadRequest> _requests = new();
        private readonly object _stateLock = new();
        private readonly System.Threading.Timer _expirationTimer;
        private bool _isDisposed;

        public event EventHandler<PendingRequestEventArgs>? RequestEnqueued;
        public event EventHandler<PendingRequestEventArgs>? RequestStateChanged;

        public int PendingCount => _requests.Values.Count(r => 
            r.Status is PendingConfirmationStatus.Pending or PendingConfirmationStatus.Displayed);

        public PendingConfirmationQueueService()
        {
            // Background timer to sweep expired requests every 30 seconds
            _expirationTimer = new System.Threading.Timer(_ =>
            {
                try
                {
                    ExpireOldRequests();
                }
                catch (Exception ex)
                {
                    LoggingService.LogException("[PendingConfirmationQueueService] Error during expiration sweep", ex);
                }
            }, null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
        }

        public PendingDownloadRequest EnqueueRequest(
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
            TimeSpan? expiration = null)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                throw new ArgumentException("URL cannot be empty.", nameof(url));
            }

            var request = new PendingDownloadRequest
            {
                PendingRequestId = Guid.NewGuid(),
                Source = source,
                Url = url.Trim(),
                SuggestedFileName = suggestedFileName?.Trim(),
                Title = title?.Trim(),
                Referrer = referrer?.Trim(),
                Cookies = cookies,
                UserAgent = userAgent,
                AuthHeader = authHeader,
                Quality = quality,
                Format = format,
                VideoUrl = videoUrl,
                AudioUrl = audioUrl,
                EstimatedSizeBytes = estimatedSizeBytes,
                RequiresFfmpegMerge = requiresFfmpegMerge,
                DestinationDirectory = destinationDirectory,
                CreatedAtUtc = DateTime.UtcNow,
                ExpiresAtUtc = DateTime.UtcNow.Add(expiration ?? TimeSpan.FromMinutes(10)),
                Status = PendingConfirmationStatus.Pending
            };

            lock (_stateLock)
            {
                _requests[request.PendingRequestId] = request;
            }

            // Audit log: Request Received & Held (Sanitized URL, NO secrets)
            LoggingService.Log($"[PendingConfirmationQueue] REQUEST_RECEIVED & HELD: Id={request.PendingRequestId:N}, Source={request.Source}, File='{request.DisplayName}', URL={ProtocolDetector.SanitizeUrlForLogging(request.Url)}");

            // Notify UI & listeners asynchronously
            NotifyRequestEnqueued(request);

            return request;
        }

        public bool TryGetRequest(Guid requestId, out PendingDownloadRequest? request)
        {
            return _requests.TryGetValue(requestId, out request);
        }

        public IReadOnlyList<PendingDownloadRequest> GetPendingRequests()
        {
            return _requests.Values
                .Where(r => r.Status is PendingConfirmationStatus.Pending or PendingConfirmationStatus.Displayed)
                .OrderBy(r => r.CreatedAtUtc)
                .ToList();
        }

        public IReadOnlyList<PendingDownloadRequest> GetAllRequests()
        {
            return _requests.Values
                .OrderByDescending(r => r.CreatedAtUtc)
                .ToList();
        }

        public bool MarkAsDisplayed(Guid requestId)
        {
            lock (_stateLock)
            {
                if (!_requests.TryGetValue(requestId, out var req)) return false;

                if (req.Status == PendingConfirmationStatus.Pending)
                {
                    req.Status = PendingConfirmationStatus.Displayed;
                    NotifyStateChanged(req, PendingConfirmationStatus.Pending, PendingConfirmationStatus.Displayed);
                    LoggingService.Log($"[PendingConfirmationQueue] REQUEST_DISPLAYED: Id={requestId:N}");
                    return true;
                }

                return false;
            }
        }

        public bool TryApprove(Guid requestId, out PendingDownloadRequest? request)
        {
            lock (_stateLock)
            {
                if (!_requests.TryGetValue(requestId, out var req))
                {
                    request = null;
                    return false;
                }

                DateTime now = DateTime.UtcNow;
                if (req.IsExpired(now))
                {
                    var prevStatus = req.Status;
                    req.Status = PendingConfirmationStatus.Expired;
                    req.DecisionTimeUtc = now;
                    request = null;
                    NotifyStateChanged(req, prevStatus, PendingConfirmationStatus.Expired);
                    LoggingService.LogWarning($"[PendingConfirmationQueue] Approval Denied: Request {requestId:N} has expired at {req.ExpiresAtUtc:u}.");
                    return false;
                }

                // Strict forward-only transition: Only Pending or Displayed can be Approved
                if (req.Status != PendingConfirmationStatus.Pending && req.Status != PendingConfirmationStatus.Displayed)
                {
                    request = null;
                    LoggingService.LogWarning($"[PendingConfirmationQueue] Approval Denied: Request {requestId:N} is in immutable state '{req.Status}'.");
                    return false;
                }

                var previous = req.Status;
                req.Status = PendingConfirmationStatus.Approved;
                req.DecisionTimeUtc = now;
                request = req;

                NotifyStateChanged(req, previous, PendingConfirmationStatus.Approved);
                LoggingService.Log($"[PendingConfirmationQueue] REQUEST_APPROVED: Id={requestId:N}, File='{req.DisplayName}', URL={ProtocolDetector.SanitizeUrlForLogging(req.Url)}");
                return true;
            }
        }

        public bool TryReject(Guid requestId, string? reason = null)
        {
            lock (_stateLock)
            {
                if (!_requests.TryGetValue(requestId, out var req)) return false;

                if (req.Status != PendingConfirmationStatus.Pending && req.Status != PendingConfirmationStatus.Displayed)
                {
                    LoggingService.LogWarning($"[PendingConfirmationQueue] Reject Denied: Request {requestId:N} is already in state '{req.Status}'.");
                    return false;
                }

                var previous = req.Status;
                req.Status = PendingConfirmationStatus.Rejected;
                req.RejectionReason = reason ?? "User rejected";
                req.DecisionTimeUtc = DateTime.UtcNow;

                NotifyStateChanged(req, previous, PendingConfirmationStatus.Rejected);
                LoggingService.Log($"[PendingConfirmationQueue] REQUEST_REJECTED: Id={requestId:N}, Reason='{req.RejectionReason}'");
                return true;
            }
        }

        public bool TryCancel(Guid requestId)
        {
            lock (_stateLock)
            {
                if (!_requests.TryGetValue(requestId, out var req)) return false;

                if (req.Status != PendingConfirmationStatus.Pending && req.Status != PendingConfirmationStatus.Displayed)
                {
                    return false;
                }

                var previous = req.Status;
                req.Status = PendingConfirmationStatus.Cancelled;
                req.DecisionTimeUtc = DateTime.UtcNow;

                NotifyStateChanged(req, previous, PendingConfirmationStatus.Cancelled);
                LoggingService.Log($"[PendingConfirmationQueue] REQUEST_CANCELLED: Id={requestId:N}");
                return true;
            }
        }

        public int ExpireOldRequests(DateTime? nowUtc = null)
        {
            lock (_stateLock)
            {
                DateTime now = nowUtc ?? DateTime.UtcNow;
                int expiredCount = 0;

                foreach (var req in _requests.Values)
                {
                    if ((req.Status == PendingConfirmationStatus.Pending || req.Status == PendingConfirmationStatus.Displayed) 
                        && req.IsExpired(now))
                    {
                        var previous = req.Status;
                        req.Status = PendingConfirmationStatus.Expired;
                        req.DecisionTimeUtc = now;
                        expiredCount++;

                        NotifyStateChanged(req, previous, PendingConfirmationStatus.Expired);
                        LoggingService.Log($"[PendingConfirmationQueue] REQUEST_EXPIRED: Id={req.PendingRequestId:N}, URL={ProtocolDetector.SanitizeUrlForLogging(req.Url)}");
                    }
                }

                return expiredCount;
            }
        }

        public void ClearTerminalRequests()
        {
            lock (_stateLock)
            {
                DateTime cutoff = DateTime.UtcNow.AddHours(-1);
                foreach (var kvp in _requests.Where(kvp => kvp.Value.IsTerminal && kvp.Value.CreatedAtUtc < cutoff).ToList())
                {
                    _requests.TryRemove(kvp.Key, out _);
                }
            }
        }

        private void NotifyRequestEnqueued(PendingDownloadRequest request)
        {
            Task.Run(() =>
            {
                try
                {
                    RequestEnqueued?.Invoke(this, new PendingRequestEventArgs(request, PendingConfirmationStatus.Pending, PendingConfirmationStatus.Pending));
                }
                catch (Exception ex)
                {
                    LoggingService.LogException("[PendingConfirmationQueueService] RequestEnqueued handler error", ex);
                }
            });
        }

        private void NotifyStateChanged(PendingDownloadRequest request, PendingConfirmationStatus prev, PendingConfirmationStatus next)
        {
            Task.Run(() =>
            {
                try
                {
                    RequestStateChanged?.Invoke(this, new PendingRequestEventArgs(request, prev, next));
                }
                catch (Exception ex)
                {
                    LoggingService.LogException("[PendingConfirmationQueueService] RequestStateChanged handler error", ex);
                }
            });
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;
            _expirationTimer.Dispose();
        }
    }
}
