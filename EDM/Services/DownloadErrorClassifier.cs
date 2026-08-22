using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Security.Authentication;

namespace EDM.Services
{
    public enum DownloadFailureCategory
    {
        NetworkTransient,
        Timeout,
        ConnectionReset,
        DnsFailure,
        Http429Throttled,
        Http5xxServer,
        RangeNotSupported,
        InvalidRange,
        AuthenticationFailure,
        Forbidden403,
        NotFound404,
        MediaUrlExpired,
        RemoteResourceChanged,
        LocalDiskFailure,
        IntegrityFailure,
        Cancellation,
        Unknown
    }

    /// <summary>
    /// DownloadErrorClassifier — Maps raw system and network exceptions into structured, actionable failure categories.
    /// </summary>
    public static class DownloadErrorClassifier
    {
        public static DownloadFailureCategory Classify(Exception ex)
        {
            if (ex == null) return DownloadFailureCategory.Unknown;

            if (ex is OperationCanceledException) return DownloadFailureCategory.Cancellation;

            if (ex is TimeoutException) return DownloadFailureCategory.Timeout;

            if (ex is HttpRequestException httpEx)
            {
                if (httpEx.StatusCode.HasValue)
                {
                    int code = (int)httpEx.StatusCode.Value;
                    if (code == 429) return DownloadFailureCategory.Http429Throttled;
                    if (code == 401) return DownloadFailureCategory.AuthenticationFailure;
                    if (code == 403) return DownloadFailureCategory.Forbidden403;
                    if (code == 404 || code == 410) return DownloadFailureCategory.NotFound404;
                    if (code == 416) return DownloadFailureCategory.InvalidRange;
                    if (code >= 500 && code <= 599) return DownloadFailureCategory.Http5xxServer;
                }

                if (httpEx.InnerException is SocketException sockEx)
                {
                    return ClassifySocketError(sockEx.SocketErrorCode);
                }
            }

            if (ex is SocketException se)
            {
                return ClassifySocketError(se.SocketErrorCode);
            }

            if (ex is AuthenticationException) return DownloadFailureCategory.AuthenticationFailure;

            if (ex is IOException ioEx)
            {
                string msg = ioEx.Message.ToLowerInvariant();
                if (msg.Contains("space") || msg.Contains("disk full")) return DownloadFailureCategory.LocalDiskFailure;
                if (msg.Contains("access") || msg.Contains("denied")) return DownloadFailureCategory.LocalDiskFailure;
                if (msg.Contains("connection reset") || msg.Contains("timed out") || msg.Contains("closed")) return DownloadFailureCategory.ConnectionReset;
                return DownloadFailureCategory.LocalDiskFailure;
            }

            if (ex is UnauthorizedAccessException) return DownloadFailureCategory.LocalDiskFailure;

            return DownloadFailureCategory.Unknown;
        }

        private static DownloadFailureCategory ClassifySocketError(SocketError error)
        {
            return error switch
            {
                SocketError.TimedOut => DownloadFailureCategory.Timeout,
                SocketError.ConnectionReset or SocketError.ConnectionAborted or SocketError.Shutdown => DownloadFailureCategory.ConnectionReset,
                SocketError.HostNotFound or SocketError.NoData or SocketError.TryAgain => DownloadFailureCategory.DnsFailure,
                SocketError.NetworkDown or SocketError.NetworkUnreachable or SocketError.HostUnreachable => DownloadFailureCategory.NetworkTransient,
                _ => DownloadFailureCategory.NetworkTransient
            };
        }

        /// <summary>
        /// Returns whether the failure category is considered recoverable via retry.
        /// </summary>
        public static bool IsRecoverable(DownloadFailureCategory category)
        {
            return category switch
            {
                DownloadFailureCategory.NotFound404 => false,
                DownloadFailureCategory.AuthenticationFailure => false,
                DownloadFailureCategory.Cancellation => false,
                DownloadFailureCategory.InvalidRange => false,
                _ => true
            };
        }
    }
}
