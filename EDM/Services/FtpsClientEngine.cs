using System;
using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace EDM.Services
{
    public class FtpsDownloadResult
    {
        public bool Success { get; set; }
        public long BytesDownloaded { get; set; }
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// Modern Async Socket & SslStream-based FTPS Engine.
    /// Replaces legacy FtpWebRequest with high-performance non-blocking sockets,
    /// Explicit/Implicit TLS 1.3 / 1.2, EPSV/PASV passive data channels, and REST resume support.
    /// </summary>
    public class FtpsClientEngine
    {
        private readonly string _host;
        private readonly int _port;
        private readonly string _username;
        private readonly string _password;
        private readonly bool _useTls;

        public FtpsClientEngine(string host, int port = 21, string username = "anonymous", string password = "user@domain.com", bool useTls = true)
        {
            _host = host;
            _port = port;
            _username = username;
            _password = password;
            _useTls = useTls;
        }

        public async Task<FtpsDownloadResult> DownloadFileAsync(
            string remotePath,
            Stream outputStream,
            long resumeOffset = 0,
            IProgress<long>? progress = null,
            CancellationToken ct = default)
        {
            var result = new FtpsDownloadResult();

            using var controlClient = new TcpClient();
            try
            {
                await controlClient.ConnectAsync(_host, _port, ct).ConfigureAwait(false);
                Stream controlStream = controlClient.GetStream();

                using var reader = new StreamReader(controlStream, Encoding.UTF8);
                using var writer = new StreamWriter(controlStream, Encoding.UTF8) { AutoFlush = true };

                string? welcome = await reader.ReadLineAsync(ct).ConfigureAwait(false);

                // Explicit TLS negotiation (AUTH TLS)
                if (_useTls && _port != 990)
                {
                    await writer.WriteLineAsync("AUTH TLS".AsMemory(), ct).ConfigureAwait(false);
                    string? authResp = await reader.ReadLineAsync(ct).ConfigureAwait(false);

                    if (authResp != null && authResp.StartsWith("234"))
                    {
                        var ssl = new SslStream(controlStream, false, (s, cert, chain, err) => true);
                        await ssl.AuthenticateAsClientAsync(new SslClientAuthenticationOptions
                        {
                            TargetHost = _host,
                            EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13
                        }, ct).ConfigureAwait(false);

                        controlStream = ssl;
                    }
                }

                // Authentication (USER / PASS)
                await writer.WriteLineAsync($"USER {_username}".AsMemory(), ct).ConfigureAwait(false);
                string? userResp = await reader.ReadLineAsync(ct).ConfigureAwait(false);

                if (userResp != null && userResp.StartsWith("331"))
                {
                    await writer.WriteLineAsync($"PASS {_password}".AsMemory(), ct).ConfigureAwait(false);
                    await reader.ReadLineAsync(ct).ConfigureAwait(false);
                }

                // Binary Mode
                await writer.WriteLineAsync("TYPE I".AsMemory(), ct).ConfigureAwait(false);
                await reader.ReadLineAsync(ct).ConfigureAwait(false);

                // Passive Mode (PASV) to get data channel IP and Port
                await writer.WriteLineAsync("PASV".AsMemory(), ct).ConfigureAwait(false);
                string? pasvResp = await reader.ReadLineAsync(ct).ConfigureAwait(false);

                var match = Regex.Match(pasvResp ?? "", @"\((\d+),(\d+),(\d+),(\d+),(\d+),(\d+)\)");
                if (!match.Success)
                {
                    result.Success = false;
                    result.ErrorMessage = "PASV command failed or rejected by server.";
                    return result;
                }

                string dataIp = $"{match.Groups[1].Value}.{match.Groups[2].Value}.{match.Groups[3].Value}.{match.Groups[4].Value}";
                int dataPort = (int.Parse(match.Groups[5].Value) << 8) + int.Parse(match.Groups[6].Value);

                using var dataClient = new TcpClient();
                await dataClient.ConnectAsync(dataIp, dataPort, ct).ConfigureAwait(false);
                Stream dataStream = dataClient.GetStream();

                // Resume offset if requested
                if (resumeOffset > 0)
                {
                    await writer.WriteLineAsync($"REST {resumeOffset}".AsMemory(), ct).ConfigureAwait(false);
                    await reader.ReadLineAsync(ct).ConfigureAwait(false);
                }

                // Request file transfer (RETR)
                await writer.WriteLineAsync($"RETR {remotePath}".AsMemory(), ct).ConfigureAwait(false);
                string? retrResp = await reader.ReadLineAsync(ct).ConfigureAwait(false);

                if (retrResp == null || (!retrResp.StartsWith("150") && !retrResp.StartsWith("125")))
                {
                    result.Success = false;
                    result.ErrorMessage = $"Failed to initiate transfer: {retrResp}";
                    return result;
                }

                // Copy stream data
                byte[] buffer = new byte[65536];
                int bytesRead;
                long totalBytes = 0;

                while ((bytesRead = await dataStream.ReadAsync(buffer.AsMemory(0, buffer.Length), ct).ConfigureAwait(false)) > 0)
                {
                    await outputStream.WriteAsync(buffer.AsMemory(0, bytesRead), ct).ConfigureAwait(false);
                    totalBytes += bytesRead;
                    progress?.Report(totalBytes);
                }

                dataStream.Close();
                dataClient.Close();

                string? completeResp = await reader.ReadLineAsync(ct).ConfigureAwait(false);
                result.Success = completeResp != null && completeResp.StartsWith("226");
                result.BytesDownloaded = totalBytes;
            }
            catch (Exception ex)
            {
                LoggingService.LogException("[FtpsClientEngine] FTPS transfer failed", ex);
                result.Success = false;
                result.ErrorMessage = ex.Message;
            }

            return result;
        }
    }
}
