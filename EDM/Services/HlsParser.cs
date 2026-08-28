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
        public bool IsAutoSelect { get; set; }
        public bool IsForced { get; set; }
        public int Channels { get; set; } = 2;
    }

    public class HlsSubtitleTrack
    {
        public string GroupId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Language { get; set; } = string.Empty;
        public string Uri { get; set; } = string.Empty;
        public bool IsDefault { get; set; }
        public bool IsAutoSelect { get; set; }
        public bool IsForced { get; set; }
    }

    public class HlsSegment
    {
        public string Uri { get; set; } = string.Empty;
        public double DurationSeconds { get; set; }
        public long SequenceNumber { get; set; }
        public long? ByteRangeOffset { get; set; }
        public long? ByteRangeLength { get; set; }
        public string KeyMethod { get; set; } = "NONE";
        public string? KeyUri { get; set; }
        public byte[]? KeyIv { get; set; }
        public string? KeyFormat { get; set; }
        public string? InitSegmentUri { get; set; }
        public long? InitByteRangeOffset { get; set; }
        public long? InitByteRangeLength { get; set; }
        public string Title { get; set; } = string.Empty;
        public bool IsDiscontinuity { get; set; }
        public long DiscontinuitySequence { get; set; }
        public string? ProgramDateTime { get; set; }
    }

    public class HlsVariant
    {
        public string Uri { get; set; } = string.Empty;
        public int Bandwidth { get; set; }
        public int AverageBandwidth { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public double FrameRate { get; set; }
        public string Resolution => Width > 0 && Height > 0 ? $"{Width}x{Height} ({Height}p)" : "Unknown";
        public string Codecs { get; set; } = string.Empty;
        public string AudioGroupId { get; set; } = string.Empty;
        public string SubtitlesGroupId { get; set; } = string.Empty;
        public bool HasAudio { get; set; } = true;
        public bool IsIFrameOnly { get; set; }
    }

    public class HlsMasterPlaylist
    {
        public bool IsMaster { get; set; }
        public bool IsLive { get; set; }
        public int Version { get; set; } = 3;
        public string PlaylistType { get; set; } = string.Empty; // VOD, EVENT, LIVE
        public bool IsDrmProtected { get; set; }
        public string DrmSystem { get; set; } = string.Empty;
        public double TargetDurationSeconds { get; set; }
        public long MediaSequence { get; set; }
        public long DiscontinuitySequence { get; set; }
        public double TotalDurationSeconds { get; set; }
        public List<HlsVariant> Variants { get; set; } = new List<HlsVariant>();
        public List<HlsVariant> IFrameVariants { get; set; } = new List<HlsVariant>();
        public List<HlsAudioTrack> AudioTracks { get; set; } = new List<HlsAudioTrack>();
        public List<HlsSubtitleTrack> SubtitleTracks { get; set; } = new List<HlsSubtitleTrack>();
        public List<HlsSegment> Segments { get; set; } = new List<HlsSegment>();
        public List<string> SegmentUrls => Segments.Select(s => s.Uri).ToList();
    }

    public static class HlsParser
    {
        public const int MaxPlaylistTextLength = 10 * 1024 * 1024; // 10 MB
        public const int MaxSegmentCount = 50000;

        public static HlsMasterPlaylist Parse(string m3u8Content, Uri baseUri)
        {
            var playlist = new HlsMasterPlaylist();
            if (string.IsNullOrWhiteSpace(m3u8Content) || m3u8Content.Length > MaxPlaylistTextLength) return playlist;

            var lines = m3u8Content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
                                  .Select(l => l.Trim())
                                  .ToList();

            if (!lines.Any() || !lines[0].StartsWith("#EXTM3U")) return playlist;

            // 0. Parse Global Manifest Tags
            foreach (var line in lines)
            {
                if (line.StartsWith("#EXT-X-VERSION:"))
                {
                    if (int.TryParse(line.Substring("#EXT-X-VERSION:".Length).Trim(), out int ver))
                    {
                        playlist.Version = ver;
                    }
                }
                else if (line.StartsWith("#EXT-X-PLAYLIST-TYPE:"))
                {
                    playlist.PlaylistType = line.Substring("#EXT-X-PLAYLIST-TYPE:".Length).Trim().ToUpperInvariant();
                }
                else if (line.StartsWith("#EXT-X-DISCONTINUITY-SEQUENCE:"))
                {
                    if (long.TryParse(line.Substring("#EXT-X-DISCONTINUITY-SEQUENCE:".Length).Trim(), out long discSeq))
                    {
                        playlist.DiscontinuitySequence = discSeq;
                    }
                }
            }

            // 1. Check DRM vs Standard AES-128 Encryption
            foreach (var line in lines.Where(l => l.StartsWith("#EXT-X-KEY:")))
            {
                var methodMatch = Regex.Match(line, @"METHOD=([^,\s]+)");
                string method = methodMatch.Success ? methodMatch.Groups[1].Value.ToUpperInvariant() : "NONE";

                var keyFormatMatch = Regex.Match(line, @"KEYFORMAT=""([^""]+)""");
                string keyFormat = keyFormatMatch.Success ? keyFormatMatch.Groups[1].Value : "identity";

                if (keyFormat.Contains("widevine", StringComparison.OrdinalIgnoreCase) ||
                    keyFormat.Contains("edef8ba9", StringComparison.OrdinalIgnoreCase))
                {
                    playlist.IsDrmProtected = true;
                    playlist.DrmSystem = "Widevine";
                }
                else if (keyFormat.Contains("playready", StringComparison.OrdinalIgnoreCase) ||
                         keyFormat.Contains("9a04f079", StringComparison.OrdinalIgnoreCase))
                {
                    playlist.IsDrmProtected = true;
                    playlist.DrmSystem = "PlayReady";
                }
                else if (keyFormat.Contains("streamingkeydelivery", StringComparison.OrdinalIgnoreCase) ||
                         keyFormat.Contains("fairplay", StringComparison.OrdinalIgnoreCase))
                {
                    playlist.IsDrmProtected = true;
                    playlist.DrmSystem = "FairPlay";
                }
                else if (method.Equals("SAMPLE-AES", StringComparison.OrdinalIgnoreCase) ||
                         method.Equals("SAMPLE-AES-CTR", StringComparison.OrdinalIgnoreCase))
                {
                    playlist.IsDrmProtected = true;
                    playlist.DrmSystem = $"{method} (DRM)";
                }
            }

            // 2. Extract Media Tracks (#EXT-X-MEDIA)
            foreach (var line in lines.Where(l => l.StartsWith("#EXT-X-MEDIA:")))
            {
                string attrLine = line.Substring("#EXT-X-MEDIA:".Length);
                var typeMatch = Regex.Match(attrLine, @"TYPE=([^,\s]+)");
                string type = typeMatch.Success ? typeMatch.Groups[1].Value.ToUpperInvariant() : "";

                var gMatch = Regex.Match(attrLine, @"GROUP-ID=""([^""]+)""");
                var nMatch = Regex.Match(attrLine, @"NAME=""([^""]+)""");
                var lMatch = Regex.Match(attrLine, @"LANGUAGE=""([^""]+)""");
                var uMatch = Regex.Match(attrLine, @"URI=""([^""]+)""");
                var cMatch = Regex.Match(attrLine, @"CHANNELS=""?(\d+)""?");

                int channels = cMatch.Success && int.TryParse(cMatch.Groups[1].Value, out int ch) ? ch : 2;

                if (type == "AUDIO")
                {
                    var track = new HlsAudioTrack
                    {
                        GroupId = gMatch.Success ? gMatch.Groups[1].Value : "",
                        Name = nMatch.Success ? nMatch.Groups[1].Value : "Audio",
                        Language = lMatch.Success ? lMatch.Groups[1].Value : "",
                        Uri = uMatch.Success ? MakeAbsoluteUri(uMatch.Groups[1].Value, baseUri) : "",
                        IsDefault = attrLine.Contains("DEFAULT=YES"),
                        IsAutoSelect = attrLine.Contains("AUTOSELECT=YES"),
                        IsForced = attrLine.Contains("FORCED=YES"),
                        Channels = channels
                    };
                    playlist.AudioTracks.Add(track);
                }
                else if (type == "SUBTITLES")
                {
                    var sub = new HlsSubtitleTrack
                    {
                        GroupId = gMatch.Success ? gMatch.Groups[1].Value : "",
                        Name = nMatch.Success ? nMatch.Groups[1].Value : "Subtitles",
                        Language = lMatch.Success ? lMatch.Groups[1].Value : "",
                        Uri = uMatch.Success ? MakeAbsoluteUri(uMatch.Groups[1].Value, baseUri) : "",
                        IsDefault = attrLine.Contains("DEFAULT=YES"),
                        IsAutoSelect = attrLine.Contains("AUTOSELECT=YES"),
                        IsForced = attrLine.Contains("FORCED=YES")
                    };
                    playlist.SubtitleTracks.Add(sub);
                }
            }

            // 3. Extract I-Frame Only Streams (#EXT-X-I-FRAME-STREAM-INF)
            foreach (var line in lines.Where(l => l.StartsWith("#EXT-X-I-FRAME-STREAM-INF:")))
            {
                string attrLine = line.Substring("#EXT-X-I-FRAME-STREAM-INF:".Length);
                var iframeVariant = ParseVariantAttributes(attrLine, baseUri);
                iframeVariant.IsIFrameOnly = true;

                var uMatch = Regex.Match(attrLine, @"URI=""([^""]+)""");
                if (uMatch.Success)
                {
                    iframeVariant.Uri = MakeAbsoluteUri(uMatch.Groups[1].Value, baseUri);
                }

                playlist.IFrameVariants.Add(iframeVariant);
            }

            // 4. Check if Master Playlist (#EXT-X-STREAM-INF)
            if (lines.Any(l => l.StartsWith("#EXT-X-STREAM-INF")))
            {
                playlist.IsMaster = true;
                HlsVariant? currentVariant = null;

                foreach (var line in lines)
                {
                    if (line.StartsWith("#EXT-X-STREAM-INF:"))
                    {
                        string attrLine = line.Substring("#EXT-X-STREAM-INF:".Length);
                        currentVariant = ParseVariantAttributes(attrLine, baseUri);
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
                // 5. Media Playlist Parsing
                playlist.IsMaster = false;
                bool hasEndList = lines.Any(l => l.StartsWith("#EXT-X-ENDLIST"));
                playlist.IsLive = !hasEndList && !playlist.PlaylistType.Equals("VOD", StringComparison.OrdinalIgnoreCase);

                // Target Duration
                var targetDurLine = lines.FirstOrDefault(l => l.StartsWith("#EXT-X-TARGETDURATION:"));
                if (targetDurLine != null)
                {
                    string durStr = targetDurLine.Substring("#EXT-X-TARGETDURATION:".Length).Trim();
                    if (double.TryParse(durStr, NumberStyles.Any, CultureInfo.InvariantCulture, out double td))
                    {
                        playlist.TargetDurationSeconds = td;
                    }
                }

                // Media Sequence
                var mediaSeqLine = lines.FirstOrDefault(l => l.StartsWith("#EXT-X-MEDIA-SEQUENCE:"));
                long currentSequence = 0;
                if (mediaSeqLine != null)
                {
                    string seqStr = mediaSeqLine.Substring("#EXT-X-MEDIA-SEQUENCE:".Length).Trim();
                    if (long.TryParse(seqStr, out long ms))
                    {
                        currentSequence = ms;
                        playlist.MediaSequence = ms;
                    }
                }

                double currentDuration = 0;
                string currentTitle = string.Empty;
                string? currentProgramDateTime = null;
                long? currentByteRangeOffset = null;
                long? currentByteRangeLength = null;
                long lastByteRangeEnd = 0;

                string currentKeyMethod = "NONE";
                string? currentKeyUri = null;
                byte[]? currentKeyIv = null;
                string? currentKeyFormat = null;

                string? currentInitUri = null;
                long? currentInitOffset = null;
                long? currentInitLength = null;

                bool isDiscontinuity = false;
                long runningDiscontinuitySeq = playlist.DiscontinuitySequence;
                double totalDuration = 0;

                foreach (var line in lines)
                {
                    if (line.StartsWith("#EXT-X-TARGETDURATION:")) continue;
                    if (line.StartsWith("#EXT-X-MEDIA-SEQUENCE:")) continue;
                    if (line.StartsWith("#EXT-X-ENDLIST")) continue;
                    if (line.StartsWith("#EXT-X-VERSION:")) continue;
                    if (line.StartsWith("#EXT-X-PLAYLIST-TYPE:")) continue;
                    if (line.StartsWith("#EXT-X-DISCONTINUITY-SEQUENCE:")) continue;

                    if (line.StartsWith("#EXT-X-DISCONTINUITY"))
                    {
                        isDiscontinuity = true;
                        runningDiscontinuitySeq++;
                    }
                    else if (line.StartsWith("#EXT-X-PROGRAM-DATE-TIME:"))
                    {
                        currentProgramDateTime = line.Substring("#EXT-X-PROGRAM-DATE-TIME:".Length).Trim();
                    }
                    else if (line.StartsWith("#EXT-X-KEY:"))
                    {
                        string attrLine = line.Substring("#EXT-X-KEY:".Length);
                        var mMatch = Regex.Match(attrLine, @"METHOD=([^,\s]+)");
                        currentKeyMethod = mMatch.Success ? mMatch.Groups[1].Value : "NONE";

                        var uMatch = Regex.Match(attrLine, @"URI=""([^""]+)""");
                        currentKeyUri = uMatch.Success ? MakeAbsoluteUri(uMatch.Groups[1].Value, baseUri) : null;

                        var ivMatch = Regex.Match(attrLine, @"IV=0x([0-9a-fA-F]+)");
                        if (ivMatch.Success)
                        {
                            currentKeyIv = ParseHexStringToBytes(ivMatch.Groups[1].Value);
                        }
                        else
                        {
                            currentKeyIv = null;
                        }

                        var kfMatch = Regex.Match(attrLine, @"KEYFORMAT=""([^""]+)""");
                        currentKeyFormat = kfMatch.Success ? kfMatch.Groups[1].Value : null;
                    }
                    else if (line.StartsWith("#EXT-X-MAP:"))
                    {
                        string attrLine = line.Substring("#EXT-X-MAP:".Length);
                        var uMatch = Regex.Match(attrLine, @"URI=""([^""]+)""");
                        if (uMatch.Success)
                        {
                            currentInitUri = MakeAbsoluteUri(uMatch.Groups[1].Value, baseUri);
                        }

                        var brMatch = Regex.Match(attrLine, @"BYTERANGE=""?(\d+)(?:@(\d+))?""?");
                        if (brMatch.Success)
                        {
                            if (long.TryParse(brMatch.Groups[1].Value, out long len)) currentInitLength = len;
                            if (brMatch.Groups[2].Success && long.TryParse(brMatch.Groups[2].Value, out long off)) currentInitOffset = off;
                        }
                    }
                    else if (line.StartsWith("#EXT-X-BYTERANGE:"))
                    {
                        string brStr = line.Substring("#EXT-X-BYTERANGE:".Length).Trim();
                        var parts = brStr.Split('@');
                        if (long.TryParse(parts[0], out long len))
                        {
                            currentByteRangeLength = len;
                            if (parts.Length > 1 && long.TryParse(parts[1], out long off))
                            {
                                currentByteRangeOffset = off;
                                lastByteRangeEnd = off + len;
                            }
                            else
                            {
                                currentByteRangeOffset = lastByteRangeEnd;
                                lastByteRangeEnd += len;
                            }
                        }
                    }
                    else if (line.StartsWith("#EXTINF:"))
                    {
                        string inf = line.Substring("#EXTINF:".Length);
                        var commaIdx = inf.IndexOf(',');
                        string durStr = commaIdx >= 0 ? inf.Substring(0, commaIdx) : inf;
                        if (double.TryParse(durStr, NumberStyles.Any, CultureInfo.InvariantCulture, out double d))
                        {
                            currentDuration = d;
                            totalDuration += d;
                        }
                        currentTitle = commaIdx >= 0 && commaIdx + 1 < inf.Length ? inf.Substring(commaIdx + 1).Trim() : "";
                    }
                    else if (!line.StartsWith("#"))
                    {
                        // Media Segment Line
                        var segment = new HlsSegment
                        {
                            Uri = MakeAbsoluteUri(line, baseUri),
                            DurationSeconds = currentDuration,
                            SequenceNumber = currentSequence++,
                            ByteRangeOffset = currentByteRangeOffset,
                            ByteRangeLength = currentByteRangeLength,
                            KeyMethod = currentKeyMethod,
                            KeyUri = currentKeyUri,
                            KeyIv = currentKeyIv,
                            KeyFormat = currentKeyFormat,
                            InitSegmentUri = currentInitUri,
                            InitByteRangeOffset = currentInitOffset,
                            InitByteRangeLength = currentInitLength,
                            Title = currentTitle,
                            IsDiscontinuity = isDiscontinuity,
                            DiscontinuitySequence = runningDiscontinuitySeq,
                            ProgramDateTime = currentProgramDateTime
                        };

                        playlist.Segments.Add(segment);
                        if (playlist.Segments.Count >= MaxSegmentCount)
                        {
                            break;
                        }

                        // Reset per-segment flags
                        currentDuration = 0;
                        currentTitle = string.Empty;
                        currentProgramDateTime = null;
                        currentByteRangeOffset = null;
                        currentByteRangeLength = null;
                        isDiscontinuity = false;
                    }
                }

                playlist.TotalDurationSeconds = totalDuration;
            }

            return playlist;
        }

        private static HlsVariant ParseVariantAttributes(string attrLine, Uri baseUri)
        {
            var variant = new HlsVariant();

            var bwMatch = Regex.Match(attrLine, @"BANDWIDTH=(\d+)");
            if (bwMatch.Success && int.TryParse(bwMatch.Groups[1].Value, out int bw))
            {
                variant.Bandwidth = bw;
            }

            var avgBwMatch = Regex.Match(attrLine, @"AVERAGE-BANDWIDTH=(\d+)");
            if (avgBwMatch.Success && int.TryParse(avgBwMatch.Groups[1].Value, out int avgBw))
            {
                variant.AverageBandwidth = avgBw;
            }

            var resMatch = Regex.Match(attrLine, @"RESOLUTION=(\d+)x(\d+)");
            if (resMatch.Success)
            {
                if (int.TryParse(resMatch.Groups[1].Value, out int w)) variant.Width = w;
                if (int.TryParse(resMatch.Groups[2].Value, out int h)) variant.Height = h;
            }

            var fpsMatch = Regex.Match(attrLine, @"FRAME-RATE=([\d\.]+)");
            if (fpsMatch.Success && double.TryParse(fpsMatch.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double fps))
            {
                variant.FrameRate = fps;
            }

            var codecMatch = Regex.Match(attrLine, @"CODECS=""([^""]+)""");
            if (codecMatch.Success)
            {
                variant.Codecs = codecMatch.Groups[1].Value;
            }

            var audioGroupMatch = Regex.Match(attrLine, @"AUDIO=""([^""]+)""");
            if (audioGroupMatch.Success)
            {
                variant.AudioGroupId = audioGroupMatch.Groups[1].Value;
            }

            var subGroupMatch = Regex.Match(attrLine, @"SUBTITLES=""([^""]+)""");
            if (subGroupMatch.Success)
            {
                variant.SubtitlesGroupId = subGroupMatch.Groups[1].Value;
            }

            return variant;
        }

        private static byte[] ParseHexStringToBytes(string hex)
        {
            if (string.IsNullOrWhiteSpace(hex)) return Array.Empty<byte>();
            if (hex.Length % 2 != 0) hex = "0" + hex;
            byte[] bytes = new byte[hex.Length / 2];
            for (int i = 0; i < bytes.Length; i++)
            {
                bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            }
            return bytes;
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
