using System;
using System.Text.Json.Serialization;

namespace EDM.NativeMessaging
{
    public static class NativeActionNames
    {
        public const string StartDownload = "download_url";
        public const string StartDownloadAlt = "START_DOWNLOAD";
        public const string StartEdmDownload = "START_EDM_DOWNLOAD";
        public const string DownloadRequest = "DOWNLOAD_REQUEST";
        public const string Intercept = "intercept";
        public const string GetMediaStreams = "get_media_streams";
        public const string GetMediaStreamsAlt = "GET_MEDIA_STREAMS";
        public const string UpdateSettings = "UPDATE_SETTINGS";
        public const string Ping = "PING";
        public const string Handshake = "HANDSHAKE";
    }

    public class NativeMessageRequest
    {
        [JsonPropertyName("action")]
        public string? Action { get; set; }

        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("requestId")]
        public string? RequestId { get; set; }

        [JsonPropertyName("url")]
        public string? Url { get; set; }

        [JsonPropertyName("pageUrl")]
        public string? PageUrl { get; set; }

        [JsonPropertyName("filename")]
        public string? Filename { get; set; }

        [JsonPropertyName("fileSize")]
        public long? FileSize { get; set; }

        [JsonPropertyName("mime")]
        public string? Mime { get; set; }

        [JsonPropertyName("cookies")]
        public string? Cookies { get; set; }

        [JsonPropertyName("quality")]
        public string? Quality { get; set; }

        [JsonPropertyName("format")]
        public string? Format { get; set; }

        [JsonPropertyName("browser")]
        public string? Browser { get; set; }

        [JsonPropertyName("correlationId")]
        public string? CorrelationId { get; set; }

        [JsonPropertyName("browserDownloadId")]
        public string? BrowserDownloadId { get; set; }

        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("manifestUrl")]
        public string? ManifestUrl { get; set; }

        [JsonPropertyName("audioCodec")]
        public string? AudioCodec { get; set; }

        [JsonPropertyName("audioUrl")]
        public string? AudioUrl { get; set; }

        [JsonPropertyName("videoUrl")]
        public string? VideoUrl { get; set; }

        [JsonPropertyName("formatArg")]
        public string? FormatArg { get; set; }

        [JsonPropertyName("requiresFfmpegMerge")]
        public bool? RequiresFfmpegMerge { get; set; }

        [JsonPropertyName("headers")]
        public string? Headers { get; set; }

        [JsonPropertyName("downloadIdentity")]
        public string? DownloadIdentity { get; set; }

        [JsonPropertyName("estimatedSizeBytes")]
        public long? EstimatedSizeBytes { get; set; }

        [JsonPropertyName("codec")]
        public string? Codec { get; set; }

        [JsonPropertyName("container")]
        public string? Container { get; set; }

        [JsonPropertyName("isAudioOnly")]
        public bool? IsAudioOnly { get; set; }

        [JsonPropertyName("selectedVariant")]
        public object? SelectedVariant { get; set; }

        public string GetEffectiveAction()
        {
            string act = !string.IsNullOrWhiteSpace(Action) ? Action.Trim() : (!string.IsNullOrWhiteSpace(Type) ? Type.Trim() : string.Empty);
            if (string.Equals(act, NativeActionNames.StartDownload, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(act, NativeActionNames.StartDownloadAlt, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(act, NativeActionNames.StartEdmDownload, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(act, NativeActionNames.DownloadRequest, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(act, NativeActionNames.Intercept, StringComparison.OrdinalIgnoreCase))
            {
                return NativeActionNames.StartDownload;
            }

            if (string.Equals(act, NativeActionNames.GetMediaStreams, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(act, NativeActionNames.GetMediaStreamsAlt, StringComparison.OrdinalIgnoreCase))
            {
                return NativeActionNames.GetMediaStreams;
            }

            if (string.Equals(act, "GET_MEDIA_VARIANTS", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(act, "resolve_media_variants", StringComparison.OrdinalIgnoreCase))
            {
                return "GET_MEDIA_VARIANTS";
            }

            if (!string.IsNullOrWhiteSpace(act)) return act;
            if (!string.IsNullOrWhiteSpace(Url)) return NativeActionNames.StartDownload;
            return "unknown";
        }

        public string GetEffectiveFileName()
        {
            if (!string.IsNullOrWhiteSpace(Filename)) return Filename.Trim();
            return string.Empty;
        }
    }

    public class NativeMessageResponse
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("action")]
        public string? Action { get; set; }

        [JsonPropertyName("requestId")]
        public string? RequestId { get; set; }

        [JsonPropertyName("status")]
        public string? Status { get; set; }

        [JsonPropertyName("version")]
        public string? Version { get; set; }

        [JsonPropertyName("result")]
        public object? Result { get; set; }

        [JsonPropertyName("data")]
        public object? Data { get; set; }

        [JsonPropertyName("variants")]
        public object? Variants { get; set; }

        [JsonPropertyName("error")]
        public string? Error { get; set; }

        [JsonPropertyName("timestamp")]
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    public class IpcHandoffPayload
    {
        [JsonPropertyName("url")]
        public string Url { get; set; } = string.Empty;

        [JsonPropertyName("filename")]
        public string? Filename { get; set; }

        [JsonPropertyName("cookies")]
        public string? Cookies { get; set; }

        [JsonPropertyName("pageUrl")]
        public string? PageUrl { get; set; }

        [JsonPropertyName("quality")]
        public string? Quality { get; set; }

        [JsonPropertyName("format")]
        public string? Format { get; set; }

        [JsonPropertyName("browser")]
        public string? Browser { get; set; }

        [JsonPropertyName("correlationId")]
        public string? CorrelationId { get; set; }

        [JsonPropertyName("downloadIdentity")]
        public string? DownloadIdentity { get; set; }

        [JsonPropertyName("source")]
        public string? Source { get; set; }

        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("manifestUrl")]
        public string? ManifestUrl { get; set; }

        [JsonPropertyName("audioCodec")]
        public string? AudioCodec { get; set; }

        [JsonPropertyName("audioUrl")]
        public string? AudioUrl { get; set; }

        [JsonPropertyName("videoUrl")]
        public string? VideoUrl { get; set; }

        [JsonPropertyName("formatArg")]
        public string? FormatArg { get; set; }

        [JsonPropertyName("requiresFfmpegMerge")]
        public bool RequiresFfmpegMerge { get; set; }

        [JsonPropertyName("headers")]
        public string? Headers { get; set; }

        [JsonPropertyName("codec")]
        public string? Codec { get; set; }

        [JsonPropertyName("container")]
        public string? Container { get; set; }

        [JsonPropertyName("estimatedSizeBytes")]
        public long? EstimatedSizeBytes { get; set; }

        [JsonPropertyName("isAudioOnly")]
        public bool? IsAudioOnly { get; set; }
    }
}
