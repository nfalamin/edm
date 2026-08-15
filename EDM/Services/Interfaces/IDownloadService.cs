using System;
using System.Threading;
using System.Threading.Tasks;

namespace EDM.Services.Interfaces
{
    /// <summary>
    /// IDownloadService - Defines the contract for download operations.
    /// Provides methods for starting downloads, retrieving stream information,
    /// and managing download state.
    /// 
    /// Usage:
    /// IDownloadService downloadService = app.Services.GetRequiredService(IDownloadService)
    /// var progress = new Progress(DownloadProgressInfo)(UpdateProgress);
    /// await downloadService.StartDownloadAsync(url, path, progress, pauseToken, getRetryCount, cancellationToken);
    /// </summary>
    public interface IDownloadService
    {
        /// <summary>
        /// Start a download operation asynchronously.
        /// Handles single-part and multi-part downloads, retry logic, and progress reporting.
        /// </summary>
        /// <param name="url">The URL to download</param>
        /// <param name="savePath">The file path where the download will be saved</param>
        /// <param name="progress">Progress reporter for download updates</param>
        /// <param name="pauseToken">Token for pausing/resuming the download</param>
        /// <param name="getRetryCount">Function that returns the current retry count</param>
        /// <param name="cancellationToken">Token for cancelling the download</param>
        /// <returns>A task representing the download operation</returns>
        Task StartDownloadAsync(
            string url,
            string savePath,
            IProgress<DownloadProgressInfo> progress,
            PauseTokenSource pauseToken,
            Func<int> getRetryCount,
            CancellationToken cancellationToken);

        /// <summary>
        /// Download and merge adaptive video and audio streams asynchronously.
        /// Typically used for video content with separate video and audio tracks.
        /// </summary>
        /// <param name="videoStreamUrl">URL to the video stream</param>
        /// <param name="audioStreamUrl">URL to the audio stream</param>
        /// <param name="outputPath">Path where the merged file will be saved</param>
        /// <param name="ffmpegPath">Path to the ffmpeg executable</param>
        /// <param name="cancellationToken">Token for cancelling the operation</param>
        /// <returns>A task representing the merge operation</returns>
        Task DownloadAndMergeAdaptiveStreamsAsync(
            string videoStreamUrl,
            string audioStreamUrl,
            string outputPath,
            string ffmpegPath,
            CancellationToken cancellationToken);

        /// <summary>
        /// Refresh the HTTP client (e.g., after proxy settings change).
        /// Useful when network configuration changes and the client needs reinitialization.
        /// </summary>
        void RefreshHttpClient();

        /// <summary>
        /// Cancel an active download and cleanup temporary and metadata files.
        /// Note: This does not cancel in-flight async tasks; the caller should manage cancellation
        /// via CancellationToken.
        /// </summary>
        /// <param name="savePath">The path of the download to cancel</param>
        void CancelAndCleanup(string savePath);
    }
}
