<?php
/**
 * Landing Page: Hero & 32-Socket Simulator Section Template Part
 *
 * @package EDM_Theme
 */

if (!defined('ABSPATH')) {
    exit;
}

$version = edm_get_latest_version();
$download_url = edm_get_download_url();
?>
<!-- ══════════════════════════════════════════════════════════════
     HERO SECTION (SAAS 2-COLUMN DUAL-THEME ENGINE)
     ══════════════════════════════════════════════════════════════ -->
<section class="hero-section" id="hero">
    <!-- Ambient Dynamic Glow Layers -->
    <div class="hero-ambient-glows">
        <div class="hero-glow-blob hero-glow-cyan"></div>
        <div class="hero-glow-blob hero-glow-purple"></div>
        <div class="hero-grid-mesh"></div>
    </div>

    <div class="container hero-container-grid">
        <!-- LEFT COLUMN: Content & CTAs -->
        <div class="hero-col-left">
            <!-- Badge / Tagline -->
            <div class="hero-pill-badge" id="hero-badge">
                <span class="pill-dot"></span>
                <span id="hero-pill-text"><?php printf(esc_html__('Exclusive Download Manager • Production Build v%s', 'edm-theme'), esc_html($version)); ?></span>
            </div>

            <!-- Hero Heading with vibrant neon gradient -->
            <h1 class="hero-title">
                EXCLUSIVE<br>
                DOWNLOAD<br>
                <span class="gradient-text">MANAGER</span>
            </h1>

            <!-- Hero Subtitle -->
            <p class="hero-subtitle">
                <?php esc_html_e('Experience lightning-fast, secure, and organized downloads. The ultimate solution for high-speed file management on all devices. Neon accent.', 'edm-theme'); ?>
            </p>

            <!-- Main Hero CTA Action Block -->
            <div class="hero-action-block">
                <a href="<?php echo esc_url($download_url); ?>" class="hero-pill-cta hero-download-btn" id="hero-primary-download" download>
                    <span class="btn-content">
                        <span class="cta-text"><?php esc_html_e('DOWNLOAD NOW', 'edm-theme'); ?></span>
                        <i data-lucide="arrow-up-right" class="cta-arrow"></i>
                    </span>
                </a>
                <div class="hero-os-badges">
                    <span class="os-label"><?php esc_html_e('Available on', 'edm-theme'); ?></span>
                    <span class="os-badge"><i data-lucide="monitor"></i> Windows</span>
                    <span class="os-badge"><i data-lucide="laptop"></i> macOS</span>
                    <span class="os-badge"><i data-lucide="terminal"></i> Linux</span>
                </div>
            </div>

            <!-- Secondary Feature Chips Sub-Grid -->
            <div class="hero-chips-subgrid">
                <div class="hero-chip-item">
                    <div class="chip-icon-wrap chip-cyan"><i data-lucide="zap"></i></div>
                    <div class="chip-text">
                        <strong><?php esc_html_e('32x Socket Turbo', 'edm-theme'); ?></strong>
                        <span><?php esc_html_e('Parallel dynamic streams', 'edm-theme'); ?></span>
                    </div>
                </div>
                <div class="hero-chip-item">
                    <div class="chip-icon-wrap chip-purple"><i data-lucide="video"></i></div>
                    <div class="chip-text">
                        <strong><?php esc_html_e('4K/8K Media Sniffer', 'edm-theme'); ?></strong>
                        <span><?php esc_html_e('Auto-grab M3U8 & DASH', 'edm-theme'); ?></span>
                    </div>
                </div>
                <div class="hero-chip-item">
                    <div class="chip-icon-wrap chip-green"><i data-lucide="shield-check"></i></div>
                    <div class="chip-text">
                        <strong><?php esc_html_e('Atomic Resume WAL', 'edm-theme'); ?></strong>
                        <span><?php esc_html_e('Zero corrupted downloads', 'edm-theme'); ?></span>
                    </div>
                </div>
                <div class="hero-chip-item">
                    <div class="chip-icon-wrap chip-pink"><i data-lucide="sparkles"></i></div>
                    <div class="chip-text">
                        <strong><?php esc_html_e('100% Ad-Free', 'edm-theme'); ?></strong>
                        <span><?php esc_html_e('Open & clean engine', 'edm-theme'); ?></span>
                    </div>
                </div>
            </div>
        </div>

        <!-- RIGHT COLUMN: Interactive & Animated Dashboard Showcase -->
        <div class="hero-col-right">
            <div class="hero-mockup-wrapper">
                <!-- Sleek Monitor / Window Frame -->
                <div class="hero-mockup-frame">
                    <!-- Top Window Bar -->
                    <div class="mockup-topbar">
                        <div class="mockup-brand-group">
                            <span class="mockup-logo-badge">EXDM</span>
                            <span class="mockup-status-dot"></span>
                            <span class="mockup-version-tag"><?php esc_html_e('Live Engine', 'edm-theme'); ?></span>
                        </div>
                        <div class="mockup-window-controls">
                            <span class="ctrl-btn ctrl-min"></span>
                            <span class="ctrl-btn ctrl-max"></span>
                            <span class="ctrl-btn ctrl-close"></span>
                        </div>
                    </div>

                    <!-- Inner Mockup Container (Sidebar + Main View) -->
                    <div class="mockup-inner-body">
                        <!-- Mini Icon Sidebar -->
                        <aside class="mockup-sidebar">
                            <button class="side-btn active" title="Active Downloads"><i data-lucide="layout-grid"></i></button>
                            <button class="side-btn" title="Download Manager"><i data-lucide="download"></i></button>
                            <button class="side-btn" title="History &amp; Schedules"><i data-lucide="clock"></i></button>
                            <button class="side-btn" title="Engine Settings"><i data-lucide="settings"></i></button>
                            <div class="sidebar-spacer"></div>
                            <button class="side-btn" title="Exit / Switch"><i data-lucide="log-out"></i></button>
                        </aside>

                        <!-- Mockup Main Content Area -->
                        <div class="mockup-main-pane">
                            <!-- Real-Time Speed Graph Header -->
                            <div class="mockup-graph-header">
                                <div class="graph-title-group">
                                    <h4 class="graph-title"><?php esc_html_e('Real-time Speed Graph', 'edm-theme'); ?></h4>
                                    <div class="graph-legend">
                                        <span class="legend-item legend-cyan"><span class="legend-dot"></span> <?php esc_html_e('Download', 'edm-theme'); ?></span>
                                        <span class="legend-item legend-purple"><span class="legend-dot"></span> <?php esc_html_e('Speed', 'edm-theme'); ?></span>
                                    </div>
                                </div>
                                <div class="graph-live-speed">
                                    <span class="speed-num" id="hero-live-speed-num">112.4</span>
                                    <span class="speed-unit">MB/s</span>
                                </div>
                            </div>

                            <!-- Real-Time Speed Waveform Canvas Container -->
                            <div class="mockup-canvas-wrapper">
                                <div class="graph-y-axis">
                                    <span>150 MB/s</span>
                                    <span>120 MB/s</span>
                                    <span>80 MB/s</span>
                                    <span>40 MB/s</span>
                                    <span>0 MB/s</span>
                                </div>
                                <div class="canvas-container">
                                    <canvas id="hero-speed-canvas" width="560" height="90"></canvas>
                                </div>
                            </div>

                            <!-- Tabs & Download Status Bar -->
                            <div class="mockup-tabs-bar">
                                <div class="mockup-tabs">
                                    <span class="tab-item active"><?php esc_html_e('Active', 'edm-theme'); ?></span>
                                    <span class="tab-item"><?php esc_html_e('Queued', 'edm-theme'); ?></span>
                                    <span class="tab-item"><?php esc_html_e('Completed', 'edm-theme'); ?></span>
                                </div>
                                <div class="mockup-task-stats">
                                    <span class="stats-bytes">8.4 GB / 12.0 GB (88%)</span>
                                    <span class="stats-count">• 4 <?php esc_html_e('Files Downloading', 'edm-theme'); ?></span>
                                </div>
                            </div>

                            <!-- Active Task List -->
                            <div class="mockup-task-list">
                                <!-- Task 1: Ubuntu ISO -->
                                <div class="mockup-task-item" data-task="ubuntu">
                                    <div class="task-icon task-icon-iso">
                                        <i data-lucide="disc"></i>
                                    </div>
                                    <div class="task-info">
                                        <div class="task-header-row">
                                            <span class="task-name">Ubuntu 23.04 ISO</span>
                                            <div class="task-metrics">
                                                <span class="task-speed text-cyan">24.8 MB/s</span>
                                                <span class="task-time">Completed 70 min 23s</span>
                                            </div>
                                        </div>
                                        <div class="task-progress-track">
                                            <div class="task-progress-fill fill-cyan" style="width: 78%;"></div>
                                        </div>
                                    </div>
                                    <div class="task-actions">
                                        <button type="button" class="btn-task-action btn-sim-toggle" title="Pause / Resume"><i data-lucide="pause"></i></button>
                                        <button type="button" class="btn-task-action" title="More Options"><i data-lucide="more-vertical"></i></button>
                                    </div>
                                </div>

                                <!-- Task 2: GameUpdate.zip -->
                                <div class="mockup-task-item" data-task="game">
                                    <div class="task-icon task-icon-zip">
                                        <i data-lucide="archive"></i>
                                    </div>
                                    <div class="task-info">
                                        <div class="task-header-row">
                                            <span class="task-name">GameUpdate.zip</span>
                                            <div class="task-metrics">
                                                <span class="task-speed text-purple">112 MB/s</span>
                                                <span class="task-time">File name 56 min 11s</span>
                                            </div>
                                        </div>
                                        <div class="task-progress-track">
                                            <div class="task-progress-fill fill-gradient" style="width: 56%;"></div>
                                        </div>
                                    </div>
                                    <div class="task-actions">
                                        <button type="button" class="btn-task-action btn-sim-toggle" title="Pause / Resume"><i data-lucide="pause"></i></button>
                                        <button type="button" class="btn-task-action" title="More Options"><i data-lucide="more-vertical"></i></button>
                                    </div>
                                </div>

                                <!-- Task 3: Project_Assets.rar -->
                                <div class="mockup-task-item" data-task="assets">
                                    <div class="task-icon task-icon-rar">
                                        <i data-lucide="folder-archive"></i>
                                    </div>
                                    <div class="task-info">
                                        <div class="task-header-row">
                                            <span class="task-name">Project_Assets.rar</span>
                                            <div class="task-metrics">
                                                <span class="task-speed text-cyan">12.3 MB/s</span>
                                                <span class="task-time">Completed 60 min 5s</span>
                                            </div>
                                        </div>
                                        <div class="task-progress-track">
                                            <div class="task-progress-fill fill-cyan" style="width: 91%;"></div>
                                        </div>
                                    </div>
                                    <div class="task-actions">
                                        <button type="button" class="btn-task-action btn-sim-toggle" title="Pause / Resume"><i data-lucide="pause"></i></button>
                                        <button type="button" class="btn-task-action" title="More Options"><i data-lucide="more-vertical"></i></button>
                                    </div>
                                </div>
                            </div>
                        </div>
                    </div>
                </div>
            </div>
        </div>
    </div>
</section>

<!-- ══════════════════════════════════════════════════════════════
     LIVE DOWNLOAD ENGINE & 32-STREAM SOCKET SIMULATOR
     ══════════════════════════════════════════════════════════════ -->
<section class="preview-section" id="live-simulator">
    <div class="container">
        <div class="product-window-card">
            <div class="window-header">
                <div class="window-dots">
                    <div class="window-dot dot-red"></div>
                    <div class="window-dot dot-yellow"></div>
                    <div class="window-dot dot-green"></div>
                </div>
                <div class="window-title"><?php esc_html_e('EDM — Active 32-Socket Download Accelerator [Live Engine]', 'edm-theme'); ?></div>
                <div style="font-size: 11.5px; color: var(--edm-green); font-weight: 700; display: flex; align-items: center; gap: 5px;">
                    <span style="width: 8px; height: 8px; border-radius: 50%; background: var(--edm-green); display: inline-block; box-shadow: 0 0 8px #10B981;"></span>
                    <span id="engine-status-text"><?php esc_html_e('Engine Online', 'edm-theme'); ?></span>
                </div>
            </div>

            <div class="window-body">
                <div class="simulator-stats-grid">
                    <div class="sim-stat-box">
                        <div class="sim-stat-label"><?php esc_html_e('Download Speed', 'edm-theme'); ?></div>
                        <div class="sim-stat-value" style="color: var(--edm-primary-light);" id="sim-speed-val">14.8 MB/s</div>
                    </div>
                    <div class="sim-stat-box">
                        <div class="sim-stat-label"><?php esc_html_e('Active Connections', 'edm-theme'); ?></div>
                        <div class="sim-stat-value" id="sim-streams-val">32 / 32 Streams</div>
                    </div>
                    <div class="sim-stat-box">
                        <div class="sim-stat-label"><?php esc_html_e('Time Remaining', 'edm-theme'); ?></div>
                        <div class="sim-stat-value" id="sim-time-val">00:38</div>
                    </div>
                    <div class="sim-stat-box">
                        <div class="sim-stat-label"><?php esc_html_e('Resume Capability', 'edm-theme'); ?></div>
                        <div class="sim-stat-value" style="color: var(--edm-green);"><?php esc_html_e('Supported', 'edm-theme'); ?></div>
                    </div>
                </div>

                <div class="sim-progress-wrap">
                    <div class="sim-file-info">
                        <span>Ubuntu-24.04-LTS-Desktop-x64.iso (5.80 GB)</span>
                        <span id="sim-progress-text"><?php esc_html_e('72% Completed (4.18 GB)', 'edm-theme'); ?></span>
                    </div>
                    <div class="sim-progress-bar-bg">
                        <div class="sim-progress-bar-fill" id="sim-progress-fill" style="width: 72%;"></div>
                    </div>

                    <!-- 32 Connection Threads Grid -->
                    <div class="streams-grid" id="streams-grid"></div>
                </div>

                <div class="simulator-controls">
                    <div style="display: flex; gap: 8px;">
                        <button type="button" class="btn btn-secondary btn-sm" id="btn-sim-pause" onclick="if(window.edmSite) window.edmSite.toggleSimPause();">
                            <i data-lucide="pause" id="sim-pause-icon" style="width: 12px; height: 12px;"></i>
                            <span id="sim-pause-text"><?php esc_html_e('Pause Engine', 'edm-theme'); ?></span>
                        </button>
                        <button type="button" class="btn btn-primary btn-sm" onclick="if(window.edmSite) window.edmSite.boostTurbo();">
                            <i data-lucide="flame" style="width: 12px; height: 12px;"></i>
                            <span><?php esc_html_e('Turbo Boost (48.6 MB/s)', 'edm-theme'); ?></span>
                        </button>
                    </div>
                    <span style="font-size: 11.5px; color: var(--edm-text-muted);"><?php esc_html_e('Dynamic HTTP Range Multi-threading Active', 'edm-theme'); ?></span>
                </div>
            </div>
        </div>
    </div>
</section>
