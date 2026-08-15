using System;
using System.IO;
using System.Text.Json;
using System.Threading;

namespace EDM.Infrastructure
{
    // Lightweight structured JSON logger (fallback, no external dependencies)
    public static class LoggingSetup
    {
        private static string? _logFilePath;
        private static readonly object _sync = new object();

        public static void Configure(string logsDirectory)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(logsDirectory)) logsDirectory = "logs";
                Directory.CreateDirectory(logsDirectory);
                _logFilePath = Path.Combine(logsDirectory, "edm-" + DateTime.UtcNow.ToString("yyyyMMdd") + ".jsonl");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LoggingSetup] Configure failed: {ex}");
                _logFilePath = null;
            }
        }

        public static void Log(string level, string message, object? props = null)
        {
            try
            {
                var entry = new
                {
                    Timestamp = DateTime.UtcNow.ToString("o"),
                    Level = level,
                    Message = message,
                    Properties = props
                };
                var line = JsonSerializer.Serialize(entry) + Environment.NewLine;
                if (!string.IsNullOrEmpty(_logFilePath))
                {
                    lock (_sync) File.AppendAllText(_logFilePath, line);
                }
                else
                {
                    // fallback to console
                    try { Console.WriteLine(line); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[LoggingSetup] Console fallback failed: {ex}"); }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LoggingSetup] Log failed: {ex}");
            }
        }
    }
}
