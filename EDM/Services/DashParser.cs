using System;
using System.Collections.Generic;
using System.Globalization;
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
        public string Resolution => Width > 0 && Height > 0 ? $"{Width}x{Height} ({Height}p)" : MimeType;
        public List<string> SegmentUrls { get; set; } = new List<string>();
    }

    public class DashManifest
    {
        public bool IsDrmProtected { get; set; }
        public List<DashRepresentation> VideoRepresentations { get; set; } = new List<DashRepresentation>();
        public List<DashRepresentation> AudioRepresentations { get; set; } = new List<DashRepresentation>();
    }

    public static class DashParser
    {
        public static DashManifest Parse(string xmlContent, Uri baseUri)
        {
            var manifest = new DashManifest();
            if (string.IsNullOrWhiteSpace(xmlContent)) return manifest;

            try
            {
                var doc = XDocument.Parse(xmlContent);
                var root = doc.Root;
                if (root == null) return manifest;

                XNamespace ns = root.Name.Namespace;

                // DRM Check
                var drmElements = root.Descendants(ns + "ContentProtection");
                if (drmElements != null && System.Linq.Enumerable.Any(drmElements))
                {
                    manifest.IsDrmProtected = true;
                }

                var adaptationSets = root.Descendants(ns + "AdaptationSet");
                foreach (var set in adaptationSets)
                {
                    string setMime = (string?)set.Attribute("mimeType") ?? "";
                    string setCodecs = (string?)set.Attribute("codecs") ?? "";
                    var reps = set.Elements(ns + "Representation");

                    foreach (var rep in reps)
                    {
                        string repMime = (string?)rep.Attribute("mimeType") ?? setMime;
                        string repCodecs = (string?)rep.Attribute("codecs") ?? setCodecs;

                        double fps = 0;
                        string? fpsStr = (string?)rep.Attribute("frameRate") ?? (string?)set.Attribute("frameRate");
                        if (!string.IsNullOrEmpty(fpsStr) && double.TryParse(fpsStr, NumberStyles.Any, CultureInfo.InvariantCulture, out double parsedFps))
                        {
                            fps = parsedFps;
                        }

                        var dashRep = new DashRepresentation
                        {
                            Id = (string?)rep.Attribute("id") ?? "",
                            MimeType = repMime,
                            Codecs = repCodecs,
                            Bandwidth = (int?)rep.Attribute("bandwidth") ?? 0,
                            Width = (int?)rep.Attribute("width") ?? 0,
                            Height = (int?)rep.Attribute("height") ?? 0,
                            FrameRate = fps
                        };

                        // Extract segment URLs or SegmentTemplate
                        var segmentBase = rep.Element(ns + "SegmentBase");
                        var init = rep.Element(ns + "Initialization") ?? segmentBase?.Element(ns + "Initialization");
                        string? sourceUrl = (string?)init?.Attribute("sourceURL");

                        if (!string.IsNullOrEmpty(sourceUrl))
                        {
                            dashRep.SegmentUrls.Add(MakeAbsoluteUri(sourceUrl, baseUri));
                        }

                        var segmentList = rep.Element(ns + "SegmentList") ?? set.Element(ns + "SegmentList");
                        if (segmentList != null)
                        {
                            var segmentUrls = segmentList.Elements(ns + "SegmentURL");
                            foreach (var seg in segmentUrls)
                            {
                                string? media = (string?)seg.Attribute("media");
                                if (!string.IsNullOrEmpty(media))
                                {
                                    dashRep.SegmentUrls.Add(MakeAbsoluteUri(media, baseUri));
                                }
                            }
                        }

                        if (dashRep.MimeType.StartsWith("video", StringComparison.OrdinalIgnoreCase) || dashRep.Width > 0)
                        {
                            manifest.VideoRepresentations.Add(dashRep);
                        }
                        else if (dashRep.MimeType.StartsWith("audio", StringComparison.OrdinalIgnoreCase))
                        {
                            manifest.AudioRepresentations.Add(dashRep);
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
