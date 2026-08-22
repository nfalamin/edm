using System;

namespace EDM.Services.Interfaces
{
    public enum ClipboardAction
    {
        AskBeforeDownload = 0,
        AutoDownload = 1,
        Ignore = 2
    }

    public class ClipboardUrlDetectedEventArgs : EventArgs
    {
        public string Url { get; }
        public string Source { get; }
        public bool Handled { get; set; }

        public ClipboardUrlDetectedEventArgs(string url, string source = "WindowsClipboard")
        {
            Url = url ?? string.Empty;
            Source = source;
        }
    }

    /// <summary>
    /// Service contract for Windows Clipboard monitoring and URL detection.
    /// Event-driven, low-overhead clipboard inspection utilizing Win32 WM_CLIPBOARDUPDATE.
    /// </summary>
    public interface IClipboardMonitorService : IDisposable
    {
        /// <summary>
        /// Gets whether clipboard monitoring is actively running.
        /// </summary>
        bool IsRunning { get; }

        /// <summary>
        /// Event raised when a valid downloadable URL is detected on the Windows clipboard.
        /// </summary>
        event EventHandler<ClipboardUrlDetectedEventArgs>? UrlDetected;

        /// <summary>
        /// Starts clipboard monitoring.
        /// </summary>
        /// <param name="windowHandle">Optional window handle (HWND) for Win32 message hook.</param>
        void Start(IntPtr windowHandle = default);

        /// <summary>
        /// Stops clipboard monitoring and releases native hooks.
        /// </summary>
        void Stop();

        /// <summary>
        /// Manually processes a text string as if copied from the clipboard.
        /// Useful for testing, drag/drop, and manual invocation.
        /// </summary>
        bool ProcessText(string? text, string source = "Manual");
    }
}
