<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>EDM — Exclusive Download Manager | The Ultimate Windows Turbo Downloader</title>
    <meta name="description" content="Exclusive Download Manager (EDM) is a next-generation high-speed download manager for Windows. 32x acceleration, 4K/8K video grabber, browser extension, and smart scheduler.">
    <link rel="canonical" href="https://edm-download.org/">

    <!-- Open Graph Metadata -->
    <meta property="og:type" content="website">
    <meta property="og:url" content="https://edm-download.org/">
    <meta property="og:title" content="EDM — Exclusive Download Manager | The Ultimate Windows Turbo Downloader">
    <meta property="og:description" content="Download faster with 32 concurrent socket connections, crash-proof durable resume, and zero-click browser integration.">
    <meta property="og:site_name" content="Exclusive Download Manager">
    <meta name="twitter:card" content="summary_large_image">

    <!-- JSON-LD Structured Data: SoftwareApplication -->
    <script type="application/ld+json">
    {
      "@context": "https://schema.org",
      "@type": "SoftwareApplication",
      "name": "Exclusive Download Manager",
      "operatingSystem": "Windows 11, Windows 10, Windows 8.1, Windows 7 (64-bit and ARM64)",
      "applicationCategory": "UtilitiesApplication",
      "softwareVersion": "2.1.0",
      "fileSize": "2.4MB",
      "author": {
        "@type": "Person",
        "name": "nfalamin"
      },
      "offers": {
        "@type": "Offer",
        "price": "0",
        "priceCurrency": "USD"
      },
      "aggregateRating": {
        "@type": "AggregateRating",
        "ratingValue": "4.9",
        "ratingCount": "18450"
      }
    }
    </script>
    
    <!-- Google Fonts: Plus Jakarta Sans & Inter -->
    <link rel="preconnect" href="https://fonts.googleapis.com">
    <link rel="preconnect" href="https://fonts.gstatic.com" crossorigin>
    <link href="https://fonts.googleapis.com/css2?family=Plus+Jakarta+Sans:wght@400;500;600;700;800&family=Inter:wght@400;500;600;700&family=JetBrains+Mono:wght@400;500&display=swap" rel="stylesheet">
    
    <!-- Lucide Icons CDN -->
    <script src="https://unpkg.com/lucide@latest"></script>
    
    <!-- EDM Design System Master Stylesheet -->
    <link rel="stylesheet" href="style.css">
</head>
<body>

    <!-- ══════════════════════════════════════════════════════════════
         1. TOP ANNOUNCEMENT & CONTACT BAR
         ══════════════════════════════════════════════════════════════ -->
    <div class="top-notice-bar">
        <div class="container top-notice-content">
            <div class="top-notice-left">
                <span class="badge-pulse" id="top-notice-badge">VERIFIED RELEASE</span>
                <span id="top-notice-text">⚡ EDM v2.1.0 Production Turbo Engine with 32-Socket Acceleration is Live!</span>
            </div>
            <div class="top-notice-right">
                <a href="support.html"><i data-lucide="help-circle" style="width: 12px; height: 12px;"></i> Support Center</a>
                <a href="download.html"><i data-lucide="download" style="width: 12px; height: 12px;"></i> Download Setup</a>
                <a href="javascript:void(0)" onclick="window.edmSite.toggleCurrency()"><i data-lucide="globe" style="width: 12px; height: 12px;"></i> <span id="currency-label">BDT (৳)</span></a>
            </div>
        </div>
    </div>

    <!-- ══════════════════════════════════════════════════════════════
         2. STICKY GLASSMORPHIC NAVBAR
         ══════════════════════════════════════════════════════════════ -->
    <header class="navbar">
        <div class="container navbar-container">
            <!-- Brand Logo -->
            <a href="index.html" class="nav-brand">
                <div class="brand-logo-box">
                    <i data-lucide="zap" style="width: 20px; height: 20px;"></i>
                </div>
                <div class="brand-title-wrap">
                    <span class="brand-title">EDM</span>
                    <span class="brand-subtitle">Exclusive Download Manager</span>
                </div>
            </a>

            <!-- Desktop Menu Links -->
            <nav class="nav-links">
                <a href="index.html" class="nav-link active">Home</a>
                <a href="features.html" class="nav-link">Features</a>
                <a href="technology.html" class="nav-link">32x Turbo</a>
                <a href="browser-extension.html" class="nav-link">Extension</a>
                <a href="download.html" class="nav-link">Download</a>
                <a href="pricing.html" class="nav-link">Pricing</a>
                <a href="screenshots.html" class="nav-link">Screenshots</a>
                <a href="changelog.html" class="nav-link">What's New</a>
                <a href="faq.html" class="nav-link">FAQ</a>
            </nav>

            <!-- Right Action Items -->
            <div class="nav-actions">
                <!-- Theme Switcher -->
                <button class="btn-theme-toggle" id="btn-theme-toggle" title="Toggle Theme" onclick="window.edmSite.toggleTheme()">
                    <i data-lucide="sun" id="theme-icon" style="width: 15px; height: 15px;"></i>
                </button>

                <!-- Primary CTA: Download EDM -->
                <a href="download.html" class="btn btn-primary btn-sm">
                    <i data-lucide="download" style="width: 14px; height: 14px;"></i>
                    <span>Download EDM</span>
                </a>

                <!-- Mobile Hamburger Toggle -->
                <button class="btn-hamburger" onclick="window.edmSite.toggleMobileMenu()">
                    <i data-lucide="menu" style="width: 20px; height: 20px;"></i>
                </button>
            </div>
        </div>
    </header>

    <!-- Mobile Navigation Drawer -->
    <div class="mobile-drawer" id="mobile-drawer">
        <a href="index.html" class="mobile-nav-link active">Home</a>
        <a href="features.html" class="mobile-nav-link">Features</a>
        <a href="technology.html" class="mobile-nav-link">32x Turbo / Technology</a>
        <a href="browser-extension.html" class="mobile-nav-link">Browser Extension</a>
        <a href="download.html" class="mobile-nav-link">Download Setup</a>
        <a href="pricing.html" class="mobile-nav-link">Pricing & Plans</a>
        <a href="screenshots.html" class="mobile-nav-link">Screenshots</a>
        <a href="changelog.html" class="mobile-nav-link">What's New / Changelog</a>
        <a href="faq.html" class="mobile-nav-link">FAQ</a>
        <a href="system-requirements.html" class="mobile-nav-link">System Requirements</a>
        <a href="about.html" class="mobile-nav-link">About EDM</a>
        <a href="support.html" class="mobile-nav-link">Support</a>
        <a href="download.html" class="btn btn-primary w-full" style="width: 100%; margin-top: 10px;">
            <i data-lucide="download" style="width: 14px; height: 14px;"></i> Download for Windows
        </a>
    </div>

    <!-- ══════════════════════════════════════════════════════════════
         3. HERO SECTION (CONVERSION FOCUS)
         ══════════════════════════════════════════════════════════════ -->
    <section class="hero-section">
        <div class="hero-glow-bg"></div>
        <div class="container">
            <div class="hero-content">
                <!-- Floating Platform Pills -->
                <div class="floating-pills-wrap">
                    <div class="floating-pill"><i data-lucide="monitor" style="width: 13px; height: 13px; color: #38BDF8;"></i> Windows 11 / 10 / 8.1 / 7</div>
                    <div class="floating-pill"><i data-lucide="cpu" style="width: 13px; height: 13px; color: #10B981;"></i> 32-Socket Turbo</div>
                    <div class="floating-pill"><i data-lucide="video" style="width: 13px; height: 13px; color: #EC4899;"></i> 4K / 8K Video Ripper</div>
                    <div class="floating-pill"><i data-lucide="puzzle" style="width: 13px; height: 13px; color: #F59E0B;"></i> Chrome & Edge MV3</div>
                </div>

                <div class="hero-pill-badge">
                    <i data-lucide="sparkles" style="width: 14px; height: 14px;"></i>
                    <span id="hero-pill-text">Exclusive Download Manager • Production Build v2.1.0</span>
                </div>

                <h1 class="hero-title">
                    The Fastest Download Manager for Windows <br>
                    <span class="gradient-text">Engineered for Unmatched Speed & Control</span>
                </h1>

                <p class="hero-subtitle">
                    Turbocharge your files, high-bitrate video streams, and large archives with 32 concurrent socket connections, crash-proof durable resume, and zero-click browser auto-interception.
                </p>

                <!-- URL Sniffer Search Capsule -->
                <div class="url-sniffer-capsule">
                    <i data-lucide="link" style="width: 18px; height: 18px; color: var(--edm-primary-light); margin-left: 6px;"></i>
                    <input type="text" id="url-sniffer-input" class="sniffer-input" placeholder="Paste any download link, YouTube/Vimeo video URL, or ISO link to test 32x sniffer...">
                    <button class="btn btn-primary" onclick="window.edmSite.handleSniffUrl()">
                        <i data-lucide="zap" style="width: 14px; height: 14px;"></i>
                        <span>Sniff & Turbo Download</span>
                    </button>
                </div>

                <!-- Call to Action Buttons -->
                <div class="hero-cta-group">
                    <a href="download.html" class="btn btn-primary btn-lg">
                        <i data-lucide="download" style="width: 18px; height: 18px;"></i>
                        <span>Download EDM for Windows</span>
                    </a>
                    <a href="features.html" class="btn btn-secondary btn-lg">
                        <i data-lucide="sliders" style="width: 18px; height: 18px; color: var(--edm-primary-light);"></i>
                        <span>Explore Features</span>
                    </a>
                </div>

                <!-- Compatibility Footnote -->
                <div class="hero-compatibility-row">
                    <span><i data-lucide="check-circle" style="width: 13px; height: 13px; color: var(--edm-green); display: inline-block; vertical-align: middle;"></i> Windows 11 / 10 / 8.1 / 7 (64-bit & ARM64)</span>
                    <span>•</span>
                    <span>Installer Size: <strong>2.4 MB</strong></span>
                    <span>•</span>
                    <span>SHA-256 Verified Clean</span>
                </div>
            </div>
        </div>
    </section>

    <!-- ══════════════════════════════════════════════════════════════
         4. LIVE DOWNLOAD ENGINE & 32-STREAM SOCKET SIMULATOR
         ══════════════════════════════════════════════════════════════ -->
    <section class="preview-section">
        <div class="container">
            <div class="product-window-card">
                <div class="window-header">
                    <div class="window-dots">
                        <div class="window-dot dot-red"></div>
                        <div class="window-dot dot-yellow"></div>
                        <div class="window-dot dot-green"></div>
                    </div>
                    <div class="window-title">EDM — Active 32-Socket Download Accelerator [Live Engine]</div>
                    <div style="font-size: 11.5px; color: var(--edm-green); font-weight: 700; display: flex; align-items: center; gap: 5px;">
                        <span style="width: 8px; height: 8px; border-radius: 50%; background: var(--edm-green); display: inline-block; box-shadow: 0 0 8px #10B981;"></span>
                        <span id="engine-status-text">Engine Online</span>
                    </div>
                </div>

                <div class="window-body">
                    <div class="simulator-stats-grid">
                        <div class="sim-stat-box">
                            <div class="sim-stat-label">Download Speed</div>
                            <div class="sim-stat-value" style="color: var(--edm-primary-light);" id="sim-speed-val">14.8 MB/s</div>
                        </div>
                        <div class="sim-stat-box">
                            <div class="sim-stat-label">Active Connections</div>
                            <div class="sim-stat-value" id="sim-streams-val">32 / 32 Streams</div>
                        </div>
                        <div class="sim-stat-box">
                            <div class="sim-stat-label">Time Remaining</div>
                            <div class="sim-stat-value" id="sim-time-val">00:38</div>
                        </div>
                        <div class="sim-stat-box">
                            <div class="sim-stat-label">Resume Capability</div>
                            <div class="sim-stat-value" style="color: var(--edm-green);">Supported</div>
                        </div>
                    </div>

                    <div class="sim-progress-wrap">
                        <div class="sim-file-info">
                            <span>Ubuntu-24.04-LTS-Desktop-x64.iso (5.80 GB)</span>
                            <span id="sim-progress-text">72% Completed (4.18 GB)</span>
                        </div>
                        <div class="sim-progress-bar-bg">
                            <div class="sim-progress-bar-fill" id="sim-progress-fill" style="width: 72%;"></div>
                        </div>

                        <!-- 32 Connection Threads Grid -->
                        <div class="streams-grid" id="streams-grid"></div>
                    </div>

                    <div class="simulator-controls">
                        <div style="display: flex; gap: 8px;">
                            <button class="btn btn-secondary btn-sm" id="btn-sim-pause" onclick="window.edmSite.toggleSimPause()">
                                <i data-lucide="pause" id="sim-pause-icon" style="width: 12px; height: 12px;"></i>
                                <span id="sim-pause-text">Pause Engine</span>
                            </button>
                            <button class="btn btn-primary btn-sm" onclick="window.edmSite.boostTurbo()">
                                <i data-lucide="flame" style="width: 12px; height: 12px;"></i>
                                <span>Turbo Boost (48.6 MB/s)</span>
                            </button>
                        </div>
                        <span style="font-size: 11.5px; color: var(--edm-text-muted);">Dynamic HTTP Range Multi-threading Active</span>
                    </div>
                </div>
            </div>
        </div>
    </section>

    <!-- ══════════════════════════════════════════════════════════════
         5. CORE HIGHLIGHTS
         ══════════════════════════════════════════════════════════════ -->
    <section class="section">
        <div class="container">
            <div class="section-header">
                <span class="section-badge">Product Highlights</span>
                <h2 class="section-title">Why Users Switch to EDM</h2>
                <p class="section-subtitle">A modern desktop download manager engineered for maximum reliability, speed, and browser auto-capture.</p>
            </div>

            <div class="features-grid-3">
                <div class="feature-card">
                    <div>
                        <div class="feature-icon-box" style="background: linear-gradient(135deg, #6366F1 0%, #4F46E5 100%);">
                            <i data-lucide="zap"></i>
                        </div>
                        <h3 class="feature-card-title">32-Socket Turbo Multi-Threading</h3>
                        <p class="feature-card-desc">Splits files dynamically into 32 simultaneous socket segments to saturate high-speed fiber internet connections.</p>
                    </div>
                    <a href="technology.html" class="btn-ghost" style="margin-top: 16px;">Read 32x Tech Specs &rarr;</a>
                </div>

                <div class="feature-card">
                    <div>
                        <div class="feature-icon-box" style="background: linear-gradient(135deg, #38BDF8 0%, #0284C7 100%);">
                            <i data-lucide="play-circle"></i>
                        </div>
                        <h3 class="feature-card-title">4K & 8K Video Stream Capture</h3>
                        <p class="feature-card-desc">Captures live streaming manifests (M3U8 and DASH) from YouTube, Vimeo, and 1,000+ video websites with zero quality loss.</p>
                    </div>
                    <a href="features.html" class="btn-ghost" style="margin-top: 16px;">View Media Ripper &rarr;</a>
                </div>

                <div class="feature-card">
                    <div>
                        <div class="feature-icon-box" style="background: linear-gradient(135deg, #10B981 0%, #059669 100%);">
                            <i data-lucide="refresh-cw"></i>
                        </div>
                        <h3 class="feature-card-title">Crash-Proof Resume</h3>
                        <p class="feature-card-desc">Schema-versioned metadata and atomic file flushes ensure your downloads resume instantly after power outages.</p>
                    </div>
                    <a href="features.html" class="btn-ghost" style="margin-top: 16px;">Explore Resume Engine &rarr;</a>
                </div>
            </div>

            <div style="margin-top: 40px; text-align: center;">
                <a href="features.html" class="btn btn-secondary">
                    <span>View All 12+ Features & Architecture</span>
                    <i data-lucide="arrow-right" style="width: 14px; height: 14px;"></i>
                </a>
            </div>
        </div>
    </section>

    <!-- ══════════════════════════════════════════════════════════════
         6. FOOTER (AUTHOR: NFALAMIN)
         ══════════════════════════════════════════════════════════════ -->
    <footer class="footer">
        <div class="container">
            <div class="footer-grid">
                <div class="footer-col">
                    <div class="nav-brand" style="margin-bottom: 16px;">
                        <div class="brand-logo-box">
                            <i data-lucide="zap" style="width: 20px; height: 20px;"></i>
                        </div>
                        <div class="brand-title-wrap">
                            <span class="brand-title">EDM</span>
                            <span class="brand-subtitle">Exclusive Download Manager</span>
                        </div>
                    </div>
                    <p style="font-size: 13px; color: var(--edm-text-muted); max-width: 320px;">
                        Next-generation Windows download acceleration software. Built for high-speed file capture, media streaming, and reliable queue scheduling.
                    </p>
                </div>

                <div class="footer-col">
                    <h4>Product</h4>
                    <ul class="footer-links">
                        <li><a href="features.html">Features</a></li>
                        <li><a href="technology.html">32x Turbo Engine</a></li>
                        <li><a href="browser-extension.html">Browser Extension</a></li>
                        <li><a href="download.html">Download Setup</a></li>
                        <li><a href="pricing.html">Pricing & Plans</a></li>
                        <li><a href="screenshots.html">Screenshots</a></li>
                        <li><a href="changelog.html">What's New / Changelog</a></li>
                    </ul>
                </div>

                <div class="footer-col">
                    <h4>Docs & Support</h4>
                    <ul class="footer-links">
                        <li><a href="system-requirements.html">System Requirements</a></li>
                        <li><a href="faq.html">FAQ</a></li>
                        <li><a href="support.html">Support Center</a></li>
                        <li><a href="about.html">About EDM</a></li>
                        <li><a href="../EDM.ControlPlane.Dashboard/index.html" target="_blank" style="color: var(--edm-text-muted);">Admin Portal &rarr;</a></li>
                    </ul>
                </div>

                <div class="footer-col">
                    <h4>Legal & Security</h4>
                    <ul class="footer-links">
                        <li><a href="privacy.html">Privacy Policy</a></li>
                        <li><a href="terms.html">Terms of Service</a></li>
                        <li><a href="terms.html">End User License (EULA)</a></li>
                    </ul>
                </div>
            </div>

            <div class="footer-bottom">
                <span>&copy; 2025-2026 nfalamin. All rights reserved.</span>
                <span style="color: var(--edm-primary-light);">SHA-256 Verified • Cryptographic Binary Integrity</span>
            </div>
        </div>
    </footer>

    <!-- Modals and Toasts -->
    <div class="modal-backdrop" id="modal-download">
        <div class="modal-dialog">
            <div class="modal-header">
                <span class="modal-title"><i data-lucide="download-cloud" style="color: var(--edm-primary-light);"></i> Downloading EDM v2.1.0</span>
                <button class="btn-theme-toggle" onclick="window.edmSite.closeModal('modal-download')"><i data-lucide="x"></i></button>
            </div>
            <div class="modal-body" style="text-align: center;">
                <div style="width: 56px; height: 56px; border-radius: 50%; background: var(--edm-primary-soft); color: var(--edm-primary-light); display: flex; align-items: center; justify-content: center; margin: 0 auto 16px auto;">
                    <i data-lucide="check" style="width: 28px; height: 28px;"></i>
                </div>
                <h3 style="font-size: 20px; font-weight: 800; margin-bottom: 6px;">Your Download Has Started!</h3>
                <p style="font-size: 13px; color: var(--edm-text-secondary); margin-bottom: 20px;">
                    Saving <code>EDM-Setup-v2.1.0.exe</code> (2.4 MB). If the download didn't start automatically, <a href="./downloads/EDM-Setup-v2.1.0.exe" download="EDM-Setup-v2.1.0.exe" style="font-weight: 700; text-decoration: underline;">click here to retry</a>.
                </p>
            </div>
            <div class="modal-footer">
                <button class="btn btn-primary" onclick="window.edmSite.closeModal('modal-download')">Done & Enjoy EDM</button>
            </div>
        </div>
    </div>

    <div class="modal-backdrop" id="modal-sniffer-result">
        <div class="modal-dialog">
            <div class="modal-header">
                <span class="modal-title"><i data-lucide="zap" style="color: var(--edm-green);"></i> Stream Captured Successfully!</span>
                <button class="btn-theme-toggle" onclick="window.edmSite.closeModal('modal-sniffer-result')"><i data-lucide="x"></i></button>
            </div>
            <div class="modal-body">
                <div style="background: var(--edm-bg-subtle); padding: 14px; border-radius: var(--edm-radius-md); border: 1px solid var(--edm-border); margin-bottom: 16px;">
                    <div style="font-size: 11px; color: var(--edm-text-muted);">Parsed Stream URL:</div>
                    <code style="font-size: 12px; color: var(--edm-primary-light); word-break: break-all;" id="sniffer-detected-url">https://stream.media/video_4k_master.m3u8</code>
                </div>
                <ul style="list-style: none; display: flex; flex-direction: column; gap: 8px; font-size: 12.5px;">
                    <li><strong>Protocol:</strong> HTTP/2 Multi-Range</li>
                    <li><strong>Allocated Streams:</strong> 32 Threads</li>
                    <li><strong>Estimated Transfer Rate:</strong> ~28.5 MB/s</li>
                </ul>
            </div>
            <div class="modal-footer">
                <button class="btn btn-secondary" onclick="window.edmSite.closeModal('modal-sniffer-result')">Dismiss</button>
                <a href="download.html" class="btn btn-primary">Download via EDM</a>
            </div>
        </div>
    </div>

    <div class="toast-stack" id="toast-stack"></div>

    <script src="app.js"></script>
</body>
</html>
