using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace EDM.Services
{
    public class LanPeerNode
    {
        public string PeerId { get; set; } = Guid.NewGuid().ToString("N");
        public string MachineName { get; set; } = Environment.MachineName;
        public string IpAddress { get; set; } = string.Empty;
        public int Port { get; set; } = 45824;
        public DateTime LastSeenUtc { get; set; } = DateTime.UtcNow;
        public List<string> SharedFileHashes { get; set; } = new();
    }

    public class LanP2PSharingEngine : IDisposable
    {
        private const int DiscoveryPort = 45824;
        private readonly UdpClient? _udpBeacon;
        private readonly HttpListener? _httpServer;
        private readonly ConcurrentDictionary<string, LanPeerNode> _discoveredPeers = new();
        private readonly ConcurrentDictionary<string, string> _sharedFilesByHash = new(StringComparer.OrdinalIgnoreCase);
        private readonly CancellationTokenSource _cts = new();
        private readonly string _peerId = Guid.NewGuid().ToString("N");
        private bool _isListening;
        private readonly HttpClient _httpClient;

        public bool IsEnabled { get; set; } = true;
        public string PeerName { get; set; } = Environment.MachineName;

        public LanP2PSharingEngine(HttpClient? httpClient = null, bool startListeners = false)
        {
            _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(5) };

            if (startListeners)
            {
                try
                {
                    _udpBeacon = new UdpClient();
                    _udpBeacon.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                    _udpBeacon.Client.Bind(new IPEndPoint(IPAddress.Any, DiscoveryPort));

                    _httpServer = new HttpListener();
                    _httpServer.Prefixes.Add($"http://*:{DiscoveryPort}/edm-p2p/");
                    _httpServer.Start();
                    _isListening = true;

                    Task.Run(() => ListenForPeerBeaconsAsync(_cts.Token));
                    Task.Run(() => HandleHttpRequestsAsync(_cts.Token));
                }
                catch
                {
                    // Fallback gracefully in testing / permission restricted environments
                    _isListening = false;
                }
            }
        }

        public void RegisterSharedFile(string filePath)
        {
            if (!File.Exists(filePath)) return;
            var hash = ComputeFileSha256(filePath);
            _sharedFilesByHash[hash] = filePath;
        }

        public IReadOnlyList<LanPeerNode> GetDiscoveredPeers()
        {
            return _discoveredPeers.Values.ToList().AsReadOnly();
        }

        public void RegisterDiscoveredPeer(LanPeerNode peer)
        {
            if (peer == null || string.IsNullOrWhiteSpace(peer.PeerId)) return;
            peer.LastSeenUtc = DateTime.UtcNow;
            _discoveredPeers[peer.PeerId] = peer;
        }

        public async Task BroadcastPresenceBeaconAsync(CancellationToken ct = default)
        {
            if (!IsEnabled || _udpBeacon == null) return;
            try
            {
                var payload = new LanPeerNode
                {
                    PeerId = _peerId,
                    MachineName = PeerName,
                    Port = DiscoveryPort,
                    SharedFileHashes = _sharedFilesByHash.Keys.ToList()
                };

                var json = JsonSerializer.Serialize(payload);
                var bytes = Encoding.UTF8.GetBytes(json);
                var broadcastEp = new IPEndPoint(IPAddress.Broadcast, DiscoveryPort);
                await _udpBeacon.SendAsync(bytes, bytes.Length, broadcastEp).ConfigureAwait(false);
            }
            catch
            {
                // Ignore transient UDP broadcast drops
            }
        }

        public async Task<bool> TryDownloadFromLanPeerAsync(
            string fileHash, 
            string saveDestinationPath, 
            IProgress<double>? progress = null, 
            CancellationToken ct = default)
        {
            if (!IsEnabled) return false;

            // Look for peer with matching hash
            foreach (var peer in _discoveredPeers.Values)
            {
                if (peer.SharedFileHashes.Contains(fileHash, StringComparer.OrdinalIgnoreCase))
                {
                    try
                    {
                        var url = $"http://{peer.IpAddress}:{peer.Port}/edm-p2p/download?hash={fileHash}";
                        using var resp = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
                        if (resp.IsSuccessStatusCode)
                        {
                            var total = resp.Content.Headers.ContentLength ?? -1L;
                            using var src = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
                            using var dst = new FileStream(saveDestinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 64 * 1024, true);

                            var buffer = System.Buffers.ArrayPool<byte>.Shared.Rent(64 * 1024);
                            long readBytes = 0;
                            try
                            {
                                int n;
                                while ((n = await src.ReadAsync(buffer.AsMemory(0, buffer.Length), ct).ConfigureAwait(false)) > 0)
                                {
                                    await dst.WriteAsync(buffer.AsMemory(0, n), ct).ConfigureAwait(false);
                                    readBytes += n;
                                    if (total > 0 && progress != null)
                                    {
                                        progress.Report((double)readBytes / total * 100.0);
                                    }
                                }
                            }
                            finally
                            {
                                System.Buffers.ArrayPool<byte>.Shared.Return(buffer);
                            }

                            // Verify integrity of downloaded file
                            var localHash = ComputeFileSha256(saveDestinationPath);
                            if (string.Equals(localHash, fileHash, StringComparison.OrdinalIgnoreCase))
                            {
                                return true;
                            }
                        }
                    }
                    catch
                    {
                        // Fallback to WAN if peer fails
                    }
                }
            }

            return false;
        }

        private async Task ListenForPeerBeaconsAsync(CancellationToken ct)
        {
            if (_udpBeacon == null) return;
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var result = await _udpBeacon.ReceiveAsync(ct).ConfigureAwait(false);
                    var json = Encoding.UTF8.GetString(result.Buffer);
                    var peer = JsonSerializer.Deserialize<LanPeerNode>(json);
                    if (peer != null && peer.PeerId != _peerId)
                    {
                        peer.IpAddress = result.RemoteEndPoint.Address.ToString();
                        RegisterDiscoveredPeer(peer);
                    }
                }
                catch (OperationCanceledException) { break; }
                catch { }
            }
        }

        private async Task HandleHttpRequestsAsync(CancellationToken ct)
        {
            if (_httpServer == null) return;
            while (!ct.IsCancellationRequested && _httpServer.IsListening)
            {
                try
                {
                    var ctx = await _httpServer.GetContextAsync().ConfigureAwait(false);
                    var req = ctx.Request;
                    var resp = ctx.Response;

                    if (req.HttpMethod == "GET" && req.Url?.AbsolutePath.EndsWith("/download") == true)
                    {
                        var hash = req.QueryString["hash"];
                        if (!string.IsNullOrEmpty(hash) && _sharedFilesByHash.TryGetValue(hash, out var filePath) && File.Exists(filePath))
                        {
                            var fileBytes = await File.ReadAllBytesAsync(filePath, ct).ConfigureAwait(false);
                            resp.ContentType = "application/octet-stream";
                            resp.ContentLength64 = fileBytes.Length;
                            resp.StatusCode = 200;
                            await resp.OutputStream.WriteAsync(fileBytes, 0, fileBytes.Length, ct).ConfigureAwait(false);
                            resp.OutputStream.Close();
                            continue;
                        }
                    }

                    resp.StatusCode = 404;
                    resp.Close();
                }
                catch (OperationCanceledException) { break; }
                catch { }
            }
        }

        public static string ComputeFileSha256(string filePath)
        {
            using var sha = SHA256.Create();
            using var stream = File.OpenRead(filePath);
            var hash = sha.ComputeHash(stream);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }

        public void Dispose()
        {
            _cts.Cancel();
            try { _udpBeacon?.Dispose(); } catch { }
            try { _httpServer?.Stop(); _httpServer?.Close(); } catch { }
            _cts.Dispose();
        }
    }
}
