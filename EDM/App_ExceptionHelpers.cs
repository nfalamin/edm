using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows;
using EDM.Services;

namespace EDM
{
    public partial class App : System.Windows.Application
    {
        private void SaveCrashReport(Exception ex, string context)
        {
            try
            {

                if (ex == null) return;

                // Build minimal report: App version, context, exception type, stack trace

                string version = "unknown";
                try
                {
                    var entry = Assembly.GetEntryAssembly();
                    if (entry != null)
                    {
                        var fvi = FileVersionInfo.GetVersionInfo(entry.Location);
                        version = fvi.ProductVersion ?? fvi.FileVersion ?? entry.GetName().Version?.ToString() ?? "unknown";
                    }
                }
                catch { }

                var sb = new StringBuilder();
                sb.AppendLine($"App Version: {version}");
                sb.AppendLine($"Context: {context}");
                sb.AppendLine($"Exception Type: {ex.GetType().FullName}");
                sb.AppendLine("Stack Trace:");
                sb.AppendLine(ex.StackTrace ?? "(no stack trace)");

                string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EDM", "crash_reports");
                try { Directory.CreateDirectory(folder); } catch (Exception dirEx) { try { EDM.Services.LoggingService.LogException("[AutoFix] Failed to create crash report folder", dirEx); } catch { } }
                string filename = $"crash_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
                string path = Path.Combine(folder, filename);

                // Save file without any personal data beyond the stack trace per policy
                File.WriteAllText(path, sb.ToString());

                LoggingService.Log($"[App] Crash report saved: {path}");
            }
            catch (Exception saveEx)
            {
                // If saving the crash report fails, log the error but do not rethrow.
                try { LoggingService.LogException("[App.SaveCrashReport] Failed to save crash report", saveEx); } catch { }
            }
        }
    }
}
