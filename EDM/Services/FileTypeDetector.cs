using System;
using System.IO;
using System.Linq;

namespace EDM.Services
{
    public enum DetectedFileType
    {
        Unknown,
        Video,
        Audio,
        Documents,
        Compressed,
        Programs,
        Images,
        General
    }

    /// <summary>
    /// Multi-signal File Type & Format Intelligence Engine.
    /// Analyzes MIME Content-Type headers, URL patterns, filename extensions, and file header magic bytes.
    /// </summary>
    public static class FileTypeDetector
    {
        public static DetectedFileType DetectFromSignals(
            string? filename,
            string? contentType = null,
            string? url = null,
            byte[]? headerBytes = null)
        {
            // Signal 1: Header Magic Bytes (Highest Confidence)
            if (headerBytes != null && headerBytes.Length >= 4)
            {
                var magicType = DetectFromMagicBytes(headerBytes);
                if (magicType != DetectedFileType.Unknown)
                {
                    return magicType;
                }
            }

            // Signal 2: Content-Type MIME Header
            if (!string.IsNullOrWhiteSpace(contentType))
            {
                var mimeType = DetectFromMimeType(contentType);
                if (mimeType != DetectedFileType.Unknown)
                {
                    return mimeType;
                }
            }

            // Signal 3: Filename Extension
            if (!string.IsNullOrWhiteSpace(filename))
            {
                var extType = DetectFromExtension(Path.GetExtension(filename));
                if (extType != DetectedFileType.Unknown)
                {
                    return extType;
                }
            }

            // Signal 4: URL Path Semantics
            if (!string.IsNullOrWhiteSpace(url))
            {
                try
                {
                    var uri = new Uri(url.Contains("://") ? url : "https://" + url);
                    string path = uri.AbsolutePath;
                    var urlExtType = DetectFromExtension(Path.GetExtension(path));
                    if (urlExtType != DetectedFileType.Unknown)
                    {
                        return urlExtType;
                    }
                }
                catch { }
            }

            return DetectedFileType.General;
        }

        public static DetectedFileType DetectFromMagicBytes(byte[] header)
        {
            if (header == null || header.Length < 2) return DetectedFileType.Unknown;

            // PE Executables: "MZ" (0x4D, 0x5A)
            if (header[0] == 0x4D && header[1] == 0x5A)
            {
                return DetectedFileType.Programs;
            }

            // ELF Executables (Linux/Android): 0x7F, 'E', 'L', 'F'
            if (header.Length >= 4 && header[0] == 0x7F && header[1] == 0x45 && header[2] == 0x4C && header[3] == 0x46)
            {
                return DetectedFileType.Programs;
            }

            // Mach-O / DMG: 0xCA 0xFE 0xBA 0xBE or 0xCF 0xFA 0xED 0xFE
            if (header.Length >= 4 && ((header[0] == 0xCA && header[1] == 0xFE && header[2] == 0xBA && header[3] == 0xBE) ||
                                       (header[0] == 0xCF && header[1] == 0xFA && header[2] == 0xED && header[3] == 0xFE)))
            {
                return DetectedFileType.Programs;
            }

            // ZIP / Office Documents / JAR / APK / EPUB: "PK\x03\x04"
            if (header.Length >= 4 && header[0] == 0x50 && header[1] == 0x4B && header[2] == 0x03 && header[3] == 0x04)
            {
                return DetectedFileType.Compressed;
            }

            // RAR: "Rar!\x1A\x07" (0x52, 0x61, 0x72, 0x21)
            if (header.Length >= 4 && header[0] == 0x52 && header[1] == 0x61 && header[2] == 0x72 && header[3] == 0x21)
            {
                return DetectedFileType.Compressed;
            }

            // 7-Zip: '7', 'z', 0xBC, 0xAF, 0x27, 0x1C
            if (header.Length >= 6 && header[0] == 0x37 && header[1] == 0x7A && header[2] == 0xBC && header[3] == 0xAF)
            {
                return DetectedFileType.Compressed;
            }

            // GZIP: 0x1F, 0x8B
            if (header[0] == 0x1F && header[1] == 0x8B)
            {
                return DetectedFileType.Compressed;
            }

            // BZIP2: "BZh" (0x42, 0x5A, 0x68)
            if (header.Length >= 3 && header[0] == 0x42 && header[1] == 0x5A && header[2] == 0x68)
            {
                return DetectedFileType.Compressed;
            }

            // XZ: 0xFD, '7', 'z', 'X', 'Z', 0x00
            if (header.Length >= 6 && header[0] == 0xFD && header[1] == 0x37 && header[2] == 0x7A && header[3] == 0x58 && header[4] == 0x5A && header[5] == 0x00)
            {
                return DetectedFileType.Compressed;
            }

            // PDF: "%PDF" (0x25, 0x50, 0x44, 0x46)
            if (header.Length >= 4 && header[0] == 0x25 && header[1] == 0x50 && header[2] == 0x44 && header[3] == 0x46)
            {
                return DetectedFileType.Documents;
            }

            // MP4 / MOV / QuickTime / M4V / M4A / AVIF / HEIC: bytes 4-7 contain "ftyp" (0x66, 0x74, 0x79, 0x70)
            if (header.Length >= 12 && header[4] == 0x66 && header[5] == 0x74 && header[6] == 0x79 && header[7] == 0x70)
            {
                // Check AVIF / HEIC / HEIF image brands
                if ((header[8] == 0x61 && header[9] == 0x76 && header[10] == 0x69 && header[11] == 0x66) || // avif
                    (header[8] == 0x68 && header[9] == 0x65 && header[10] == 0x69 && header[11] == 0x63) || // heic
                    (header[8] == 0x6D && header[9] == 0x69 && header[10] == 0x66 && header[11] == 0x31))   // mif1
                {
                    return DetectedFileType.Images;
                }
                // Check M4A audio brand (M4A )
                if (header[8] == 0x4D && header[9] == 0x34 && header[10] == 0x41 && header[11] == 0x20)
                {
                    return DetectedFileType.Audio;
                }
                return DetectedFileType.Video;
            }

            // Matroska / WebM: 0x1A, 0x45, 0xDF, 0xA3
            if (header.Length >= 4 && header[0] == 0x1A && header[1] == 0x45 && header[2] == 0xDF && header[3] == 0xA3)
            {
                return DetectedFileType.Video;
            }

            // MPEG Transport Stream (TS): Sync byte 0x47
            if (header[0] == 0x47 && (header.Length < 188 || header[188] == 0x47))
            {
                return DetectedFileType.Video;
            }

            // FLV: "FLV\x01" (0x46, 0x4C, 0x56)
            if (header.Length >= 3 && header[0] == 0x46 && header[1] == 0x4C && header[2] == 0x56)
            {
                return DetectedFileType.Video;
            }

            // FLAC: "fLaC" (0x66, 0x4C, 0x61, 0x43)
            if (header.Length >= 4 && header[0] == 0x66 && header[1] == 0x4C && header[2] == 0x61 && header[3] == 0x43)
            {
                return DetectedFileType.Audio;
            }

            // OGG / Opus / Vorbis: "OggS" (0x4F, 0x67, 0x67, 0x53)
            if (header.Length >= 4 && header[0] == 0x4F && header[1] == 0x67 && header[2] == 0x67 && header[3] == 0x53)
            {
                return DetectedFileType.Audio;
            }

            // MP3: ID3 tag ("ID3") or sync frame 0xFF, 0xFB/0xF3/0xE0
            if (header.Length >= 3 && header[0] == 0x49 && header[1] == 0x44 && header[2] == 0x33)
            {
                return DetectedFileType.Audio;
            }
            if (header.Length >= 2 && header[0] == 0xFF && (header[1] & 0xE0) == 0xE0)
            {
                return DetectedFileType.Audio;
            }

            // RIFF container: "RIFF" (WAV, AVI, or WebP)
            if (header.Length >= 12 && header[0] == 0x52 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x46)
            {
                // Bytes 8-11: "WAVE" or "AVI " or "WEBP"
                if (header[8] == 0x57 && header[9] == 0x41 && header[10] == 0x56 && header[11] == 0x45)
                    return DetectedFileType.Audio;
                if (header[8] == 0x41 && header[9] == 0x56 && header[10] == 0x49 && header[11] == 0x20)
                    return DetectedFileType.Video;
                if (header[8] == 0x57 && header[9] == 0x45 && header[10] == 0x42 && header[11] == 0x50)
                    return DetectedFileType.Images;
            }

            // PNG: 0x89, 'P', 'N', 'G'
            if (header.Length >= 4 && header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47)
            {
                return DetectedFileType.Images;
            }

            // JPEG: 0xFF, 0xD8, 0xFF
            if (header.Length >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
            {
                return DetectedFileType.Images;
            }

            // GIF: "GIF87a" or "GIF89a" (0x47, 0x49, 0x46, 0x38)
            if (header.Length >= 4 && header[0] == 0x47 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x38)
            {
                return DetectedFileType.Images;
            }

            // BMP: "BM" (0x42, 0x4D)
            if (header[0] == 0x42 && header[1] == 0x4D)
            {
                return DetectedFileType.Images;
            }

            // SQLite Database: "SQLite format 3\0"
            if (header.Length >= 15 && header[0] == 0x53 && header[1] == 0x51 && header[2] == 0x4C && header[3] == 0x69 && header[4] == 0x74 && header[5] == 0x65)
            {
                return DetectedFileType.Documents;
            }

            return DetectedFileType.Unknown;
        }

        public static DetectedFileType DetectFromMimeType(string mimeType)
        {
            if (string.IsNullOrWhiteSpace(mimeType)) return DetectedFileType.Unknown;

            string clean = mimeType.Split(';')[0].Trim().ToLowerInvariant();

            if (clean.StartsWith("video/")) return DetectedFileType.Video;
            if (clean.StartsWith("audio/")) return DetectedFileType.Audio;
            if (clean.StartsWith("image/")) return DetectedFileType.Images;

            return clean switch
            {
                "application/zip" or "application/x-zip" or "application/x-zip-compressed" or 
                "application/x-rar-compressed" or "application/x-7z-compressed" or
                "application/x-tar" or "application/gzip" or "application/x-bzip2" or 
                "application/x-xz" or "application/x-iso9660-image" => DetectedFileType.Compressed,

                "application/pdf" or "application/msword" or
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document" or
                "application/vnd.ms-excel" or
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" or
                "application/vnd.ms-powerpoint" or
                "application/vnd.openxmlformats-officedocument.presentationml.presentation" or
                "text/plain" or "text/csv" or "application/json" or "application/xml" or "application/epub+zip" => DetectedFileType.Documents,

                "application/x-msdownload" or "application/x-executable" or "application/vnd.android.package-archive" or
                "application/java-archive" or "application/x-msi" or "application/x-apple-diskimage" or 
                "application/x-debian-package" or "application/x-redhat-package-manager" => DetectedFileType.Programs,

                _ => DetectedFileType.Unknown
            };
        }

        public static DetectedFileType DetectFromExtension(string? ext)
        {
            if (string.IsNullOrWhiteSpace(ext)) return DetectedFileType.Unknown;
            string clean = ext.Trim().ToLowerInvariant();
            if (!clean.StartsWith(".")) clean = "." + clean;

            return clean switch
            {
                ".mp4" or ".mkv" or ".webm" or ".avi" or ".mov" or ".flv" or ".ts" or ".m2ts" or ".mts" or ".m4v" or ".wmv" or ".3gp" or ".vob" or ".ogv" => DetectedFileType.Video,
                ".mp3" or ".wav" or ".flac" or ".aac" or ".ogg" or ".m4a" or ".opus" or ".wma" or ".alac" or ".aiff" or ".mid" or ".midi" => DetectedFileType.Audio,
                ".pdf" or ".doc" or ".docx" or ".xls" or ".xlsx" or ".ppt" or ".pptx" or ".txt" or ".epub" or ".rtf" or ".csv" or ".odt" or ".ods" or ".odp" or ".sqlite" or ".db" => DetectedFileType.Documents,
                ".zip" or ".rar" or ".7z" or ".tar" or ".gz" or ".bz2" or ".iso" or ".xz" or ".tgz" or ".cab" or ".dmg" or ".pkg" or ".deb" or ".rpm" => DetectedFileType.Compressed,
                ".exe" or ".msi" or ".bat" or ".cmd" or ".apk" or ".jar" or ".appx" or ".msix" or ".bin" or ".run" or ".crx" or ".xpi" => DetectedFileType.Programs,
                ".png" or ".jpg" or ".jpeg" or ".gif" or ".bmp" or ".webp" or ".svg" or ".ico" or ".avif" or ".heic" or ".heif" or ".tiff" or ".tif" => DetectedFileType.Images,
                _ => DetectedFileType.Unknown
            };
        }
    }
}
