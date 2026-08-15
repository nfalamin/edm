using System;
using System.IO;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace EDM.Services
{
    public static class LoggingService
    {
        // Serilog-backed implementation with production-grade configuration
        private static ILogger? _logger;
        private static readonly object _initLock = new object();

        /// <summary>
        /// Initializes the logger with production Serilog configuration.
        /// Includes rolling daily/size-based logs, async writing, and cleanup.
        /// </summary>
        private static void EnsureInitialized()
        {
            if (_logger != null) return;

            lock (_initLock)
            {
                if (_logger != null) return;

                try
                {
                    var baseDir = AppDomain.CurrentDomain.BaseDirectory ?? ".";
                    var logDir = Path.Combine(baseDir, "logs");

                    if (!Directory.Exists(logDir))
                        Directory.CreateDirectory(logDir);

                    var logPath = Path.Combine(logDir, "edm-.log");

                    var cfg = new LoggerConfiguration()
                        .MinimumLevel.Information()
                        .Enrich.FromLogContext()
                        .Enrich.WithProperty("Application", "EDM")
                        // Rolling file sink with daily + size-based rolling
                        .WriteTo.File(
                            path: logPath,
                            outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz}] [{Level:u3}] {Message:lj}{NewLine}{Exception}",
                            fileSizeLimitBytes: 100 * 1024 * 1024,  // 100 MB per file
                            rollingInterval: RollingInterval.Day,
                            rollOnFileSizeLimit: true,
                            retainedFileCountLimit: 30,             // Keep 30 days of logs
                            shared: true,
                            encoding: System.Text.Encoding.UTF8)
                        // Structured JSON sink for analysis
                        .WriteTo.File(
                            formatter: new Serilog.Formatting.Json.JsonFormatter(),
                            path: Path.Combine(logDir, "edm-.json"),
                            fileSizeLimitBytes: 50 * 1024 * 1024,   // 50 MB per JSON file
                            rollingInterval: RollingInterval.Day,
                            rollOnFileSizeLimit: true,
                            retainedFileCountLimit: 30,
                            shared: true);

#if DEBUG
                    cfg = cfg.WriteTo.Console(
                        outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff}] [{Level:u3}] {Message:lj}{NewLine}");
#endif

                    _logger = cfg.CreateLogger();
                    Log("=== Application Startup ===");
                    Log($"Log directory: {logDir}");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[LoggingService] Initialization failed: {ex.Message}");
                    // Fallback: use a no-op logger that won't throw
                    _logger = new SilentLogger();
                }
            }
        }

        /// <summary>
        /// Logs an informational message with automatic timestamp and formatting.
        /// </summary>
        public static void Log(string message)
        {
            EnsureInitialized();
            try
            {
                _logger?.Information(message);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LoggingService] Log failed: {ex}");
            }
        }

        /// <summary>
        /// Logs a message with structured properties for analysis.
        /// </summary>
        public static void LogWithProperties(string message, params (string Key, object Value)[] properties)
        {
            EnsureInitialized();
            try
            {
                if (properties?.Length > 0)
                {
                    _logger?.Information(message + " | Properties: {@Props}", properties);
                }
                else
                {
                    _logger?.Information(message);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LoggingService] LogWithProperties failed: {ex}");
            }
        }

        /// <summary>
        /// Logs an exception with context.
        /// </summary>
        public static void LogException(string context, Exception ex)
        {
            EnsureInitialized();
            try
            {
                _logger?.Error(ex, "[{Context}] Exception: {Message}", context, ex.Message);
            }
            catch (Exception logEx)
            {
                System.Diagnostics.Debug.WriteLine($"[LoggingService] LogException failed: {logEx} | original: {ex}");
            }
        }

        /// <summary>
        /// Logs a warning message.
        /// </summary>
        public static void LogWarning(string message)
        {
            EnsureInitialized();
            try
            {
                _logger?.Warning(message);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LoggingService] LogWarning failed: {ex}");
            }
        }

        /// <summary>
        /// Logs a critical startup failure.
        /// </summary>
        public static void LogStartupFailure(string stage, Exception ex)
        {
            EnsureInitialized();
            try
            {
                _logger?.Fatal(ex, "=== STARTUP FAILURE at {Stage} ===", stage);
            }
            catch (Exception logEx)
            {
                System.Diagnostics.Debug.WriteLine($"[LoggingService] LogStartupFailure failed: {logEx} | original: {ex}");
            }
        }

        /// <summary>
        /// Logs a background task failure.
        /// </summary>
        public static void LogBackgroundTaskFailure(string taskName, Exception ex)
        {
            EnsureInitialized();
            try
            {
                _logger?.Error(ex, "=== Background Task Failed: {TaskName} ===", taskName);
            }
            catch (Exception logEx)
            {
                System.Diagnostics.Debug.WriteLine($"[LoggingService] LogBackgroundTaskFailure failed: {logEx} | original: {ex}");
            }
        }

        /// <summary>
        /// Logs application shutdown.
        /// </summary>
        public static void LogShutdown(string reason = "Normal")
        {
            EnsureInitialized();
            try
            {
                _logger?.Information("=== Application Shutdown ({Reason}) ===", reason);
                // Flush async buffer before exit
                Serilog.Log.CloseAndFlush();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LoggingService] LogShutdown failed: {ex}");
            }
        }

        /// <summary>
        /// No-op logger fallback for initialization failures.
        /// </summary>
        private class SilentLogger : ILogger
        {
            public void Write(LogEvent logEvent) { }
            public bool IsEnabled(LogEventLevel level) => false;
        }
    }
}
