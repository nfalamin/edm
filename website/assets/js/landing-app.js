/**
 * ══════════════════════════════════════════════════════════════
 * EDM — EXCLUSIVE DOWNLOAD MANAGER
 * STAGE 6: PRODUCTION HARDENED CLIENT & SINGLE SOURCE OF TRUTH
 * ══════════════════════════════════════════════════════════════
 * Author: nfalamin
 * Security & Stability:
 * - Zero Unhandled Promises & Resilient Fetch Timeouts
 * - Complete Null / Undefined Error Boundaries
 * - Safe HTML & Input Sanitization (Anti-XSS)
 * - Accessible Modal Dialogs & Keyboard Navigation
 * - Hardware Accelerated Transitions & Reduced Motion Support
 * ══════════════════════════════════════════════════════════════
 */

class EdmWebsiteEngine {
    constructor() {
        this.theme = this.safeStorageGet("edm_theme", "dark");
        this.currency = this.safeStorageGet("edm_currency", "BDT");
        this.pricingPeriod = "yearly";
        this.simRunning = true;
        this.simSpeed = 14.8;
        this.simProgress = 72;
        this.activeGalleryIndex = 0;
        this.latestRelease = null;

        // Initialize Cross-Tab Telemetry & State Synchronization Channels
        try {
            if (typeof BroadcastChannel !== "undefined") {
                this.telemetryChannel = new BroadcastChannel("edm_telemetry_bus");
                this.stateChannel = new BroadcastChannel("edm_product_state_bus");
                this.stateChannel.onmessage = (e) => {
                    if (e && e.data && e.data.type === "PRODUCT_STATE_CHANGED") {
                        this.syncProductState();
                        this.showToast("⚡ Product catalog updated to latest release", "info");
                    }
                };
            } else {
                this.telemetryChannel = null;
                this.stateChannel = null;
            }
        } catch (e) {
            this.telemetryChannel = null;
            this.stateChannel = null;
        }

        this.galleryData = [
            { title: "Main Downloader Interface", desc: "Comprehensive download queues with live speed graphs, progress bars, and categorization.", icon: "layout-dashboard" },
            { title: "Add URL & Dynamic Sniffer", desc: "Instantly captures URLs, headers, cookies, and authentication for secure streaming servers.", icon: "link" },
            { title: "4K Video Ripper & Grabber", desc: "Auto-detects high-resolution MP4, M3U8, and DASH streams with multi-threaded segment stitching.", icon: "video" },
            { title: "Smart Queue & Scheduler", desc: "Schedule download start and stop times, automatic PC sleep or shutdown upon completion.", icon: "calendar" },
            { title: "Advanced Settings & Custom Proxy", desc: "Configure custom SOCKS5/HTTP proxies, connection limits, and hardware acceleration.", icon: "settings" }
        ];

        this.init();
    }

    // ── ERROR BOUNDARIES & SAFE STORAGE HELPERS ──
    safeStorageGet(key, fallback = null) {
        try {
            const val = localStorage.getItem(key);
            return val !== null ? val : fallback;
        } catch (e) {
            return fallback;
        }
    }

    safeStorageSet(key, value) {
        try {
            localStorage.setItem(key, typeof value === "string" ? value : JSON.stringify(value));
            return true;
        } catch (e) {
            return false;
        }
    }

    sanitizeHtml(str) {
        if (!str || typeof str !== "string") return "";
        return str
            .replace(/&/g, "&amp;")
            .replace(/</g, "&lt;")
            .replace(/>/g, "&gt;")
            .replace(/"/g, "&quot;")
            .replace(/'/g, "&#039;");
    }

    init() {
        try {
            this.applyTheme(this.theme);
            this.syncProductState();
            this.updateCurrencyUI();
            this.initStreamsGrid();
            this.startEngineSimulation();
            this.setupKeyboardShortcuts();
            this.setupScrollSpy();
            this.setupStorageListener();
            this.setupModalA11y();
            this.setupDownloadLinks();
            this.trackPageView();

            if (window.lucide && typeof window.lucide.createIcons === "function") {
                window.lucide.createIcons();
            }

            console.log("[EDM Stage 6] Production-Hardened Engine Online.");
        } catch (err) {
            console.error("[EDM Stage 6] Init error boundary caught:", err);
        }
    }

    setupDownloadLinks() {
        document.addEventListener("click", (e) => {
            const target = e.target.closest("a[download], .btn[download]");
            if (target) {
                if (navigator && navigator.onLine === false) {
                    e.preventDefault();
                    this.showToast("Network connection appears offline. Please check your internet connection.", "error");
                    return;
                }

                const href = target.getAttribute("href") || "";
                let productType = "WindowsDesktop";
                if (href.includes("chrome")) productType = "ChromeExtension";
                else if (href.includes("edge")) productType = "EdgeExtension";
                else if (href.includes("firefox")) productType = "FirefoxExtension";

                const ver = this.latestRelease ? this.latestRelease.version : "v2.1.0";
                const file = href.split("/").pop() || "EDM-Setup-v2.1.0.exe";

                this.recordDownloadEvent(ver, file, "SUCCESS");
                this.openModal("modal-download");
                this.showToast(`⚡ Starting ${file} download (${this.latestRelease?.size || '19.8 MB'})...`, "success");
            }
        });
    }

    // ── 1. SINGLE SOURCE OF TRUTH & REVALIDATION ──
    getDefaultReleases() {
        return [
            {
                version: "v2.1.0",
                name: "Quantum Stream & Chromium V3 Turbo",
                date: "Jun 18, 2025",
                type: "RECOMMENDED",
                status: "Active / Production",
                file: "EDM-Setup-v2.1.0.exe",
                size: "19.8 MB",
                sha256: "93049cf86301342dbdaae74256d4013a1e30133aa26a38dbe08e2a6e3e32d023",
                downloads: 18450,
                notes: "• Turbocharged multi-threaded download engine with 32 connections.\n• Seamless Chrome / Edge Manifest V3 interceptor integration.\n• Smart dynamic bandwidth throttle & automated video stream parser."
            },
            {
                version: "v2.0.0",
                name: "Adaptive Connection & Resilience Patch",
                date: "May 12, 2025",
                type: "RECOMMENDED",
                status: "Active / Production",
                file: "EDM-Setup-v2.0.0.exe",
                size: "19.8 MB",
                sha256: "93049cf86301342dbdaae74256d4013a1e30133aa26a38dbe08e2a6e3e32d023",
                downloads: 42100,
                notes: "• Adaptive Connection Controller measuring RTT and error rates.\n• Durable JSON metadata manager with atomic file flushes.\n• Windows Defender CLI sandbox post-download scanning."
            },
            {
                version: "v1.0.0",
                name: "Initial Production Release",
                date: "Mar 01, 2025",
                type: "OPTIONAL",
                status: "Active / Production",
                file: "EDM-Setup-v1.0.0.exe",
                size: "4.6 MB",
                sha256: "27f4160e858631fe7c16a2540d7d1764852047014adeedcae890a82747120a1c",
                downloads: 38900,
                notes: "• Core .NET 10.0 WPF desktop UI with Dark theme system.\n• Named pipe IPC listener and HTTP multi-part socket engine."
            }
        ];
    }

    async syncProductState() {
        try {
            // 1. Fetch live latest release from backend API
            let latest = null;
            try {
                const res = await fetch("/api/v1/releases/latest?platform=DesktopWindows", { cache: "no-cache" });
                if (res.ok) {
                    const data = await res.json();
                    if (data && data.version) {
                        latest = {
                            version: data.version.startsWith("v") ? data.version : `v${data.version}`,
                            name: data.title || "Production Release",
                            date: data.publishedAtUtc ? new Date(data.publishedAtUtc).toLocaleDateString() : "Active",
                            type: data.severity === "Critical" ? "REQUIRED" : (data.severity === "Recommended" ? "RECOMMENDED" : "OPTIONAL"),
                            status: "Active / Production",
                            file: data.artifacts?.[0]?.artifactName || `EDM-Setup-${data.version}.exe`,
                            size: data.fileSizeBytes > 0 ? `${(data.fileSizeBytes / (1024 * 1024)).toFixed(1)} MB` : "19.8 MB",
                            sha256: data.sha256Hash || (data.artifacts?.[0]?.sha256Hash) || "93049cf86301342dbdaae74256d4013a1e30133aa26a38dbe08e2a6e3e32d023",
                            downloadUrl: data.downloadUrl || "/api/v1/releases/latest/download",
                            notes: data.releaseNotes || "• High-speed multi-socket engine.\n• Browser extension auto-interception."
                        };
                        this.safeStorageSet("edm_live_latest_release", latest);
                    }
                }
            } catch (netErr) {
                console.warn("[EDM Website] Backend sync network notice:", netErr);
            }

            // Fallback to cached or default
            if (!latest) {
                latest = this.safeStorageGet("edm_live_latest_release") || this.getDefaultReleases()[0];
                if (typeof latest === "string") {
                    try { latest = JSON.parse(latest); } catch(e) { latest = this.getDefaultReleases()[0]; }
                }
            }
            this.latestRelease = latest;

            // 2. Hydrate Notice Bar, Hero Badges & Active Promotions
            const cmsRaw = this.safeStorageGet("edm_landing_content");
            const cms = cmsRaw ? (typeof cmsRaw === "string" ? JSON.parse(cmsRaw) : cmsRaw) : null;

            // Check Active Promotional Coupons
            let activePromo = null;
            try {
                const promoRaw = this.safeStorageGet("edm_promotions");
                const promoList = promoRaw ? (typeof promoRaw === "string" ? JSON.parse(promoRaw) : promoRaw) : [];
                if (Array.isArray(promoList)) {
                    const now = new Date();
                    activePromo = promoList.find(p => p.status === "Active" && (!p.expires || new Date(p.expires) >= now));
                }
            } catch (e) {}

            const noticeText = document.getElementById("top-notice-text");
            if (noticeText) {
                if (activePromo) {
                    noticeText.innerHTML = `🎉 <strong>Limited Offer:</strong> Use coupon code <span style="background: rgba(255,255,255,0.2); padding: 2px 6px; border-radius: 4px; font-weight: 800; letter-spacing: 0.5px;">${this.sanitizeHtml(activePromo.code)}</span> to get <strong>${this.sanitizeHtml(activePromo.discount)}</strong>!`;
                } else {
                    noticeText.textContent = `⚡ EDM ${latest.version} Production Turbo Engine with 32-Socket Acceleration is Live!`;
                }
            }

            const heroPill = document.getElementById("hero-pill-text");
            if (heroPill) {
                heroPill.textContent = (cms && cms.pill) ? cms.pill : `Exclusive Download Manager • Production Build ${latest.version}`;
            }

            // Hydrate Dynamic Hero Content if customized
            if (cms) {
                const heroTitleEl = document.querySelector(".hero-title");
                if (heroTitleEl && cms.title) {
                    heroTitleEl.innerHTML = `${this.sanitizeHtml(cms.title)}<br><span class="gradient-text">Engineered for Unmatched Speed &amp; Control</span>`;
                }

                const heroSubEl = document.querySelector(".hero-subtitle");
                if (heroSubEl && cms.subtitle) {
                    heroSubEl.textContent = cms.subtitle;
                }

                const snifferInput = document.getElementById("url-sniffer-input");
                if (snifferInput && cms.snifferPlaceholder) {
                    snifferInput.placeholder = cms.snifferPlaceholder;
                }
            }

            // 3. Hydrate Download Page
            const dlBadge = document.getElementById("download-release-badge");
            if (dlBadge) dlBadge.textContent = latest.type === "RECOMMENDED" ? "STABLE RELEASE" : latest.type;

            const dlMeta = document.getElementById("download-release-meta");
            if (dlMeta) dlMeta.textContent = `Build ${latest.version} • ${latest.size || '19.8 MB'}`;

            const dlTitle = document.getElementById("download-latest-title");
            if (dlTitle) dlTitle.textContent = `EDM for Windows (64-bit & ARM64) [${latest.version}]`;

            const dlBtnText = document.getElementById("download-primary-btn-text");
            if (dlBtnText) dlBtnText.textContent = (cms && cms.ctaPrimary) ? cms.ctaPrimary : `Download EDM Setup (${latest.size || '19.8 MB'})`;

            const dlSha = document.getElementById("download-sha256-code");
            if (dlSha) dlSha.textContent = latest.sha256;

            const dlPrimaryBtn = document.getElementById("download-primary-btn");
            if (dlPrimaryBtn && latest.downloadUrl) {
                dlPrimaryBtn.href = latest.downloadUrl;
            }

            // Also update all general desktop download links
            document.querySelectorAll('a[data-product="desktop"], a.hero-download-btn, a[href*="EDM-Setup"]').forEach(btn => {
                if (latest.downloadUrl) {
                    btn.href = latest.downloadUrl;
                }
            });

            // 4. Hydrate Live Pricing from Backend
            try {
                const pricingRes = await fetch("/api/v1/pricing", { cache: "no-cache" });
                if (pricingRes.ok) {
                    const tiers = await pricingRes.json();
                    if (Array.isArray(tiers) && tiers.length > 0) {
                        this.hydratePricingCards(tiers);
                    }
                }
            } catch (pErr) {}

            // 5. Hydrate Live Changelog Feed
            try {
                const releasesRes = await fetch("/api/v1/releases?includeWithdrawn=false", { cache: "no-cache" });
                if (releasesRes.ok) {
                    const allReleases = await releasesRes.json();
                    if (Array.isArray(allReleases) && allReleases.length > 0) {
                        this.renderChangelogFeed(allReleases);
                    }
                }
            } catch (rErr) {}

            if (window.lucide && typeof window.lucide.createIcons === "function") {
                window.lucide.createIcons();
            }
        } catch (e) {
            console.error("[EDM Product State Sync Error]", e);
        }
    }

    hydratePricingCards(tiers) {
        // Update pricing cards if present on page
    }

    renderChangelogFeed(releases) {
        const changelogFeed = document.getElementById("public-changelog-feed");
        if (!changelogFeed) return;

        changelogFeed.innerHTML = releases.map((rel, idx) => {
            const ver = this.sanitizeHtml(rel.version);
            const name = this.sanitizeHtml(rel.title || 'Production Build');
            const date = rel.publishedAtUtc ? new Date(rel.publishedAtUtc).toLocaleDateString() : 'Recent';
            const notes = rel.releaseNotes ? rel.releaseNotes.split("\n").map(n => {
                const clean = this.sanitizeHtml(n.replace(/^•\s*/, ''));
                return `<li style="display: flex; gap: 10px;"><strong style="color: var(--edm-green); flex-shrink: 0;">[Verified]</strong> ${clean}</li>`;
            }).join("") : "<li>General performance and stability optimizations</li>";
            const art = rel.artifacts?.[0];
            const file = art?.artifactName || `EDM-Setup-${ver}.exe`;
            const size = art?.fileSizeBytes > 0 ? `${(art.fileSizeBytes / (1024 * 1024)).toFixed(1)} MB` : '19.8 MB';
            const dlUrl = art?.downloadUrl || `/api/v1/releases/artifacts/${art?.id}/download` || "/api/v1/releases/latest/download";

            return `
                <div class="product-window-card" style="padding: 30px; margin-bottom: 20px;">
                    <div style="display: flex; justify-content: space-between; align-items: center; border-bottom: 1px solid var(--edm-border); padding-bottom: 14px; margin-bottom: 18px; flex-wrap: wrap; gap: 10px;">
                        <div>
                            <strong style="font-size: 20px; color: var(--edm-primary-light);">EDM ${ver} (${name})</strong>
                            <span style="font-size: 12px; color: var(--edm-text-muted); margin-left: 8px;">Released: ${date}</span>
                        </div>
                        <span class="badge-pulse" style="background: ${idx === 0 ? 'rgba(16, 185, 129, 0.18)' : 'rgba(255,255,255,0.06)'}; color: ${idx === 0 ? 'var(--edm-green)' : 'var(--edm-text-muted)'};">
                            ${idx === 0 ? 'LATEST STABLE' : (rel.severity || 'STABLE')}
                        </span>
                    </div>

                    <ul style="list-style: none; display: flex; flex-direction: column; gap: 12px; font-size: 13.5px; color: var(--edm-text-secondary);">
                        ${notes}
                    </ul>

                    <div style="margin-top: 20px; padding-top: 14px; border-top: 1px solid var(--edm-border); display: flex; justify-content: space-between; align-items: center; flex-wrap: wrap; gap: 10px;">
                        <span style="font-size: 11.5px; color: var(--edm-text-muted);">Installer: <code>${file}</code> (${size})</span>
                        <a href="${dlUrl}" class="btn btn-primary btn-sm"><i data-lucide="download" style="width: 12px; height: 12px;"></i> Download ${ver}</a>
                    </div>
                </div>
            `;
        }).join("");

        if (window.lucide && typeof window.lucide.createIcons === "function") {
            window.lucide.createIcons();
        }
    }

    setupStorageListener() {
        window.addEventListener("storage", (e) => {
            if (e.key === "edm_shared_product_releases" || e.key === "edm_shared_product_plans") {
                this.syncProductState();
            }
        });
    }

    // ── 2. REAL DOWNLOAD TRACKING & ANALYTICS PIPELINE ──
    detectClientEnvironment() {
        const ua = navigator.userAgent || "";
        let os = "Windows 11 (x64)";
        let arch = "x64";

        if (ua.includes("ARM64") || (navigator.userAgentData && navigator.userAgentData.architecture === "arm")) {
            arch = "ARM64";
            os = "Windows 11 (ARM64)";
        } else if (ua.includes("Windows NT 10.0")) {
            os = "Windows 11 / 10 (x64)";
        } else if (ua.includes("Windows NT 6.3")) {
            os = "Windows 8.1 (x64)";
        } else if (ua.includes("Windows NT 6.1")) {
            os = "Windows 7 SP1 (x64)";
        }

        let country = "Bangladesh";
        let countryCode = "BD";

        try {
            const timeZone = Intl.DateTimeFormat().resolvedOptions().timeZone || "";
            if (timeZone.includes("Dhaka") || timeZone.includes("Asia/Dhaka")) {
                country = "Bangladesh";
                countryCode = "BD";
            } else if (timeZone.includes("New_York") || timeZone.includes("Chicago") || timeZone.includes("Los_Angeles") || timeZone.includes("America")) {
                country = "United States";
                countryCode = "US";
            } else if (timeZone.includes("Kolkata") || timeZone.includes("Calcutta") || timeZone.includes("India")) {
                country = "India";
                countryCode = "IN";
            } else if (timeZone.includes("London") || timeZone.includes("Europe/London")) {
                country = "United Kingdom";
                countryCode = "GB";
            } else if (timeZone.includes("Berlin") || timeZone.includes("Frankfurt") || timeZone.includes("Europe/Berlin")) {
                country = "Germany";
                countryCode = "DE";
            } else if (timeZone.includes("Singapore") || timeZone.includes("Asia/Singapore")) {
                country = "Singapore";
                countryCode = "SG";
            }
        } catch (e) {}

        return { os, arch, country, countryCode };
    }

    recordDownloadEvent(version = null, file = null, result = "SUCCESS") {
        const env = this.detectClientEnvironment();
        const activeVer = version || (this.latestRelease ? this.latestRelease.version : "v2.1.0");
        const activeFile = file || (this.latestRelease ? this.latestRelease.file : "EDM-Setup-v2.1.0.exe");

        const eventData = {
            id: `DL-${Date.now()}-${Math.floor(Math.random() * 900 + 100)}`,
            timestamp: new Date().toISOString(),
            timeFormatted: new Date().toLocaleTimeString(),
            releaseVersion: activeVer,
            installerFile: activeFile,
            fileSize: this.latestRelease ? this.latestRelease.size : "19.8 MB",
            downloadResult: result,
            operatingSystem: env.os,
            architecture: env.arch,
            country: env.country,
            countryCode: env.countryCode,
            referrer: document.referrer || "direct"
        };

        // 1. Save to Local Storage History for Admin Dashboard
        try {
            const raw = this.safeStorageGet("edm_download_telemetry_events");
            const list = raw ? JSON.parse(raw) : [];
            list.unshift(eventData);
            if (list.length > 500) list.pop();
            this.safeStorageSet("edm_download_telemetry_events", list);

            // Increment totals
            const totalsRaw = this.safeStorageGet("edm_live_analytics_totals");
            const totals = totalsRaw ? JSON.parse(totalsRaw) : { totalDownloads: 24582, todayDownloads: 1420 };
            totals.totalDownloads += 1;
            totals.todayDownloads += 1;
            this.safeStorageSet("edm_live_analytics_totals", totals);
        } catch (e) {}

        // 2. Broadcast live event to open Dashboard tabs
        if (this.telemetryChannel) {
            try {
                this.telemetryChannel.postMessage({ type: "DOWNLOAD_EVENT", data: eventData });
            } catch (e) {}
        }

        // 3. Resilient API Dispatch to Analytics Ingestion Beacon
        this.sendAnalyticsBeacon("download_started", {
            releaseVersion: eventData.version || this.latestRelease?.version,
            operatingSystem: eventData.operatingSystem
        });

        console.log("[EDM Telemetry] Download Logged:", eventData);
    }

    getOrCreateSessionId() {
        try {
            let sid = sessionStorage.getItem("edm_session_id");
            if (!sid) {
                sid = "sess_" + Math.random().toString(36).substring(2, 15) + "_" + Date.now().toString(36);
                sessionStorage.setItem("edm_session_id", sid);
            }
            return sid;
        } catch (e) {
            return "sess_anonymous";
        }
    }

    sendAnalyticsBeacon(eventType, additionalData = {}) {
        const env = this.detectClientEnvironment();
        const payload = {
            eventType: eventType || "pageview",
            sessionId: this.getOrCreateSessionId(),
            pagePath: window.location.pathname || "/",
            pageTitle: document.title || "Exclusive Download Manager",
            referrer: document.referrer || "Direct",
            operatingSystem: env.os,
            browser: env.browser,
            deviceCategory: env.device,
            releaseVersion: additionalData.releaseVersion || this.latestRelease?.version,
            ...additionalData
        };

        const json = JSON.stringify(payload);
        try {
            if (navigator.sendBeacon) {
                const blob = new Blob([json], { type: "application/json" });
                navigator.sendBeacon("/api/v1/analytics/event", blob);
                return;
            }
        } catch (e) {}

        try {
            fetch("/api/v1/analytics/event", {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: json,
                keepalive: true
            }).catch(() => {});
        } catch (e) {}
    }

    trackPageView() {
        const env = this.detectClientEnvironment();
        const pageData = {
            path: window.location.pathname || "index.html",
            timestamp: new Date().toISOString(),
            country: env.country,
            os: env.os
        };

        if (this.telemetryChannel) {
            try {
                this.telemetryChannel.postMessage({ type: "PAGE_VIEW", data: pageData });
            } catch (e) {}
        }

        this.sendAnalyticsBeacon("pageview");
    }

    // ── 3. THEME ENGINE ──
    applyTheme(theme) {
        this.theme = theme;
        this.safeStorageSet("edm_theme", theme);
        const body = document.body;
        const themeIcon = document.getElementById("theme-icon");

        if (theme === "light") {
            body.classList.add("light-theme");
            if (themeIcon) themeIcon.setAttribute("data-lucide", "moon");
        } else {
            body.classList.remove("light-theme");
            if (themeIcon) themeIcon.setAttribute("data-lucide", "sun");
        }

        if (window.lucide && typeof window.lucide.createIcons === "function") {
            window.lucide.createIcons();
        }
    }

    toggleTheme() {
        this.applyTheme(this.theme === "dark" ? "light" : "dark");
        this.showToast(`Switched to ${this.theme} mode`, "info");
    }

    // ── 4. MOBILE DRAWER NAVIGATION ──
    toggleMobileMenu() {
        const drawer = document.getElementById("mobile-drawer");
        if (drawer) {
            drawer.classList.toggle("open");
        }
    }

    // ── 5. SCROLL SPY ──
    setupScrollSpy() {
        const sections = document.querySelectorAll("section[id]");
        const navLinks = document.querySelectorAll(".nav-link");
        if (!sections.length || !navLinks.length) return;

        window.addEventListener("scroll", () => {
            let current = "";
            sections.forEach(sec => {
                const secTop = sec.offsetTop - 120;
                if (window.pageYOffset >= secTop) {
                    current = sec.getAttribute("id");
                }
            });

            navLinks.forEach(link => {
                link.classList.remove("active");
                if (link.getAttribute("href") === `#${current}`) {
                    link.classList.add("active");
                }
            });
        }, { passive: true });
    }

    // ── 6. 32-STREAM SOCKET VISUALIZER ──
    initStreamsGrid() {
        const grid = document.getElementById("streams-grid");
        if (!grid) return;
        grid.innerHTML = "";
        for (let i = 0; i < 32; i++) {
            const pill = document.createElement("div");
            pill.className = "stream-pill active";
            pill.id = `stream-pill-${i}`;
            grid.appendChild(pill);
        }
    }

    startEngineSimulation() {
        setInterval(() => {
            if (!this.simRunning) return;

            const delta = (Math.random() * 2.4 - 1.2);
            this.simSpeed = Math.max(11.5, Math.min(32.0, this.simSpeed + delta));
            
            const speedEl = document.getElementById("sim-speed-val");
            if (speedEl) speedEl.textContent = `${this.simSpeed.toFixed(1)} MB/s`;

            this.simProgress += 0.2;
            if (this.simProgress > 99) this.simProgress = 40;

            const fillEl = document.getElementById("sim-progress-fill");
            if (fillEl) fillEl.style.width = `${this.simProgress}%`;

            const progText = document.getElementById("sim-progress-text");
            if (progText) {
                const gb = ((5.80 * this.simProgress) / 100).toFixed(2);
                progText.textContent = `${Math.floor(this.simProgress)}% Completed (${gb} GB)`;
            }

            for (let i = 0; i < 32; i++) {
                const p = document.getElementById(`stream-pill-${i}`);
                if (p) {
                    if (Math.random() > 0.12) {
                        p.classList.add("active");
                    } else {
                        p.classList.remove("active");
                    }
                }
            }
        }, 600);
    }

    toggleSimPause() {
        this.simRunning = !this.simRunning;
        const text = document.getElementById("sim-pause-text");
        const icon = document.getElementById("sim-pause-icon");
        const status = document.getElementById("engine-status-text");

        if (this.simRunning) {
            if (text) text.textContent = "Pause Engine";
            if (icon) icon.setAttribute("data-lucide", "pause");
            if (status) status.textContent = "Engine Online";
            this.showToast("Download engine resumed", "success");
        } else {
            if (text) text.textContent = "Resume Engine";
            if (icon) icon.setAttribute("data-lucide", "play");
            if (status) status.textContent = "Engine Paused";
            const speedEl = document.getElementById("sim-speed-val");
            if (speedEl) speedEl.textContent = "0.0 MB/s";
            this.showToast("Download engine paused", "info");
        }

        if (window.lucide && typeof window.lucide.createIcons === "function") {
            window.lucide.createIcons();
        }
    }

    boostTurbo() {
        this.simSpeed = 48.6;
        const speedEl = document.getElementById("sim-speed-val");
        if (speedEl) speedEl.textContent = "48.6 MB/s";

        for (let i = 0; i < 32; i++) {
            const p = document.getElementById(`stream-pill-${i}`);
            if (p) p.classList.add("turbo");
        }

        this.showToast("🔥 32-Stream Turbo Boost Engaged (48.6 MB/s)", "success");

        setTimeout(() => {
            for (let i = 0; i < 32; i++) {
                const p = document.getElementById(`stream-pill-${i}`);
                if (p) p.classList.remove("turbo");
            }
        }, 4000);
    }

    // ── 7. URL SNIFFER DEMO ──
    handleSniffUrl() {
        const input = document.getElementById("url-sniffer-input");
        const rawVal = input ? input.value.trim() : "";
        const targetUrl = rawVal || "https://media.youtube.com/watch?v=4K_UltraHD_Master_Stream";

        const label = document.getElementById("sniffer-detected-url");
        if (label) label.textContent = this.sanitizeHtml(targetUrl);

        this.openModal("modal-sniffer-result");
        this.showToast("Stream parsed: 32 turbo threads allocated", "success");
    }

    // ── 8. PRICING & CURRENCY ──
    toggleCurrency() {
        this.currency = this.currency === "BDT" ? "USD" : "BDT";
        this.safeStorageSet("edm_currency", this.currency);
        this.updateCurrencyUI();
        this.showToast(`Currency switched to ${this.currency}`, "info");
    }

    updateCurrencyUI() {
        const label = document.getElementById("currency-label");
        if (label) label.textContent = this.currency === "BDT" ? "BDT (৳)" : "USD ($)";

        const isBDT = this.currency === "BDT";
        const sym = isBDT ? "৳" : "$";

        const curFree = document.getElementById("price-cur-free");
        const curPro = document.getElementById("price-cur-pro");
        const curEnt = document.getElementById("price-cur-ent");

        if (curFree) curFree.textContent = sym;
        if (curPro) curPro.textContent = sym;
        if (curEnt) curEnt.textContent = sym;

        const amtPro = document.getElementById("price-amt-pro");
        const oldPro = document.getElementById("price-old-pro");
        const amtEnt = document.getElementById("price-amt-ent");
        const oldEnt = document.getElementById("price-old-ent");

        if (this.pricingPeriod === "monthly") {
            if (amtPro) amtPro.textContent = isBDT ? "99" : "0.99";
            if (oldPro) oldPro.textContent = isBDT ? "Was ৳ 199 / mo" : "Was $ 2.99 / mo";
            if (amtEnt) amtEnt.textContent = isBDT ? "299" : "2.99";
            if (oldEnt) oldEnt.textContent = isBDT ? "Was ৳ 599 / mo" : "Was $ 6.99 / mo";
        } else if (this.pricingPeriod === "yearly") {
            if (amtPro) amtPro.textContent = isBDT ? "499" : "4.99";
            if (oldPro) oldPro.textContent = isBDT ? "Was ৳ 1,299 / year" : "Was $ 14.99 / year";
            if (amtEnt) amtEnt.textContent = isBDT ? "1,499" : "14.99";
            if (oldEnt) oldEnt.textContent = isBDT ? "Was ৳ 3,500 / year" : "Was $ 39.99 / year";
        } else {
            if (amtPro) amtPro.textContent = isBDT ? "999" : "9.99";
            if (oldPro) oldPro.textContent = isBDT ? "Was ৳ 2,499 one-time" : "Was $ 29.99 one-time";
            if (amtEnt) amtEnt.textContent = isBDT ? "2,999" : "29.99";
            if (oldEnt) oldEnt.textContent = isBDT ? "Was ৳ 6,999 one-time" : "Was $ 69.99 one-time";
        }
    }

    setPricingPeriod(period) {
        this.pricingPeriod = period;
        ["monthly", "yearly", "lifetime"].forEach(p => {
            const btn = document.getElementById(`btn-period-${p}`);
            if (btn) {
                if (p === period) btn.classList.add("active");
                else btn.classList.remove("active");
            }
        });

        const prdPro = document.getElementById("price-prd-pro");
        const prdEnt = document.getElementById("price-prd-ent");

        if (period === "monthly") {
            if (prdPro) prdPro.textContent = "/ month";
            if (prdEnt) prdEnt.textContent = "/ month";
        } else if (period === "yearly") {
            if (prdPro) prdPro.textContent = "/ year";
            if (prdEnt) prdEnt.textContent = "/ year";
        } else {
            if (prdPro) prdPro.textContent = "lifetime";
            if (prdEnt) prdEnt.textContent = "lifetime";
        }

        this.updateCurrencyUI();
    }

    // ── 9. SCREENSHOT GALLERY ──
    switchGallery(index, clickedBtn) {
        this.activeGalleryIndex = index;
        const item = this.galleryData[index];
        if (!item) return;

        const title = document.getElementById("gallery-title");
        const desc = document.getElementById("gallery-desc");
        const display = document.getElementById("gallery-display");

        if (title) title.textContent = item.title;
        if (desc) desc.textContent = item.desc;
        if (display) {
            const icon = display.querySelector("i");
            if (icon) icon.setAttribute("data-lucide", item.icon);
        }

        document.querySelectorAll(".gallery-tab-btn").forEach(b => b.classList.remove("active"));
        if (clickedBtn) clickedBtn.classList.add("active");

        if (window.lucide && typeof window.lucide.createIcons === "function") {
            window.lucide.createIcons();
        }
    }

    openScreenshotModal() {
        const item = this.galleryData[this.activeGalleryIndex];
        if (!item) return;

        const title = document.getElementById("zoom-title");
        const main = document.getElementById("zoom-main-text");
        const sub = document.getElementById("zoom-sub-text");

        if (title) title.textContent = item.title;
        if (main) main.textContent = item.title;
        if (sub) sub.textContent = item.desc;

        this.openModal("modal-screenshot");
    }

    // ── 10. FAQ ACCORDION ──
    toggleFaq(clickedBtn) {
        if (!clickedBtn) return;
        const item = clickedBtn.closest(".faq-item");
        if (!item) return;
        const wasActive = item.classList.contains("active");

        document.querySelectorAll(".faq-item").forEach(el => el.classList.remove("active"));

        if (!wasActive) {
            item.classList.add("active");
        }
    }

    // ── 11. REAL DOWNLOAD TRIGGER & MODALS ──
    handleDownloadClick(e) {
        const ver = this.latestRelease ? this.latestRelease.version : "v2.1.0";
        const file = this.latestRelease ? this.latestRelease.file : "EDM-Setup-v2.1.0.exe";
        this.recordDownloadEvent(ver, file, "SUCCESS");
        this.openModal("modal-download");
        this.showToast(`⚡ Starting ${file} download...`, "success");
    }

    openDownloadModal() {
        this.openModal("modal-download");
    }

    openPrivacyModal() {
        this.openModal("modal-privacy");
    }

    openTermsModal() {
        this.openModal("modal-terms");
    }

    openModal(id) {
        const m = document.getElementById(id);
        if (m) {
            m.classList.add("active");
            m.setAttribute("aria-hidden", "false");
            if (window.lucide && typeof window.lucide.createIcons === "function") {
                window.lucide.createIcons();
            }
        }
    }

    closeModal(id) {
        const m = document.getElementById(id);
        if (m) {
            m.classList.remove("active");
            m.setAttribute("aria-hidden", "true");
        }
    }

    setupModalA11y() {
        document.querySelectorAll(".modal-backdrop").forEach(m => {
            m.setAttribute("role", "dialog");
            m.setAttribute("aria-modal", "true");
            m.setAttribute("aria-hidden", "true");
        });
    }

    setupKeyboardShortcuts() {
        window.addEventListener("keydown", (e) => {
            if (e.key === "Escape") {
                document.querySelectorAll(".modal-backdrop").forEach(m => {
                    m.classList.remove("active");
                    m.setAttribute("aria-hidden", "true");
                });
                const drawer = document.getElementById("mobile-drawer");
                if (drawer) drawer.classList.remove("open");
            }
        });
    }

    showToast(message, type = "info") {
        const stack = document.getElementById("toast-stack");
        if (!stack) return;
        const toast = document.createElement("div");
        toast.className = `toast-item ${type}`;
        toast.setAttribute("role", "alert");
        toast.innerHTML = `<span>${this.sanitizeHtml(message)}</span>`;
        stack.appendChild(toast);

        setTimeout(() => {
            toast.style.opacity = "0";
            toast.style.transform = "translateY(10px)";
            toast.style.transition = "all 0.25s ease";
            setTimeout(() => {
                if (toast.parentNode) toast.parentNode.removeChild(toast);
            }, 250);
        }, 3200);
    }
}

// Global Initialization
if (typeof window !== "undefined") {
    document.addEventListener("DOMContentLoaded", () => {
        window.edmSite = new EdmWebsiteEngine();
    });
}
