using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace EDM.Services
{
    public class HlsAudioTrack
    {
        public string GroupId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Language { get; set; } = string.Empty;
        public string Uri { get; set; } = string.Empty;
        public bool IsDefault { get; set; }
    }

    public class HlsVariant
    {
        public string Uri { get; set; } = string.Empty;
        public int Bandwidth { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public double FrameRate { get; set; }
        public string Resolution => Width > 0 && Height > 0 ? $"{Width}x{Height} ({Height}p)" : "Unknown";
        public string Codecs { get; set; } = string.Empty;
        public string AudioGroupId { get; set; } = string.Empty;
        public bool HasAudio { get; set; } = true;
    }

    public class HlsMasterPlaylist
    {
        public bool IsMaster { get; set; }
        public bool IsDrmProtected { get; set; }
        public double TotalDurationSeconds { get; set; }
        public List<HlsVariant> Variants { get; set; } = new List<HlsVariant>();
        public List<HlsAudioTrack> AudioTracks { get; set; } = new List<HlsAudioTrack>();
        public List<string> SegmentUrls { get; set; } = new List<string>();
    }

    public static class HlsParser
    {
        public static HlsMasterPlaylist Parse(string m3u8Content, Uri baseUri)
        {
            var playlist = new HlsMasterPlaylist();
            if (string.IsNullOrWhiteSpace(m3u8Content)) return playlist;

            var lines = m3u8Content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
                                  .Select(l => l.Trim())
                                  .ToList();

            if (!lines.Any() || !lines[0].StartsWith("#EXTM3U")) return playlist;

            // DRM check
            if (lines.Any(l => l.StartsWith("#EXT-X-KEY:") && !l.Contains("METHOD=NONE")))
            {
                playlist.IsDrmProtected = true;
            }

            // Extract Audio Media Tracks (#EXT-X-MEDIA:TYPE=AUDIO)
            foreach (var line in lines.Where(l => l.StartsWith("#EXT-X-MEDIA:")))
            {
                if (line.Contains("TYPE=AUDIO"))
                {
                    var track = new HlsAudioTrack();
                    var gMatch = Regex.Match(line, @"GROUP-ID=""([^""]+)""");
                    if (gMatch.Success) track.GroupId = gMatch.Groups[1].Value;

                    var nMatch = Regex.Match(line, @"NAME=""([^""]+)""");
                    if (nMatch.Success) track.Name = nMatch.Groups[1].Value;

                    var lMatch = Regex.Match(line, @"LANGUAGE=""([^""]+)""");
                    if (lMatch.Success) track.Language = lMatch.Groups[1].Value;

                    var uMatch = Regex.Match(line, @"URI=""([^""]+)""");
                    if (uMatch.Success) track.Uri = MakeAbsoluteUri(uMatch.Groups[1].Value, baseUri);

                    track.IsDefault = line.Contains("DEFAULT=YES");
                    playlist.AudioTracks.Add(track);
                }
            }

            // Check if Master Playlist
            if (lines.Any(l => l.StartsWith("#EXT-X-STREAM-INF")))
            {
                playlist.IsMaster = true;
                HlsVariant? currentVariant = null;

                foreach (var line in lines)
                {
                    if (line.StartsWith("#EXT-X-STREAM-INF:"))
                    {
                        currentVariant = new HlsVariant();
                        string attrLine = line.Substring("#EXT-X-STREAM-INF:".Length);

                        var bwMatch = Regex.Match(attrLine, @"BANDWIDTH=(\d+)");
                        if (bwMatch.Success && int.TryParse(bwMatch.Groups[1].Value, out int bw))
                        {
                            currentVariant.Bandwidth = bw;
                        }

                        var resMatch = Regex.Match(attrLine, @"RESOLUTION=(\d+)x(\d+)");
                        if (resMatch.Success)
                        {
                            if (int.TryParse(resMatch.Groups[1].Value, out int w)) currentVariant.Width = w;
                            if (int.TryParse(resMatch.Groups[2].Value, out int h)) currentVariant.Height = h;
                        }

                        var fpsMatch = Regex.Match(attrLine, @"FRAME-RATE=([\d\.]+)");
                        if (fpsMatch.Success && double.TryParse(fpsMatch.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double fps))
                        {
                            currentVariant.FrameRate = fps;
                        }

                        var codecMatch = Regex.Match(attrLine, @"CODECS=""([^""]+)""");
                        if (codecMatch.Success)
                        {
                            currentVariant.Codecs = codecMatch.Groups[1].Value;
                        }

                        var audioGroupMatch = Regex.Match(attrLine, @"AUDIO=""([^""]+)""");
                        if (audioGroupMatch.Success)
                        {
                            currentVariant.AudioGroupId = audioGroupMatch.Groups[1].Value;
                        }
                    }
                    else if (!line.StartsWith("#") && currentVariant != null)
                    {
                        currentVariant.Uri = MakeAbsoluteUri(line, baseUri);
                        playlist.Variants.Add(currentVariant);
                        currentVariant = null;
                    }
                }
            }
            else
            {
                playlist.IsMaster = false;
                double totalDuration = 0;
                foreach (var line in lines)
                {
                    if (line.StartsWith("#EXTINF:"))
                    {
                        var durStr = line.Substring("#EXTINF:".Length).Split(',')[0];
                        if (double.TryParse(durStr, NumberStyles.Any, CultureInfo.InvariantCulture, out double d))
                        {
                            totalDuration += d;
                        }
                    }
                    else if (!line.StartsWith("#"))
                    {
                        playlist.SegmentUrls.Add(MakeAbsoluteUri(line, baseUri));
                    }
                }
                playlist.TotalDurationSeconds = totalDuration;
            }

            return playlist;
        }

        private static string MakeAbsoluteUri(string relativeOrAbsolute, Uri baseUri)
        {
            if (Uri.TryCreate(relativeOrAbsolute, UriKind.Absolute, out var absUri))
            {
                return absUri.ToString();
            }
            if (Uri.TryCreate(baseUri, relativeOrAbsolute, out var resolvedUri))
            {
                return resolvedUri.ToString();
            }
            return relativeOrAbsolute;
        }
    }
}
