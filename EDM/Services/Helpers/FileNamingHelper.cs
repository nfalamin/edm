using System;
using System.IO;
using System.Net.Http;
using EDM.Services;
using System.Text;

namespace EDM.Services.Helpers
{
    internal static class FileNamingHelper
    {
        public static bool TryCreateHttpUri(string url, out Uri? uri)
        {
            uri = null;
            if (string.IsNullOrWhiteSpace(url)) return false;
            if (!Uri.TryCreate(url, UriKind.RelativeOrAbsolute, out var tmp)) return false;
            if (!tmp.IsAbsoluteUri)
            {
                // try to prepend http:// as a last resort
                if (Uri.TryCreate("http://" + url, UriKind.Absolute, out var tmp2))
                {
                    uri = tmp2;
                    return true;
                }
                return false;
            }
            // ensure scheme is http or https
            if (!(string.Equals(tmp.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) || string.Equals(tmp.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)))
                return false;
            uri = tmp;
            return true;
        }

        public static string DetermineFileNameFromResponse(HttpResponseMessage? response, Uri requestUri)
        {
            var cd = response?.Content?.Headers?.ContentDisposition;
            var mime = response?.Content?.Headers?.ContentType?.MediaType;
            return DetermineFileNameFromHeaders(cd, mime, requestUri);
        }

        public static string DetermineFileNameFromHeaders(System.Net.Http.Headers.ContentDispositionHeaderValue? cd, string? mime, Uri requestUri)
        {
            try
            {
                // 1) Content-Disposition header (filename*= or filename=)
                try
                {
                    if (cd != null)
                    {
                        if (!string.IsNullOrWhiteSpace(cd.FileNameStar)) return SanitizeFileName(cd.FileNameStar.Trim('"'));
                        if (!string.IsNullOrWhiteSpace(cd.FileName)) return SanitizeFileName(cd.FileName.Trim('"'));
                    }
                }
                catch (Exception ex) { LoggingService.Log($"[ExtractFileNameFromResponse] Failed to parse Content-Disposition header: {ex.Message}"); }

                // 2) Try to use the last segment of the request URI
                try
                {
                    var name = Path.GetFileName(requestUri.LocalPath);
                    if (!string.IsNullOrWhiteSpace(name) && name.IndexOf('.') >= 0)
                    {
                        return SanitizeFileName(name);
                    }
                }
                catch (Exception ex) { LoggingService.Log($"[ExtractFileNameFromResponse] Failed to extract filename from request URI: {ex.Message}"); }

                // 3) Use Content-Type to guess extension
                try
                {
                    if (!string.IsNullOrWhiteSpace(mime))
                    {
                        var ext = GetExtensionFromMime(mime!);
                        return "download" + ext;
                    }
                }
                catch (Exception ex) { LoggingService.Log($"[ExtractFileNameFromResponse] Failed to extract filename from Content-Type: {ex.Message}"); }

                // 4) Fallback
                return "download.bin";
            }
            catch
            {
                return "download.bin";
            }
        }

        public static string SanitizeFileName(string name)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(name)) return "download.bin";
                try
                {
                    name = Uri.UnescapeDataString(name.Trim('"'));
                }
                catch { name = name.Trim('"'); }

                // Strip directory traversal paths if path traversal or rooted path is present
                if (name.Contains("..") || Path.IsPathRooted(name))
                {
                    var fn = Path.GetFileName(name);
                    if (!string.IsNullOrWhiteSpace(fn)) name = fn;
                }

                foreach (var c in Path.GetInvalidFileNameChars())
                {
                    name = name.Replace(c, '_');
                }
                return string.IsNullOrWhiteSpace(name) ? "download.bin" : name;
            }
            catch (Exception ex)
            {
                LoggingService.Log($"[SanitizeFileName] Sanitization failed for '{name}', using default: {ex.Message}");
                return "download.bin";
            }
        }


        public static string GetExtensionFromMime(string mime)
        {
            if (string.IsNullOrWhiteSpace(mime)) return ".bin";
            mime = mime.Split(';')[0].Trim().ToLowerInvariant();
            // Common mapping (not exhaustive)
            return mime switch
            {
                "video/mp4" => ".mp4",
                "video/x-matroska" => ".mkv",
                "video/x-msvideo" => ".avi",
                "video/x-flv" => ".flv",
                "video/quicktime" => ".mov",
                "audio/mpeg" => ".mp3",
                "audio/mp4" => ".m4a",
                "application/zip" => ".zip",
                "application/x-rar-compressed" => ".rar",
                "application/x-7z-compressed" => ".7z",
                "application/x-iso9660-image" => ".iso",
                "application/vnd.microsoft.portable-executable" => ".exe",
                "application/vnd.android.package-archive" => ".apk",
                "application/pdf" => ".pdf",
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document" => ".docx",
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" => ".xlsx",
                "application/vnd.openxmlformats-officedocument.presentationml.presentation" => ".pptx",
                "text/html" => ".html",
                "text/css" => ".css",
                "application/javascript" => ".js",
                "application/json" => ".json",
                "application/xml" => ".xml",
                "text/csv" => ".csv",
                "image/png" => ".png",
                "image/jpeg" => ".jpg",
                "image/gif" => ".gif",
                "image/webp" => ".webp",
                "image/svg+xml" => ".svg",
                "font/ttf" => ".ttf",
                "font/otf" => ".otf",
                "application/octet-stream" => ".bin",
                _ => ".bin",
            };
        }
    }
}
