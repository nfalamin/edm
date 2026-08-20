<?php
/**
 * Template Name: EDM Admin Dashboard
 * Description: Dedicated SaaS Control Plane & Admin Dashboard template for EDM (/dashboard).
 *
 * @package EDM_Theme
 */

if (!defined('ABSPATH')) {
    exit;
}

get_header();
?>

<!-- ══════════════════════════════════════════════════════════════
     EDM DASHBOARD / CONTROL PLANE APPLICATION SHELL
     ══════════════════════════════════════════════════════════════ -->
<div class="dash-app-layout" id="dash-app-layout">

    <!-- 1. SIDEBAR NAVIGATION -->
    <?php get_template_part('template-parts/dashboard/sidebar'); ?>

    <!-- 2. MAIN APPLICATION CONTENT -->
    <div class="app-main-content">
        <!-- TOPBAR -->
        <?php get_template_part('template-parts/dashboard/topbar'); ?>

        <!-- DYNAMIC PAGE CONTAINER -->
        <main class="dash-view-container" id="dash-view-container">
            
            <!-- ── VIEW 1: OVERVIEW ── -->
            <section class="dash-page-view active" id="view-dashboard">
                <div class="view-header">
                    <div>
                        <h1 class="view-title"><?php esc_html_e('Executive Overview', 'edm-theme'); ?></h1>
                        <p class="view-subtitle"><?php esc_html_e('Real-time download metrics, active activations, and system telemetry.', 'edm-theme'); ?></p>
                    </div>
                    <div class="view-header-actions">
                        <button class="btn btn-outline btn-sm" onclick="if(window.edmDashboard) window.edmDashboard.refreshData();">
                            <i data-lucide="refresh-cw" style="width: 14px; height: 14px;"></i> <?php esc_html_e('Sync Live Data', 'edm-theme'); ?>
                        </button>
                    </div>
                </div>

                <!-- KPI CARDS GRID (Total Visitors, EDM Downloads, Extension Downloads, Countries) -->
                <div class="dash-kpi-grid">
                    <div class="kpi-card">
                        <div class="kpi-icon-wrap" style="background: rgba(93, 95, 239, 0.15); color: var(--edm-primary-light);">
                            <i data-lucide="users"></i>
                        </div>
                        <div class="kpi-meta">
                            <span class="kpi-label"><?php esc_html_e('Total Visitors', 'edm-theme'); ?></span>
                            <span class="kpi-value" id="kpi-total-users">24,582</span>
                            <span class="kpi-trend trend-up"><i data-lucide="trending-up"></i> +12.4% this month</span>
                        </div>
                    </div>

                    <div class="kpi-card">
                        <div class="kpi-icon-wrap" style="background: rgba(16, 185, 129, 0.15); color: var(--edm-green);">
                            <i data-lucide="download-cloud"></i>
                        </div>
                        <div class="kpi-meta">
                            <span class="kpi-label"><?php esc_html_e('EDM Downloads', 'edm-theme'); ?></span>
                            <span class="kpi-value" id="kpi-active-downloads">18,450</span>
                            <span class="kpi-trend trend-up"><i data-lucide="trending-up"></i> +6.7% vs yesterday</span>
                        </div>
                    </div>

                    <div class="kpi-card">
                        <div class="kpi-icon-wrap" style="background: rgba(56, 189, 248, 0.15); color: var(--edm-blue);">
                            <i data-lucide="puzzle"></i>
                        </div>
                        <div class="kpi-meta">
                            <span class="kpi-label"><?php esc_html_e('Extension Downloads', 'edm-theme'); ?></span>
                            <span class="kpi-value" id="kpi-active-licenses">9,840</span>
                            <span class="kpi-trend trend-up"><i data-lucide="trending-up"></i> Chrome / Edge / Firefox</span>
                        </div>
                    </div>

                    <div class="kpi-card">
                        <div class="kpi-icon-wrap" style="background: rgba(245, 158, 11, 0.15); color: var(--edm-amber);">
                            <i data-lucide="globe"></i>
                        </div>
                        <div class="kpi-meta">
                            <span class="kpi-label"><?php esc_html_e('Countries', 'edm-theme'); ?></span>
                            <span class="kpi-value" id="kpi-bandwidth-delivered">142</span>
                            <span class="kpi-trend trend-up"><i data-lucide="activity"></i> Worldwide telemetry</span>
                        </div>
                    </div>
                </div>

                <!-- CHARTS ROW -->
                <div class="dash-charts-row">
                    <div class="dash-chart-card">
                        <div class="chart-card-header">
                            <h3><?php esc_html_e('Download Throughput & Active Sockets (30 Days)', 'edm-theme'); ?></h3>
                        </div>
                        <div class="chart-canvas-wrap">
                            <canvas id="chart-downloads-overview" height="260"></canvas>
                        </div>
                    </div>

                    <div class="dash-chart-card">
                        <div class="chart-card-header">
                            <h3><?php esc_html_e('Product & Extension Distribution', 'edm-theme'); ?></h3>
                        </div>
                        <div class="chart-canvas-wrap">
                            <canvas id="chart-plan-distribution" height="260"></canvas>
                        </div>
                    </div>
                </div>

                <!-- RECENT ACTIVITY TABLE -->
                <div class="dash-table-card">
                    <div class="table-card-header">
                        <h3><?php esc_html_e('Recent Activity & Live Telemetry Events', 'edm-theme'); ?></h3>
                        <button class="btn btn-outline btn-sm" onclick="if(window.edmDashboard) window.edmDashboard.navigate('analytics');"><?php esc_html_e('View Detailed Analytics', 'edm-theme'); ?></button>
                    </div>
                    <div class="table-responsive">
                        <table class="dash-data-table" id="table-overview-users">
                            <thead>
                                <tr>
                                    <th>Event / Product</th>
                                    <th>Version</th>
                                    <th>Platform / Browser</th>
                                    <th>Country</th>
                                    <th>Status</th>
                                    <th>Time</th>
                                </tr>
                            </thead>
                            <tbody id="tbody-overview-users">
                                <tr>
                                    <td><strong>EDM Desktop Installer</strong></td>
                                    <td><span class="badge-tag">v2.1.0</span></td>
                                    <td>Windows 11 (x64)</td>
                                    <td>United States</td>
                                    <td><span class="badge-status-active">Completed</span></td>
                                    <td>Just now</td>
                                </tr>
                                <tr>
                                    <td><strong>Chrome Extension</strong></td>
                                    <td><span class="badge-tag">v1.0.0</span></td>
                                    <td>Google Chrome 124</td>
                                    <td>Germany</td>
                                    <td><span class="badge-status-active">Completed</span></td>
                                    <td>2 mins ago</td>
                                </tr>
                                <tr>
                                    <td><strong>Edge Extension</strong></td>
                                    <td><span class="badge-tag">v1.0.0</span></td>
                                    <td>Microsoft Edge 124</td>
                                    <td>United Kingdom</td>
                                    <td><span class="badge-status-active">Completed</span></td>
                                    <td>5 mins ago</td>
                                </tr>
                            </tbody>
                        </table>
                    </div>
                </div>
            </section>

            <!-- ── VIEW 2: LANDING PAGE CMS (Hero, Features, CTA, Extensions, FAQ, Footer) ── -->
            <section class="dash-page-view" id="view-content">
                <div class="view-header">
                    <div>
                        <h1 class="view-title"><?php esc_html_e('Landing Page CMS Editor', 'edm-theme'); ?></h1>
                        <p class="view-subtitle"><?php esc_html_e('Configure public headlines, features, extensions showcase, FAQ items, and CTAs on /edm.', 'edm-theme'); ?></p>
                    </div>
                    <div class="view-header-actions">
                        <button class="btn btn-primary btn-sm" onclick="if(window.edmDashboard) window.edmDashboard.openModal('modal-content-hero');">
                            <i data-lucide="edit-3"></i> <?php esc_html_e('Edit Hero & CTAs', 'edm-theme'); ?>
                        </button>
                    </div>
                </div>

                <div class="dash-table-card">
                    <div class="table-card-header">
                        <h3><?php esc_html_e('Public Landing Page Sections (/edm)', 'edm-theme'); ?></h3>
                        <span class="badge-tag">Live Sync Active</span>
                    </div>
                    <div class="table-responsive">
                        <table class="dash-data-table">
                            <thead>
                                <tr>
                                    <th>Section Module</th>
                                    <th>Target Component</th>
                                    <th>Display Status</th>
                                    <th>Order</th>
                                    <th>Actions</th>
                                </tr>
                            </thead>
                            <tbody id="tbody-cms-features">
                                <tr>
                                    <td><strong>1. Hero & Socket Simulator</strong></td>
                                    <td><code>hero.php</code></td>
                                    <td><span class="badge-status-active">Visible</span></td>
                                    <td>1</td>
                                    <td><button class="btn-action-icon" onclick="if(window.edmDashboard) window.edmDashboard.openModal('modal-content-hero');"><i data-lucide="edit-2"></i></button></td>
                                </tr>
                                <tr>
                                    <td><strong>2. Features & 32x Turbo Architecture</strong></td>
                                    <td><code>features.php</code></td>
                                    <td><span class="badge-status-active">Visible</span></td>
                                    <td>2</td>
                                    <td><button class="btn-action-icon"><i data-lucide="eye"></i></button></td>
                                </tr>
                                <tr>
                                    <td><strong>3. CTA & Primary Download Hub</strong></td>
                                    <td><code>download-cta.php</code></td>
                                    <td><span class="badge-status-active">Visible</span></td>
                                    <td>3</td>
                                    <td><button class="btn-action-icon"><i data-lucide="edit-2"></i></button></td>
                                </tr>
                                <tr>
                                    <td><strong>4. Browser Extensions (3 Browsers)</strong></td>
                                    <td><code>browser-extensions.php</code></td>
                                    <td><span class="badge-status-active">Visible</span></td>
                                    <td>4</td>
                                    <td><button class="btn-action-icon" onclick="if(window.edmDashboard) window.edmDashboard.navigate('extensions');"><i data-lucide="external-link"></i></button></td>
                                </tr>
                                <tr>
                                    <td><strong>5. FAQ Accordion Section</strong></td>
                                    <td><code>faq-section.php</code></td>
                                    <td><span class="badge-status-active">Visible</span></td>
                                    <td>5</td>
                                    <td><button class="btn-action-icon"><i data-lucide="edit-2"></i></button></td>
                                </tr>
                                <tr>
                                    <td><strong>6. Global Footer & Legal Columns</strong></td>
                                    <td><code>footer.php</code></td>
                                    <td><span class="badge-status-active">Visible</span></td>
                                    <td>6</td>
                                    <td><button class="btn-action-icon"><i data-lucide="edit-2"></i></button></td>
                                </tr>
                            </tbody>
                        </table>
                    </div>
                </div>
            </section>

            <!-- ── VIEW 3: RELEASES (EDM Versions, Upload, Publish, Previous Versions, Release Notes) ── -->
            <section class="dash-page-view" id="view-releases">
                <div class="view-header">
                    <div>
                        <h1 class="view-title"><?php esc_html_e('Release Manager & Version Storage', 'edm-theme'); ?></h1>
                        <p class="view-subtitle"><?php esc_html_e('Deploy new EDM builds, inspect cryptographic SHA-256 signatures, and retain version history.', 'edm-theme'); ?></p>
                    </div>
                    <div class="view-header-actions">
                        <button class="btn btn-primary btn-sm" onclick="if(window.edmDashboard) window.edmDashboard.openModal('modal-release');">
                            <i data-lucide="upload"></i> <?php esc_html_e('Upload New Release', 'edm-theme'); ?>
                        </button>
                    </div>
                </div>

                <div class="dash-table-card">
                    <div class="table-card-header">
                        <h3><?php esc_html_e('Published Releases & Previous Versions History', 'edm-theme'); ?></h3>
                    </div>
                    <div class="table-responsive">
                        <table class="dash-data-table">
                            <thead>
                                <tr>
                                    <th>Version</th>
                                    <th>Severity</th>
                                    <th>SHA-256 Checksum</th>
                                    <th>File Size</th>
                                    <th>Status</th>
                                    <th>Downloads</th>
                                    <th>Actions</th>
                                </tr>
                            </thead>
                            <tbody id="tbody-releases-list">
                                <tr>
                                    <td><strong>v2.1.0</strong> <span class="badge-tag">Current</span></td>
                                    <td><span class="badge-status-recommended">Recommended</span></td>
                                    <td><code class="code-hash">93049cf86301342dbdaae74256d4013a1e30133aa26a38dbe08e2a6e3e32d023</code></td>
                                    <td>19.8 MB</td>
                                    <td><span class="badge-status-active">Published</span></td>
                                    <td>18,450</td>
                                    <td>
                                        <button class="btn-action-icon" title="Rollback" onclick="if(window.edmDashboard) window.edmDashboard.rollbackRelease('v2.1.0');"><i data-lucide="rotate-ccw"></i></button>
                                    </td>
                                </tr>
                                <tr>
                                    <td><strong>v2.0.0</strong></td>
                                    <td><span class="badge-status-optional">Stable</span></td>
                                    <td><code class="code-hash">93049cf86301342dbdaae74256d4013a1e30133aa26a38dbe08e2a6e3e32d023</code></td>
                                    <td>19.8 MB</td>
                                    <td><span class="badge-status-active">Retained</span></td>
                                    <td>12,210</td>
                                    <td>
                                        <button class="btn-action-icon" title="Inspect"><i data-lucide="eye"></i></button>
                                    </td>
                                </tr>
                                <tr>
                                    <td><strong>v1.0.0</strong></td>
                                    <td><span class="badge-status-withdrawn">Legacy</span></td>
                                    <td><code class="code-hash">27f4160e858631fe7c16a2540d7d1764852047014adeedc73d1d80e6f00b0c13</code></td>
                                    <td>4.63 MB</td>
                                    <td><span class="badge-status-withdrawn">Archived</span></td>
                                    <td>5,400</td>
                                    <td>
                                        <button class="btn-action-icon" title="Inspect"><i data-lucide="eye"></i></button>
                                    </td>
                                </tr>
                            </tbody>
                        </table>
                    </div>
                </div>
            </section>

            <!-- ── VIEW 4: EXTENSIONS (Extension 1 Chrome, Extension 2 Edge, Extension 3 Firefox) ── -->
            <section class="dash-page-view" id="view-extensions">
                <div class="view-header">
                    <div>
                        <h1 class="view-title"><?php esc_html_e('Browser Extensions Distribution', 'edm-theme'); ?></h1>
                        <p class="view-subtitle"><?php esc_html_e('Manage browser extension packages, Manifest V3 integrations, and update bundles.', 'edm-theme'); ?></p>
                    </div>
                </div>

                <div class="dash-kpi-grid" style="grid-template-columns: repeat(3, 1fr);">
                    <!-- EXTENSION 1: CHROME -->
                    <div class="dash-table-card" style="padding: 24px;">
                        <div style="display: flex; align-items: center; gap: 12px; margin-bottom: 16px;">
                            <div class="kpi-icon-wrap" style="background: rgba(245, 158, 11, 0.15); color: var(--edm-amber);">
                                <i data-lucide="chrome"></i>
                            </div>
                            <div>
                                <h3 style="margin: 0; font-size: 16px; font-weight: 700;">Extension 1: Google Chrome</h3>
                                <span class="badge-tag">Manifest V3 · v1.0.0</span>
                            </div>
                        </div>
                        <p style="font-size: 13px; color: var(--edm-text-secondary); margin-bottom: 16px;">
                            Package: <code>edm-chrome-extension-v1.0.0.zip</code><br>
                            Size: <strong>80.1 KB</strong> · Downloads: <strong>5,120</strong>
                        </p>
                        <a href="<?php echo esc_url(get_template_directory_uri() . '/downloads/edm-chrome-extension-v1.0.0.zip'); ?>" class="btn btn-outline btn-sm w-full" download>
                            <i data-lucide="download"></i> Download Package
                        </a>
                    </div>

                    <!-- EXTENSION 2: EDGE -->
                    <div class="dash-table-card" style="padding: 24px;">
                        <div style="display: flex; align-items: center; gap: 12px; margin-bottom: 16px;">
                            <div class="kpi-icon-wrap" style="background: rgba(56, 189, 248, 0.15); color: var(--edm-blue);">
                                <i data-lucide="globe"></i>
                            </div>
                            <div>
                                <h3 style="margin: 0; font-size: 16px; font-weight: 700;">Extension 2: Microsoft Edge</h3>
                                <span class="badge-tag">Manifest V3 · v1.0.0</span>
                            </div>
                        </div>
                        <p style="font-size: 13px; color: var(--edm-text-secondary); margin-bottom: 16px;">
                            Package: <code>edm-edge-extension-v1.0.0.zip</code><br>
                            Size: <strong>80.1 KB</strong> · Downloads: <strong>2,840</strong>
                        </p>
                        <a href="<?php echo esc_url(get_template_directory_uri() . '/downloads/edm-edge-extension-v1.0.0.zip'); ?>" class="btn btn-outline btn-sm w-full" download>
                            <i data-lucide="download"></i> Download Package
                        </a>
                    </div>

                    <!-- EXTENSION 3: FIREFOX -->
                    <div class="dash-table-card" style="padding: 24px;">
                        <div style="display: flex; align-items: center; gap: 12px; margin-bottom: 16px;">
                            <div class="kpi-icon-wrap" style="background: rgba(236, 72, 153, 0.15); color: var(--edm-pink);">
                                <i data-lucide="flame"></i>
                            </div>
                            <div>
                                <h3 style="margin: 0; font-size: 16px; font-weight: 700;">Extension 3: Mozilla Firefox</h3>
                                <span class="badge-tag">Gecko WebExt · v1.0.0</span>
                            </div>
                        </div>
                        <p style="font-size: 13px; color: var(--edm-text-secondary); margin-bottom: 16px;">
                            Package: <code>edm-firefox-extension-v1.0.0.zip</code><br>
                            Size: <strong>80.3 KB</strong> · Downloads: <strong>1,880</strong>
                        </p>
                        <a href="<?php echo esc_url(get_template_directory_uri() . '/downloads/edm-firefox-extension-v1.0.0.zip'); ?>" class="btn btn-outline btn-sm w-full" download>
                            <i data-lucide="download"></i> Download Package
                        </a>
                    </div>
                </div>
            </section>

            <!-- ── VIEW 5: ANALYTICS (Visitors, Downloads, Countries, Versions, Products) ── -->
            <section class="dash-page-view" id="view-analytics">
                <div class="view-header">
                    <div>
                        <h1 class="view-title"><?php esc_html_e('Analytics & Distribution Intelligence', 'edm-theme'); ?></h1>
                        <p class="view-subtitle"><?php esc_html_e('Real-time telemetry broken down by Visitors, Downloads, Countries, Versions, and Products.', 'edm-theme'); ?></p>
                    </div>
                </div>

                <div class="dash-charts-row">
                    <div class="dash-chart-card">
                        <div class="chart-card-header">
                            <h3><?php esc_html_e('Top Geographic Regions (Countries)', 'edm-theme'); ?></h3>
                        </div>
                        <div class="chart-canvas-wrap">
                            <canvas id="chart-geo-distribution" height="260"></canvas>
                        </div>
                    </div>

                    <div class="dash-chart-card">
                        <div class="chart-card-header">
                            <h3><?php esc_html_e('Product Breakdown & Adoption by Version', 'edm-theme'); ?></h3>
                        </div>
                        <div class="chart-canvas-wrap">
                            <canvas id="chart-os-distribution" height="260"></canvas>
                        </div>
                    </div>
                </div>
            </section>

            <!-- ── VIEW 6: 30-DAY TRIAL ── -->
            <section class="dash-page-view" id="view-trials">
                <div class="view-header">
                    <div>
                        <h1 class="view-title"><?php esc_html_e('30-Day Trial & License Policies', 'edm-theme'); ?></h1>
                        <p class="view-subtitle"><?php esc_html_e('Configure trial parameters, hardware ID binding, device seat limits, and offline grace hours.', 'edm-theme'); ?></p>
                    </div>
                    <div class="view-header-actions">
                        <button class="btn btn-primary btn-sm" onclick="if(window.edmDashboard) window.edmDashboard.openModal('modal-trial-config');">
                            <i data-lucide="settings-2"></i> <?php esc_html_e('Edit Trial Policy', 'edm-theme'); ?>
                        </button>
                    </div>
                </div>

                <div class="dash-kpi-grid">
                    <div class="kpi-card">
                        <div class="kpi-icon-wrap" style="background: rgba(93, 95, 239, 0.15); color: var(--edm-primary-light);">
                            <i data-lucide="clock"></i>
                        </div>
                        <div class="kpi-meta">
                            <span class="kpi-label"><?php esc_html_e('Trial Duration', 'edm-theme'); ?></span>
                            <span class="kpi-value" id="kpi-trial-days">30 Days</span>
                            <span class="kpi-trend trend-up">Full feature access</span>
                        </div>
                    </div>
                    <div class="kpi-card">
                        <div class="kpi-icon-wrap" style="background: rgba(16, 185, 129, 0.15); color: var(--edm-green);">
                            <i data-lucide="shield-check"></i>
                        </div>
                        <div class="kpi-meta">
                            <span class="kpi-label"><?php esc_html_e('Cryptographic HWID', 'edm-theme'); ?></span>
                            <span class="kpi-value">Enforced</span>
                            <span class="kpi-trend trend-up">Anti-tamper active</span>
                        </div>
                    </div>
                    <div class="kpi-card">
                        <div class="kpi-icon-wrap" style="background: rgba(56, 189, 248, 0.15); color: var(--edm-blue);">
                            <i data-lucide="monitor"></i>
                        </div>
                        <div class="kpi-meta">
                            <span class="kpi-label"><?php esc_html_e('Max Device Seats', 'edm-theme'); ?></span>
                            <span class="kpi-value" id="kpi-max-devices">5 Devices</span>
                            <span class="kpi-trend trend-up">Concurrent slots</span>
                        </div>
                    </div>
                    <div class="kpi-card">
                        <div class="kpi-icon-wrap" style="background: rgba(245, 158, 11, 0.15); color: var(--edm-amber);">
                            <i data-lucide="wifi-off"></i>
                        </div>
                        <div class="kpi-meta">
                            <span class="kpi-label"><?php esc_html_e('Offline Grace Allowance', 'edm-theme'); ?></span>
                            <span class="kpi-value" id="kpi-offline-hours">72 Hours</span>
                            <span class="kpi-trend trend-up">Offline resilience</span>
                        </div>
                    </div>
                </div>
            </section>

            <!-- ── VIEW 7: COUPONS / OFFERS (Create, Activate, Expire, Usage) ── -->
            <section class="dash-page-view" id="view-promotions">
                <div class="view-header">
                    <div>
                        <h1 class="view-title"><?php esc_html_e('Coupons & Promotional Offers', 'edm-theme'); ?></h1>
                        <p class="view-subtitle"><?php esc_html_e('Create, activate, schedule expiration, and track usage of promotional discounts.', 'edm-theme'); ?></p>
                    </div>
                    <div class="view-header-actions">
                        <button class="btn btn-primary btn-sm" onclick="if(window.edmDashboard) window.edmDashboard.openModal('modal-promotion');">
                            <i data-lucide="tag"></i> <?php esc_html_e('Create New Offer', 'edm-theme'); ?>
                        </button>
                    </div>
                </div>

                <div class="dash-table-card">
                    <div class="table-responsive">
                        <table class="dash-data-table">
                            <thead>
                                <tr>
                                    <th>Coupon Code</th>
                                    <th>Discount</th>
                                    <th>Discount Type</th>
                                    <th>Usage / Limit</th>
                                    <th>Status</th>
                                    <th>Expiration</th>
                                    <th>Actions</th>
                                </tr>
                            </thead>
                            <tbody id="tbody-promotions-list">
                                <tr>
                                    <td><strong><code>SUMMER50</code></strong></td>
                                    <td>50% OFF</td>
                                    <td>Percentage</td>
                                    <td>1,420 / 2,000</td>
                                    <td><span class="badge-status-active">Active</span></td>
                                    <td>2026-12-31</td>
                                    <td>
                                        <button class="btn-action-icon" title="Toggle Active" onclick="if(window.edmDashboard) window.edmDashboard.togglePromotionStatus('SUMMER50');"><i data-lucide="power"></i></button>
                                    </td>
                                </tr>
                                <tr>
                                    <td><strong><code>EDMPRO10</code></strong></td>
                                    <td>$10 OFF</td>
                                    <td>Fixed Amount</td>
                                    <td>890 / 1,000</td>
                                    <td><span class="badge-status-active">Active</span></td>
                                    <td>2026-12-31</td>
                                    <td>
                                        <button class="btn-action-icon" title="Toggle Active" onclick="if(window.edmDashboard) window.edmDashboard.togglePromotionStatus('EDMPRO10');"><i data-lucide="power"></i></button>
                                    </td>
                                </tr>
                            </tbody>
                        </table>
                    </div>
                </div>
            </section>

            <!-- ── VIEW 8: SYSTEM (Security, Logs, Configuration) ── -->
            <section class="dash-page-view" id="view-audit-logs">
                <div class="view-header">
                    <div>
                        <h1 class="view-title"><?php esc_html_e('System Security, Logs & Configuration', 'edm-theme'); ?></h1>
                        <p class="view-subtitle"><?php esc_html_e('Forensic immutable logs, active security policies, and API configuration.', 'edm-theme'); ?></p>
                    </div>
                </div>

                <div class="health-grid-3" style="margin-bottom: 24px;">
                    <div class="health-card health-card-good">
                        <div class="health-icon"><i data-lucide="shield-check"></i></div>
                        <div class="health-meta">
                            <h4>Security Middleware</h4>
                            <p>CSP • HSTS • Permissions-Policy</p>
                            <span class="health-status-badge">ACTIVE</span>
                        </div>
                    </div>
                    <div class="health-card health-card-good">
                        <div class="health-icon"><i data-lucide="server"></i></div>
                        <div class="health-meta">
                            <h4>API Control Plane</h4>
                            <p>ASP.NET Core Kestrel • HTTP/2</p>
                            <span class="health-status-badge">HEALTHY</span>
                        </div>
                    </div>
                    <div class="health-card health-card-good">
                        <div class="health-icon"><i data-lucide="database"></i></div>
                        <div class="health-meta">
                            <h4>Database Engine</h4>
                            <p>SQLite / EF Core Primary Storage</p>
                            <span class="health-status-badge">SYNCED</span>
                        </div>
                    </div>
                </div>

                <div class="dash-table-card">
                    <div class="table-card-header">
                        <h3><?php esc_html_e('Forensic Security Audit Journal', 'edm-theme'); ?></h3>
                    </div>
                    <div class="table-responsive">
                        <table class="dash-data-table">
                            <thead>
                                <tr>
                                    <th>Timestamp (UTC)</th>
                                    <th>Actor</th>
                                    <th>Action</th>
                                    <th>Target Entity</th>
                                    <th>Masked Subnet</th>
                                    <th>Status</th>
                                </tr>
                            </thead>
                            <tbody id="tbody-audit-logs">
                                <tr>
                                    <td>Just now</td>
                                    <td>superadmin</td>
                                    <td>AUTH_LOGIN_SUCCESS</td>
                                    <td>Session (2FA Verified)</td>
                                    <td><code>192.168.1.0/24</code></td>
                                    <td><span class="badge-status-active">SUCCESS</span></td>
                                </tr>
                                <tr>
                                    <td>12 mins ago</td>
                                    <td>superadmin</td>
                                    <td>RELEASE_PUBLISH</td>
                                    <td>Release v2.1.0</td>
                                    <td><code>192.168.1.0/24</code></td>
                                    <td><span class="badge-status-active">SUCCESS</span></td>
                                </tr>
                            </tbody>
                        </table>
                    </div>
                </div>
            </section>

            <!-- ── VIEW 9: SYSTEM CONFIGURATION ── -->
            <section class="dash-page-view" id="view-system-health">
                <div class="view-header">
                    <div>
                        <h1 class="view-title"><?php esc_html_e('System Health & Services', 'edm-theme'); ?></h1>
                        <p class="view-subtitle"><?php esc_html_e('Real-time probe status of Control Plane API, Database, and Release Storage.', 'edm-theme'); ?></p>
                    </div>
                </div>

                <div class="health-grid-3">
                    <div class="health-card health-card-good">
                        <div class="health-icon"><i data-lucide="database"></i></div>
                        <div class="health-meta">
                            <h4>Control Plane Database</h4>
                            <p>SQLite / EF Core Primary · Latency: 0.8ms</p>
                            <span class="health-status-badge">HEALTHY</span>
                        </div>
                    </div>
                    <div class="health-card health-card-good">
                        <div class="health-icon"><i data-lucide="server"></i></div>
                        <div class="health-meta">
                            <h4>ASP.NET Core API</h4>
                            <p>Kestrel Server · HTTP/2 Active · Latency: 2.1ms</p>
                            <span class="health-status-badge">HEALTHY</span>
                        </div>
                    </div>
                    <div class="health-card health-card-good">
                        <div class="health-icon"><i data-lucide="hard-drive"></i></div>
                        <div class="health-meta">
                            <h4>Release Storage Volume</h4>
                            <p>Local Encrypted Storage Provider · Free: 480 GB</p>
                            <span class="health-status-badge">HEALTHY</span>
                        </div>
                    </div>
                </div>
            </section>

        </main>
    </div>
</div>

<!-- DASHBOARD ACTION MODALS -->
<?php get_template_part('template-parts/dashboard/modals'); ?>

<?php
get_footer();
