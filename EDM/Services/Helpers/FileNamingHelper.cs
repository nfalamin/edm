using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using EDM.Services;

namespace EDM.Services.Helpers
{
    public static class FileNamingHelper
    {
        private static readonly HashSet<string> ReservedWindowsDeviceNames = new(StringComparer.OrdinalIgnoreCase)
        {
            "CON", "PRN", "AUX", "NUL",
            "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
            "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
        };

        public static bool TryCreateHttpUri(string url, out Uri? uri)
        {
            uri = null;
            if (string.IsNullOrWhiteSpace(url)) return false;
            if (!Uri.TryCreate(url, UriKind.RelativeOrAbsolute, out var tmp)) return false;
            if (!tmp.IsAbsoluteUri)
            {
                if (Uri.TryCreate("http://" + url, UriKind.Absolute, out var tmp2))
                {
                    uri = tmp2;
                    return true;
                }
                return false;
            }
            if (!(string.Equals(tmp.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) || 
                  string.Equals(tmp.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)))
                return false;

            uri = tmp;
            return true;
        }

        /// <summary>
        /// Single authoritative filename resolver following strict precedence:
        /// 1. Explicit user filename (if provided)
        /// 2. Content-Disposition header (RFC 5987 filename*= or filename=)
        /// 3. Resolved media title
        /// 4. URL path segment
        /// 5. Safe fallback
        /// </summary>
        public static string ResolveAuthoritativeFileName(
            string? explicitUserFilename,
            ContentDispositionHeaderValue? cd,
            string? mediaTitle,
            string? mimeType,
            Uri? requestUri,
            string fallback = "download.bin")
        {
            // 1. Explicit user filename
            if (!string.IsNullOrWhiteSpace(explicitUserFilename))
            {
                return SanitizeFileName(explicitUserFilename);
            }

            // 2. Content-Disposition header
            if (cd != null)
            {
                string? cdName = ExtractFileNameFromContentDisposition(cd);
                if (!string.IsNullOrWhiteSpace(cdName))
                {
                    return SanitizeFileName(cdName);
                }
            }

            // 3. Resolved media title
            if (!string.IsNullOrWhiteSpace(mediaTitle))
            {
                string cleanMedia = SanitizeFileName(mediaTitle);
                if (!Path.HasExtension(cleanMedia) && !string.IsNullOrWhiteSpace(mimeType))
                {
                    cleanMedia += GetExtensionFromMime(mimeType);
                }
                return cleanMedia;
            }

            // 4. URL path segment
            if (requestUri != null)
            {
                try
                {
                    string urlPath = requestUri.LocalPath;
                    string name = Path.GetFileName(urlPath);
                    if (!string.IsNullOrWhiteSpace(name) && name.IndexOf('.') >= 0)
                    {
                        return SanitizeFileName(name);
                    }
                }
                catch { }
            }

            // 5. Mime-type based fallback
            if (!string.IsNullOrWhiteSpace(mimeType))
            {
                string ext = GetExtensionFromMime(mimeType);
                if (!string.IsNullOrEmpty(ext) && ext != ".bin")
                {
                    return "download" + ext;
                }
            }

            return SanitizeFileName(fallback);
        }

        public static string DetermineFileNameFromResponse(HttpResponseMessage? response, Uri requestUri)
        {
            var cd = response?.Content?.Headers?.ContentDisposition;
            var mime = response?.Content?.Headers?.ContentType?.MediaType;
            return DetermineFileNameFromHeaders(cd, mime, requestUri);
        }

        public static string DetermineFileNameFromHeaders(ContentDispositionHeaderValue? cd, string? mime, Uri requestUri)
        {
            return ResolveAuthoritativeFileName(null, cd, null, mime, requestUri);
        }

        private static string? ExtractFileNameFromContentDisposition(ContentDispositionHeaderValue cd)
        {
            try
            {
                // RFC 5987 filename*=UTF-8''encoded_name
                if (!string.IsNullOrWhiteSpace(cd.FileNameStar))
                {
                    string star = cd.FileNameStar.Trim('"');
                    if (star.StartsWith("UTF-8''", StringComparison.OrdinalIgnoreCase))
                    {
                        star = star.Substring(7);
                    }
                    try { return Uri.UnescapeDataString(star); }
                    catch { return star; }
                }

                if (!string.IsNullOrWhiteSpace(cd.FileName))
                {
                    string fn = cd.FileName.Trim('"');
                    try { return Uri.UnescapeDataString(fn); }
                    catch { return fn; }
                }
            }
            catch (Exception ex)
            {
                LoggingService.Log($"[FileNamingHelper] Error extracting filename from Content-Disposition: {ex.Message}");
            }
            return null;
        }

        public static string SanitizeFileName(string name)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(name)) return "download.bin";

                // Strip quotes & URL decode if encoded
                try { name = Uri.UnescapeDataString(name.Trim('"', ' ')); }
                catch { name = name.Trim('"', ' '); }

                // Strip directory traversal paths (e.g. "../../../etc/passwd" or "C:\foo\bar.exe")
                if (name.Contains("..") || Path.IsPathRooted(name))
                {
                    var fn = Path.GetFileName(name.Replace('/', Path.DirectorySeparatorChar));
                    if (!string.IsNullOrWhiteSpace(fn)) name = fn;
                }

                // Strip invalid Windows characters: < > : " / \ | ? *
                char[] invalidChars = Path.GetInvalidFileNameChars();
                var sb = new StringBuilder(name.Length);
                foreach (char c in name)
                {
                    // Also filter non-printable control characters
                    if (char.IsControl(c) || Array.IndexOf(invalidChars, c) >= 0)
                    {
                        sb.Append('_');
                    }
                    else
                    {
                        sb.Append(c);
                    }
                }
                name = sb.ToString();

                // Trim leading/trailing spaces and dots (Win32 disallows trailing dots and spaces)
                name = name.Trim().TrimEnd('.');
                if (string.IsNullOrWhiteSpace(name)) return "download.bin";

                // Deduplicate redundant duplicate extensions (e.g. "file.mp4.mp4" -> "file.mp4")
                name = DeduplicateExtension(name);

                // Guard Windows DOS reserved device names (CON, PRN, AUX, NUL, COM1-9, LPT1-9) across multi-extensions
                string rootName = name.Split('.')[0];
                if (ReservedWindowsDeviceNames.Contains(rootName))
                {
                    string remainder = name.Substring(rootName.Length);
                    name = $"{rootName}_file{remainder}";
                }

                return string.IsNullOrWhiteSpace(name) ? "download.bin" : name;
            }
            catch (Exception ex)
            {
                LoggingService.Log($"[FileNamingHelper.SanitizeFileName] Sanitization failed for '{name}': {ex.Message}");
                return "download.bin";
            }
        }

        public static string DeduplicateExtension(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName)) return fileName;

            string ext = Path.GetExtension(fileName);
            if (string.IsNullOrEmpty(ext)) return fileName;

            string withoutExt = Path.GetFileNameWithoutExtension(fileName);
            if (withoutExt.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
            {
                return withoutExt;
            }

            return fileName;
        }

        public static string GetExtensionFromMime(string mime)
        {
            if (string.IsNullOrWhiteSpace(mime)) return ".bin";
            mime = mime.Split(';')[0].Trim().ToLowerInvariant();

            return mime switch
            {
                "video/mp4" => ".mp4",
                "video/webm" => ".webm",
                "video/x-matroska" => ".mkv",
                "video/x-msvideo" => ".avi",
                "video/x-flv" => ".flv",
                "video/quicktime" => ".mov",
                "audio/mpeg" or "audio/mp3" => ".mp3",
                "audio/mp4" or "audio/m4a" or "audio/x-m4a" => ".m4a",
                "audio/ogg" or "audio/opus" => ".opus",
                "audio/wav" or "audio/x-wav" => ".wav",
                "audio/flac" => ".flac",
                "application/zip" or "application/x-zip-compressed" => ".zip",
                "application/x-rar-compressed" or "application/vnd.rar" => ".rar",
                "application/x-7z-compressed" => ".7z",
                "application/x-tar" => ".tar",
                "application/gzip" or "application/x-gzip" => ".gz",
                "application/x-iso9660-image" => ".iso",
                "application/vnd.microsoft.portable-executable" or "application/x-msdownload" => ".exe",
                "application/vnd.android.package-archive" => ".apk",
                "application/pdf" => ".pdf",
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document" or "application/msword" => ".docx",
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" or "application/vnd.ms-excel" => ".xlsx",
                "application/vnd.openxmlformats-officedocument.presentationml.presentation" or "application/vnd.ms-powerpoint" => ".pptx",
                "text/html" => ".html",
                "text/css" => ".css",
                "application/javascript" or "text/javascript" => ".js",
                "application/json" => ".json",
                "application/xml" or "text/xml" => ".xml",
                "text/csv" => ".csv",
                "text/plain" => ".txt",
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
