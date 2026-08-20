using System;
using System.Threading;
using System.Threading.Tasks;

namespace EDM.Domain.Protocols
{
    public enum EngineProtocolType
    {
        HttpMultiPart,
        HlsDashStreaming,
        BitTorrent,
        FtpSecure,
        YtDlpMediaExtractor
    }

    public sealed class EngineDownloadRequest
    {
        public Guid Id { get; init; } = Guid.NewGuid();
        public required string SourceUrl { get; init; }
        public required string DestinationFilePath { get; init; }
        public string? Category { get; init; }
        public int DesiredParallelStreams { get; init; } = 8;
        public long? SpeedLimitBytesPerSecond { get; init; }
        public string? AuthCredentials { get; init; }
        public string? AuthHeader { get; init; }
        public string? Cookies { get; init; }
        public string? UserAgent { get; init; }
        public string? Referer { get; init; }
        public string? PostData { get; init; }
        public string? ExpectedChecksum { get; init; }
    }

    public sealed class EngineProgressReport
    {
        public long BytesReceived { get; init; }
        public long? TotalBytes { get; init; }
        public double CurrentSpeedBytesPerSec { get; init; }
        public double AverageSpeedBytesPerSec { get; init; }
        public double PeakSpeedBytesPerSec { get; init; }
        public double ProgressPercentage => TotalBytes.HasValue && TotalBytes.Value > 0
            ? Math.Clamp((double)BytesReceived / TotalBytes.Value * 100.0, 0.0, 100.0)
            : 0.0;
        public int ActiveConnections { get; init; }
        public bool CanResume { get; init; }
        public string? StatusText { get; init; }
    }

    public interface IPauseToken
    {
        bool IsPaused { get; }
        Task WaitWhilePausedAsync(CancellationToken cancellationToken);
    }

    public interface IDownloadEngine
    {
        EngineProtocolType SupportedProtocol { get; }
        bool CanHandle(string url);
        Task<long?> ProbeContentLengthAsync(EngineDownloadRequest request, CancellationToken ct);
        Task DownloadAsync(
            EngineDownloadRequest request,
            IProgress<EngineProgressReport> progress,
            IPauseToken pauseToken,
            CancellationToken ct);
    }
}
