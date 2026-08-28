using System;
using System.Collections.Generic;
using System.Linq;

namespace EDM.Services
{
    public enum QualityProfile
    {
        BestQuality,
        BestAvailable,
        MaximumResolution,
        MaximumBitrate,
        Balanced,
        AudioOnly,
        VideoOnly,
        Custom
    }

    /// <summary>
    /// Centralized adaptive representation selection engine for HLS, DASH, and dynamic media streams.
    /// Evaluates resolution, bitrate, codecs, frame rate, audio channels, language, and user preferences.
    /// </summary>
    public static class MediaRepresentationSelector
    {
        public static HlsVariant SelectHlsVariant(
            IReadOnlyList<HlsVariant> variants,
            QualityProfile profile = QualityProfile.BestQuality,
            string? customResolution = null,
            string? preferredCodec = null,
            int maxBandwidth = int.MaxValue)
        {
            if (variants == null || !variants.Any())
            {
                throw new InvalidOperationException("No HLS variants provided for selection.");
            }

            var candidates = variants.Where(v => !v.IsIFrameOnly && v.Bandwidth <= maxBandwidth).ToList();
            if (!candidates.Any())
            {
                candidates = variants.ToList(); // Fallback if bandwidth constraint excludes all
            }

            if (!string.IsNullOrEmpty(preferredCodec))
            {
                var codecMatches = candidates.Where(v => v.Codecs.Contains(preferredCodec, StringComparison.OrdinalIgnoreCase)).ToList();
                if (codecMatches.Any()) candidates = codecMatches;
            }

            switch (profile)
            {
                case QualityProfile.MaximumResolution:
                case QualityProfile.BestQuality:
                case QualityProfile.BestAvailable:
                    return candidates.OrderByDescending(v => v.Height)
                                     .ThenByDescending(v => v.Bandwidth)
                                     .ThenByDescending(v => v.FrameRate)
                                     .First();

                case QualityProfile.MaximumBitrate:
                    return candidates.OrderByDescending(v => v.Bandwidth)
                                     .ThenByDescending(v => v.Height)
                                     .First();

                case QualityProfile.Balanced:
                    // Aim for 1080p or 720p with balanced bitrate
                    var balanced = candidates.Where(v => v.Height <= 1080 && v.Height >= 720)
                                             .OrderByDescending(v => v.Height)
                                             .ThenByDescending(v => v.Bandwidth)
                                             .FirstOrDefault();
                    return balanced ?? candidates.OrderByDescending(v => v.Height).ThenByDescending(v => v.Bandwidth).First();

                case QualityProfile.Custom:
                    if (int.TryParse(customResolution?.Replace("p", "", StringComparison.OrdinalIgnoreCase), out int targetHeight))
                    {
                        var exact = candidates.Where(v => v.Height == targetHeight).OrderByDescending(v => v.Bandwidth).FirstOrDefault();
                        if (exact != null) return exact;

                        // Nearest resolution
                        return candidates.OrderBy(v => Math.Abs(v.Height - targetHeight))
                                         .ThenByDescending(v => v.Bandwidth)
                                         .First();
                    }
                    return candidates.OrderByDescending(v => v.Height).ThenByDescending(v => v.Bandwidth).First();

                case QualityProfile.AudioOnly:
                case QualityProfile.VideoOnly:
                default:
                    return candidates.OrderByDescending(v => v.Height).ThenByDescending(v => v.Bandwidth).First();
            }
        }

        public static DashRepresentation? SelectDashVideoRepresentation(
            IReadOnlyList<DashRepresentation> videoRepresentations,
            QualityProfile profile = QualityProfile.BestQuality,
            string? customResolution = null,
            string? preferredCodec = null,
            int maxBandwidth = int.MaxValue)
        {
            if (videoRepresentations == null || !videoRepresentations.Any()) return null;

            if (profile == QualityProfile.AudioOnly) return null;

            var candidates = videoRepresentations.Where(v => v.Bandwidth <= maxBandwidth).ToList();
            if (!candidates.Any()) candidates = videoRepresentations.ToList();

            if (!string.IsNullOrEmpty(preferredCodec))
            {
                var codecMatches = candidates.Where(v => v.Codecs.Contains(preferredCodec, StringComparison.OrdinalIgnoreCase)).ToList();
                if (codecMatches.Any()) candidates = codecMatches;
            }

            switch (profile)
            {
                case QualityProfile.MaximumResolution:
                case QualityProfile.BestQuality:
                case QualityProfile.BestAvailable:
                case QualityProfile.VideoOnly:
                    return candidates.OrderByDescending(v => v.Height)
                                     .ThenByDescending(v => v.Bandwidth)
                                     .ThenByDescending(v => v.FrameRate)
                                     .First();

                case QualityProfile.MaximumBitrate:
                    return candidates.OrderByDescending(v => v.Bandwidth)
                                     .ThenByDescending(v => v.Height)
                                     .First();

                case QualityProfile.Balanced:
                    var balanced = candidates.Where(v => v.Height <= 1080 && v.Height >= 720)
                                             .OrderByDescending(v => v.Height)
                                             .ThenByDescending(v => v.Bandwidth)
                                             .FirstOrDefault();
                    return balanced ?? candidates.OrderByDescending(v => v.Height).ThenByDescending(v => v.Bandwidth).First();

                case QualityProfile.Custom:
                    if (int.TryParse(customResolution?.Replace("p", "", StringComparison.OrdinalIgnoreCase), out int targetHeight))
                    {
                        var exact = candidates.Where(v => v.Height == targetHeight).OrderByDescending(v => v.Bandwidth).FirstOrDefault();
                        if (exact != null) return exact;

                        return candidates.OrderBy(v => Math.Abs(v.Height - targetHeight))
                                         .ThenByDescending(v => v.Bandwidth)
                                         .First();
                    }
                    return candidates.OrderByDescending(v => v.Height).ThenByDescending(v => v.Bandwidth).First();

                default:
                    return candidates.OrderByDescending(v => v.Height).ThenByDescending(v => v.Bandwidth).First();
            }
        }

        public static DashRepresentation? SelectDashAudioRepresentation(
            IReadOnlyList<DashRepresentation> audioRepresentations,
            string? preferredLanguage = null,
            bool highestQuality = true)
        {
            if (audioRepresentations == null || !audioRepresentations.Any()) return null;

            var candidates = audioRepresentations.ToList();

            if (!string.IsNullOrEmpty(preferredLanguage))
            {
                var langMatches = candidates.Where(a => a.Language.Equals(preferredLanguage, StringComparison.OrdinalIgnoreCase) ||
                                                        a.Language.StartsWith(preferredLanguage, StringComparison.OrdinalIgnoreCase)).ToList();
                if (langMatches.Any()) candidates = langMatches;
            }

            return highestQuality
                ? candidates.OrderByDescending(a => a.Bandwidth).ThenByDescending(a => a.AudioSamplingRate).First()
                : candidates.OrderBy(a => a.Bandwidth).First();
        }

        public static MediaVariantOption? SelectBestVariant(
            IReadOnlyList<MediaVariantOption> options,
            QualityProfile profile = QualityProfile.BestQuality,
            string? customPreference = null)
        {
            if (options == null || !options.Any()) return null;

            switch (profile)
            {
                case QualityProfile.AudioOnly:
                    var audioOnly = options.Where(o => o.IsAudioOnly).OrderByDescending(o => o.Bitrate).FirstOrDefault();
                    return audioOnly ?? options.OrderByDescending(o => o.Bitrate).First();

                case QualityProfile.VideoOnly:
                    var videoOnly = options.Where(o => !o.IsAudioOnly).OrderByDescending(o => o.Height).ThenByDescending(o => o.Bitrate).FirstOrDefault();
                    return videoOnly ?? options.First();

                case QualityProfile.MaximumResolution:
                case QualityProfile.BestQuality:
                case QualityProfile.BestAvailable:
                    return options.Where(o => !o.IsAudioOnly)
                                  .OrderByDescending(o => o.Height)
                                  .ThenByDescending(o => o.Bitrate)
                                  .ThenByDescending(o => o.FrameRate)
                                  .FirstOrDefault() ?? options.First();

                case QualityProfile.MaximumBitrate:
                    return options.OrderByDescending(o => o.Bitrate).First();

                case QualityProfile.Balanced:
                    var balanced = options.Where(o => !o.IsAudioOnly && o.Height <= 1080 && o.Height >= 720)
                                          .OrderByDescending(o => o.Height)
                                          .ThenByDescending(o => o.Bitrate)
                                          .FirstOrDefault();
                    return balanced ?? options.Where(o => !o.IsAudioOnly).OrderByDescending(o => o.Height).FirstOrDefault() ?? options.First();

                case QualityProfile.Custom:
                    if (int.TryParse(customPreference?.Replace("p", "", StringComparison.OrdinalIgnoreCase), out int targetH))
                    {
                        var exact = options.Where(o => !o.IsAudioOnly && o.Height == targetH).OrderByDescending(o => o.Bitrate).FirstOrDefault();
                        if (exact != null) return exact;

                        var nearest = options.Where(o => !o.IsAudioOnly)
                                             .OrderBy(o => Math.Abs(o.Height - targetH))
                                             .ThenByDescending(o => o.Bitrate)
                                             .FirstOrDefault();
                        if (nearest != null) return nearest;
                    }
                    return options.Where(o => !o.IsAudioOnly).OrderByDescending(o => o.Height).FirstOrDefault() ?? options.First();

                default:
                    return options.First();
            }
        }
    }
}
