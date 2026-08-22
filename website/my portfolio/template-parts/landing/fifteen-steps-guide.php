<?php
/**
 * Landing Page: 15-Step Comprehensive User Search & Knowledge Playbook
 * High-intent SEO guide answering the top 15 queries and workflows users search for.
 *
 * @package EDM_Theme
 */

if (!defined('ABSPATH')) {
    exit;
}

$download_url = function_exists('edm_get_download_url') ? edm_get_download_url() : '#';
$version = function_exists('edm_get_latest_version') ? edm_get_latest_version() : '2.1.0';

$steps = [
    [
        'id'       => '01',
        'category' => 'speed',
        'badge'    => 'CORE ACCELERATION',
        'title'    => 'How to Maximize 32 Concurrent Turbo Sockets for 100% Full ISP Bandwidth',
        'desc'     => 'EDM splits any single file into 32 simultaneous HTTP byte-range chunks. Open EDM Options &rarr; Connection &rarr; Set Max Connection Sockets to 32 &rarr; Select "High Speed Fiber/Broadband". This forces your ISP pipeline to deliver up to 32x faster downloads by bypassing single-stream server throttles.',
        'tag'      => '32 Sockets · Zero Throttling'
    ],
    [
        'id'       => '02',
        'category' => 'video',
        'badge'    => 'VIDEO RIPPER',
        'title'    => 'How to Auto-Grab 4K/8K 60FPS Video & 320kbps Audio Streams from Any Website',
        'desc'     => 'When watching videos on YouTube, Vimeo, Facebook, or Dailymotion, the EDM floating video capture bar pops up above the player. Click "Download This Video", choose your preferred resolution (4K UHD 2160p, 1440p, 1080p, or Audio Only MP3), and EDM automatically muxes video and audio streams seamlessly.',
        'tag'      => '4K/8K UHD · Audio Mux'
    ],
    [
        'id'       => '03',
        'category' => 'browser',
        'badge'    => 'MANIFEST V3',
        'title'    => 'How to Install & Enable Browser Extension on Chrome, Edge, Brave & Opera',
        'desc'     => 'Download the Manifest V3 extension archive (.zip) from EDM. In Chrome/Edge, navigate to chrome://extensions &rarr; Enable "Developer Mode" &rarr; Drag and drop the extension folder. The extension immediately syncs with EDM via Windows Native Messaging for zero-click automatic download takeover.',
        'tag'      => 'Chrome · Edge · Firefox'
    ],
    [
        'id'       => '04',
        'category' => 'resume',
        'badge'    => 'SMART RECOVERY',
        'title'    => 'How to Bypass Cloudflare & Google Drive Quota Limits with Dynamic Resume',
        'desc'     => 'When Google Drive or Cloudflare links expire mid-download, simply right-click the file in EDM &rarr; select "Refresh Download Address". EDM opens the original page, grabs the refreshed security token, and resumes your download exactly where it stopped without downloading from scratch.',
        'tag'      => 'No Progress Loss · Token Refresh'
    ],
    [
        'id'       => '05',
        'category' => 'resume',
        'badge'    => 'CRASH-PROOF',
        'title'    => 'How to Resume Broken or Expired Downloads with SQLite Transaction Journal',
        'desc'     => 'Unlike basic browser downloads that corrupt when your Wi-Fi disconnects or power cuts, EDM writes every byte to a persistent SQLite recovery journal. When your internet reconnects, EDM verifies chunk integrity and resumes downloading immediately.',
        'tag'      => 'Power-Cut Safe · SHA-256 Verified'
    ],
    [
        'id'       => '06',
        'category' => 'power',
        'badge'    => 'BATCH DOWNLOAD',
        'title'    => 'How to Download Bulk Files & Full Playlists in One Click (Batch Queue)',
        'desc'     => 'Navigate to File &rarr; "Add Batch Download" or right-click any webpage and choose "Download all links with EDM". You can filter by extension (.mp4, .zip, .pdf, .iso), select all files, and set queue priority with sequential or parallel downloading.',
        'tag'      => 'Playlist Grabber · Bulk Filter'
    ],
    [
        'id'       => '07',
        'category' => 'power',
        'badge'    => 'ORGANIZATION',
        'title'    => 'How to Auto-Categorize Downloaded Files into Clean Folders',
        'desc'     => 'EDM automatically sorts incoming files by format into dedicated subfolders: Compressed (.zip, .rar, .7z), Video (.mp4, .mkv), Audio (.mp3, .flac), Documents (.pdf, .docx), Programs (.exe, .msi). You can customize folder destinations under Options &rarr; Save To.',
        'tag'      => 'Smart Sorting · Clean Drive'
    ],
    [
        'id'       => '08',
        'category' => 'power',
        'badge'    => 'SCHEDULER',
        'title'    => 'How to Schedule Midnight Off-Peak Downloads with Auto-PC Shutdown',
        'desc'     => 'Set heavy game updates and ISO downloads to run between 2:00 AM and 6:00 AM when your internet bandwidth is fastest. In EDM Scheduler, check "Start download at 02:00" and check "Turn off computer when completed". EDM safely closes all sockets and shuts down Windows.',
        'tag'      => 'Off-Peak Speed · Auto-Shutdown'
    ],
    [
        'id'       => '09',
        'category' => 'speed',
        'badge'    => 'SPEED LIMITER',
        'title'    => 'How to Setup Dynamic Speed Limiter for Zero-Lag Online Gaming & Zoom Calls',
        'desc'     => 'Need to download while playing competitive games or streaming? Toggle EDM "Speed Limiter" from the bottom status bar. Set a max cap (e.g. 5 MB/s) to keep ping low and bandwidth free for Discord, Zoom, and multiplayer gaming.',
        'tag'      => 'Bandwidth QoS · Zero Lag'
    ],
    [
        'id'       => '10',
        'category' => 'power',
        'badge'    => 'AUTO EXTRACT',
        'title'    => 'How to Auto-Extract Password-Protected RAR & ZIP Archives on Completion',
        'desc'     => 'In EDM Options &rarr; Automation, enable "Extract Archives on Complete". Store your common extraction passwords in the password manager list. EDM automatically unzips multi-part archives (.part1.rar, .zip) into a clean folder upon reaching 100%.',
        'tag'      => 'One-Click Unpack · Multi-part RAR'
    ],
    [
        'id'       => '11',
        'category' => 'security',
        'badge'    => 'ANTIVIRUS SCAN',
        'title'    => 'How to Auto-Scan Files with Windows Defender & Antivirus Before Opening',
        'desc'     => 'Ensure total malware safety. In Options &rarr; Downloads &rarr; Antivirus Integration, EDM connects with Windows Defender or third-party engines (Kaspersky, Bitdefender, Malwarebytes) to scan every finished binary before it executes.',
        'tag'      => 'Clean of Adware · Zero Malware'
    ],
    [
        'id'       => '12',
        'category' => 'speed',
        'badge'    => 'VPN & PROXY',
        'title'    => 'How to Download via HTTP, HTTPS, FTP, FTPS & SOCKS5 Proxy / VPN Tunnels',
        'desc'     => 'Bypass geo-restrictions and ISP blockades. EDM features native support for SOCKS5, HTTP, and HTTPS proxy servers with user authentication. Configure custom proxy rules per site or route all traffic through your secure VPN tunnel.',
        'tag'      => 'Proxy Auth · Geo-Bypass'
    ],
    [
        'id'       => '13',
        'category' => 'video',
        'badge'    => 'STREAM SNIFFER',
        'title'    => 'How to Sniff Hidden Live HLS (.m3u8) & MPEG-DASH (.mpd) Video Streams',
        'desc'     => 'Protected live broadcasts and encrypted streaming sites often hide video behind segmented .m3u8 index files. EDM deep packet sniffer detects HLS manifests, downloads all encrypted TS segments concurrently, and outputs a single crisp MP4.',
        'tag'      => '.m3u8 to MP4 · Live Sniffer'
    ],
    [
        'id'       => '14',
        'category' => 'power',
        'badge'    => 'DARK GUI',
        'title'    => 'How to Enable Dark Cyber Mode & High-Contrast Visual Themes',
        'desc'     => 'Switch between OLED Deep Dark, Cyber Blue, and Clean Light themes with a single click. EDM interface uses GPU hardware acceleration for smooth 60 FPS scrolling and ultra-crisp typography on 4K/HiDPI monitors.',
        'tag'      => 'OLED Dark · 4K HiDPI Ready'
    ],
    [
        'id'       => '15',
        'category' => 'power',
        'badge'    => 'MIGRATION',
        'title'    => 'How to Transfer Download Queues & Settings to a New Windows 10/11 PC',
        'desc'     => 'Moving to a new laptop or desktop? Go to File &rarr; "Export Download State & Settings". Import the lightweight .edmbackup file on your new PC to restore all active queues, categories, site passwords, and history with zero data loss.',
        'tag'      => 'One-Click Backup · Seamless Migration'
    ]
];
?>
<section class="section section-darker" id="knowledge-hub" x-data="{ activeFilter: 'all', openStep: 1 }">
    <div class="container">
        
        <!-- Section Header -->
        <div class="section-header">
            <span class="section-badge"><?php esc_html_e('Complete Power Playbook', 'edm-theme'); ?></span>
            <h2 class="section-title"><?php esc_html_e('15 Essential Steps & Solutions Every User Searches For', 'edm-theme'); ?></h2>
            <p class="section-subtitle">
                <?php esc_html_e('Comprehensive step-by-step master guide covering 32-socket tuning, 4K video stream ripping, browser integration, cloud quota bypass, and automation.', 'edm-theme'); ?>
            </p>

            <!-- Interactive Category Filter Tabs -->
            <div class="playbook-filter-tabs">
                <button @click="activeFilter = 'all'" :class="activeFilter === 'all' ? 'active' : ''" class="tab-btn">
                    <span><?php esc_html_e('All 15 Steps', 'edm-theme'); ?></span>
                </button>
                <button @click="activeFilter = 'speed'" :class="activeFilter === 'speed' ? 'active' : ''" class="tab-btn">
                    <span>⚡ <?php esc_html_e('Speed & Sockets', 'edm-theme'); ?></span>
                </button>
                <button @click="activeFilter = 'video'" :class="activeFilter === 'video' ? 'active' : ''" class="tab-btn">
                    <span>🎬 <?php esc_html_e('Video & Stream Ripper', 'edm-theme'); ?></span>
                </button>
                <button @click="activeFilter = 'browser'" :class="activeFilter === 'browser' ? 'active' : ''" class="tab-btn">
                    <span>🧩 <?php esc_html_e('Browser Extensions', 'edm-theme'); ?></span>
                </button>
                <button @click="activeFilter = 'resume'" :class="activeFilter === 'resume' ? 'active' : ''" class="tab-btn">
                    <span>🛡️ <?php esc_html_e('Resume & Cloud Quotas', 'edm-theme'); ?></span>
                </button>
                <button @click="activeFilter = 'power'" :class="activeFilter === 'power' ? 'active' : ''" class="tab-btn">
                    <span>⚙️ <?php esc_html_e('Power Tools & Automation', 'edm-theme'); ?></span>
                </button>
            </div>
        </div>

        <!-- 15-Steps Grid Accordion -->
        <div class="playbook-steps-grid">
            <?php foreach ($steps as $index => $s): ?>
                <div class="playbook-step-card" 
                     x-show="activeFilter === 'all' || activeFilter === '<?php echo esc_attr($s['category']); ?>'"
                     x-transition:enter="transition ease-out duration-300"
                     x-transition:enter-start="opacity-0 transform scale-98"
                     x-transition:enter-end="opacity-100 transform scale-100">
                    
                    <div class="step-card-top">
                        <span class="step-id-tag">STEP <?php echo esc_html($s['id']); ?></span>
                        <span class="step-category-badge"><?php echo esc_html($s['badge']); ?></span>
                    </div>

                    <h3 class="step-card-title"><?php echo esc_html($s['title']); ?></h3>
                    <p class="step-card-desc"><?php echo esc_html($s['desc']); ?></p>

                    <div class="step-card-footer">
                        <span class="step-tag-pill"><i data-lucide="check" style="width: 12px; height: 12px;"></i> <?php echo esc_html($s['tag']); ?></span>
                        <a href="<?php echo esc_url($download_url); ?>" class="step-dl-action" download title="Download EDM Setup">
                            <i data-lucide="arrow-down-to-line" style="width: 14px; height: 14px;"></i>
                            <span><?php esc_html_e('Get EDM v' . $version, 'edm-theme'); ?></span>
                        </a>
                    </div>
                </div>
            <?php endforeach; ?>
        </div>

        <!-- Quick Help Card -->
        <div class="playbook-bottom-banner glass-panel">
            <div class="banner-left">
                <div class="banner-icon-box">
                    <i data-lucide="sparkles" style="width: 28px; height: 28px; color: var(--edm-primary-light);"></i>
                </div>
                <div>
                    <h4 class="text-white text-lg font-bold"><?php esc_html_e('Ready to Experience 32-Socket Download Speed?', 'edm-theme'); ?></h4>
                    <p class="text-slate-400 text-xs mt-1"><?php esc_html_e('Clean setup installer for Windows 11 & 10. No registration or credit card needed for 30-day full turbo trial.', 'edm-theme'); ?></p>
                </div>
            </div>
            <div class="banner-right">
                <a href="<?php echo esc_url($download_url); ?>" class="btn btn-primary btn-lg" download>
                    <i data-lucide="download" style="width: 18px; height: 18px;"></i>
                    <span><?php esc_html_e('Download EDM Setup (Free)', 'edm-theme'); ?></span>
                </a>
            </div>
        </div>

    </div>
</section>
