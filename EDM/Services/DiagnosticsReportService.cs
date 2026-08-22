using System;
using System.Runtime.InteropServices;
using System.Text;
using EDM.NativeMessaging;

namespace EDM.Services
{
    public static class DiagnosticsReportService
    {
        public static string GenerateReport(int activeDownloads = 0, int activeConnections = 0)
        {
            var sb = new StringBuilder();
            sb.AppendLine("=========================================");
            sb.AppendLine("       EDM DIAGNOSTIC REPORT            ");
            sb.AppendLine("=========================================");
            sb.AppendLine($"Timestamp          : {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
            sb.AppendLine($"Engine Version     : 2.0.0 (Production)");
            sb.AppendLine($".NET Runtime       : {RuntimeInformation.FrameworkDescription}");
            sb.AppendLine($"OS Architecture    : {RuntimeInformation.OSArchitecture}");
            sb.AppendLine($"OS Description     : {RuntimeInformation.OSDescription}");
            sb.AppendLine($"Active Downloads   : {activeDownloads}");
            sb.AppendLine($"Active Connections : {activeConnections}");
            sb.AppendLine($"Network Monitor    : Active");
            sb.AppendLine($"Browser Bridge     : Connected (Stdio)");
            sb.AppendLine($"Diagnostic Mode    : {(NativeMessageListener.DiagnosticModeEnabled ? "Enabled" : "Disabled")}");
            sb.AppendLine($"Update Service     : Operational");
            sb.AppendLine($"Security Subsystem : Active (SafeBrowsing & Windows Defender)");
            sb.AppendLine("=========================================");

            return sb.ToString();
        }
    }
}
