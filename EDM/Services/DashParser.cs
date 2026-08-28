using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace EDM.Services
{
    public class DashRepresentation
    {
        public string Id { get; set; } = string.Empty;
        public string MimeType { get; set; } = string.Empty;
        public int Bandwidth { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public double FrameRate { get; set; }
        public string Codecs { get; set; } = string.Empty;
        public string Language { get; set; } = string.Empty;
        public int AudioSamplingRate { get; set; }
        public int AudioChannels { get; set; } = 2;
        public string Resolution => Width > 0 && Height > 0 ? $"{Width}x{Height} ({Height}p)" : MimeType;
        public string? InitializationUrl { get; set; }
        public List<string> SegmentUrls { get; set; } = new List<string>();
        public List<DashSegment> Segments { get; set; } = new List<DashSegment>();
    }

    public class DashSegment
    {
        public string Uri { get; set; } = string.Empty;
        public long SequenceNumber { get; set; }
        public double DurationSeconds { get; set; }
        public long? ByteRangeOffset { get; set; }
        public long? ByteRangeLength { get; set; }
        public string? InitSegmentUri { get; set; }
    }

    public class DashManifest
    {
        public bool IsLive { get; set; }
        public bool IsDrmProtected { get; set; }
        public string DrmSystem { get; set; } = string.Empty;
        public double TotalDurationSeconds { get; set; }
        public List<DashRepresentation> VideoRepresentations { get; set; } = new List<DashRepresentation>();
        public List<DashRepresentation> AudioRepresentations { get; set; } = new List<DashRepresentation>();
        public List<DashRepresentation> SubtitleRepresentations { get; set; } = new List<DashRepresentation>();
    }

    public static class DashParser
    {
        public const int MaxMpdTextLength = 10 * 1024 * 1024; // 10 MB
        public const int MaxSegmentCount = 50000;

        public static DashManifest Parse(string xmlContent, Uri baseUri)
        {
            var manifest = new DashManifest();
            if (string.IsNullOrWhiteSpace(xmlContent) || xmlContent.Length > MaxMpdTextLength) return manifest;

            try
            {
                var doc = XDocument.Parse(xmlContent);
                var root = doc.Root;
                if (root == null) return manifest;

                XNamespace ns = root.Name.Namespace;

                // 1. MPD Level attributes
                string? typeAttr = (string?)root.Attribute("type");
                manifest.IsLive = string.Equals(typeAttr, "dynamic", StringComparison.OrdinalIgnoreCase);

                string? durationAttr = (string?)root.Attribute("mediaPresentationDuration");
                if (!string.IsNullOrEmpty(durationAttr))
                {
                    manifest.TotalDurationSeconds = ParseIso8601Duration(durationAttr);
                }

                string mpdBaseUrl = ResolveBaseUrl(root, ns, baseUri.ToString());

                // 2. DRM Check in MPD / Period / AdaptationSet
                var contentProtections = root.Descendants(ns + "ContentProtection");
                foreach (var cp in contentProtections)
                {
                    string scheme = ((string?)cp.Attribute("schemeIdUri") ?? "").ToLowerInvariant();
                    string value = ((string?)cp.Attribute("value") ?? "").ToLowerInvariant();

                    if (scheme.Contains("edef8ba9") || scheme.Contains("widevine"))
                    {
                        manifest.IsDrmProtected = true;
                        manifest.DrmSystem = "Widevine";
                    }
                    else if (scheme.Contains("9a04f079") || scheme.Contains("playready"))
                    {
                        manifest.IsDrmProtected = true;
                        manifest.DrmSystem = "PlayReady";
                    }
                    else if (scheme.Contains("94ce86fb") || scheme.Contains("fairplay"))
                    {
                        manifest.IsDrmProtected = true;
                        manifest.DrmSystem = "FairPlay";
                    }
                    else if (scheme.Contains("e2719d58") || scheme.Contains("clearkey"))
                    {
                        manifest.IsDrmProtected = true;
                        manifest.DrmSystem = "ClearKey";
                    }
                    else if (scheme.Contains("mp4protection") || value.Contains("cenc"))
                    {
                        manifest.IsDrmProtected = true;
                        manifest.DrmSystem = "CENC (DRM)";
                    }
                    else if (!string.IsNullOrEmpty(scheme))
                    {
                        manifest.IsDrmProtected = true;
                        manifest.DrmSystem = "DRM";
                    }
                }

                // 3. Periods & AdaptationSets
                var periods = root.Elements(ns + "Period").ToList();
                if (!periods.Any()) periods.Add(root); // Fallback if no Period element

                foreach (var period in periods)
                {
                    string periodBaseUrl = ResolveBaseUrl(period, ns, mpdBaseUrl);

                    var adaptationSets = period.Elements(ns + "AdaptationSet");
                    foreach (var set in adaptationSets)
                    {
                        string setBaseUrl = ResolveBaseUrl(set, ns, periodBaseUrl);
                        string setMime = (string?)set.Attribute("mimeType") ?? "";
                        string setCodecs = (string?)set.Attribute("codecs") ?? "";
                        string setLang = (string?)set.Attribute("lang") ?? "";
                        string setContentType = (string?)set.Attribute("contentType") ?? "";

                        var reps = set.Elements(ns + "Representation");
                        var setSegmentTemplate = set.Element(ns + "SegmentTemplate");
                        var setSegmentList = set.Element(ns + "SegmentList");

                        foreach (var rep in reps)
                        {
                            string repBaseUrl = ResolveBaseUrl(rep, ns, setBaseUrl);
                            string repMime = (string?)rep.Attribute("mimeType") ?? setMime;
                            string repCodecs = (string?)rep.Attribute("codecs") ?? setCodecs;
                            string repId = (string?)rep.Attribute("id") ?? Guid.NewGuid().ToString("N");
                            int bandwidth = (int?)rep.Attribute("bandwidth") ?? 0;
                            int width = (int?)rep.Attribute("width") ?? (int?)set.Attribute("width") ?? 0;
                            int height = (int?)rep.Attribute("height") ?? (int?)set.Attribute("height") ?? 0;
                            int samplingRate = (int?)rep.Attribute("audioSamplingRate") ?? (int?)set.Attribute("audioSamplingRate") ?? 0;

                            double fps = 0;
                            string? fpsStr = (string?)rep.Attribute("frameRate") ?? (string?)set.Attribute("frameRate");
                            if (!string.IsNullOrEmpty(fpsStr))
                            {
                                if (fpsStr.Contains('/'))
                                {
                                    var parts = fpsStr.Split('/');
                                    if (double.TryParse(parts[0], NumberStyles.Any, CultureInfo.InvariantCulture, out double num) &&
                                        double.TryParse(parts[1], NumberStyles.Any, CultureInfo.InvariantCulture, out double den) && den > 0)
                                    {
                                        fps = num / den;
                                    }
                                }
                                else if (double.TryParse(fpsStr, NumberStyles.Any, CultureInfo.InvariantCulture, out double parsedFps))
                                {
                                    fps = parsedFps;
                                }
                            }

                            var dashRep = new DashRepresentation
                            {
                                Id = repId,
                                MimeType = repMime,
                                Codecs = repCodecs,
                                Bandwidth = bandwidth,
                                Width = width,
                                Height = height,
                                FrameRate = fps,
                                Language = setLang,
                                AudioSamplingRate = samplingRate
                            };

                            var repBaseUri = new Uri(repBaseUrl);

                            // Mode A: SegmentTemplate
                            var template = rep.Element(ns + "SegmentTemplate") ?? setSegmentTemplate;
                            if (template != null)
                            {
                                ParseSegmentTemplate(template, ns, dashRep, repBaseUri, manifest.TotalDurationSeconds);
                            }

                            // Mode B: SegmentList
                            var segList = rep.Element(ns + "SegmentList") ?? setSegmentList;
                            if (segList != null && !dashRep.SegmentUrls.Any())
                            {
                                var initElem = segList.Element(ns + "Initialization");
                                string? initSource = (string?)initElem?.Attribute("sourceURL");
                                if (!string.IsNullOrEmpty(initSource))
                                {
                                    dashRep.InitializationUrl = MakeAbsoluteUri(initSource, repBaseUri);
                                }

                                foreach (var segUrlElem in segList.Elements(ns + "SegmentURL"))
                                {
                                    string? media = (string?)segUrlElem.Attribute("media");
                                    if (!string.IsNullOrEmpty(media))
                                    {
                                        string absMedia = MakeAbsoluteUri(media, repBaseUri);
                                        dashRep.SegmentUrls.Add(absMedia);
                                        dashRep.Segments.Add(new DashSegment { Uri = absMedia });
                                    }
                                }
                            }

                            // Mode C: SegmentBase / Direct Initialization
                            var directInit = rep.Element(ns + "Initialization") ?? set.Element(ns + "Initialization");
                            if (directInit != null)
                            {
                                string? initSource = (string?)directInit.Attribute("sourceURL");
                                if (!string.IsNullOrEmpty(initSource))
                                {
                                    dashRep.InitializationUrl = MakeAbsoluteUri(initSource, repBaseUri);
                                }
                            }

                            var segBase = rep.Element(ns + "SegmentBase") ?? set.Element(ns + "SegmentBase");
                            if (segBase != null && !dashRep.SegmentUrls.Any())
                            {
                                var initElem = segBase.Element(ns + "Initialization");
                                string? initSource = (string?)initElem?.Attribute("sourceURL");
                                if (!string.IsNullOrEmpty(initSource))
                                {
                                    dashRep.InitializationUrl = MakeAbsoluteUri(initSource, repBaseUri);
                                }

                                // BaseURL is the direct media URL
                                dashRep.SegmentUrls.Add(repBaseUrl);
                                dashRep.Segments.Add(new DashSegment { Uri = repBaseUrl });
                            }

                            // If no media segments found yet but InitializationUrl is present
                            if (!dashRep.SegmentUrls.Any() && !string.IsNullOrEmpty(dashRep.InitializationUrl))
                            {
                                dashRep.SegmentUrls.Add(dashRep.InitializationUrl);
                                dashRep.Segments.Add(new DashSegment { Uri = dashRep.InitializationUrl });
                            }

                            // If no segments found yet but BaseURL is a media file
                            if (!dashRep.SegmentUrls.Any() && !string.IsNullOrEmpty(repBaseUrl) && !repBaseUrl.EndsWith(".mpd", StringComparison.OrdinalIgnoreCase))
                            {
                                dashRep.SegmentUrls.Add(repBaseUrl);
                                dashRep.Segments.Add(new DashSegment { Uri = repBaseUrl });
                            }

                            // Categorize Representation
                            bool isVideo = repMime.StartsWith("video", StringComparison.OrdinalIgnoreCase) ||
                                           setContentType.Equals("video", StringComparison.OrdinalIgnoreCase) ||
                                           dashRep.Width > 0 || dashRep.Height > 0;

                            bool isAudio = repMime.StartsWith("audio", StringComparison.OrdinalIgnoreCase) ||
                                           setContentType.Equals("audio", StringComparison.OrdinalIgnoreCase) ||
                                           dashRep.AudioSamplingRate > 0;

                            bool isSubtitle = repMime.Contains("vtt") || repMime.Contains("ttml") ||
                                              setContentType.Equals("text", StringComparison.OrdinalIgnoreCase);

                            if (isVideo)
                            {
                                manifest.VideoRepresentations.Add(dashRep);
                            }
                            else if (isAudio)
                            {
                                manifest.AudioRepresentations.Add(dashRep);
                            }
                            else if (isSubtitle)
                            {
                                manifest.SubtitleRepresentations.Add(dashRep);
                            }
                            else
                            {
                                // Default classification
                                if (dashRep.Width > 0) manifest.VideoRepresentations.Add(dashRep);
                                else manifest.AudioRepresentations.Add(dashRep);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LoggingService.Log($"[DashParser] Error parsing DASH XML manifest: {ex.Message}");
            }

            return manifest;
        }

        private static void ParseSegmentTemplate(
            XElement template,
            XNamespace ns,
            DashRepresentation dashRep,
            Uri baseUri,
            double totalDurationSeconds)
        {
            string? initPattern = (string?)template.Attribute("initialization");
            string? mediaPattern = (string?)template.Attribute("media");
            long startNumber = (long?)template.Attribute("startNumber") ?? 1;
            long timescale = (long?)template.Attribute("timescale") ?? 1;
            long durationUnits = (long?)template.Attribute("duration") ?? 0;

            if (!string.IsNullOrEmpty(initPattern))
            {
                string initUrl = FormatTemplateUrl(initPattern, dashRep.Id, 0, 0, dashRep.Bandwidth);
                dashRep.InitializationUrl = MakeAbsoluteUri(initUrl, baseUri);
            }

            if (string.IsNullOrEmpty(mediaPattern)) return;

            var timeline = template.Element(ns + "SegmentTimeline");
            if (timeline != null)
            {
                // Timeline-driven segment expansion
                long currentTime = 0;
                long currentNumber = startNumber;

                foreach (var sElem in timeline.Elements(ns + "S"))
                {
                    long? tAttr = (long?)sElem.Attribute("t");
                    if (tAttr.HasValue) currentTime = tAttr.Value;

                    long dAttr = (long?)sElem.Attribute("d") ?? durationUnits;
                    long rAttr = (long?)sElem.Attribute("r") ?? 0;

                    // Positive or zero repeat
                    long repeatCount = Math.Max(0, rAttr);

                    for (long r = 0; r <= repeatCount; r++)
                    {
                        string segUrl = FormatTemplateUrl(mediaPattern, dashRep.Id, currentNumber, currentTime, dashRep.Bandwidth);
                        string absUrl = MakeAbsoluteUri(segUrl, baseUri);

                        dashRep.SegmentUrls.Add(absUrl);
                        dashRep.Segments.Add(new DashSegment
                        {
                            Uri = absUrl,
                            SequenceNumber = currentNumber,
                            DurationSeconds = timescale > 0 ? (double)dAttr / timescale : 0,
                            InitSegmentUri = dashRep.InitializationUrl
                        });

                        currentTime += dAttr;
                        currentNumber++;

                        if (dashRep.Segments.Count >= MaxSegmentCount) break;
                    }

                    if (dashRep.Segments.Count >= MaxSegmentCount) break;
                }
            }
            else if (durationUnits > 0 && timescale > 0)
            {
                // Fixed-duration segment expansion
                double segDurationSec = (double)durationUnits / timescale;
                int segmentCount = (totalDurationSeconds > 0 && segDurationSec > 0)
                    ? (int)Math.Ceiling(totalDurationSeconds / segDurationSec)
                    : 100; // Sensible default bounded cap

                segmentCount = Math.Min(segmentCount, 10000); // Safety limit

                long currentNumber = startNumber;
                long currentTime = 0;

                for (int i = 0; i < segmentCount; i++)
                {
                    string segUrl = FormatTemplateUrl(mediaPattern, dashRep.Id, currentNumber, currentTime, dashRep.Bandwidth);
                    string absUrl = MakeAbsoluteUri(segUrl, baseUri);

                    dashRep.SegmentUrls.Add(absUrl);
                    dashRep.Segments.Add(new DashSegment
                    {
                        Uri = absUrl,
                        SequenceNumber = currentNumber,
                        DurationSeconds = segDurationSec,
                        InitSegmentUri = dashRep.InitializationUrl
                    });

                    currentNumber++;
                    currentTime += durationUnits;
                }
            }
        }

        public static string FormatTemplateUrl(string pattern, string repId, long number, long time, int bandwidth)
        {
            string formatted = pattern;
            formatted = formatted.Replace("$RepresentationID$", repId);
            formatted = formatted.Replace("$Bandwidth$", bandwidth.ToString());

            // Replace $Number$ or $Number%05d$
            formatted = Regex.Replace(formatted, @"\$Number(?:%0(\d+)d)?\$", m =>
            {
                if (m.Groups[1].Success && int.TryParse(m.Groups[1].Value, out int pad))
                {
                    return number.ToString().PadLeft(pad, '0');
                }
                return number.ToString();
            });

            // Replace $Time$ or $Time%05d$
            formatted = Regex.Replace(formatted, @"\$Time(?:%0(\d+)d)?\$", m =>
            {
                if (m.Groups[1].Success && int.TryParse(m.Groups[1].Value, out int pad))
                {
                    return time.ToString().PadLeft(pad, '0');
                }
                return time.ToString();
            });

            return formatted;
        }

        private static string ResolveBaseUrl(XElement elem, XNamespace ns, string currentBaseUrl)
        {
            var baseUrlElem = elem.Element(ns + "BaseURL");
            if (baseUrlElem != null && !string.IsNullOrWhiteSpace(baseUrlElem.Value))
            {
                string val = baseUrlElem.Value.Trim();
                if (Uri.TryCreate(val, UriKind.Absolute, out _))
                {
                    return val;
                }
                if (Uri.TryCreate(new Uri(currentBaseUrl), val, out var combined))
                {
                    return combined.ToString();
                }
            }
            return currentBaseUrl;
        }

        public static double ParseIso8601Duration(string durationStr)
        {
            try
            {
                var match = Regex.Match(durationStr, @"PT(?:(\d+)H)?(?:(\d+)M)?(?:([\d\.]+)S)?");
                if (match.Success)
                {
                    double total = 0;
                    if (match.Groups[1].Success && double.TryParse(match.Groups[1].Value, out double h)) total += h * 3600;
                    if (match.Groups[2].Success && double.TryParse(match.Groups[2].Value, out double m)) total += m * 60;
                    if (match.Groups[3].Success && double.TryParse(match.Groups[3].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double s)) total += s;
                    return total;
                }
            }
            catch { }
            return 0;
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
