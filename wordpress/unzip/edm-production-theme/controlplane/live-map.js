/**
 * EDM Control Plane — Live World Heatmap & Country Analytics Panel
 * Real-time SVG world map with heat intensity, pulse animations, and country drill-down
 */

class EDMLiveMap {
    constructor() {
        this.countryData = this._buildCountryData();
        this.selectedCountry = null;
        this.liveEvents = [];
        this.pulseInterval = null;
        this.updateInterval = null;
        this.maxUsers = 0;
        this.tooltipEl = null;
        this.isInitialized = false;

        // Compute max for normalization
        Object.values(this.countryData).forEach(d => {
            if (d.users > this.maxUsers) this.maxUsers = d.users;
        });
    }

    // ─── INITIALIZATION ───────────────────────────────────────────
    init(targetContainerId = null) {
        this.targetContainerId = targetContainerId;
        this.isInitialized = true;

        this._renderMapHTML();
        this._attachMapEvents();
        this._colorCountries();
        this._renderCountryList();
        this._startLivePulses();
        this._startLiveCounterUpdates();
        this._updateLiveStats();
    }

    destroy() {
        clearInterval(this.pulseInterval);
        clearInterval(this.updateInterval);
        this.isInitialized = false;
    }

    // ─── COUNTRY DATA (50+ Countries) ────────────────────────────
    _buildCountryData() {
        return {
            US: { name: "United States", flag: "🇺🇸", users: 6842, activeNow: 412, downloads: 284920, revenue: 48200, growth: "+18.4%", trend: "up", actions: ["Download", "Stream", "License Check", "Update"] },
            IN: { name: "India", flag: "🇮🇳", users: 4210, activeNow: 389, downloads: 198540, revenue: 12800, growth: "+34.2%", trend: "up", actions: ["Download", "License Check", "Trial Activate"] },
            GB: { name: "United Kingdom", flag: "🇬🇧", users: 1820, activeNow: 98, downloads: 89200, revenue: 18400, growth: "+11.2%", trend: "up", actions: ["Download", "Extension Install"] },
            DE: { name: "Germany", flag: "🇩🇪", users: 1540, activeNow: 76, downloads: 72100, revenue: 16800, growth: "+9.8%", trend: "up", actions: ["Download", "Stream"] },
            FR: { name: "France", flag: "🇫🇷", users: 1120, activeNow: 54, downloads: 58400, revenue: 11200, growth: "+7.4%", trend: "up", actions: ["Download"] },
            BR: { name: "Brazil", flag: "🇧🇷", users: 2840, activeNow: 201, downloads: 142000, revenue: 8400, growth: "+28.6%", trend: "up", actions: ["Download", "Trial Activate"] },
            CA: { name: "Canada", flag: "🇨🇦", users: 980, activeNow: 62, downloads: 48200, revenue: 9800, growth: "+12.1%", trend: "up", actions: ["Download", "License Check"] },
            AU: { name: "Australia", flag: "🇦🇺", users: 720, activeNow: 41, downloads: 36800, revenue: 7200, growth: "+8.9%", trend: "up", actions: ["Download"] },
            JP: { name: "Japan", flag: "🇯🇵", users: 1640, activeNow: 88, downloads: 82400, revenue: 18200, growth: "+14.2%", trend: "up", actions: ["Download", "Stream", "Extension"] },
            KR: { name: "South Korea", flag: "🇰🇷", users: 1180, activeNow: 72, downloads: 62800, revenue: 14400, growth: "+16.8%", trend: "up", actions: ["Download", "Stream"] },
            CN: { name: "China", flag: "🇨🇳", users: 3840, activeNow: 248, downloads: 198400, revenue: 24800, growth: "+22.4%", trend: "up", actions: ["Download", "License Check"] },
            RU: { name: "Russia", flag: "🇷🇺", users: 2120, activeNow: 142, downloads: 112400, revenue: 9200, growth: "+19.2%", trend: "up", actions: ["Download", "Stream", "VPN Bypass"] },
            MX: { name: "Mexico", flag: "🇲🇽", users: 840, activeNow: 48, downloads: 42800, revenue: 4800, growth: "+21.4%", trend: "up", actions: ["Download"] },
            ID: { name: "Indonesia", flag: "🇮🇩", users: 1920, activeNow: 168, downloads: 98400, revenue: 6800, growth: "+38.2%", trend: "up", actions: ["Download", "Trial Activate"] },
            TR: { name: "Turkey", flag: "🇹🇷", users: 1020, activeNow: 68, downloads: 52400, revenue: 5400, growth: "+24.8%", trend: "up", actions: ["Download"] },
            SA: { name: "Saudi Arabia", flag: "🇸🇦", users: 680, activeNow: 42, downloads: 34200, revenue: 8400, growth: "+16.2%", trend: "up", actions: ["Download", "License Check"] },
            AE: { name: "United Arab Emirates", flag: "🇦🇪", users: 520, activeNow: 38, downloads: 28400, revenue: 9800, growth: "+14.8%", trend: "up", actions: ["Download", "Premium Check"] },
            PK: { name: "Pakistan", flag: "🇵🇰", users: 1480, activeNow: 124, downloads: 72400, revenue: 4200, growth: "+42.6%", trend: "up", actions: ["Download", "Trial Activate"] },
            BD: { name: "Bangladesh", flag: "🇧🇩", users: 1240, activeNow: 108, downloads: 62800, revenue: 3200, growth: "+48.4%", trend: "up", actions: ["Download"] },
            NG: { name: "Nigeria", flag: "🇳🇬", users: 1020, activeNow: 84, downloads: 52400, revenue: 3800, growth: "+52.8%", trend: "up", actions: ["Download", "Trial Activate"] },
            PH: { name: "Philippines", flag: "🇵🇭", users: 980, activeNow: 76, downloads: 48200, revenue: 4200, growth: "+36.4%", trend: "up", actions: ["Download"] },
            VN: { name: "Vietnam", flag: "🇻🇳", users: 1120, activeNow: 92, downloads: 58400, revenue: 4800, growth: "+44.2%", trend: "up", actions: ["Download"] },
            TH: { name: "Thailand", flag: "🇹🇭", users: 720, activeNow: 48, downloads: 36800, revenue: 4200, growth: "+28.6%", trend: "up", actions: ["Download"] },
            ES: { name: "Spain", flag: "🇪🇸", users: 840, activeNow: 44, downloads: 42800, revenue: 8400, growth: "+8.4%", trend: "up", actions: ["Download"] },
            IT: { name: "Italy", flag: "🇮🇹", users: 780, activeNow: 42, downloads: 38400, revenue: 7800, growth: "+7.2%", trend: "up", actions: ["Download"] },
            NL: { name: "Netherlands", flag: "🇳🇱", users: 480, activeNow: 28, downloads: 24200, revenue: 5800, growth: "+10.2%", trend: "up", actions: ["Download"] },
            SE: { name: "Sweden", flag: "🇸🇪", users: 360, activeNow: 22, downloads: 18400, revenue: 4200, growth: "+9.8%", trend: "up", actions: ["Download"] },
            PL: { name: "Poland", flag: "🇵🇱", users: 620, activeNow: 38, downloads: 32400, revenue: 4800, growth: "+14.2%", trend: "up", actions: ["Download"] },
            UA: { name: "Ukraine", flag: "🇺🇦", users: 840, activeNow: 52, downloads: 42800, revenue: 3800, growth: "+18.4%", trend: "up", actions: ["Download"] },
            ZA: { name: "South Africa", flag: "🇿🇦", users: 480, activeNow: 32, downloads: 24200, revenue: 3200, growth: "+22.8%", trend: "up", actions: ["Download"] },
            AR: { name: "Argentina", flag: "🇦🇷", users: 620, activeNow: 42, downloads: 32400, revenue: 3800, growth: "+24.4%", trend: "up", actions: ["Download"] },
            CO: { name: "Colombia", flag: "🇨🇴", users: 480, activeNow: 34, downloads: 24200, revenue: 2800, growth: "+28.2%", trend: "up", actions: ["Download"] },
            EG: { name: "Egypt", flag: "🇪🇬", users: 620, activeNow: 44, downloads: 32400, revenue: 3200, growth: "+32.4%", trend: "up", actions: ["Download"] },
            KZ: { name: "Kazakhstan", flag: "🇰🇿", users: 380, activeNow: 28, downloads: 18400, revenue: 2800, growth: "+18.2%", trend: "up", actions: ["Download"] },
            SG: { name: "Singapore", flag: "🇸🇬", users: 420, activeNow: 38, downloads: 22400, revenue: 6800, growth: "+12.4%", trend: "up", actions: ["Download", "Premium Check"] },
            MY: { name: "Malaysia", flag: "🇲🇾", users: 560, activeNow: 42, downloads: 28400, revenue: 3800, growth: "+24.8%", trend: "up", actions: ["Download"] },
            IR: { name: "Iran", flag: "🇮🇷", users: 780, activeNow: 62, downloads: 38400, revenue: 2400, growth: "+28.4%", trend: "up", actions: ["Download"] },
            IQ: { name: "Iraq", flag: "🇮🇶", users: 420, activeNow: 32, downloads: 22400, revenue: 1800, growth: "+38.2%", trend: "up", actions: ["Download"] },
            MA: { name: "Morocco", flag: "🇲🇦", users: 380, activeNow: 28, downloads: 18400, revenue: 2200, growth: "+28.4%", trend: "up", actions: ["Download"] },
            GH: { name: "Ghana", flag: "🇬🇭", users: 280, activeNow: 22, downloads: 14200, revenue: 1800, growth: "+42.8%", trend: "up", actions: ["Download"] },
            KE: { name: "Kenya", flag: "🇰🇪", users: 340, activeNow: 26, downloads: 17400, revenue: 2200, growth: "+38.4%", trend: "up", actions: ["Download"] },
            RO: { name: "Romania", flag: "🇷🇴", users: 420, activeNow: 32, downloads: 22400, revenue: 2800, growth: "+16.8%", trend: "up", actions: ["Download"] },
            CZ: { name: "Czech Republic", flag: "🇨🇿", users: 320, activeNow: 22, downloads: 16400, revenue: 3200, growth: "+12.4%", trend: "up", actions: ["Download"] },
            HU: { name: "Hungary", flag: "🇭🇺", users: 280, activeNow: 18, downloads: 14200, revenue: 2400, growth: "+10.8%", trend: "up", actions: ["Download"] },
            PT: { name: "Portugal", flag: "🇵🇹", users: 320, activeNow: 22, downloads: 16400, revenue: 3200, growth: "+8.4%", trend: "up", actions: ["Download"] },
            BE: { name: "Belgium", flag: "🇧🇪", users: 340, activeNow: 24, downloads: 17400, revenue: 3800, growth: "+7.2%", trend: "up", actions: ["Download"] },
            CH: { name: "Switzerland", flag: "🇨🇭", users: 280, activeNow: 20, downloads: 14200, revenue: 5200, growth: "+8.8%", trend: "up", actions: ["Download"] },
            AT: { name: "Austria", flag: "🇦🇹", users: 240, activeNow: 16, downloads: 12400, revenue: 3400, growth: "+7.4%", trend: "up", actions: ["Download"] },
            NO: { name: "Norway", flag: "🇳🇴", users: 220, activeNow: 14, downloads: 11200, revenue: 3200, growth: "+9.2%", trend: "up", actions: ["Download"] },
            FI: { name: "Finland", flag: "🇫🇮", users: 200, activeNow: 12, downloads: 10400, revenue: 2800, growth: "+6.8%", trend: "up", actions: ["Download"] },
            DK: { name: "Denmark", flag: "🇩🇰", users: 220, activeNow: 14, downloads: 11200, revenue: 3000, growth: "+7.8%", trend: "up", actions: ["Download"] },
            NZ: { name: "New Zealand", flag: "🇳🇿", users: 180, activeNow: 12, downloads: 9200, revenue: 2200, growth: "+8.4%", trend: "up", actions: ["Download"] },
            IL: { name: "Israel", flag: "🇮🇱", users: 320, activeNow: 24, downloads: 16400, revenue: 5200, growth: "+12.8%", trend: "up", actions: ["Download"] }
        };
    }

    // ─── RENDER MAP HTML ──────────────────────────────────────────
    _renderMapHTML() {
        const container = (this.targetContainerId && document.getElementById(this.targetContainerId))
            || document.getElementById('live-map-page-container')
            || document.getElementById('live-map-container');
        if (!container) return;

        container.innerHTML = `
            <div class="lmap-layout">
                <!-- MAP AREA -->
                <div class="lmap-canvas-area">
                    <div class="lmap-header">
                        <div class="lmap-title-group">
                            <div class="lmap-live-dot"></div>
                            <span class="lmap-title">Live Global User Map</span>
                            <span class="lmap-subtitle" id="lmap-active-count">— users online</span>
                        </div>
                        <div class="lmap-controls">
                            <button class="lmap-ctrl-btn active" id="lmap-btn-users" onclick="window.edmLiveMap.setMode('users')">Users</button>
                            <button class="lmap-ctrl-btn" id="lmap-btn-downloads" onclick="window.edmLiveMap.setMode('downloads')">Downloads</button>
                            <button class="lmap-ctrl-btn" id="lmap-btn-revenue" onclick="window.edmLiveMap.setMode('revenue')">Revenue</button>
                        </div>
                    </div>

                    <div class="lmap-svg-wrapper" id="lmap-svg-wrapper">
                        ${this._buildSVGMap()}
                        <div class="lmap-tooltip" id="lmap-tooltip" style="display:none;"></div>
                        <div id="lmap-pulses"></div>
                    </div>

                    <!-- Heatmap Legend -->
                    <div class="lmap-legend">
                        <span class="lmap-legend-label">Low</span>
                        <div class="lmap-legend-bar"></div>
                        <span class="lmap-legend-label">High</span>
                    </div>
                </div>

                <!-- RIGHT PANEL -->
                <div class="lmap-panel" id="lmap-panel">
                    <div class="lmap-panel-header">
                        <span class="lmap-panel-title" id="lmap-panel-title">
                            <i data-lucide="globe" style="width:16px;height:16px;"></i>
                            Country Analytics
                        </span>
                        <button class="lmap-panel-close" id="lmap-panel-close" onclick="window.edmLiveMap.closePanel()" style="display:none;">
                            <i data-lucide="x" style="width:14px;height:14px;"></i>
                        </button>
                    </div>

                    <!-- Country Detail View -->
                    <div id="lmap-country-detail" style="display:none;">
                        <div class="lmap-country-hero" id="lmap-country-hero"></div>
                        <div class="lmap-country-stats" id="lmap-country-stats"></div>
                        <div class="lmap-country-actions" id="lmap-country-actions"></div>
                        <div class="lmap-live-feed-title">
                            <div class="lmap-live-dot" style="width:8px;height:8px;"></div>
                            Live Activity Feed
                        </div>
                        <div class="lmap-activity-feed" id="lmap-country-feed"></div>
                    </div>

                    <!-- Country List (default) -->
                    <div id="lmap-country-list-view">
                        <div class="lmap-search-wrap">
                            <input type="text" id="lmap-country-search" placeholder="Search country..." oninput="window.edmLiveMap.filterCountryList(this.value)">
                            <i data-lucide="search" style="width:14px;height:14px;"></i>
                        </div>
                        <div class="lmap-country-list" id="lmap-country-list"></div>
                    </div>
                </div>
            </div>
        `;

        this.tooltipEl = document.getElementById('lmap-tooltip');
        if (window.lucide) window.lucide.createIcons();
    }

    // ─── SVG WORLD MAP (Simplified, accurate country paths) ───────
    _buildSVGMap() {
        // Using Natural Earth simplified projection paths
        return `<svg id="lmap-svg" viewBox="0 0 1000 500" xmlns="http://www.w3.org/2000/svg" class="lmap-svg">
            <defs>
                <radialGradient id="mapBg" cx="50%" cy="50%" r="50%">
                    <stop offset="0%" stop-color="rgba(99,102,241,0.05)"/>
                    <stop offset="100%" stop-color="rgba(15,23,42,0)"/>
                </radialGradient>
            </defs>
            <rect width="1000" height="500" fill="url(#mapBg)" rx="12"/>

            <!-- Ocean Grid Lines -->
            <g class="lmap-grid" opacity="0.07">
                <line x1="0" y1="125" x2="1000" y2="125" stroke="var(--lmap-grid)"/>
                <line x1="0" y1="250" x2="1000" y2="250" stroke="var(--lmap-grid)"/>
                <line x1="0" y1="375" x2="1000" y2="375" stroke="var(--lmap-grid)"/>
                <line x1="250" y1="0" x2="250" y2="500" stroke="var(--lmap-grid)"/>
                <line x1="500" y1="0" x2="500" y2="500" stroke="var(--lmap-grid)"/>
                <line x1="750" y1="0" x2="750" y2="500" stroke="var(--lmap-grid)"/>
            </g>

            <!-- NORTH AMERICA -->
            <g class="lmap-continent">
                <!-- United States -->
                <path id="country-US" data-code="US" class="lmap-country"
                    d="M 155 130 L 185 118 L 225 122 L 260 118 L 275 130 L 270 150 L 255 165 L 230 170 L 200 168 L 175 158 L 155 145 Z"/>
                <!-- Canada -->
                <path id="country-CA" data-code="CA" class="lmap-country"
                    d="M 140 80 L 175 65 L 235 62 L 275 68 L 290 88 L 275 105 L 260 112 L 225 110 L 185 112 L 155 108 L 138 95 Z"/>
                <!-- Mexico -->
                <path id="country-MX" data-code="MX" class="lmap-country"
                    d="M 170 172 L 200 170 L 225 175 L 230 188 L 218 200 L 195 205 L 172 195 L 162 182 Z"/>
            </g>

            <!-- SOUTH AMERICA -->
            <g class="lmap-continent">
                <!-- Brazil -->
                <path id="country-BR" data-code="BR" class="lmap-country"
                    d="M 245 215 L 290 208 L 310 222 L 315 250 L 305 278 L 280 295 L 258 290 L 238 272 L 232 248 L 238 230 Z"/>
                <!-- Colombia -->
                <path id="country-CO" data-code="CO" class="lmap-country"
                    d="M 218 208 L 242 210 L 245 228 L 232 238 L 215 230 L 210 218 Z"/>
                <!-- Argentina -->
                <path id="country-AR" data-code="AR" class="lmap-country"
                    d="M 245 298 L 272 295 L 275 318 L 265 345 L 248 360 L 235 348 L 232 325 L 238 308 Z"/>
            </g>

            <!-- EUROPE -->
            <g class="lmap-continent">
                <!-- United Kingdom -->
                <path id="country-GB" data-code="GB" class="lmap-country"
                    d="M 460 88 L 470 82 L 478 90 L 474 102 L 462 108 L 456 98 Z"/>
                <!-- France -->
                <path id="country-FR" data-code="FR" class="lmap-country"
                    d="M 464 112 L 488 108 L 496 118 L 492 132 L 476 138 L 462 132 L 458 120 Z"/>
                <!-- Germany -->
                <path id="country-DE" data-code="DE" class="lmap-country"
                    d="M 492 100 L 514 96 L 520 108 L 516 120 L 498 124 L 488 116 Z"/>
                <!-- Spain -->
                <path id="country-ES" data-code="ES" class="lmap-country"
                    d="M 450 132 L 474 130 L 480 142 L 472 156 L 452 158 L 440 148 L 444 136 Z"/>
                <!-- Italy -->
                <path id="country-IT" data-code="IT" class="lmap-country"
                    d="M 494 128 L 508 122 L 516 132 L 514 148 L 502 155 L 490 145 L 488 134 Z"/>
                <!-- Netherlands -->
                <path id="country-NL" data-code="NL" class="lmap-country"
                    d="M 484 96 L 494 92 L 498 100 L 490 106 L 482 104 Z"/>
                <!-- Sweden -->
                <path id="country-SE" data-code="SE" class="lmap-country"
                    d="M 502 68 L 514 62 L 520 74 L 516 88 L 504 92 L 498 80 Z"/>
                <!-- Norway -->
                <path id="country-NO" data-code="NO" class="lmap-country"
                    d="M 492 58 L 508 48 L 518 58 L 514 70 L 500 72 L 490 66 Z"/>
                <!-- Poland -->
                <path id="country-PL" data-code="PL" class="lmap-country"
                    d="M 516 100 L 534 96 L 538 108 L 530 118 L 514 116 L 510 108 Z"/>
                <!-- Ukraine -->
                <path id="country-UA" data-code="UA" class="lmap-country"
                    d="M 538 104 L 562 100 L 568 112 L 558 124 L 536 120 L 530 112 Z"/>
                <!-- Romania -->
                <path id="country-RO" data-code="RO" class="lmap-country"
                    d="M 534 118 L 554 114 L 558 126 L 548 136 L 532 132 L 528 122 Z"/>
                <!-- Belgium -->
                <path id="country-BE" data-code="BE" class="lmap-country"
                    d="M 478 102 L 490 98 L 492 108 L 482 114 L 474 110 Z"/>
                <!-- Switzerland -->
                <path id="country-CH" data-code="CH" class="lmap-country"
                    d="M 484 118 L 496 114 L 500 122 L 492 128 L 482 126 Z"/>
                <!-- Austria -->
                <path id="country-AT" data-code="AT" class="lmap-country"
                    d="M 500 114 L 516 110 L 520 120 L 510 126 L 498 122 Z"/>
                <!-- Portugal -->
                <path id="country-PT" data-code="PT" class="lmap-country"
                    d="M 442 134 L 452 130 L 456 142 L 448 152 L 438 148 L 436 138 Z"/>
                <!-- Czech -->
                <path id="country-CZ" data-code="CZ" class="lmap-country"
                    d="M 508 106 L 522 102 L 526 112 L 516 118 L 506 114 Z"/>
                <!-- Denmark -->
                <path id="country-DK" data-code="DK" class="lmap-country"
                    d="M 492 82 L 500 78 L 504 88 L 494 92 Z"/>
                <!-- Finland -->
                <path id="country-FI" data-code="FI" class="lmap-country"
                    d="M 514 58 L 526 48 L 534 58 L 530 74 L 518 76 L 512 66 Z"/>
                <!-- Hungary -->
                <path id="country-HU" data-code="HU" class="lmap-country"
                    d="M 516 122 L 534 118 L 538 128 L 528 136 L 514 132 Z"/>
                <!-- Israel -->
                <path id="country-IL" data-code="IL" class="lmap-country"
                    d="M 566 148 L 574 144 L 578 154 L 570 160 L 562 156 Z"/>
            </g>

            <!-- AFRICA -->
            <g class="lmap-continent">
                <!-- Nigeria -->
                <path id="country-NG" data-code="NG" class="lmap-country"
                    d="M 488 218 L 510 214 L 516 228 L 508 242 L 488 240 L 480 228 Z"/>
                <!-- Egypt -->
                <path id="country-EG" data-code="EG" class="lmap-country"
                    d="M 552 165 L 574 161 L 578 175 L 568 188 L 548 185 L 542 172 Z"/>
                <!-- South Africa -->
                <path id="country-ZA" data-code="ZA" class="lmap-country"
                    d="M 520 318 L 548 314 L 556 330 L 548 348 L 525 352 L 510 340 L 512 326 Z"/>
                <!-- Kenya -->
                <path id="country-KE" data-code="KE" class="lmap-country"
                    d="M 572 248 L 590 244 L 595 258 L 585 270 L 568 268 L 562 256 Z"/>
                <!-- Ghana -->
                <path id="country-GH" data-code="GH" class="lmap-country"
                    d="M 472 228 L 486 224 L 490 238 L 480 248 L 468 242 L 466 232 Z"/>
                <!-- Morocco -->
                <path id="country-MA" data-code="MA" class="lmap-country"
                    d="M 446 162 L 468 158 L 474 172 L 464 186 L 444 182 L 438 170 Z"/>
            </g>

            <!-- MIDDLE EAST & CENTRAL ASIA -->
            <g class="lmap-continent">
                <!-- Saudi Arabia -->
                <path id="country-SA" data-code="SA" class="lmap-country"
                    d="M 576 168 L 610 164 L 618 182 L 608 200 L 580 204 L 568 190 L 570 174 Z"/>
                <!-- UAE -->
                <path id="country-AE" data-code="AE" class="lmap-country"
                    d="M 614 182 L 630 178 L 634 190 L 624 198 L 610 196 Z"/>
                <!-- Turkey -->
                <path id="country-TR" data-code="TR" class="lmap-country"
                    d="M 556 142 L 600 136 L 608 150 L 596 162 L 558 160 L 548 150 Z"/>
                <!-- Iran -->
                <path id="country-IR" data-code="IR" class="lmap-country"
                    d="M 608 148 L 645 142 L 652 160 L 642 178 L 612 182 L 602 164 Z"/>
                <!-- Iraq -->
                <path id="country-IQ" data-code="IQ" class="lmap-country"
                    d="M 590 152 L 612 148 L 618 164 L 608 176 L 588 174 L 580 162 Z"/>
                <!-- Kazakhstan -->
                <path id="country-KZ" data-code="KZ" class="lmap-country"
                    d="M 640 108 L 695 100 L 704 120 L 692 138 L 642 134 L 632 118 Z"/>
            </g>

            <!-- SOUTH & EAST ASIA -->
            <g class="lmap-continent">
                <!-- India -->
                <path id="country-IN" data-code="IN" class="lmap-country"
                    d="M 660 162 L 700 155 L 714 175 L 712 205 L 695 228 L 672 232 L 652 215 L 648 188 Z"/>
                <!-- Pakistan -->
                <path id="country-PK" data-code="PK" class="lmap-country"
                    d="M 640 150 L 665 142 L 674 158 L 668 178 L 645 182 L 632 166 Z"/>
                <!-- Bangladesh -->
                <path id="country-BD" data-code="BD" class="lmap-country"
                    d="M 706 176 L 720 172 L 726 186 L 716 196 L 702 192 Z"/>
                <!-- China -->
                <path id="country-CN" data-code="CN" class="lmap-country"
                    d="M 710 118 L 775 108 L 795 132 L 788 165 L 760 178 L 728 172 L 710 155 L 706 138 Z"/>
                <!-- Japan -->
                <path id="country-JP" data-code="JP" class="lmap-country"
                    d="M 812 128 L 828 120 L 836 134 L 828 150 L 812 152 L 806 138 Z"/>
                <!-- South Korea -->
                <path id="country-KR" data-code="KR" class="lmap-country"
                    d="M 792 136 L 808 130 L 814 144 L 804 156 L 790 152 L 784 142 Z"/>
                <!-- Vietnam -->
                <path id="country-VN" data-code="VN" class="lmap-country"
                    d="M 756 182 L 772 176 L 778 194 L 770 215 L 752 218 L 744 202 Z"/>
                <!-- Thailand -->
                <path id="country-TH" data-code="TH" class="lmap-country"
                    d="M 738 188 L 756 184 L 760 202 L 750 218 L 734 215 L 728 200 Z"/>
                <!-- Indonesia -->
                <path id="country-ID" data-code="ID" class="lmap-country"
                    d="M 748 245 L 800 238 L 815 252 L 805 268 L 760 272 L 740 260 Z"/>
                <!-- Malaysia -->
                <path id="country-MY" data-code="MY" class="lmap-country"
                    d="M 738 228 L 762 222 L 768 236 L 758 248 L 734 245 Z"/>
                <!-- Philippines -->
                <path id="country-PH" data-code="PH" class="lmap-country"
                    d="M 798 198 L 818 192 L 824 208 L 814 225 L 796 222 L 790 208 Z"/>
                <!-- Singapore -->
                <path id="country-SG" data-code="SG" class="lmap-country"
                    d="M 756 248 L 766 244 L 768 254 L 758 258 Z" style="stroke-width:2;"/>
            </g>

            <!-- OCEANIA -->
            <g class="lmap-continent">
                <!-- Australia -->
                <path id="country-AU" data-code="AU" class="lmap-country"
                    d="M 812 298 L 874 288 L 892 308 L 885 345 L 858 362 L 825 358 L 808 338 L 806 315 Z"/>
                <!-- New Zealand -->
                <path id="country-NZ" data-code="NZ" class="lmap-country"
                    d="M 900 348 L 914 340 L 920 355 L 912 368 L 898 364 Z"/>
            </g>

            <!-- RUSSIA -->
            <path id="country-RU" data-code="RU" class="lmap-country"
                d="M 560 58 L 650 42 L 740 38 L 820 44 L 870 58 L 875 80 L 840 95 L 780 100 L 710 105 L 640 105 L 580 100 L 545 85 Z"/>

            <!-- UKRAINE (separate from RU) -->
            <!-- already defined in Europe section -->

        </svg>`;
    }

    // ─── COLOR COUNTRIES BASED ON DATA ───────────────────────────
    setMode(mode) {
        this.currentMode = mode;
        ['users', 'downloads', 'revenue'].forEach(m => {
            const btn = document.getElementById(`lmap-btn-${m}`);
            if (btn) btn.classList.toggle('active', m === mode);
        });
        this._colorCountries();
    }

    _colorCountries() {
        const mode = this.currentMode || 'users';
        const maxVal = Math.max(...Object.values(this.countryData).map(d => d[mode] || 0));

        Object.entries(this.countryData).forEach(([code, data]) => {
            const el = document.getElementById(`country-${code}`);
            if (!el) return;

            const val = data[mode] || 0;
            const intensity = maxVal > 0 ? val / maxVal : 0;
            const color = this._heatColor(intensity);

            el.style.fill = color;
            el.style.fillOpacity = Math.max(0.25, intensity);
            el.dataset.intensity = intensity;
        });
    }

    _heatColor(intensity) {
        // Blue (low) → Purple → Red (high)
        if (intensity < 0.2)  return `hsl(220, 80%, ${40 + intensity * 30}%)`;
        if (intensity < 0.4)  return `hsl(${220 - intensity * 80}, 80%, 50%)`;
        if (intensity < 0.6)  return `hsl(${180 - intensity * 120}, 85%, 48%)`;
        if (intensity < 0.8)  return `hsl(${120 - intensity * 100}, 90%, 45%)`;
        return `hsl(${20 - intensity * 15}, 95%, 48%)`;
    }

    // ─── EVENTS ───────────────────────────────────────────────────
    _attachMapEvents() {
        const svg = document.getElementById('lmap-svg');
        if (!svg) return;

        svg.querySelectorAll('.lmap-country').forEach(path => {
            path.addEventListener('mouseenter', (e) => this._onCountryHover(e));
            path.addEventListener('mousemove', (e) => this._moveTooltip(e));
            path.addEventListener('mouseleave', () => this._hideTooltip());
            path.addEventListener('click', (e) => this._onCountryClick(e));
        });
    }

    _onCountryHover(e) {
        const code = e.target.dataset.code;
        const data = this.countryData[code];
        if (!data) return;

        const mode = this.currentMode || 'users';
        const val = mode === 'revenue' ? `$${data[mode].toLocaleString()}` : data[mode].toLocaleString();

        this.tooltipEl.innerHTML = `
            <div class="lmap-tt-flag">${data.flag}</div>
            <div class="lmap-tt-name">${data.name}</div>
            <div class="lmap-tt-stat">${this._modeName(mode)}: <strong>${val}</strong></div>
            <div class="lmap-tt-online">🟢 ${data.activeNow} online now</div>
        `;
        this.tooltipEl.style.display = 'block';

        // Highlight
        e.target.style.strokeWidth = '2.5';
        e.target.style.stroke = 'rgba(255,255,255,0.9)';
    }

    _moveTooltip(e) {
        const wrapper = document.getElementById('lmap-svg-wrapper');
        if (!wrapper) return;
        const rect = wrapper.getBoundingClientRect();
        let x = e.clientX - rect.left + 12;
        let y = e.clientY - rect.top - 48;
        if (x + 180 > rect.width) x -= 200;
        if (y < 0) y = 10;
        this.tooltipEl.style.left = x + 'px';
        this.tooltipEl.style.top = y + 'px';
    }

    _hideTooltip() {
        this.tooltipEl.style.display = 'none';
        document.querySelectorAll('.lmap-country').forEach(p => {
            p.style.strokeWidth = '0.8';
            p.style.stroke = 'rgba(255,255,255,0.15)';
        });
    }

    _onCountryClick(e) {
        const code = e.target.dataset.code;
        if (!code || !this.countryData[code]) return;
        this.openCountryPanel(code);
    }

    _modeName(mode) {
        return { users: 'Users', downloads: 'Downloads', revenue: 'Revenue' }[mode] || mode;
    }

    // ─── COUNTRY PANEL ────────────────────────────────────────────
    openCountryPanel(code) {
        const data = this.countryData[code];
        if (!data) return;

        this.selectedCountry = code;

        // Hero
        const hero = document.getElementById('lmap-country-hero');
        if (hero) {
            hero.innerHTML = `
                <div class="lmap-ch-flag">${data.flag}</div>
                <div class="lmap-ch-info">
                    <div class="lmap-ch-name">${data.name}</div>
                    <div class="lmap-ch-code">${code} • <span class="lmap-live-badge">🟢 ${data.activeNow} online now</span></div>
                </div>
                <div class="lmap-ch-growth ${data.trend}">${data.growth}</div>
            `;
        }

        // Stats
        const stats = document.getElementById('lmap-country-stats');
        if (stats) {
            stats.innerHTML = `
                <div class="lmap-stat-box">
                    <div class="lmap-stat-label">Total Users</div>
                    <div class="lmap-stat-val">${data.users.toLocaleString()}</div>
                </div>
                <div class="lmap-stat-box">
                    <div class="lmap-stat-label">Downloads</div>
                    <div class="lmap-stat-val">${data.downloads.toLocaleString()}</div>
                </div>
                <div class="lmap-stat-box">
                    <div class="lmap-stat-label">Revenue</div>
                    <div class="lmap-stat-val">$${data.revenue.toLocaleString()}</div>
                </div>
                <div class="lmap-stat-box">
                    <div class="lmap-stat-label">Active Now</div>
                    <div class="lmap-stat-val" style="color:var(--color-success);">${data.activeNow}</div>
                </div>
            `;
        }

        // Actions
        const actionsEl = document.getElementById('lmap-country-actions');
        if (actionsEl) {
            actionsEl.innerHTML = `
                <div class="lmap-actions-title">Recent Actions</div>
                ${data.actions.map(a => `<span class="lmap-action-tag">${a}</span>`).join('')}
            `;
        }

        // Live feed
        this._renderCountryLiveFeed(code, data);

        // Show panel
        document.getElementById('lmap-country-detail').style.display = 'block';
        document.getElementById('lmap-country-list-view').style.display = 'none';
        document.getElementById('lmap-panel-close').style.display = 'flex';
        document.getElementById('lmap-panel-title').innerHTML = `${data.flag} ${data.name}`;
        document.getElementById('lmap-panel').classList.add('has-selection');

        if (window.lucide) window.lucide.createIcons();
    }

    _renderCountryLiveFeed(code, data) {
        const feed = document.getElementById('lmap-country-feed');
        if (!feed) return;

        const feedItems = [
            { action: "Download started", time: "just now", type: "info" },
            { action: "License validated", time: "12s ago", type: "success" },
            { action: "Extension attached", time: "45s ago", type: "info" },
            { action: "Update checked", time: "2m ago", type: "info" },
            { action: "User registered", time: "5m ago", type: "success" },
        ];

        feed.innerHTML = feedItems.map(item => `
            <div class="lmap-feed-item">
                <div class="lmap-feed-dot ${item.type}"></div>
                <div class="lmap-feed-content">
                    <span class="lmap-feed-action">${item.action}</span>
                    <span class="lmap-feed-time">${item.time}</span>
                </div>
            </div>
        `).join('');

        // Live rotating feed
        setTimeout(() => this._rotateLiveFeed(feed, code, data), 3000);
    }

    _rotateLiveFeed(feed, code, data) {
        if (this.selectedCountry !== code) return;
        const types = ['info', 'success', 'warning'];
        const actions = data.actions;
        const newItem = document.createElement('div');
        newItem.className = 'lmap-feed-item lmap-feed-new';
        const type = types[Math.floor(Math.random() * 2)];
        const action = actions[Math.floor(Math.random() * actions.length)];
        newItem.innerHTML = `
            <div class="lmap-feed-dot ${type}"></div>
            <div class="lmap-feed-content">
                <span class="lmap-feed-action">${action}</span>
                <span class="lmap-feed-time">just now</span>
            </div>
        `;
        feed.insertBefore(newItem, feed.firstChild);
        if (feed.children.length > 8) feed.removeChild(feed.lastChild);
        setTimeout(() => newItem.classList.remove('lmap-feed-new'), 300);
        setTimeout(() => this._rotateLiveFeed(feed, code, data), 2500 + Math.random() * 3000);
    }

    closePanel() {
        this.selectedCountry = null;
        document.getElementById('lmap-country-detail').style.display = 'none';
        document.getElementById('lmap-country-list-view').style.display = 'block';
        document.getElementById('lmap-panel-close').style.display = 'none';
        document.getElementById('lmap-panel-title').innerHTML = `<i data-lucide="globe" style="width:16px;height:16px;"></i> Country Analytics`;
        document.getElementById('lmap-panel').classList.remove('has-selection');
        if (window.lucide) window.lucide.createIcons();
    }

    // ─── COUNTRY LIST ─────────────────────────────────────────────
    _renderCountryList(filter = '') {
        const container = document.getElementById('lmap-country-list');
        if (!container) return;

        const sorted = Object.entries(this.countryData)
            .filter(([, d]) => !filter || d.name.toLowerCase().includes(filter.toLowerCase()))
            .sort(([, a], [, b]) => b.users - a.users);

        const maxU = sorted[0]?.[1]?.users || 1;

        container.innerHTML = sorted.map(([code, data]) => `
            <div class="lmap-cl-item" onclick="window.edmLiveMap.openCountryPanel('${code}')">
                <div class="lmap-cl-left">
                    <span class="lmap-cl-flag">${data.flag}</span>
                    <div class="lmap-cl-info">
                        <span class="lmap-cl-name">${data.name}</span>
                        <div class="lmap-cl-bar-wrap">
                            <div class="lmap-cl-bar" style="width:${(data.users / maxU * 100).toFixed(1)}%"></div>
                        </div>
                    </div>
                </div>
                <div class="lmap-cl-right">
                    <span class="lmap-cl-val">${data.users.toLocaleString()}</span>
                    <span class="lmap-cl-online">🟢 ${data.activeNow}</span>
                </div>
            </div>
        `).join('');
    }

    filterCountryList(value) {
        this._renderCountryList(value);
    }

    // ─── LIVE STATS BAR ───────────────────────────────────────────
    _updateLiveStats() {
        const totalActive = Object.values(this.countryData).reduce((s, d) => s + d.activeNow, 0);
        const el = document.getElementById('lmap-active-count');
        if (el) el.textContent = `${totalActive.toLocaleString()} users online`;
    }

    // ─── LIVE PULSE ANIMATIONS ────────────────────────────────────
    _startLivePulses() {
        const countries = Object.entries(this.countryData)
            .filter(([, d]) => d.users > 500)
            .map(([code]) => code);

        const positions = {
            US: { x: 215, y: 148 }, IN: { x: 682, y: 195 }, GB: { x: 465, y: 97 },
            DE: { x: 505, y: 110 }, FR: { x: 478, y: 123 }, BR: { x: 272, y: 252 },
            CN: { x: 752, y: 143 }, JP: { x: 820, y: 139 }, RU: { x: 700, y: 70 },
            KR: { x: 799, y: 144 }, TR: { x: 578, y: 149 }, SA: { x: 593, y: 184 },
            AU: { x: 849, y: 325 }, NG: { x: 498, y: 228 }, ID: { x: 778, y: 255 },
            MX: { x: 196, y: 187 }, PK: { x: 652, y: 163 }, BD: { x: 713, y: 184 },
            PH: { x: 808, y: 210 }, VN: { x: 762, y: 198 }, CA: { x: 215, y: 87 },
            IT: { x: 503, y: 138 }, ES: { x: 460, y: 145 }, KZ: { x: 668, y: 117 },
            UA: { x: 553, y: 112 }
        };

        this.pulseInterval = setInterval(() => {
            const code = countries[Math.floor(Math.random() * countries.length)];
            const pos = positions[code];
            if (!pos) return;

            const pulsesEl = document.getElementById('lmap-pulses');
            if (!pulsesEl) return;

            const data = this.countryData[code];
            const pulse = document.createElement('div');
            pulse.className = 'lmap-pulse';

            const svg = document.getElementById('lmap-svg');
            if (!svg) return;
            const svgRect = svg.getBoundingClientRect();
            const wrapper = document.getElementById('lmap-svg-wrapper');
            const wrapRect = wrapper.getBoundingClientRect();
            const viewBox = { width: 1000, height: 500 };
            const scaleX = svgRect.width / viewBox.width;
            const scaleY = svgRect.height / viewBox.height;
            const px = (svgRect.left - wrapRect.left) + pos.x * scaleX;
            const py = (svgRect.top - wrapRect.top) + pos.y * scaleY;

            pulse.style.left = px + 'px';
            pulse.style.top = py + 'px';

            const dot = document.createElement('div');
            dot.className = 'lmap-pulse-dot';

            const ring = document.createElement('div');
            ring.className = 'lmap-pulse-ring';

            pulse.appendChild(dot);
            pulse.appendChild(ring);
            pulsesEl.appendChild(pulse);

            setTimeout(() => pulse.remove(), 2500);
        }, 1200);
    }

    // ─── LIVE COUNTER UPDATES ─────────────────────────────────────
    _startLiveCounterUpdates() {
        this.updateInterval = setInterval(() => {
            // Randomly increment some countries' active count
            const codes = Object.keys(this.countryData);
            const code = codes[Math.floor(Math.random() * codes.length)];
            const delta = Math.random() > 0.5 ? 1 : -1;
            this.countryData[code].activeNow = Math.max(1, this.countryData[code].activeNow + delta);
            this._updateLiveStats();

            // Also update the list if visible
            if (!this.selectedCountry) {
                const item = document.querySelector(`[onclick="window.edmLiveMap.openCountryPanel('${code}')"] .lmap-cl-online`);
                if (item) item.textContent = `🟢 ${this.countryData[code].activeNow}`;
            }
        }, 2800);
    }
}

// ─── Global Init ─────────────────────────────────────────────────
window.edmLiveMap = new EDMLiveMap();

// Initialize when Dashboard view is activated
document.addEventListener('DOMContentLoaded', () => {
    // Hook into EDM app navigation
    const origNavigate = EdmApp.prototype.navigateTo;
    if (origNavigate) {
        EdmApp.prototype.navigateTo = function(page) {
            origNavigate.call(this, page);
            if (page === 'dashboard') {
                setTimeout(() => window.edmLiveMap.init(), 100);
            } else {
                window.edmLiveMap.destroy();
            }
        };
    }

    // Auto-init if on dashboard
    setTimeout(() => {
        if (document.getElementById('live-map-container')) {
            window.edmLiveMap.init();
        }
    }, 400);
});
