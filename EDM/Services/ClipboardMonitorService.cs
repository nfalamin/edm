using System;
using System.Threading;
using System.Windows;

namespace EDM.Services
{
    // Simple clipboard polling monitor. Runs a background thread and invokes a callback when a URL-like text appears.
    public class ClipboardMonitorService : IDisposable
    {
        private readonly Action<string> _onUrlDetected;
        private Thread? _thread;
        private bool _running;
        private string? _lastText;

        public ClipboardMonitorService(Action<string> onUrlDetected)
        {
            _onUrlDetected = onUrlDetected ?? throw new ArgumentNullException(nameof(onUrlDetected));
        }

        public void Start()
        {
            if (_running) return;
            _running = true;
            _thread = new Thread(Run) { IsBackground = true };
            _thread.Start();
        }

        public void Stop()
        {
            _running = false;
            try { _thread?.Join(200); } catch (Exception ex) { LoggingService.LogException("[ClipboardMonitorService] Thread join failed", ex); }
        }

        public void Dispose()
        {
            Stop();
        }

        private void Run()
        {
            while (_running)
            {
                try
                {
                    string? txt = null;

                    // If application is shutting down, exit gracefully to avoid Dispatcher.Invoke on closed dispatcher.
                    var app = System.Windows.Application.Current;
                    if (app == null || app.Dispatcher.HasShutdownStarted || app.Dispatcher.HasShutdownFinished)
                    {
                        break;
                    }

                    app.Dispatcher.Invoke(() =>
                    {
                        try { txt = System.Windows.Clipboard.ContainsText() ? System.Windows.Clipboard.GetText() : null; } catch (Exception ex) { LoggingService.LogException("[ClipboardMonitorService] Reading clipboard failed", ex); txt = null; }
                    });

                    if (!string.IsNullOrEmpty(txt) && txt != _lastText)
                    {
                        _lastText = txt;
                        // quick URL heuristic
                        if ((txt.StartsWith("http://") || txt.StartsWith("https://")) && txt.Length < 2000)
                        {
                            try { _onUrlDetected?.Invoke(txt); } catch (Exception ex) { LoggingService.LogException("[ClipboardMonitorService] URL callback failed", ex); }
                        }
                    }
                }
                catch (Exception ex) { LoggingService.LogException("[ClipboardMonitorService] Run loop failed", ex); }

                Thread.Sleep(1200);
            }
        }
    }
}
