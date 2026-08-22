using System;
using System.Collections.Generic;
using System.Linq;

namespace EDM.Services
{
    public class SupportArticle
    {
        public string Id { get; set; } = string.Empty;
        public int CategoryId { get; set; }
        public string CategoryName { get; set; } = string.Empty;
        public string CategoryIcon { get; set; } = "📁";
        public string Title { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
        public List<string> Keywords { get; set; } = new();
        public List<string> PossibleCauses { get; set; } = new();
        public List<string> StepByStepSolution { get; set; } = new();
        public List<string> WhatToCheck { get; set; } = new();
        public string WhenToContactSupport { get; set; } = string.Empty;
        public List<string> RelatedArticleIds { get; set; } = new();
    }

    public class SupportCategory
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Icon { get; set; } = "📁";
        public string Description { get; set; } = string.Empty;
        public int ArticleCount { get; set; }
    }

    /// <summary>
    /// Comprehensive Support & Troubleshooting Knowledge Base for EDM.
    /// Contains genuine, detailed troubleshooting articles for all 32 EDM core topics.
    /// </summary>
    public class SupportKnowledgeBase
    {
        private static readonly Lazy<SupportKnowledgeBase> _instance = new(() => new SupportKnowledgeBase());
        public static SupportKnowledgeBase Instance => _instance.Value;

        private readonly List<SupportArticle> _articles = new();
        private readonly List<SupportCategory> _categories = new();

        public SupportKnowledgeBase()
        {
            InitializeKnowledgeBase();
        }

        public IReadOnlyList<SupportCategory> GetCategories() => _categories;

        public IReadOnlyList<SupportArticle> GetAllArticles() => _articles;

        public IReadOnlyList<SupportArticle> GetArticlesByCategory(int categoryId)
        {
            return _articles.Where(a => a.CategoryId == categoryId).ToList();
        }

        public SupportArticle? GetArticleById(string id)
        {
            return _articles.FirstOrDefault(a => string.Equals(a.Id, id, StringComparison.OrdinalIgnoreCase));
        }

        public List<SupportArticle> Search(string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return _articles.ToList();

            var terms = query.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);

            return _articles
                .Select(article =>
                {
                    int score = 0;
                    string titleLower = article.Title.ToLowerInvariant();
                    string summaryLower = article.Summary.ToLowerInvariant();
                    string catLower = article.CategoryName.ToLowerInvariant();

                    foreach (var term in terms)
                    {
                        if (titleLower.Contains(term)) score += 10;
                        if (article.Keywords.Any(k => k.ToLowerInvariant().Contains(term))) score += 8;
                        if (catLower.Contains(term)) score += 5;
                        if (summaryLower.Contains(term)) score += 3;
                        if (article.PossibleCauses.Any(c => c.ToLowerInvariant().Contains(term))) score += 2;
                        if (article.StepByStepSolution.Any(s => s.ToLowerInvariant().Contains(term))) score += 2;
                    }

                    return new { Article = article, Score = score };
                })
                .Where(x => x.Score > 0)
                .OrderByDescending(x => x.Score)
                .Select(x => x.Article)
                .ToList();
        }

        private void InitializeKnowledgeBase()
        {
            var rawArticles = new List<SupportArticle>
            {
                // 1. Getting Started
                new SupportArticle
                {
                    Id = "getting-started",
                    CategoryId = 1,
                    CategoryName = "1. Getting Started",
                    CategoryIcon = "🚀",
                    Title = "Getting Started with Exclusive Download Manager (EDM)",
                    Summary = "Learn how to configure your download directories, install browser integration, and start your first high-speed download.",
                    Keywords = new() { "install", "setup", "first download", "configuration", "quickstart" },
                    PossibleCauses = new()
                    {
                        "First time running EDM on a new Windows installation.",
                        "Default download directories have not been configured.",
                        "Browser extensions are not yet paired with the native messaging host."
                    },
                    StepByStepSolution = new()
                    {
                        "1. Open Settings (⚙️) from the top-right toolbar or sidebar.",
                        "2. Under 'Save To', select your preferred default download folder (e.g. C:\\Downloads or D:\\Downloads).",
                        "3. Go to the 'Browser Integration' tab and click 'Register Native Host' to link Chrome, Edge, or Firefox.",
                        "4. Click 'Add URL' (+) on the dashboard, paste any direct download link, and click 'Download'."
                    },
                    WhatToCheck = new()
                    {
                        "Verify EDM has write permissions to your chosen download directory.",
                        "Ensure your network connection is active and stable."
                    },
                    WhenToContactSupport = "Contact support if the initial installation crashes or if EDM cannot create folders on your storage drive.",
                    RelatedArticleIds = new() { "adding-download", "browser-integration", "download-location" }
                },

                // 2. Adding a Download
                new SupportArticle
                {
                    Id = "adding-download",
                    CategoryId = 2,
                    CategoryName = "2. Adding a Download",
                    CategoryIcon = "➕",
                    Title = "How to Add URLs, Batch Links, and Clipboard Captures",
                    Summary = "Step-by-step guide to adding direct links, FTP URIs, torrent files, and batch downloads using EDM.",
                    Keywords = new() { "add url", "clipboard", "batch", "links", "import" },
                    PossibleCauses = new()
                    {
                        "URL format is invalid or contains unescaped special characters.",
                        "Clipboard monitoring is temporarily disabled in settings.",
                        "Target server requires specific HTTP authentication credentials or custom user-agent headers."
                    },
                    StepByStepSolution = new()
                    {
                        "1. Copy any valid HTTP, HTTPS, or FTP download link to your clipboard.",
                        "2. If EDM is running, it will automatically detect the link and present the 'Add Download' dialog.",
                        "3. Alternatively, click 'Add URL' on the top toolbar and paste the link manually.",
                        "4. To add multiple links simultaneously, click 'Batch Download' under the File menu, enter one URL per line, and click 'Start All'."
                    },
                    WhatToCheck = new()
                    {
                        "Check that the URL begins with http://, https://, or ftp://.",
                        "Verify 'Clipboard Monitoring' is enabled under Settings > General."
                    },
                    WhenToContactSupport = "Contact support if valid URLs fail to parse or cause an unexpected format validation error.",
                    RelatedArticleIds = new() { "getting-started", "starting-download", "duplicate-downloads" }
                },

                // 3. Starting a Download
                new SupportArticle
                {
                    Id = "starting-download",
                    CategoryId = 3,
                    CategoryName = "3. Starting a Download",
                    CategoryIcon = "▶️",
                    Title = "Starting and Initializing Multi-Part Connection Streams",
                    Summary = "Understanding how EDM establishes HTTP range handshakes, probes server capabilities, and splits files into parallel chunks.",
                    Keywords = new() { "start download", "connecting", "probe", "multipart", "handshake" },
                    PossibleCauses = new()
                    {
                        "Server does not respond to initial HTTP HEAD probe request.",
                        "Target host has a rate-limiting firewall blocking multiple connection handshakes.",
                        "Proxy or VPN tunnel is dropping SYN packets."
                    },
                    StepByStepSolution = new()
                    {
                        "1. Verify that the target server is online by testing the link in your web browser.",
                        "2. Select the download item in the EDM dashboard table and click 'Resume' or double-click to start.",
                        "3. If the server only supports single connections, EDM will automatically fall back to single-stream streaming."
                    },
                    WhatToCheck = new()
                    {
                        "Check the Status column: 'Connecting...', 'Downloading', or 'Queued'.",
                        "Look at the Transfer Rate column to confirm data transmission has begun."
                    },
                    WhenToContactSupport = "Contact support if downloads stay indefinitely in 'Connecting...' across all different websites.",
                    RelatedArticleIds = new() { "download-stuck-0", "download-speed", "network-problems" }
                },

                // 4. Pause and Resume
                new SupportArticle
                {
                    Id = "pause-and-resume",
                    CategoryId = 4,
                    CategoryName = "4. Pause and Resume",
                    CategoryIcon = "⏯️",
                    Title = "Pausing, Resuming, and Partial Chunk Recovery",
                    Summary = "How EDM saves byte offsets and segment states to allow seamless resumption without restarting large files from 0%.",
                    Keywords = new() { "pause", "resume", "ranges", "interrupted", "continue" },
                    PossibleCauses = new()
                    {
                        "Remote server does not support HTTP Range headers (Accept-Ranges: none).",
                        "Presigned download URL has expired while the download was paused.",
                        "Temporary segment files (.edm_part) were moved or deleted by third-party cleaner tools."
                    },
                    StepByStepSolution = new()
                    {
                        "1. Click the 'Pause' button on any active download to safely flush data buffers to disk.",
                        "2. When ready to continue, click 'Resume'. EDM will re-open existing segment files and request only remaining bytes.",
                        "3. If the server returned '403 Forbidden' upon resume due to an expired token, right-click the download and choose 'Refresh Download Address' to paste a fresh link without losing downloaded progress."
                    },
                    WhatToCheck = new()
                    {
                        "Verify whether the server header contains 'Accept-Ranges: bytes'.",
                        "Ensure disk space is still available in the temporary download directory."
                    },
                    WhenToContactSupport = "Contact support if resume causes file corruption on Range-supported servers.",
                    RelatedArticleIds = new() { "download-stuck-0", "download-failed", "disk-space" }
                },

                // 5. Download Speed Problems
                new SupportArticle
                {
                    Id = "download-speed",
                    CategoryId = 5,
                    CategoryName = "5. Download Speed Problems",
                    CategoryIcon = "⚡",
                    Title = "Resolving Slow Download Speeds & ISP Throttling",
                    Summary = "Techniques to maximize throughput using dynamic connection scaling, buffer optimization, and network tuning.",
                    Keywords = new() { "slow speed", "bandwidth", "throttling", "boost", "connections", "fast" },
                    PossibleCauses = new()
                    {
                        "EDM connection count is set too low (e.g. 1 or 2 connections).",
                        "Bandwidth Speed Limiter is actively enabled in EDM settings.",
                        "Remote web server caps per-IP bandwidth.",
                        "Local Wi-Fi interference or ISP traffic shaping."
                    },
                    StepByStepSolution = new()
                    {
                        "1. Open Settings > Connection and set 'Max Connections per Download' to 8, 16, or 24.",
                        "2. Ensure 'Speed Limiter' on the bottom status bar is toggled OFF (or set to 0 KB/s for unlimited).",
                        "3. If your ISP throttles specific protocols, enable HTTPS or test through a high-speed VPN tunnel.",
                        "4. Enable 'High Throughput ArrayPool Buffering' in Advanced Settings."
                    },
                    WhatToCheck = new()
                    {
                        "Check your baseline internet speed using an external speed test.",
                        "Verify other local devices are not saturating the local router bandwidth."
                    },
                    WhenToContactSupport = "Contact support if speeds in EDM are significantly lower than single-threaded browser downloads on the same network.",
                    RelatedArticleIds = new() { "starting-download", "network-problems", "proxy-problems" }
                },

                // 6. Download Stuck at 0%
                new SupportArticle
                {
                    Id = "download-stuck-0",
                    CategoryId = 6,
                    CategoryName = "6. Download Stuck at 0%",
                    CategoryIcon = "🛑",
                    Title = "Troubleshooting Downloads Stuck at 0% or 'Connecting...'",
                    Summary = "Complete guide to resolving downloads that fail to start transferring data.",
                    Keywords = new() { "stuck", "0 percent", "not starting", "frozen", "hanging", "connecting" },
                    PossibleCauses = new()
                    {
                        "Remote server did not provide Content-Length and hangs on multi-connection probe.",
                        "Cloudflare, reCAPTCHA, or anti-bot challenge page was returned instead of file data.",
                        "Firewall or Antivirus software is intercepting and holding the socket connection.",
                        "Browser cookie / session token is required to authenticate the download.",
                        "Invalid or expired redirect URL."
                    },
                    StepByStepSolution = new()
                    {
                        "1. Copy the download link and open it in your browser to verify whether a Cloudflare captcha or login is required.",
                        "2. If a login is required, open 'Site Logins Manager' in EDM Settings and add your credentials or session cookies.",
                        "3. Right-click the stuck download, click 'Properties', and check if the file size was resolved. If not, set connection count to 1.",
                        "4. Temporarily add an exclusion for EDM.exe in Windows Defender / your antivirus suite.",
                        "5. Restart the download by clicking Stop and then Resume."
                    },
                    WhatToCheck = new()
                    {
                        "Check if the server returns HTTP 200 OK or HTTP 302 Found.",
                        "Inspect the EDM log window under Settings > Diagnostics for detailed HTTP status codes."
                    },
                    WhenToContactSupport = "Contact support if all links from standard CDN hosts (e.g. GitHub, Google Drive) remain stuck at 0%.",
                    RelatedArticleIds = new() { "download-failed", "firewall-antivirus", "browser-integration" }
                },

                // 7. Download Failed
                new SupportArticle
                {
                    Id = "download-failed",
                    CategoryId = 7,
                    CategoryName = "7. Download Failed",
                    CategoryIcon = "❌",
                    Title = "Handling HTTP 403 Forbidden, 404 Not Found, and Socket Drop Errors",
                    Summary = "Diagnosing common HTTP status codes and network socket termination errors.",
                    Keywords = new() { "failed", "error", "403 forbidden", "404 not found", "503", "socket reset" },
                    PossibleCauses = new()
                    {
                        "HTTP 403 Forbidden: Session cookie expired, hotlinking prevention, or geo-restriction.",
                        "HTTP 404 Not Found: File has been deleted or moved from the remote server.",
                        "HTTP 503 Service Unavailable: Remote server is under heavy load.",
                        "Connection Reset by Peer: Network packet loss or gateway timeout."
                    },
                    StepByStepSolution = new()
                    {
                        "1. For 403 Forbidden: Re-visit the download page in your browser to generate a fresh download token.",
                        "2. Right-click the failed download and select 'Refresh Download Address' to update the URL without losing progress.",
                        "3. For 503 errors: Wait 60 seconds; EDM's automated retry engine will automatically retry with exponential backoff.",
                        "4. For socket drops: Go to Settings > Connection and reduce max parallel segments to 4."
                    },
                    WhatToCheck = new()
                    {
                        "Check the Status column error message (e.g., 'Error: HTTP 403').",
                        "Verify whether you can access the URL in an incognito browser window."
                    },
                    WhenToContactSupport = "Contact support if downloads fail consistently across all domains with an unknown internal exception.",
                    RelatedArticleIds = new() { "download-stuck-0", "network-problems", "proxy-problems" }
                },

                // 8. File Not Found After Download
                new SupportArticle
                {
                    Id = "file-not-found",
                    CategoryId = 8,
                    CategoryName = "8. File Not Found After Download",
                    CategoryIcon = "🔍",
                    Title = "Locating Completed Files and Managing Default Folders",
                    Summary = "Where completed files are saved based on Smart Categorization rules and disk path configurations.",
                    Keywords = new() { "file missing", "where is file", "smart folder", "lost download", "open folder" },
                    PossibleCauses = new()
                    {
                        "EDM Smart Categorization routed the file to a subfolder (e.g. Downloads\\Video or Downloads\\Programs).",
                        "Windows Defender quarantined or moved the file immediately upon completion.",
                        "The target drive was disconnected or unmounted."
                    },
                    StepByStepSolution = new()
                    {
                        "1. In the EDM dashboard, right-click the completed download and select 'Open Containing Folder'.",
                        "2. To check category folders, navigate to your default downloads directory and check subdirectories: Compressed, Documents, Music, Programs, Video.",
                        "3. Check Windows Security > Protection History to verify if antivirus quarantined the downloaded binary."
                    },
                    WhatToCheck = new()
                    {
                        "Check Settings > 'Edit Category Routing Rules' to see where specific file extensions are routed.",
                        "Verify that the destination hard drive is connected and healthy."
                    },
                    WhenToContactSupport = "Contact support if EDM marks files as Completed but no file exists anywhere on the disk.",
                    RelatedArticleIds = new() { "download-location", "firewall-antivirus", "smart-file-organizer" }
                },

                // 9. Incorrect File Size
                new SupportArticle
                {
                    Id = "incorrect-file-size",
                    CategoryId = 9,
                    CategoryName = "9. Incorrect File Size",
                    CategoryIcon = "📏",
                    Title = "Fixing 'Unknown Size' or Truncated File Lengths",
                    Summary = "Understanding chunked transfer encoding, dynamic streaming lengths, and size formatting.",
                    Keywords = new() { "size", "unknown size", "-1 b", "truncated", "wrong size" },
                    PossibleCauses = new()
                    {
                        "Remote server uses 'Transfer-Encoding: chunked' without declaring a total Content-Length header.",
                        "The file is dynamically generated on-the-fly by a backend script.",
                        "Legacy EDM versions calculated unknown sizes as negative numbers (resolved in v6.0.0)."
                    },
                    StepByStepSolution = new()
                    {
                        "1. Upgrade to EDM v6.0.0 or later to ensure unknown stream lengths format safely as 'Unknown' instead of '-1 B'.",
                        "2. Chunked downloads will download continuously in single-connection mode until the remote server sends EOF.",
                        "3. Once finished, EDM will update the exact final byte count in the database and display the accurate file size."
                    },
                    WhatToCheck = new()
                    {
                        "Check if the download status shows 'Downloading' and the byte count increases steadily.",
                        "Verify the completed file's size in Windows Explorer matches the actual file size."
                    },
                    WhenToContactSupport = "Contact support if a download terminates prematurely before all bytes are transferred.",
                    RelatedArticleIds = new() { "duplicate-downloads", "database-history", "getting-started" }
                },

                // 10. Duplicate Downloads
                new SupportArticle
                {
                    Id = "duplicate-downloads",
                    CategoryId = 10,
                    CategoryName = "10. Duplicate Downloads",
                    CategoryIcon = "📋",
                    Title = "Eliminating Duplicate Download Entries in the List",
                    Summary = "How EDM v6.0.0 canonicalizes URLs and file destinations to prevent duplicate list rows.",
                    Keywords = new() { "duplicate", "redundant", "repeated", "double", "cleanup" },
                    PossibleCauses = new()
                    {
                        "Multiple browser clicks on the same download link.",
                        "Repeated batch addition of the same URL.",
                        "Legacy database entries created before v6.0.0 deduplication."
                    },
                    StepByStepSolution = new()
                    {
                        "1. EDM v6.0.0 automatically runs a SQLite window deduplication routine on startup to purge redundant rows.",
                        "2. If you attempt to add an identical URL that is already active, EDM will offer to restart or resume the existing item.",
                        "3. To manually remove duplicates, right-click the row and select 'Delete', or click 'Clear Completed' on the toolbar."
                    },
                    WhatToCheck = new()
                    {
                        "Verify that your database is using SQLite WAL mode under `%LOCALAPPDATA%\\EDM\\edm_history.db`.",
                        "Ensure you are running EDM version 6.0.0 or higher."
                    },
                    WhenToContactSupport = "Contact support if the same URL repeatedly creates new duplicate rows on every launch.",
                    RelatedArticleIds = new() { "database-history", "adding-download", "settings-problems" }
                },

                // 11. Browser Integration
                new SupportArticle
                {
                    Id = "browser-integration",
                    CategoryId = 11,
                    CategoryName = "11. Browser Integration",
                    CategoryIcon = "🌐",
                    Title = "Setting Up Chrome, Edge, and Firefox Browser Integration",
                    Summary = "Installing the EDM browser companion extension and configuring native messaging host communication.",
                    Keywords = new() { "chrome", "edge", "firefox", "browser extension", "native host", "intercept" },
                    PossibleCauses = new()
                    {
                        "Native messaging host JSON manifest is not registered in the Windows Registry.",
                        "Browser extension is disabled or lacks permission to communicate with native applications.",
                        "Third-party privacy extension is blocking localhost IPC communication."
                    },
                    StepByStepSolution = new()
                    {
                        "1. Open EDM Settings > 'Browser Integration'.",
                        "2. Click 'Register Extension for All Browsers' (requires standard user registry permissions).",
                        "3. Install the official 'EDM Download Assistant' extension from the Chrome Web Store or Firefox Add-ons.",
                        "4. Click any download link in your browser: EDM will automatically intercept and launch the download window."
                    },
                    WhatToCheck = new()
                    {
                        "Check registry key: `HKCU\\Software\\Google\\Chrome\\NativeMessagingHosts\\com.edm.downloader`.",
                        "Verify the extension icon in your browser toolbar displays 'Connected'."
                    },
                    WhenToContactSupport = "Contact support if browser downloads are not intercepted even after registering the native host.",
                    RelatedArticleIds = new() { "getting-started", "video-downloads", "firewall-antivirus" }
                },

                // 12. Video Downloads
                new SupportArticle
                {
                    Id = "video-downloads",
                    CategoryId = 12,
                    CategoryName = "12. Video Downloads",
                    CategoryIcon = "🎬",
                    Title = "Downloading High-Resolution Video Streams (YouTube, HLS, DASH)",
                    Summary = "Extracting 1080p, 4K, and 8K videos and merging separate audio/video streams using FFmpeg.",
                    Keywords = new() { "youtube", "video", "4k", "1080p", "hls", "dash", "stream", "yt-dlp" },
                    PossibleCauses = new()
                    {
                        "YouTube stream signatures changed (requires yt-dlp core update).",
                        "FFmpeg executable is not configured or missing from the system path.",
                        "Video is geo-restricted or age-restricted requiring authentication."
                    },
                    StepByStepSolution = new()
                    {
                        "1. Paste the video URL into EDM's 'Add URL' box: EDM will analyze available formats (1080p, 4K, MP3, etc.).",
                        "2. Select your desired video quality and audio track.",
                        "3. Ensure FFmpeg path is set in Settings > General: EDM will automatically multiplex video and audio into a final .mp4 or .mkv file.",
                        "4. To update the video extraction engine, click 'Update yt-dlp Core' in Settings."
                    },
                    WhatToCheck = new()
                    {
                        "Check that `ffmpeg.exe` exists in your EDM installation folder or custom path.",
                        "Verify you have sufficient disk space for the separate audio + video temporary buffers."
                    },
                    WhenToContactSupport = "Contact support if video extraction fails on a public, non-copyrighted video stream.",
                    RelatedArticleIds = new() { "audio-downloads", "browser-integration", "disk-space" }
                },

                // 13. Audio Downloads
                new SupportArticle
                {
                    Id = "audio-downloads",
                    CategoryId = 13,
                    CategoryName = "13. Audio Downloads",
                    CategoryIcon = "🎵",
                    Title = "Extracting Audio & Converting Video Streams to MP3 / FLAC",
                    Summary = "Direct audio extraction from web streams and converting media files to high-bitrate MP3, AAC, and FLAC.",
                    Keywords = new() { "audio", "mp3", "flac", "convert", "music", "podcast", "extract" },
                    PossibleCauses = new()
                    {
                        "Auto-convert checkbox is disabled in Settings.",
                        "FFmpeg audio codec libraries are missing.",
                        "Target stream contains encrypted DRM audio."
                    },
                    StepByStepSolution = new()
                    {
                        "1. Open Settings > General and check 'Auto-convert downloaded video to MP3 when requested'.",
                        "2. When adding a video URL, select the 'Audio Only (MP3 320kbps)' quality option.",
                        "3. EDM will download the pristine audio stream and tag metadata (artist, title, album art) automatically."
                    },
                    WhatToCheck = new()
                    {
                        "Verify completed audio files appear in your Downloads\\Music directory.",
                        "Check that the output file plays properly in Windows Media Player / VLC."
                    },
                    WhenToContactSupport = "Contact support if audio conversion produces corrupted or 0-byte output files.",
                    RelatedArticleIds = new() { "video-downloads", "file-not-found", "download-location" }
                },

                // 14. Download Location
                new SupportArticle
                {
                    Id = "download-location",
                    CategoryId = 14,
                    CategoryName = "14. Download Location",
                    CategoryIcon = "📂",
                    Title = "Customizing Download Folders and Category Routing",
                    Summary = "Configuring custom directories, external hard drives, and network UNC shares (NAS).",
                    Keywords = new() { "folder", "directory", "save to", "category rules", "nas", "path" },
                    PossibleCauses = new()
                    {
                        "Default download path points to a non-existent or read-only directory.",
                        "Category routing rule overrides the default folder for specific file extensions.",
                        "Network share (SMB/UNC) requires authentication before file creation."
                    },
                    StepByStepSolution = new()
                    {
                        "1. Open Settings > 'Save To' tab.",
                        "2. Click 'Browse...' to select a new default directory on any internal or external drive.",
                        "3. Click 'Edit Category Routing Rules' to customize specific folders for Documents, Compressed, Video, and Music.",
                        "4. Click 'Save Settings' to apply."
                    },
                    WhatToCheck = new()
                    {
                        "Verify your user account has Write and Modify NTFS permissions on the target directory.",
                        "Ensure network shares are mapped with persistent credentials."
                    },
                    WhenToContactSupport = "Contact support if EDM fails to save files to a verified, writable local drive path.",
                    RelatedArticleIds = new() { "file-not-found", "disk-space", "permission-problems" }
                },

                // 15. Disk Space Problems
                new SupportArticle
                {
                    Id = "disk-space",
                    CategoryId = 15,
                    CategoryName = "15. Disk Space Problems",
                    CategoryIcon = "💾",
                    Title = "Managing Low Disk Space & Temporary Segment Allocation",
                    Summary = "How EDM pre-allocates file space, cleans temporary chunks, and avoids out-of-disk crashes.",
                    Keywords = new() { "disk full", "low disk space", "preallocate", "temp folder", "storage" },
                    PossibleCauses = new()
                    {
                        "The destination drive does not have enough free space for the total declared file size.",
                        "Temporary segment files (.edm_part) occupy space on drive C: while downloading to drive D:.",
                        "Disk pre-allocation failed due to fragmentation."
                    },
                    StepByStepSolution = new()
                    {
                        "1. In Settings > 'Storage', configure 'Per-Disk Temp Storage' so temporary files are kept on the target drive rather than drive C:.",
                        "2. Delete completed downloads or empty your Windows Recycle Bin to free up disk capacity.",
                        "3. Resume paused downloads after freeing space: EDM will verify existing segments and continue."
                    },
                    WhatToCheck = new()
                    {
                        "Check available disk space in Windows Explorer (This PC).",
                        "Ensure the target drive has at least 110% of the download file size available."
                    },
                    WhenToContactSupport = "Contact support if disk space errors occur when hundreds of gigabytes are free.",
                    RelatedArticleIds = new() { "download-location", "pause-and-resume", "permission-problems" }
                },

                // 16. Network Problems
                new SupportArticle
                {
                    Id = "network-problems",
                    CategoryId = 16,
                    CategoryName = "16. Network Problems",
                    CategoryIcon = "📶",
                    Title = "Diagnosing Socket Timeouts, DNS Failures, and Packet Drops",
                    Summary = "Fixing connection drops, high latency, and network adapter configuration issues in EDM.",
                    Keywords = new() { "network", "dns error", "timeout", "socket", "disconnect", "wifi" },
                    PossibleCauses = new()
                    {
                        "DNS resolution failure on the remote hostname.",
                        "Router MTU mismatch causing TCP packet fragmentation.",
                        "Network interface switching between Wi-Fi and Ethernet."
                    },
                    StepByStepSolution = new()
                    {
                        "1. Test opening the download website in your browser to confirm DNS connectivity.",
                        "2. In EDM Settings > Network, switch DNS provider to 'Cloudflare (1.1.1.1)' or 'Google (8.8.8.8)'.",
                        "3. Enable 'Automated Network Reconnection' to automatically resume transfers if Wi-Fi drops.",
                        "4. Restart your router or network adapter if timeouts persist."
                    },
                    WhatToCheck = new()
                    {
                        "Check Windows Network Status in system settings.",
                        "Verify whether pinging the remote host returns valid latency."
                    },
                    WhenToContactSupport = "Contact support if socket errors only occur within EDM while all other applications have internet access.",
                    RelatedArticleIds = new() { "download-speed", "proxy-problems", "firewall-antivirus" }
                },

                // 17. Proxy Problems
                new SupportArticle
                {
                    Id = "proxy-problems",
                    CategoryId = 17,
                    CategoryName = "17. Proxy Problems",
                    CategoryIcon = "🛡️",
                    Title = "Configuring HTTP, HTTPS, SOCKS5 Proxies and PAC Scripts",
                    Summary = "How to route EDM traffic through authenticated proxy servers, corporate gateways, and Tor SOCKS5.",
                    Keywords = new() { "proxy", "socks5", "pac script", "gateway", "corporate", "anonymous" },
                    PossibleCauses = new()
                    {
                        "Proxy server IP or port is incorrect or currently offline.",
                        "Proxy credentials (username/password) are expired or invalid.",
                        "Proxy protocol mismatch (e.g. configuring SOCKS5 as HTTP)."
                    },
                    StepByStepSolution = new()
                    {
                        "1. Open Settings > Proxy.",
                        "2. Select your proxy protocol: 'HTTP', 'HTTPS', or 'SOCKS5'.",
                        "3. Enter the Host IP, Port, and optional authentication credentials (saved securely in DPAPI vault).",
                        "4. Click 'Test Proxy Connection' to verify handshake before saving."
                    },
                    WhatToCheck = new()
                    {
                        "Check if your corporate network requires a PAC auto-configuration script.",
                        "Verify proxy status in your browser's proxy settings."
                    },
                    WhenToContactSupport = "Contact support if authenticated SOCKS5 proxies fail handshake verification.",
                    RelatedArticleIds = new() { "network-problems", "settings-problems", "security-policy" }
                },

                // 18. Firewall/Antivirus Issues
                new SupportArticle
                {
                    Id = "firewall-antivirus",
                    CategoryId = 18,
                    CategoryName = "18. Firewall/Antivirus Issues",
                    CategoryIcon = "🛡️",
                    Title = "Resolving Windows Defender and Antivirus False Positives",
                    Summary = "How to whitelist EDM, prevent download locks, and configure custom antivirus scanner hooks.",
                    Keywords = new() { "antivirus", "defender", "firewall", "blocked", "whitelist", "quarantine" },
                    PossibleCauses = new()
                    {
                        "Windows Defender or third-party AV (Kaspersky, Bitdefender, Avast) locks partial files during download.",
                        "Windows Firewall blocks outbound TCP connections from EDM.exe on non-standard ports."
                    },
                    StepByStepSolution = new()
                    {
                        "1. Open Windows Security > Virus & threat protection > Manage settings.",
                        "2. Scroll to 'Exclusions' and add `EDM.exe` and your EDM download directory.",
                        "3. In Windows Defender Firewall, verify EDM is checked under 'Allowed Apps'.",
                        "4. In EDM Settings > Antivirus, enable 'Post-Download Antivirus Scan' to scan files only after 100% completion."
                    },
                    WhatToCheck = new()
                    {
                        "Check antivirus protection logs for blocked outbound connections from EDM.",
                        "Ensure your EDM binary has a valid digital signature."
                    },
                    WhenToContactSupport = "Contact support if your security software flags EDM executable as a false positive.",
                    RelatedArticleIds = new() { "download-failed", "permission-problems", "security-policy" }
                },

                // 19. Permission Problems
                new SupportArticle
                {
                    Id = "permission-problems",
                    CategoryId = 19,
                    CategoryName = "19. Permission Problems",
                    CategoryIcon = "🔒",
                    Title = "Fixing 'Access Denied' and Windows UAC Permission Errors",
                    Summary = "Resolving directory access errors when writing to protected Windows locations or secondary drives.",
                    Keywords = new() { "permission", "access denied", "administrator", "uac", "write error" },
                    PossibleCauses = new()
                    {
                        "Attempting to download directly into root `C:\\` or `C:\\Program Files` without elevation.",
                        "Destination directory is owned by another Windows user profile.",
                        "File is locked by another running process (e.g. video editor or player)."
                    },
                    StepByStepSolution = new()
                    {
                        "1. Change your default download directory to a user-owned folder like `C:\\Users\\<Username>\\Downloads`.",
                        "2. Right-click the destination folder, select Properties > Security, and ensure your user has 'Full Control'.",
                        "3. If downloading to an external drive, ensure the drive is not formatted as Read-Only."
                    },
                    WhatToCheck = new()
                    {
                        "Check the specific error in EDM: 'UnauthorizedAccessException' or 'Access is Denied'.",
                        "Verify no other media player currently has the target filename open."
                    },
                    WhenToContactSupport = "Contact support if access denied errors persist in standard user Downloads directory.",
                    RelatedArticleIds = new() { "download-location", "disk-space", "file-not-found" }
                },

                // 20. Scheduler Problems
                new SupportArticle
                {
                    Id = "scheduler-problems",
                    CategoryId = 20,
                    CategoryName = "20. Scheduler Problems",
                    CategoryIcon = "⏰",
                    Title = "Setting Up Automated Download Times & Power Off Actions",
                    Summary = "Scheduling downloads during off-peak night hours and automatically shutting down your PC upon completion.",
                    Keywords = new() { "scheduler", "timer", "night download", "power off", "shutdown", "sleep" },
                    PossibleCauses = new()
                    {
                        "Scheduler toggle is switched off.",
                        "Computer entered Windows Sleep mode before scheduled start time.",
                        "Windows Power settings prevented programmatic shutdown."
                    },
                    StepByStepSolution = new()
                    {
                        "1. Open 'Scheduler' from the sidebar or toolbar.",
                        "2. Check 'Enable Scheduler', set your desired Start Time (e.g. 02:00 AM) and Stop Time (e.g. 07:00 AM).",
                        "3. Select what action to take when all downloads finish: 'Shutdown Computer', 'Sleep', or 'Hibernate'.",
                        "4. Enable 'Prevent Computer from Sleeping While Downloading' in EDM Settings."
                    },
                    WhatToCheck = new()
                    {
                        "Check that EDM remains running in the system tray during the night.",
                        "Verify your Windows power plan allows EDM to execute wake timers."
                    },
                    WhenToContactSupport = "Contact support if power actions fail to execute after batch completion.",
                    RelatedArticleIds = new() { "queue-problems", "settings-problems", "getting-started" }
                },

                // 21. Queue Problems
                new SupportArticle
                {
                    Id = "queue-problems",
                    CategoryId = 21,
                    CategoryName = "21. Queue Problems",
                    CategoryIcon = "🔢",
                    Title = "Managing Download Queues, Limits, and Prioritization",
                    Summary = "How to order downloads, set max active simultaneous downloads, and configure multiple queue profiles.",
                    Keywords = new() { "queue", "concurrent", "order", "priority", "limits", "batch queue" },
                    PossibleCauses = new()
                    {
                        "Max active concurrent downloads limit is set too high, saturating bandwidth.",
                        "Downloads were manually paused and removed from the active queue.",
                        "Queue synchronization mutex conflict."
                    },
                    StepByStepSolution = new()
                    {
                        "1. Open Queues in the sidebar to view active queue categories.",
                        "2. In Settings > Connection, adjust 'Maximum Simultaneous Downloads' (default is 4).",
                        "3. Use the Up / Down arrows on the toolbar to re-order priority of queued items.",
                        "4. Click 'Start Queue' to begin processing items in sequential order."
                    },
                    WhatToCheck = new()
                    {
                        "Check that queued items display 'Queued' in the Status column.",
                        "Verify that when one download completes, the next immediately begins."
                    },
                    WhenToContactSupport = "Contact support if the queue stops advancing automatically after a download completes.",
                    RelatedArticleIds = new() { "scheduler-problems", "download-speed", "starting-download" }
                },

                // 22. Application Startup Problems
                new SupportArticle
                {
                    Id = "startup-problems",
                    CategoryId = 22,
                    CategoryName = "22. Application Startup Problems",
                    CategoryIcon = "🚀",
                    Title = "Resolving Crashes, Single-Instance Locks, and Startup Delays",
                    Summary = "Troubleshooting EDM startup issues, missing .NET 10 runtimes, and corrupted configuration caches.",
                    Keywords = new() { "startup", "crash on launch", "not opening", "single instance", "dotnet runtime" },
                    PossibleCauses = new()
                    {
                        "An existing zombie EDM process is still running in the background.",
                        "Corrupted settings XML file or locked SQLite database.",
                        "Missing .NET Desktop Runtime dependency."
                    },
                    StepByStepSolution = new()
                    {
                        "1. Open Windows Task Manager (Ctrl+Shift+Esc) and terminate any existing `EDM.exe` processes.",
                        "2. Ensure the latest .NET Desktop Runtime (x64) is installed on your computer.",
                        "3. If settings are corrupted, delete `%LOCALAPPDATA%\\EDM\\settings.json` to restore factory defaults.",
                        "4. Launch EDM as standard user."
                    },
                    WhatToCheck = new()
                    {
                        "Check `%LOCALAPPDATA%\\EDM\\logs\\` for crash log files.",
                        "Verify Windows Event Viewer > Application Logs for .NET runtime exceptions."
                    },
                    WhenToContactSupport = "Contact support with your crash log attached if EDM crashes immediately upon opening.",
                    RelatedArticleIds = new() { "database-history", "settings-problems", "troubleshooting-diagnostics" }
                },

                // 23. Database/History Problems
                new SupportArticle
                {
                    Id = "database-history",
                    CategoryId = 23,
                    CategoryName = "23. Database/History Problems",
                    CategoryIcon = "🗄️",
                    Title = "SQLite Database Maintenance, Backup, and History Recovery",
                    Summary = "How EDM safely persists download history using SQLite WAL mode and handles automated deduplication.",
                    Keywords = new() { "database", "sqlite", "history lost", "wal", "backup", "corrupted db" },
                    PossibleCauses = new()
                    {
                        "Sudden power loss while SQLite transaction was active.",
                        "Database locked by external backup software.",
                        "Legacy duplicate records in older database schema."
                    },
                    StepByStepSolution = new()
                    {
                        "1. EDM v6.0.0 uses SQLite Write-Ahead Logging (WAL) and automatic migration safety on startup.",
                        "2. To create an instant backup of your history, open Settings > Database and click 'Create Backup Now'.",
                        "3. To clear your history, click 'Clear History' from the Dashboard menu (this does not delete your files on disk)."
                    },
                    WhatToCheck = new()
                    {
                        "Check that `%LOCALAPPDATA%\\EDM\\edm_history.db` is accessible and not marked read-only.",
                        "Ensure WAL and SHM temporary database files can be created in the folder."
                    },
                    WhenToContactSupport = "Contact support if you receive a 'SQLite database is locked or corrupted' error dialog.",
                    RelatedArticleIds = new() { "duplicate-downloads", "startup-problems", "troubleshooting-diagnostics" }
                },

                // 24. Settings Problems
                new SupportArticle
                {
                    Id = "settings-problems",
                    CategoryId = 24,
                    CategoryName = "24. Settings Problems",
                    CategoryIcon = "⚙️",
                    Title = "Saving, Exporting, and Resetting EDM Preferences",
                    Summary = "Managing application settings, connection limits, and restoring default configuration.",
                    Keywords = new() { "settings", "save preferences", "reset defaults", "config", "export settings" },
                    PossibleCauses = new()
                    {
                        "Settings file is marked read-only by Windows permissions.",
                        "Antivirus sandboxing prevents writing to AppData."
                    },
                    StepByStepSolution = new()
                    {
                        "1. Open Settings (⚙️) from the top-right toolbar.",
                        "2. Make your desired adjustments across General, Connection, File Types, or Proxy.",
                        "3. Click 'Save Settings' in the bottom-right corner of the dialog.",
                        "4. To restore all settings to default, click 'Restore Factory Defaults' at the bottom of the General tab."
                    },
                    WhatToCheck = new()
                    {
                        "Verify settings persist after restarting EDM.",
                        "Check that `%LOCALAPPDATA%\\EDM\\settings.json` is being updated."
                    },
                    WhenToContactSupport = "Contact support if changes in the Settings window revert automatically after clicking Save.",
                    RelatedArticleIds = new() { "download-location", "theme-problems", "language-problems" }
                },

                // 25. Theme Problems
                new SupportArticle
                {
                    Id = "theme-problems",
                    CategoryId = 25,
                    CategoryName = "25. Theme Problems",
                    CategoryIcon = "🎨",
                    Title = "Switching Between Dark and Light Modes & Mica Backdrops",
                    Summary = "Customizing EDM's visual appearance, high-contrast modes, and live theme swapping.",
                    Keywords = new() { "theme", "dark mode", "light mode", "mica", "colors", "contrast" },
                    PossibleCauses = new()
                    {
                        "Windows system theme synchronization overridden by custom user setting.",
                        "High contrast Windows accessibility mode is enforcing system colors."
                    },
                    StepByStepSolution = new()
                    {
                        "1. Click the Theme Toggle button (🌙 / ☀️) in the top-right header for instant live theme switching.",
                        "2. The interface will immediately adapt all background, card, border, and text colors without restarting.",
                        "3. In Settings > Appearance, you can choose 'System Default', 'Dark Theme', or 'Light Theme'."
                    },
                    WhatToCheck = new()
                    {
                        "Verify that all text elements maintain high contrast and legibility.",
                        "Check that your choice is saved in settings across application restarts."
                    },
                    WhenToContactSupport = "Contact support if UI elements render with invisible text or mismatched background colors.",
                    RelatedArticleIds = new() { "settings-problems", "language-problems", "getting-started" }
                },

                // 26. Account/Login Problems
                new SupportArticle
                {
                    Id = "account-login",
                    CategoryId = 26,
                    CategoryName = "26. Account/Login Problems",
                    CategoryIcon = "👤",
                    Title = "Managing User Accounts, Cloud Sync, and Site Credentials",
                    Summary = "Using the Site Logins Manager and DPAPI encrypted vault for premium file host accounts.",
                    Keywords = new() { "account", "login", "password vault", "dpapi", "credentials", "cloud sync" },
                    PossibleCauses = new()
                    {
                        "Website login credentials have expired or two-factor authentication (2FA) is required.",
                        "DPAPI encryption key unavailable on a different user Windows profile."
                    },
                    StepByStepSolution = new()
                    {
                        "1. Click the User Profile pill in the top-right toolbar to open the Account Center.",
                        "2. To manage site logins for premium download services (e.g. Mega, Rapidgator), open Settings > 'Manage Site Logins'.",
                        "3. Add the domain name, your username, and password. All passwords are encrypted with Windows DPAPI hardware keys.",
                        "4. When EDM downloads from that domain, it will automatically authenticate your requests."
                    },
                    WhatToCheck = new()
                    {
                        "Verify your login credentials work by logging into the website directly in your browser.",
                        "Ensure your domain name in the vault matches the download host (e.g. `example.com`)."
                    },
                    WhenToContactSupport = "Contact support if saved credentials fail to pass authentication headers on supported file hosts.",
                    RelatedArticleIds = new() { "premium-membership", "license-problems", "security-policy" }
                },

                // 27. Premium Membership Problems
                new SupportArticle
                {
                    Id = "premium-membership",
                    CategoryId = 27,
                    CategoryName = "27. Premium Membership Problems",
                    CategoryIcon = "⭐",
                    Title = "Accessing Premium Features, Cloud Handoff & Turbo Speed",
                    Summary = "Unlocking 32-connection turbo mode, cloud handoff uploads, and priority customer support.",
                    Keywords = new() { "premium", "tier", "turbo mode", "cloud handoff", "upgrade", "subscription" },
                    PossibleCauses = new()
                    {
                        "License tier has not been activated on the current machine.",
                        "Internet connection required for initial license entitlement sync."
                    },
                    StepByStepSolution = new()
                    {
                        "1. Open User Profile by clicking your user pill in the top-right header.",
                        "2. Check your 'License Tier' status: Free or Pro / Licensed.",
                        "3. Pro users enjoy unlimited parallel connections (up to 32), multi-cloud handoff, and zero bandwidth caps.",
                        "4. Click 'Sync License' to refresh your entitlement status with the control plane."
                    },
                    WhatToCheck = new()
                    {
                        "Verify the green 'Licensed' badge is displayed next to your username.",
                        "Ensure you are running the latest version of EDM."
                    },
                    WhenToContactSupport = "Contact support if your premium subscription is active but EDM displays Free tier.",
                    RelatedArticleIds = new() { "license-problems", "account-login", "about-version" }
                },

                // 28. License Problems
                new SupportArticle
                {
                    Id = "license-problems",
                    CategoryId = 28,
                    CategoryName = "28. License Problems",
                    CategoryIcon = "🔑",
                    Title = "Hardware Fingerprint Licensing & Machine Transfers",
                    Summary = "How hardware-bound license validation works and how to transfer your license to a new PC.",
                    Keywords = new() { "license", "activation key", "hardware id", "transfer pc", "offline activation" },
                    PossibleCauses = new()
                    {
                        "Motherboard, CPU, or primary disk change altered the hardware machine fingerprint.",
                        "Invalid activation key entered in the registration dialog."
                    },
                    StepByStepSolution = new()
                    {
                        "1. Open User Profile > License Information.",
                        "2. View your unique Hardware Machine ID.",
                        "3. Enter your official license product key and click 'Activate License'.",
                        "4. To transfer a license to a new computer, deactivate on the old machine first or contact support for a reset."
                    },
                    WhatToCheck = new()
                    {
                        "Check that you entered the key correctly without trailing spaces.",
                        "Ensure your system clock is set accurately."
                    },
                    WhenToContactSupport = "Contact support if you need to reset your machine activation after a major hardware upgrade.",
                    RelatedArticleIds = new() { "premium-membership", "account-login", "about-version" }
                },

                // 29. Update Problems
                new SupportArticle
                {
                    Id = "update-problems",
                    CategoryId = 29,
                    CategoryName = "29. Update Problems",
                    CategoryIcon = "🔄",
                    Title = "Checking for Updates, Installer Signatures & Offline Patches",
                    Summary = "How EDM verifies update manifests, downloads delta patches, and handles offline updates.",
                    Keywords = new() { "update", "check for updates", "patch", "installer signature", "new version" },
                    PossibleCauses = new()
                    {
                        "Update server is temporarily unreachable or blocked by firewall.",
                        "Downloaded update installer failed SHA-256 signature verification.",
                        "EDM is installed in a restricted directory requiring administrative elevation."
                    },
                    StepByStepSolution = new()
                    {
                        "1. Click 'Check for Updates' in the About EDM window or top-right menu.",
                        "2. EDM will query the update manifest: if a newer version exists, the changelog will be displayed.",
                        "3. Click 'Download & Install' to perform an in-place seamless upgrade.",
                        "4. If automatic updates are blocked, download the latest standalone setup installer from the official website."
                    },
                    WhatToCheck = new()
                    {
                        "Check your current version in the About window (e.g. v6.0.0).",
                        "Verify your firewall allows HTTPS connections to the update domain."
                    },
                    WhenToContactSupport = "Contact support if update checks return an unexpected signature verification failure.",
                    RelatedArticleIds = new() { "about-version", "firewall-antivirus", "startup-problems" }
                },

                // 30. Privacy & Security
                new SupportArticle
                {
                    Id = "privacy-security",
                    CategoryId = 30,
                    CategoryName = "30. Privacy & Security",
                    CategoryIcon = "🔒",
                    Title = "Zero-Telemetry Architecture & Local-First Data Privacy",
                    Summary = "Understanding how EDM protects your privacy: no tracking, local SQLite storage, and hardware encryption.",
                    Keywords = new() { "privacy", "security", "telemetry", "tracking", "local storage", "gdpr" },
                    PossibleCauses = new()
                    {
                        "Questions regarding what data EDM stores and transmits over the internet.",
                        "Need to completely purge download history and credentials."
                    },
                    StepByStepSolution = new()
                    {
                        "1. EDM is built on a strict Local-First privacy architecture: download URLs, filenames, and history NEVER leave your PC.",
                        "2. Passwords and site tokens are encrypted with Windows DPAPI hardware keys.",
                        "3. To review the full legal documentation, open the 'Privacy & Policy' center from the sidebar.",
                        "4. To completely purge all stored history, click 'Clear History' in Settings."
                    },
                    WhatToCheck = new()
                    {
                        "Inspect `%LOCALAPPDATA%\\EDM\\` to verify all data is stored locally on your device.",
                        "Check the Privacy Policy tab for detailed GDPR and CCPA disclosures."
                    },
                    WhenToContactSupport = "Contact support for any data privacy inquiries or security disclosures.",
                    RelatedArticleIds = new() { "privacy-policy-doc", "settings-problems", "account-login" }
                },

                // 31. Troubleshooting
                new SupportArticle
                {
                    Id = "troubleshooting-diagnostics",
                    CategoryId = 31,
                    CategoryName = "31. Troubleshooting",
                    CategoryIcon = "🛠️",
                    Title = "Exporting Diagnostic Reports, Logs, and System Metrics",
                    Summary = "How to gather verbose diagnostics and logs to resolve complex technical issues.",
                    Keywords = new() { "logs", "diagnostics", "system info", "report", "debug", "troubleshoot" },
                    PossibleCauses = new()
                    {
                        "Unexpected error or crash requiring deep technical log inspection.",
                        "Preparing a technical support ticket with hardware details."
                    },
                    StepByStepSolution = new()
                    {
                        "1. Open the 'About EDM' window and select the 'System Information' tab.",
                        "2. Review your runtime environment, memory usage, processor count, and OS build.",
                        "3. Click 'Export Diagnostic Report' to save a complete, anonymized diagnostic text file.",
                        "4. Open `%LOCALAPPDATA%\\EDM\\logs\\` to access detailed Serilog execution logs."
                    },
                    WhatToCheck = new()
                    {
                        "Check the latest log file for error timestamps matching your issue.",
                        "Verify no sensitive personal credentials are included before sharing logs."
                    },
                    WhenToContactSupport = "Attach the exported diagnostic report when opening a support ticket.",
                    RelatedArticleIds = new() { "contact-support", "about-version", "startup-problems" }
                },

                // 32. Contact Support
                new SupportArticle
                {
                    Id = "contact-support",
                    CategoryId = 32,
                    CategoryName = "32. Contact Support",
                    CategoryIcon = "✉️",
                    Title = "Contacting EDM Technical Support & Community Channels",
                    Summary = "Direct support email, issue reporting guidelines, and response turnaround times.",
                    Keywords = new() { "contact", "support email", "helpdesk", "report bug", "ticket", "customer service" },
                    PossibleCauses = new()
                    {
                        "Encountering an unresolvable technical bug or feature request.",
                        "License activation or billing inquiry."
                    },
                    StepByStepSolution = new()
                    {
                        "1. Click the 'Contact Technical Support' button below or email: `support@exclusive-download-manager.com`.",
                        "2. Include your EDM Version (e.g. v6.0.0), Windows OS Version, and a description of the issue.",
                        "3. Attach your exported Diagnostic Report for accelerated tier-2 engineering triage.",
                        "4. Expected response time is within 24 hours on business days."
                    },
                    WhatToCheck = new()
                    {
                        "Make sure you searched this Support Center first, as 95% of common issues have instant step-by-step guides.",
                        "Verify your EDM version is up to date before reporting bugs."
                    },
                    WhenToContactSupport = "Whenever you need personal technical assistance from our engineering team.",
                    RelatedArticleIds = new() { "troubleshooting-diagnostics", "about-version", "getting-started" }
                },

                // Stage 9 Engine Deep-Dive Articles
                new SupportArticle
                {
                    Id = "multithread-turbo-engine",
                    CategoryId = 5,
                    CategoryName = "5. Download Speed Problems",
                    CategoryIcon = "⚡",
                    Title = "IDM-Style Dynamic Multi-Thread Parallel Execution Engine",
                    Summary = "How EDM achieves maximum bandwidth saturation by dynamically splitting files into parallel concurrent worker streams.",
                    Keywords = new() { "turbo", "multithread", "parallel", "segments", "chunks", "speed", "concurrency" },
                    PossibleCauses = new()
                    {
                        "Understanding how EDM calculates thread counts based on file size.",
                        "Verifying that all threads are actively downloading simultaneously rather than waiting sequentially."
                    },
                    StepByStepSolution = new()
                    {
                        "1. EDM automatically calculates optimal thread counts: <5MB = 2 threads, 5-25MB = 4 threads, 25-100MB = 6 threads, 100-500MB = 8 threads, >500MB = 12 threads.",
                        "2. All active threads start concurrently across distinct file byte ranges (e.g. 0-25%, 25-50%, 50-75%, 75-100%).",
                        "3. Every thread displays its live downloaded bytes, independent transfer speed (e.g. 2.4 MB/s), and vibrant progress bar.",
                        "4. In the event of a connection stall on one thread, remaining threads continue at full speed while the stalled thread reconnects automatically."
                    },
                    WhatToCheck = new()
                    {
                        "Observe the Thread progress table in the Turbo Downloader window: all threads should show 'Transferring' in cyan.",
                        "Confirm the combined speed in the title bar equals the sum of individual thread throughputs."
                    },
                    WhenToContactSupport = "Contact support if multi-threaded downloads fail on HTTP 206 Range-supported servers.",
                    RelatedArticleIds = new() { "download-speed", "starting-download", "live-throughput-graph-telemetry" }
                },
                new SupportArticle
                {
                    Id = "video-fast-analysis-sizing",
                    CategoryId = 12,
                    CategoryName = "12. Video Downloads",
                    CategoryIcon = "🎬",
                    Title = "Instant Media Stream Inspection & Millisecond Size Detection",
                    Summary = "How EDM inspects YouTube, HLS (.m3u8), and DASH (.mpd) manifests to detect video qualities, codecs, and precise file sizes in milliseconds.",
                    Keywords = new() { "youtube", "media analysis", "unknown size", "video resolution", "hls", "dash", "stream" },
                    PossibleCauses = new()
                    {
                        "Resolving 'Unknown Size' displays during remote media analysis.",
                        "Selecting preferred video containers (MP4, MKV, WebM) and audio bitrates."
                    },
                    StepByStepSolution = new()
                    {
                        "1. When you paste any media link (YouTube, Vimeo, Twitch, HLS, DASH), EDM triggers millisecond stream probing.",
                        "2. The analyzer extracts audio/video representations, calculate total content lengths, and formats available resolutions (4K, 1440p, 1080p, 720p, Audio MP3).",
                        "3. Exact file sizes (e.g. 136.56 MB) are populated immediately in the Add URL and Progress windows.",
                        "4. If a site uses dynamic chunked streams without fixed Content-Length, EDM applies dynamic live stream estimation."
                    },
                    WhatToCheck = new()
                    {
                        "Check the 'Quality' and 'Format' dropdowns in the Add Download dialog to pick your desired stream variant.",
                        "Look at the Downloaded text to verify instant size resolution."
                    },
                    WhenToContactSupport = "Contact support if a video URL fails to extract available qualities or format options.",
                    RelatedArticleIds = new() { "adding-download", "multithread-turbo-engine", "troubleshooting-diagnostics" }
                },
                new SupportArticle
                {
                    Id = "live-throughput-graph-telemetry",
                    CategoryId = 31,
                    CategoryName = "31. Troubleshooting",
                    CategoryIcon = "🛠️",
                    Title = "Understanding the Live Dynamic Throughput Graph (30-60 FPS Wave)",
                    Summary = "How the 30-60 FPS real-time wave graph monitors throughput oscillations, rolling averages, and peak connection speeds.",
                    Keywords = new() { "graph", "throughput", "wave", "fps", "live telemetry", "peak speed", "average speed" },
                    PossibleCauses = new()
                    {
                        "Understanding the visual indicators inside the Live Throughput Monitor.",
                        "Verifying graph render performance on high refresh-rate monitors."
                    },
                    StepByStepSolution = new()
                    {
                        "1. The glowing violet-indigo area wave represents instantaneous download throughput sampled at 30-60 FPS.",
                        "2. The solid blue stroke line tracks high-resolution network bandwidth fluctuations in real-time.",
                        "3. The dashed amber horizontal line represents your rolling average transfer speed across the download session.",
                        "4. The top-right 'Peak' badge records the highest single-second transfer rate achieved by your network connection."
                    },
                    WhatToCheck = new()
                    {
                        "The wave curve dynamically undulates with real byte transfers, eliminating flat/frozen graph lines.",
                        "When downloading is paused, the wave smoothly decays to 0 KB/s via spring physics interpolation."
                    },
                    WhenToContactSupport = "Contact support if the graph causes unexpected GPU/CPU lag on low-power hardware.",
                    RelatedArticleIds = new() { "download-speed", "multithread-turbo-engine", "troubleshooting-diagnostics" }
                }
            };

            _articles.AddRange(rawArticles);

            // Populate categories
            var categoryMap = new Dictionary<int, (string Name, string Icon, string Description)>
            {
                [1] = ("1. Getting Started", "🚀", "Installation, quickstart, and initial setup guide."),
                [2] = ("2. Adding a Download", "➕", "Adding direct URLs, clipboard sniffing, and batch links."),
                [3] = ("3. Starting a Download", "▶️", "Initiating transfers, connection probing, and multi-part handshakes."),
                [4] = ("4. Pause and Resume", "⏯️", "Pausing, resuming, and partial segment recovery."),
                [5] = ("5. Download Speed Problems", "⚡", "Resolving slow speeds, ISP throttling, and buffer tuning."),
                [6] = ("6. Download Stuck at 0%", "🛑", "Troubleshooting transfers hanging at 0% or 'Connecting...'"),
                [7] = ("7. Download Failed", "❌", "Handling HTTP 403, 404, 503, and socket drops."),
                [8] = ("8. File Not Found After Download", "🔍", "Locating completed downloads and smart category paths."),
                [9] = ("9. Incorrect File Size", "📏", "Chunked transfer encoding and unknown file sizes."),
                [10] = ("10. Duplicate Downloads", "📋", "Eliminating duplicate download rows and URL canonicalization."),
                [11] = ("11. Browser Integration", "🌐", "Setting up Chrome, Edge, and Firefox extensions."),
                [12] = ("12. Video Downloads", "🎬", "Downloading YouTube, DASH, and HLS video streams."),
                [13] = ("13. Audio Downloads", "🎵", "Extracting audio and converting to MP3 / FLAC."),
                [14] = ("14. Download Location", "📂", "Custom directories, NAS network shares, and category rules."),
                [15] = ("15. Disk Space Problems", "💾", "Low disk space, temp chunk placement, and preallocation."),
                [16] = ("16. Network Problems", "📶", "DNS failures, timeouts, and adapter reconnection."),
                [17] = ("17. Proxy Problems", "🛡️", "HTTP, HTTPS, SOCKS5 proxies, and PAC configuration."),
                [18] = ("18. Firewall/Antivirus Issues", "🛡️", "Windows Defender false positives and whitelist rules."),
                [19] = ("19. Permission Problems", "🔒", "Fixing 'Access Denied' and Windows UAC issues."),
                [20] = ("20. Scheduler Problems", "⏰", "Automated download timers and PC power actions."),
                [21] = ("21. Queue Problems", "🔢", "Managing queue limits, priorities, and batch order."),
                [22] = ("22. Application Startup Problems", "🚀", "Resolving startup crashes and .NET runtime requirements."),
                [23] = ("23. Database/History Problems", "🗄️", "SQLite WAL maintenance, backups, and recovery."),
                [24] = ("24. Settings Problems", "⚙️", "Saving preferences and restoring default config."),
                [25] = ("25. Theme Problems", "🎨", "Dark / Light theme switching and contrast mode."),
                [26] = ("26. Account/Login Problems", "👤", "User profile and DPAPI site password vault."),
                [27] = ("27. Premium Membership Problems", "⭐", "Turbo connections, cloud handoff, and tier features."),
                [28] = ("28. License Problems", "🔑", "Hardware ID verification and machine transfers."),
                [29] = ("29. Update Problems", "🔄", "Checking updates and installer signature validation."),
                [30] = ("30. Privacy & Security", "🔒", "Zero-telemetry policy and local data protection."),
                [31] = ("31. Troubleshooting", "🛠️", "Diagnostic reports, logs, and system metrics."),
                [32] = ("32. Contact Support", "✉️", "Technical helpdesk, email, and issue escalation.")
            };

            foreach (var kvp in categoryMap)
            {
                int count = _articles.Count(a => a.CategoryId == kvp.Key);
                _categories.Add(new SupportCategory
                {
                    Id = kvp.Key,
                    Name = kvp.Value.Name,
                    Icon = kvp.Value.Icon,
                    Description = kvp.Value.Description,
                    ArticleCount = count
                });
            }
        }
    }
}
