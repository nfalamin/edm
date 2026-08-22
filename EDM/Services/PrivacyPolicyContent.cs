using System;
using System.Collections.Generic;
using System.Linq;

namespace EDM.Services
{
    public class PolicySection
    {
        public string Id { get; set; } = string.Empty;
        public int Number { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Icon { get; set; } = "📜";
        public string Content { get; set; } = string.Empty;
        public List<string> KeyPoints { get; set; } = new();
    }

    /// <summary>
    /// Accurate, Comprehensive Privacy Policy and Legal Agreements for EDM.
    /// Reflects true local-first architecture and real project capabilities.
    /// </summary>
    public class PrivacyPolicyContent
    {
        private static readonly Lazy<PrivacyPolicyContent> _instance = new(() => new PrivacyPolicyContent());
        public static PrivacyPolicyContent Instance => _instance.Value;

        public string PolicyVersion => "2.7";
        public string LastUpdatedDate => "August 16, 2026";
        public string CompanyName => "Exclusive Download Manager (EDM) Open Ecosystem";

        private readonly List<PolicySection> _sections = new();

        public PrivacyPolicyContent()
        {
            InitializeSections();
        }

        public IReadOnlyList<PolicySection> GetSections() => _sections;

        public PolicySection? GetSectionById(string id)
        {
            return _sections.FirstOrDefault(s => string.Equals(s.Id, id, StringComparison.OrdinalIgnoreCase));
        }

        public List<PolicySection> Search(string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return _sections.ToList();

            var terms = query.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);

            return _sections
                .Where(s =>
                {
                    string title = s.Title.ToLowerInvariant();
                    string content = s.Content.ToLowerInvariant();
                    return terms.All(t => title.Contains(t) || content.Contains(t) || s.KeyPoints.Any(k => k.ToLowerInvariant().Contains(t)));
                })
                .ToList();
        }

        private void InitializeSections()
        {
            _sections.AddRange(new List<PolicySection>
            {
                new PolicySection
                {
                    Id = "overview",
                    Number = 1,
                    Title = "1. Privacy Policy Overview",
                    Icon = "🛡️",
                    Content = "Exclusive Download Manager (EDM) is engineered with a strict 'Local-First' privacy principle. We believe that what you download, where you download it from, and how you manage your files is private personal data that belongs exclusively to you. EDM does not track, monetize, sell, or profile your download activity.",
                    KeyPoints = new()
                    {
                        "Local-first design philosophy.",
                        "Zero tracking, advertising, or data monetization.",
                        "Direct connection between your machine and your download host."
                    }
                },
                new PolicySection
                {
                    Id = "data-collection",
                    Number = 2,
                    Title = "2. Data Collection (Local-First Design)",
                    Icon = "📥",
                    Content = "EDM does not collect personal data from your system. The application functions completely offline or in direct peer connection with the remote URLs you provide. There are no silent background telemetry daemons transmitting your browsing or download logs to any remote server.",
                    KeyPoints = new()
                    {
                        "No automatic data collection or transmission.",
                        "No background analytics pinging.",
                        "All network requests originate strictly from user-initiated download actions."
                    }
                },
                new PolicySection
                {
                    Id = "data-usage",
                    Number = 3,
                    Title = "3. Data Usage",
                    Icon = "⚙️",
                    Content = "Any information provided by you (such as download URLs, custom file destination paths, proxy credentials, and bandwidth limits) is used strictly and exclusively by the local application to execute your requested file transfer tasks.",
                    KeyPoints = new()
                    {
                        "Data is used solely to execute requested download transfers.",
                        "No secondary data processing or behavioral profiling."
                    }
                },
                new PolicySection
                {
                    Id = "account-information",
                    Number = 4,
                    Title = "4. Account Information & DPAPI",
                    Icon = "👤",
                    Content = "If you configure premium website credentials in the 'Site Logins Manager', these passwords and tokens are encrypted locally using Windows Data Protection API (DPAPI) hardware-bound keys. Your decrypted credentials never leave memory during HTTP authentication handshakes.",
                    KeyPoints = new()
                    {
                        "Hardware-bound Windows DPAPI credential encryption.",
                        "Site logins and passwords are never transmitted to EDM servers.",
                        "Protected against memory scraping and local file theft."
                    }
                },
                new PolicySection
                {
                    Id = "download-information",
                    Number = 5,
                    Title = "5. Download Information & URLs",
                    Icon = "🔗",
                    Content = "Download URLs, file hashes, Content-Length headers, and HTTP cookies provided during download interception are stored only on your local hard drive. EDM transmits these URLs only to the specific host server hosting the file.",
                    KeyPoints = new()
                    {
                        "URLs are only sent to the requested target host.",
                        "Session cookies are utilized solely for server authentication.",
                        "No central registry of downloaded URLs exists."
                    }
                },
                new PolicySection
                {
                    Id = "download-history",
                    Number = 6,
                    Title = "6. Download History & SQLite Storage",
                    Icon = "🗄️",
                    Content = "Your download history is saved in a local SQLite database under `%LOCALAPPDATA%\\EDM\\edm_history.db`. This database uses Write-Ahead Logging (WAL) for high performance and data integrity. You can export, backup, or wipe this database at any time.",
                    KeyPoints = new()
                    {
                        "Stored in user local app data (%LOCALAPPDATA%\\EDM).",
                        "High reliability SQLite WAL storage engine.",
                        "User maintains 100% control over database lifecycle."
                    }
                },
                new PolicySection
                {
                    Id = "local-storage",
                    Number = 7,
                    Title = "7. Local Storage & Temporary Files",
                    Icon = "💾",
                    Content = "During multi-part downloads, EDM creates temporary partial segment files (.edm_part) in your configured temp folder. Once a download reaches 100%, segments are atomically merged into the final file, and temporary parts are immediately deleted.",
                    KeyPoints = new()
                    {
                        "Temporary segment files (.edm_part) are cleaned automatically.",
                        "Per-disk temp storage allocator minimizes SSD wear.",
                        "No leftover chunk artifacts after download completion."
                    }
                },
                new PolicySection
                {
                    Id = "diagnostics-logging",
                    Number = 8,
                    Title = "8. Diagnostics & Logging (Serilog)",
                    Icon = "📝",
                    Content = "EDM maintains local rolling diagnostic logs in `%LOCALAPPDATA%\\EDM\\logs\\` using Serilog. These logs record socket errors, HTTP status codes, and queue transitions to help you troubleshoot issues. Logs never contain decrypted passwords.",
                    KeyPoints = new()
                    {
                        "Local rolling log files only.",
                        "Sensitive credentials automatically redacted.",
                        "Logs are never transmitted without explicit user export."
                    }
                },
                new PolicySection
                {
                    Id = "crash-reports",
                    Number = 9,
                    Title = "9. Crash Reports",
                    Icon = "💥",
                    Content = "If an unhandled exception occurs, EDM generates a local crash dump in `%LOCALAPPDATA%\\EDM\\CrashReports\\`. In Settings, 'Send anonymous crash reports' is disabled by default. If enabled, only stack traces and OS build numbers are packaged.",
                    KeyPoints = new()
                    {
                        "Crash reporting is strictly OPT-IN.",
                        "Disabled by default on all installations.",
                        "Dumps can be reviewed locally by the user prior to sharing."
                    }
                },
                new PolicySection
                {
                    Id = "analytics",
                    Number = 10,
                    Title = "10. Analytics & Telemetry",
                    Icon = "📊",
                    Content = "EDM contains ZERO third-party advertising SDKs, marketing pixels, Google Analytics, or user engagement trackers. We do not monitor how many files you download, what types of files you store, or when you use the software.",
                    KeyPoints = new()
                    {
                        "Zero third-party trackers or SDKs.",
                        "No user behavior profiling or telemetry pings.",
                        "Completely clean desktop binary."
                    }
                },
                new PolicySection
                {
                    Id = "cookies-web",
                    Number = 11,
                    Title = "11. Cookies & Web Technologies",
                    Icon = "🍪",
                    Content = "When the browser extension intercepts a download, it forwards the necessary session cookies to EDM so authenticated downloads do not fail with HTTP 403. These cookies are stored ephemerally in RAM and cleared when the transfer finishes.",
                    KeyPoints = new()
                    {
                        "Session cookies used solely for transfer authentication.",
                        "Kept in volatile memory and purged upon completion.",
                        "Never written to persistent plain text logs."
                    }
                },
                new PolicySection
                {
                    Id = "browser-integration",
                    Number = 12,
                    Title = "12. Browser Integration & Native Messaging",
                    Icon = "🌐",
                    Content = "The EDM companion browser extension communicates with EDM via the official Chrome/Edge/Firefox Native Messaging Host standard over standard stdin/stdout JSON RPC IPC. It does not open public network ports or expose local listening servers.",
                    KeyPoints = new()
                    {
                        "Uses official browser Native Messaging Host protocol.",
                        "Zero open public listening ports.",
                        "Secure localhost-only inter-process communication."
                    }
                },
                new PolicySection
                {
                    Id = "third-party-services",
                    Number = 13,
                    Title = "13. Third-Party Services (yt-dlp, FFmpeg, Safe Browsing)",
                    Icon = "🧩",
                    Content = "EDM bundles or interfaces with open-source tools: yt-dlp (video stream extraction), FFmpeg (audio/video demuxing), and optionally Google Safe Browsing API v4. If you provide a Google Safe Browsing API key, URL hashes are checked directly against Google's threat database.",
                    KeyPoints = new()
                    {
                        "Open-source tooling runs locally on your PC.",
                        "Google Safe Browsing is optional and user-configured.",
                        "No intermediate proxy servers are used."
                    }
                },
                new PolicySection
                {
                    Id = "licensing-payments",
                    Number = 14,
                    Title = "14. Licensing & Hardware Verification",
                    Icon = "🔑",
                    Content = "To validate premium license entitlements and prevent unauthorized software redistribution, EDM creates a one-way cryptographic hash of your system hardware fingerprint (Motherboard UUID + CPU ID). No personal hardware names or serial numbers leave the machine unhashed.",
                    KeyPoints = new()
                    {
                        "One-way SHA-256 cryptographic hardware fingerprinting.",
                        "Used solely for license seat validation.",
                        "No personally identifiable hardware information transmitted."
                    }
                },
                new PolicySection
                {
                    Id = "security",
                    Number = 15,
                    Title = "15. Security Hardening",
                    Icon = "🛡️",
                    Content = "EDM binaries are compiled with .NET 10 Control Flow Guard, Authenticode digital signature checking, TLS 1.3 socket negotiation, and memory-safe buffer management with automated array recycling.",
                    KeyPoints = new()
                    {
                        "TLS 1.3 encrypted network streams.",
                        "Control Flow Guard and memory safety.",
                        "Digitally signed assemblies."
                    }
                },
                new PolicySection
                {
                    Id = "data-retention",
                    Number = 16,
                    Title = "16. Data Retention",
                    Icon = "⏳",
                    Content = "All data stored by EDM remains on your device for as long as you choose to keep it. EDM does not impose mandatory data expiration or retain remote copies of your files or history.",
                    KeyPoints = new()
                    {
                        "Indefinite local retention under user control.",
                        "No remote server storage or expiration."
                    }
                },
                new PolicySection
                {
                    Id = "data-deletion",
                    Number = 17,
                    Title = "17. Data Deletion & One-Click Purge",
                    Icon = "🗑️",
                    Content = "You have the right and capability to completely erase all data stored by EDM. Clicking 'Clear History' wipes the SQLite database. Uninstalling EDM and deleting `%LOCALAPPDATA%\\EDM\\` leaves zero remnant data on your computer.",
                    KeyPoints = new()
                    {
                        "One-click complete database purge.",
                        "Clean uninstallation with zero residual artifacts.",
                        "Total user data autonomy."
                    }
                },
                new PolicySection
                {
                    Id = "user-rights",
                    Number = 18,
                    Title = "18. User Rights (GDPR & CCPA Alignment)",
                    Icon = "⚖️",
                    Content = "In accordance with GDPR, CCPA, and global privacy standards, you maintain the right of access, rectification, portability, and erasure over all your data. Because EDM operates locally, you can exercise these rights directly within the software at any time without submitting a request.",
                    KeyPoints = new()
                    {
                        "Full GDPR Article 17 (Right to Erasure) compliance.",
                        "Full CCPA 'Do Not Sell My Data' compliance.",
                        "Direct local execution of all privacy rights."
                    }
                },
                new PolicySection
                {
                    Id = "children-privacy",
                    Number = 19,
                    Title = "19. Children's Privacy",
                    Icon = "👶",
                    Content = "EDM does not knowingly collect or solicit personal information from children under the age of 13. Because the application does not collect personal data from any user, it is compliant with COPPA regulations.",
                    KeyPoints = new()
                    {
                        "Compliant with COPPA.",
                        "No personal information collected from any user age group."
                    }
                },
                new PolicySection
                {
                    Id = "international-transfers",
                    Number = 20,
                    Title = "20. International Data Transfer",
                    Icon = "✈️",
                    Content = "EDM does not operate central international telemetry repositories. When downloading files, your computer communicates directly with the hosting server located in whatever country that host maintains its servers.",
                    KeyPoints = new()
                    {
                        "Direct peer-to-host international routing.",
                        "No intermediary data transit servers."
                    }
                },
                new PolicySection
                {
                    Id = "policy-changes",
                    Number = 21,
                    Title = "21. Policy Changes & Updates",
                    Icon = "📅",
                    Content = "We may update this Privacy Policy periodically to reflect new features or regulatory requirements. Any updates will be included directly in the application release notes and published within this Privacy & Policy Center with an updated version number and date.",
                    KeyPoints = new()
                    {
                        "Transparent policy versioning.",
                        "Updated version and date maintainable in software."
                    }
                },
                new PolicySection
                {
                    Id = "contact-info",
                    Number = 22,
                    Title = "22. Contact Information",
                    Icon = "✉️",
                    Content = "For any questions or concerns regarding our Privacy Policy or data security practices, please contact our Data Protection Officer at: `privacy@exclusive-download-manager.com`.",
                    KeyPoints = new()
                    {
                        "Dedicated privacy contact: privacy@exclusive-download-manager.com",
                        "Fast engineering response turnaround."
                    }
                },
                new PolicySection
                {
                    Id = "terms-of-service",
                    Number = 23,
                    Title = "23. Terms of Service",
                    Icon = "📜",
                    Content = "By downloading, installing, or using Exclusive Download Manager (EDM), you agree to be bound by these Terms of Service. If you do not agree to these terms, do not install or use the application.",
                    KeyPoints = new()
                    {
                        "Legally binding software terms of use.",
                        "Applies to all versions and distribution channels."
                    }
                },
                new PolicySection
                {
                    Id = "acceptable-use",
                    Number = 24,
                    Title = "24. Acceptable Use Policy",
                    Icon = "✅",
                    Content = "EDM is designed for lawful download acceleration and media management. You agree not to use EDM to download content in violation of third-party intellectual property rights, local copyright laws, or terms of service of the target hosts.",
                    KeyPoints = new()
                    {
                        "Users are responsible for verifying download permissions.",
                        "No illegal distribution or infringement facilitation.",
                        "Respect bandwidth rules of target content providers."
                    }
                },
                new PolicySection
                {
                    Id = "disclaimer-warranty",
                    Number = 25,
                    Title = "25. Disclaimer & Limitation of Warranty",
                    Icon = "⚠️",
                    Content = "EDM is provided on an 'AS IS' and 'AS AVAILABLE' basis without warranties of any kind, whether express or implied. In no event shall the authors or copyright holders be liable for any direct, indirect, incidental, or consequential damages arising from the use of the software.",
                    KeyPoints = new()
                    {
                        "Software provided 'AS IS' without implied warranty.",
                        "Standard limitation of liability."
                    }
                },
                new PolicySection
                {
                    Id = "software-license",
                    Number = 26,
                    Title = "26. Software License Agreement (EULA)",
                    Icon = "📄",
                    Content = "The author grants you a revocable, non-exclusive, non-transferable license to install and use EDM on your personal or commercial Windows devices in accordance with your registered license tier.",
                    KeyPoints = new()
                    {
                        "Non-exclusive personal and commercial license.",
                        "Respects registered license seat limits."
                    }
                },
                new PolicySection
                {
                    Id = "open-source-notices",
                    Number = 27,
                    Title = "27. Open Source Notices & Attributions",
                    Icon = "💖",
                    Content = "EDM proudly utilizes high-quality open source libraries: Microsoft.Data.Sqlite (MIT), YoutubeExplode (LGPLv3), AngleSharp (MIT), ModernWpf (MIT), Serilog (Apache 2.0), and SQLitePCLRaw (Apache 2.0). All third-party copyrights belong to their respective owners.",
                    KeyPoints = new()
                    {
                        "Attribution for all included open source libraries.",
                        "Compliance with MIT, Apache 2.0, and LGPL licensing.",
                        "Gratitude to the global open source community."
                    }
                },
                new PolicySection
                {
                    Id = "local-telemetry-privacy",
                    Number = 28,
                    Title = "28. Real-Time Telemetry & Throughput Graph (Zero-Cloud Guarantee)",
                    Icon = "📊",
                    Content = "All real-time telemetry metrics—including the 30-60 FPS Live Throughput Wave Graph, instantaneous transfer speeds, rolling averages, peak bitrate trackers, and active per-thread chunk progress bars—are calculated strictly in-memory on your local CPU and GPU. EDM never transmits your bandwidth metrics, download history, or file streaming logs to any remote server or third-party analytics provider.",
                    KeyPoints = new()
                    {
                        "Live 30-60 FPS Throughput wave graph is rendered 100% locally via Windows WPF Dispatcher.",
                        "Zero cloud data logging or bandwidth telemetry harvesting.",
                        "Direct, encrypted peer connection to your requested download host."
                    }
                },
                new PolicySection
                {
                    Id = "multithread-data-integrity",
                    Number = 29,
                    Title = "29. Multi-Thread Parallel Stream Security & Data Integrity",
                    Icon = "⚡",
                    Content = "When downloading via multi-part HTTP 206 range requests or media streaming engines, EDM partitions the target file into dynamic parallel worker segments. Each segment is isolated in temporary encrypted local storage (.part) and reassembled using atomic zero-copy block transfers upon completion. EDM performs local MD5/SHA-256 cryptographic verification to guarantee file authenticity and prevent packet corruption without compromising user privacy.",
                    KeyPoints = new()
                    {
                        "Multi-thread range streams are processed and assembled entirely on local disk.",
                        "Automatic end-to-end cryptographic checksum verification (MD5/SHA-256).",
                        "Atomic cleanup of temporary segment parts upon successful download completion."
                    }
                }
            });
        }
    }
}
