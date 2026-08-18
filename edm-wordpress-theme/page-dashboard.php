<?php
/**
 * Template Name: EDM Admin Dashboard
 * Description: Custom Page Template for the full EDM Control Plane & Admin Dashboard.
 */

get_header();
?>

    <!-- ══════════════════════════════════════════════════════════════
         GLOBAL APP SHELL CONTAINER
         ══════════════════════════════════════════════════════════════ -->
    <div class="app-container" id="app">
        
        <!-- ── SIDEBAR NAVIGATION ── -->
        <aside class="app-sidebar" id="sidebar">
            <div class="sidebar-header">
                <div class="brand-logo-wrap">
                    <i data-lucide="zap" style="width: 18px; height: 18px;"></i>
                </div>
                <div class="brand-info">
                    <span class="brand-name">EDM</span>
                    <span class="brand-tagline">Exclusive Download Manager</span>
                </div>
            </div>

            <!-- Scrollable Navigation Items -->
            <nav class="sidebar-nav">
                <!-- OVERVIEW -->
                <div class="nav-section">
                    <button class="nav-item active" data-page="dashboard">
                        <span class="nav-item-icon"><i data-lucide="layout-dashboard"></i></span>
                        <span class="nav-item-text">Dashboard</span>
                    </button>
                </div>

                <!-- USERS -->
                <div class="nav-section">
                    <div class="nav-section-title">Users</div>
                    <button class="nav-item" data-page="users">
                        <span class="nav-item-icon"><i data-lucide="users"></i></span>
                        <span class="nav-item-text">Users</span>
                    </button>
                    <button class="nav-item" data-page="devices">
                        <span class="nav-item-icon"><i data-lucide="monitor"></i></span>
                        <span class="nav-item-text">Devices</span>
                    </button>
                    <button class="nav-item" data-page="user-activity">
                        <span class="nav-item-icon"><i data-lucide="activity"></i></span>
                        <span class="nav-item-text">User Activity</span>
                    </button>
                </div>

                <!-- DOWNLOADS -->
                <div class="nav-section">
                    <div class="nav-section-title">Downloads</div>
                    <button class="nav-item" data-page="download-analytics">
                        <span class="nav-item-icon"><i data-lucide="bar-chart-2"></i></span>
                        <span class="nav-item-text">Download Analytics</span>
                    </button>
                    <button class="nav-item" data-page="download-activity">
                        <span class="nav-item-icon"><i data-lucide="play-circle"></i></span>
                        <span class="nav-item-text">Download Activity</span>
                    </button>
                    <button class="nav-item" data-page="browser-extension">
                        <span class="nav-item-icon"><i data-lucide="puzzle"></i></span>
                        <span class="nav-item-text">Browser Extension</span>
                    </button>
                </div>

                <!-- SUBSCRIPTIONS -->
                <div class="nav-section">
                    <div class="nav-section-title">Subscriptions</div>
                    <button class="nav-item" data-page="plans">
                        <span class="nav-item-icon"><i data-lucide="sliders"></i></span>
                        <span class="nav-item-text">Plans</span>
                    </button>
                    <button class="nav-item" data-page="trials">
                        <span class="nav-item-icon"><i data-lucide="hourglass"></i></span>
                        <span class="nav-item-text">Trials</span>
                    </button>
                    <button class="nav-item" data-page="licenses">
                        <span class="nav-item-icon"><i data-lucide="key"></i></span>
                        <span class="nav-item-text">Licenses</span>
                    </button>
                    <button class="nav-item" data-page="transactions">
                        <span class="nav-item-icon"><i data-lucide="refresh-cw"></i></span>
                        <span class="nav-item-text">Transactions</span>
                    </button>
                    <button class="nav-item" data-page="coupons">
                        <span class="nav-item-icon"><i data-lucide="tag"></i></span>
                        <span class="nav-item-text">Coupons</span>
                    </button>
                </div>

                <!-- SYSTEM -->
                <div class="nav-section">
                    <div class="nav-section-title">System</div>
                    <button class="nav-item" data-page="update-center">
                        <span class="nav-item-icon"><i data-lucide="refresh-ccw"></i></span>
                        <span class="nav-item-text">Update Center</span>
                    </button>
                    <button class="nav-item" data-page="releases">
                        <span class="nav-item-icon"><i data-lucide="disc"></i></span>
                        <span class="nav-item-text">Release Manager</span>
                        <span class="nav-item-badge">v1.3.0</span>
                    </button>
                    <button class="nav-item" data-page="system-health">
                        <span class="nav-item-icon"><i data-lucide="heart-pulse"></i></span>
                        <span class="nav-item-text">System Health</span>
                    </button>
                    <button class="nav-item" data-page="api-status">
                        <span class="nav-item-icon"><i data-lucide="code"></i></span>
                        <span class="nav-item-text">API Status</span>
                    </button>
                </div>

                <!-- COMMUNICATION -->
                <div class="nav-section">
                    <div class="nav-section-title">Communication</div>
                    <button class="nav-item" data-page="notifications">
                        <span class="nav-item-icon"><i data-lucide="bell"></i></span>
                        <span class="nav-item-text">Notifications</span>
                    </button>
                    <button class="nav-item" data-page="email-campaigns">
                        <span class="nav-item-icon"><i data-lucide="mail"></i></span>
                        <span class="nav-item-text">Email Campaigns</span>
                    </button>
                    <button class="nav-item" data-page="announcements">
                        <span class="nav-item-icon"><i data-lucide="megaphone"></i></span>
                        <span class="nav-item-text">Announcements</span>
                    </button>
                </div>

                <!-- ANALYTICS -->
                <div class="nav-section">
                    <div class="nav-section-title">Analytics</div>
                    <button class="nav-item" data-page="reports">
                        <span class="nav-item-icon"><i data-lucide="file-text"></i></span>
                        <span class="nav-item-text">Reports</span>
                    </button>
                    <button class="nav-item" data-page="revenue-analytics">
                        <span class="nav-item-icon"><i data-lucide="dollar-sign"></i></span>
                        <span class="nav-item-text">Revenue Analytics</span>
                    </button>
                    <button class="nav-item" data-page="feature-analytics">
                        <span class="nav-item-icon"><i data-lucide="pie-chart"></i></span>
                        <span class="nav-item-text">Feature Analytics</span>
                    </button>
                </div>

                <!-- WEBSITE CMS -->
                <div class="nav-section">
                    <div class="nav-section-title">Website CMS</div>
                    <button class="nav-item" data-page="website-manager">
                        <span class="nav-item-icon"><i data-lucide="layout-template"></i></span>
                        <span class="nav-item-text">Website Content</span>
                    </button>
                </div>
            </nav>

            <!-- Bottom Upgrade Promo Box -->
            <div class="sidebar-promo-card">
                <div class="sidebar-promo-title">EDM v1.3.0 is available!</div>
                <div class="sidebar-promo-desc">New features and improvements</div>
                <button class="sidebar-promo-btn" onclick="window.edmApp.navigateTo('releases')">View Release Notes</button>
            </div>
        </aside>

        <!-- ── MAIN WRAPPER ── -->
        <div class="app-main">
            
            <!-- TOP HEADER -->
            <header class="app-header">
                <div class="header-left">
                    <button class="btn-menu-trigger" id="btn-toggle-sidebar" title="Toggle Sidebar">
                        <i data-lucide="menu" style="width: 16px; height: 16px;"></i>
                    </button>
                    <button class="search-trigger" id="btn-open-cmd" title="Quick Search">
                        <div class="search-trigger-content">
                            <i data-lucide="search" style="width: 14px; height: 14px;"></i>
                            <span>Search users, licenses, releases, devices...</span>
                        </div>
                        <span class="kbd-shortcut">Ctrl + K &lt;</span>
                    </button>
                </div>

                <div class="header-right">
                    <!-- Public Website Link Button -->
                    <a href="<?php echo esc_url(home_url('/')); ?>" class="header-pill-btn" title="Open Public EDM Website">
                        <i data-lucide="globe" style="width: 13px; height: 13px; color: var(--color-blue);"></i>
                        <span>Live Website</span>
                    </a>

                    <!-- What's New Button -->
                    <button class="header-pill-btn" onclick="window.edmApp.navigateTo('releases')">
                        <i data-lucide="sparkles" style="width: 13px; height: 13px; color: var(--color-primary-light);"></i>
                        <span>What's New ✨</span>
                    </button>

                    <!-- Notifications Bell Dropdown Trigger -->
                    <button class="header-btn" id="btn-notifications-dropdown" title="Notifications" onclick="window.edmApp.openNotificationsDrawer()">
                        <i data-lucide="bell" style="width: 15px; height: 15px;"></i>
                        <span class="header-badge">12</span>
                    </button>

                    <!-- Dark/Moon Theme Toggle -->
                    <button class="header-btn" id="btn-theme-toggle" title="Toggle Theme">
                        <i data-lucide="moon" id="theme-icon" style="width: 15px; height: 15px;"></i>
                    </button>

                    <!-- Admin Profile Pill -->
                    <div class="profile-pill" id="btn-profile-menu">
                        <div class="profile-avatar">AD</div>
                        <div style="display: flex; flex-direction: column; text-align: left;">
                            <span class="profile-name">Admin</span>
                            <span class="profile-role">Super Admin</span>
                        </div>
                    </div>
                </div>
            </header>

            <!-- ── SCROLLABLE VIEWS CONTAINER ── -->
            <main class="app-view-container">

                <!-- VIEW 1: DASHBOARD (EXACT RECREATION OF REFERENCE IMAGE) -->
                <div class="view-page active" id="view-dashboard">
                    
                    <!-- Subheader Banner -->
                    <div class="page-subheader">
                        <div class="page-title-wrap">
                            <h1>Welcome back, Admin! 👋</h1>
                            <p>Here's what's happening with EDM today.</p>
                        </div>
                        <div class="subheader-actions">
                            <div class="time-pill-group" style="padding: 5px 12px; font-size: 12px; cursor: pointer;" id="btn-date-picker">
                                <i data-lucide="calendar" style="width: 13px; height: 13px; margin-right: 6px;"></i>
                                <span id="current-date-range-label">May 20 – Jun 20, 2025 ⌵</span>
                            </div>
                            <div class="time-pill-group" style="padding: 5px 12px; font-size: 12px; cursor: pointer;" onclick="window.edmApp.showToast('Custom time range filter active', 'info')">
                                <span>Custom ⌵</span>
                            </div>
                            <button class="btn btn-primary" id="btn-export-report">
                                <i data-lucide="download" style="width: 14px; height: 14px;"></i>
                                <span>Export Report</span>
                            </button>
                        </div>
                    </div>

                    <!-- ── ROW 1: 7 KPI CARDS ── -->
                    <div class="kpi-grid-7">
                        <!-- 1. Total Users -->
                        <div class="kpi-card">
                            <div class="kpi-top-row">
                                <div>
                                    <div class="kpi-label">Total Users</div>
                                    <div class="kpi-value" style="margin-top: 4px;">24,582</div>
                                    <div class="kpi-trend-row">
                                        <span class="kpi-change-tag up">↑ 12.4%</span>
                                    </div>
                                    <div class="kpi-comparison">vs Apr 20 – May 20</div>
                                </div>
                                <div class="kpi-icon-box purple">
                                    <i data-lucide="users" style="width: 16px; height: 16px;"></i>
                                </div>
                            </div>
                            <div class="kpi-sparkline-wrap">
                                <canvas id="spark-total-users"></canvas>
                            </div>
                        </div>

                        <!-- 2. Active Users -->
                        <div class="kpi-card">
                            <div class="kpi-top-row">
                                <div>
                                    <div class="kpi-label">Active Users</div>
                                    <div class="kpi-value" style="margin-top: 4px;">8,432</div>
                                    <div class="kpi-trend-row">
                                        <span class="kpi-change-tag up">↑ 8.7%</span>
                                    </div>
                                    <div class="kpi-comparison">vs Apr 20 – May 20</div>
                                </div>
                                <div class="kpi-icon-box blue">
                                    <i data-lucide="user" style="width: 16px; height: 16px;"></i>
                                </div>
                            </div>
                            <div class="kpi-sparkline-wrap">
                                <canvas id="spark-active-users"></canvas>
                            </div>
                        </div>

                        <!-- 3. Premium Users -->
                        <div class="kpi-card">
                            <div class="kpi-top-row">
                                <div>
                                    <div class="kpi-label">Premium Users</div>
                                    <div class="kpi-value" style="margin-top: 4px;">6,215</div>
                                    <div class="kpi-trend-row">
                                        <span class="kpi-change-tag up">↑ 15.3%</span>
                                    </div>
                                    <div class="kpi-comparison">vs Apr 20 – May 20</div>
                                </div>
                                <div class="kpi-icon-box amber">
                                    <i data-lucide="crown" style="width: 16px; height: 16px;"></i>
                                </div>
                            </div>
                            <div class="kpi-sparkline-wrap">
                                <canvas id="spark-premium-users"></canvas>
                            </div>
                        </div>

                        <!-- 4. Trial Users -->
                        <div class="kpi-card">
                            <div class="kpi-top-row">
                                <div>
                                    <div class="kpi-label">Trial Users</div>
                                    <div class="kpi-value" style="margin-top: 4px;">2,217</div>
                                    <div class="kpi-trend-row">
                                        <span class="kpi-change-tag up">↑ 5.6%</span>
                                    </div>
                                    <div class="kpi-comparison">vs Apr 20 – May 20</div>
                                </div>
                                <div class="kpi-icon-box pink">
                                    <i data-lucide="hourglass" style="width: 16px; height: 16px;"></i>
                                </div>
                            </div>
                            <div class="kpi-sparkline-wrap">
                                <canvas id="spark-trial-users"></canvas>
                            </div>
                        </div>

                        <!-- 5. Monthly Revenue -->
                        <div class="kpi-card">
                            <div class="kpi-top-row">
                                <div>
                                    <div class="kpi-label">Monthly Revenue</div>
                                    <div class="kpi-value" style="margin-top: 4px;">$48,586</div>
                                    <div class="kpi-trend-row">
                                        <span class="kpi-change-tag up">↑ 18.9%</span>
                                    </div>
                                    <div class="kpi-comparison">vs Apr 20 – May 20</div>
                                </div>
                                <div class="kpi-icon-box green">
                                    <i data-lucide="dollar-sign" style="width: 16px; height: 16px;"></i>
                                </div>
                            </div>
                            <div class="kpi-sparkline-wrap">
                                <canvas id="spark-revenue"></canvas>
                            </div>
                        </div>

                        <!-- 6. Active Downloads -->
                        <div class="kpi-card">
                            <div class="kpi-top-row">
                                <div>
                                    <div class="kpi-label">Active Downloads</div>
                                    <div class="kpi-value" style="margin-top: 4px;">1,582</div>
                                    <div class="kpi-trend-row">
                                        <span class="kpi-change-tag up">↑ 7.3%</span>
                                    </div>
                                    <div class="kpi-comparison">vs Apr 20 – May 20</div>
                                </div>
                                <div class="kpi-icon-box cyan">
                                    <i data-lucide="download" style="width: 16px; height: 16px;"></i>
                                </div>
                            </div>
                            <div class="kpi-sparkline-wrap">
                                <canvas id="spark-downloads"></canvas>
                            </div>
                        </div>

                        <!-- 7. Current Version -->
                        <div class="kpi-card">
                            <div class="kpi-top-row">
                                <div>
                                    <div class="kpi-label">Current Version</div>
                                    <div class="kpi-value" style="margin-top: 4px;">v1.3.0</div>
                                    <div class="kpi-trend-row">
                                        <span class="badge badge-latest" style="font-size: 9.5px; padding: 1px 6px;">Latest</span>
                                    </div>
                                    <div class="kpi-comparison" style="margin-top: 4px;">Released 5 days ago</div>
                                </div>
                                <div class="kpi-icon-box teal">
                                    <i data-lucide="box" style="width: 16px; height: 16px;"></i>
                                </div>
                            </div>
                            <div style="height: 34px; display: flex; align-items: flex-end;">
                                <span style="font-size: 10.5px; color: var(--color-teal); font-weight: 700;">● Production Stable</span>
                            </div>
                        </div>
                    </div>

                    <!-- ── ROW 2: 3 CARDS ── -->
                    <div class="grid-row-3">
                        <div class="chart-card">
                            <div class="card-header">
                                <div>
                                    <span class="card-title">User Growth Overview <i data-lucide="info" style="width: 13px; height: 13px; color: var(--color-text-muted);"></i></span>
                                    <div class="chart-legend-row">
                                        <span><span class="legend-dot purple"></span> Total Users</span>
                                        <span><span class="legend-dot pink"></span> Premium Users</span>
                                    </div>
                                </div>
                                <div class="time-pill-group">
                                    <button class="time-pill-btn" onclick="window.edmApp.switchGrowthFilter('Daily', this)">Daily</button>
                                    <button class="time-pill-btn" onclick="window.edmApp.switchGrowthFilter('Weekly', this)">Weekly</button>
                                    <button class="time-pill-btn active" onclick="window.edmApp.switchGrowthFilter('Monthly', this)">Monthly</button>
                                    <button class="time-pill-btn" onclick="window.edmApp.switchGrowthFilter('Yearly', this)">Yearly</button>
                                </div>
                            </div>
                            <div class="chart-container-row2">
                                <canvas id="chart-user-growth-overview"></canvas>
                            </div>
                        </div>

                        <div class="card">
                            <div class="card-header">
                                <div>
                                    <span class="card-title">System Health <i data-lucide="info" style="width: 13px; height: 13px; color: var(--color-text-muted);"></i></span>
                                    <span style="font-size: 11px; color: var(--color-text-muted);">All systems are operational</span>
                                </div>
                                <a href="javascript:void(0)" class="btn-ghost" onclick="window.edmApp.navigateTo('system-health')">View All</a>
                            </div>
                            <table class="health-table" id="dashboard-health-table"></table>
                        </div>

                        <div class="card">
                            <div class="card-header">
                                <span class="card-title">Recent Activity</span>
                                <a href="javascript:void(0)" class="btn-ghost" onclick="window.edmApp.navigateTo('user-activity')">View All</a>
                            </div>
                            <div class="activity-feed-list" id="dashboard-activity-feed"></div>
                        </div>
                    </div>

                    <!-- ── ROW 3: 3 CARDS ── -->
                    <div class="grid-row-bottom">
                        <div class="card">
                            <div class="card-header">
                                <span class="card-title">Top Countries</span>
                            </div>
                            <div class="country-map-split">
                                <div class="world-map-svg-wrap">
                                    <svg viewBox="0 0 1000 500" style="width: 100%; height: 100%;" preserveAspectRatio="xMidYMid meet">
                                        <path d="M150,120 Q180,80 260,90 Q280,140 240,190 Q170,220 130,160 Z" fill="#6366F1" opacity="0.85" id="map-na" class="map-country-path" />
                                        <path d="M250,260 Q320,250 330,340 Q280,440 240,380 Q220,300 250,260 Z" fill="#4F46E5" opacity="0.75" id="map-sa" class="map-country-path" />
                                        <path d="M480,100 Q560,90 580,150 Q520,180 470,160 Q460,120 480,100 Z" fill="#8183FF" opacity="0.9" id="map-eu" class="map-country-path" />
                                        <path d="M480,200 Q580,190 590,280 Q550,380 490,340 Q450,250 480,200 Z" fill="#3B82F6" opacity="0.6" id="map-af" class="map-country-path" />
                                        <path d="M600,100 Q820,80 850,220 Q750,280 620,220 Q590,150 600,100 Z" fill="#6366F1" opacity="0.85" id="map-as" class="map-country-path" />
                                        <path d="M780,320 Q870,310 860,390 Q790,410 760,360 Z" fill="#0EA5E9" opacity="0.7" id="map-au" class="map-country-path" />
                                        <circle cx="210" cy="140" r="5" fill="#38BDF8"><animate attributeName="r" values="3;7;3" dur="2s" repeatCount="indefinite"/></circle>
                                        <circle cx="700" cy="200" r="5" fill="#EC4899"><animate attributeName="r" values="3;7;3" dur="2.2s" repeatCount="indefinite"/></circle>
                                        <circle cx="280" cy="330" r="5" fill="#10B981"><animate attributeName="r" values="3;7;3" dur="1.8s" repeatCount="indefinite"/></circle>
                                        <circle cx="520" cy="130" r="5" fill="#F59E0B"><animate attributeName="r" values="3;7;3" dur="2.4s" repeatCount="indefinite"/></circle>
                                        <circle cx="490" cy="120" r="5" fill="#8183FF"><animate attributeName="r" values="3;7;3" dur="1.9s" repeatCount="indefinite"/></circle>
                                    </svg>
                                </div>
                                <table class="country-table">
                                    <thead>
                                        <tr style="color: var(--color-text-muted); font-size: 10.5px;">
                                            <th>Country</th>
                                            <th style="text-align: right;">Users</th>
                                        </tr>
                                    </thead>
                                    <tbody id="dashboard-countries-tbody"></tbody>
                                </table>
                            </div>
                            <div style="margin-top: 10px; text-align: center;">
                                <button class="btn btn-secondary btn-sm w-full" style="width: 100%;" onclick="window.edmApp.showToast('Country Analytics view opened', 'info')">View All Countries</button>
                            </div>
                        </div>

                        <div class="card">
                            <div class="card-header">
                                <div>
                                    <span class="card-title">Download Analytics <i data-lucide="info" style="width: 13px; height: 13px; color: var(--color-text-muted);"></i></span>
                                    <div class="chart-legend-row">
                                        <span><span class="legend-dot purple"></span> Downloads</span>
                                        <span><span class="legend-dot green"></span> Bandwidth (GB)</span>
                                    </div>
                                </div>
                                <div class="time-pill-group" style="font-size: 11.5px; padding: 2px 8px; cursor: pointer;" onclick="window.edmApp.showToast('Filter: This Week', 'info')">
                                    <span>This Week ⌵</span>
                                </div>
                            </div>
                            <div style="height: 200px; width: 100%; position: relative;">
                                <canvas id="chart-download-combo"></canvas>
                            </div>
                        </div>

                        <div class="card">
                            <div class="card-header">
                                <span class="card-title">Trial Conversion</span>
                            </div>
                            <div class="donut-center-wrap">
                                <canvas id="chart-trial-donut"></canvas>
                                <div class="donut-label-center">
                                    <span class="donut-rate-num">23.7%</span>
                                    <span class="donut-rate-text">Conversion Rate</span>
                                </div>
                            </div>
                            <div class="donut-legend-split">
                                <div class="donut-stat-col">
                                    <span style="color: #10B981; font-weight: 700;">■ Converted</span>
                                    <strong style="color: var(--color-text-main);">1,582 (23.7%)</strong>
                                </div>
                                <div class="donut-stat-col">
                                    <span style="color: #38BDF8; font-weight: 700;">■ In Trial</span>
                                    <strong style="color: var(--color-text-main);">3,217 (48.1%)</strong>
                                </div>
                                <div class="donut-stat-col">
                                    <span style="color: #EC4899; font-weight: 700;">■ Expired</span>
                                    <strong style="color: var(--color-text-main);">1,887 (28.2%)</strong>
                                </div>
                            </div>
                            <div style="margin-top: 10px; text-align: center;">
                                <a href="javascript:void(0)" class="btn-ghost" onclick="window.edmApp.navigateTo('trials')">View Full Report</a>
                            </div>
                        </div>
                    </div>
                </div>

                <!-- VIEW 2: USERS DIRECTORY -->
                <div class="view-page" id="view-users">
                    <div class="page-subheader">
                        <div class="page-title-wrap">
                            <h1>User Directory</h1>
                            <p>Manage active accounts, hardware binds (HWID), and subscription status</p>
                        </div>
                        <div class="subheader-actions">
                            <button class="btn btn-secondary" onclick="window.edmApp.exportUsersCSV()">
                                <i data-lucide="download" style="width: 14px; height: 14px;"></i> Export CSV
                            </button>
                        </div>
                    </div>
                    <div class="table-container">
                        <table class="data-table">
                            <thead>
                                <tr>
                                    <th>User</th>
                                    <th>Country</th>
                                    <th>Plan</th>
                                    <th>Status</th>
                                    <th>Devices</th>
                                    <th>Last Active</th>
                                    <th style="text-align: right;">Action</th>
                                </tr>
                            </thead>
                            <tbody id="users-table-body"></tbody>
                        </table>
                    </div>
                </div>

                <!-- VIEW 3: DEVICES TELEMETRY -->
                <div class="view-page" id="view-devices">
                    <div class="page-subheader">
                        <div class="page-title-wrap">
                            <h1>Registered Client Devices</h1>
                            <p>Hardware identities, Windows telemetry, and client versions</p>
                        </div>
                    </div>
                    <div class="table-container">
                        <table class="data-table">
                            <thead>
                                <tr>
                                    <th>Device Name</th>
                                    <th>User</th>
                                    <th>OS Version</th>
                                    <th>EDM Build</th>
                                    <th>IP (Masked)</th>
                                    <th>Status</th>
                                    <th style="text-align: right;">Action</th>
                                </tr>
                            </thead>
                            <tbody id="devices-table-body"></tbody>
                        </table>
                    </div>
                </div>

                <!-- VIEW 4: RELEASE MANAGER -->
                <div class="view-page" id="view-releases">
                    <div class="page-subheader">
                        <div class="page-title-wrap">
                            <h1>Release Manager</h1>
                            <p>Manage application builds, publish channels, and installer binaries</p>
                        </div>
                        <div class="subheader-actions">
                            <button class="btn btn-primary" onclick="window.edmApp.openModal('modal-release-wizard')">
                                <i data-lucide="plus-circle" style="width: 14px; height: 14px;"></i> Create Release
                            </button>
                        </div>
                    </div>
                    <div class="table-container">
                        <table class="data-table">
                            <thead>
                                <tr>
                                    <th>Version</th>
                                    <th>Name</th>
                                    <th>Date</th>
                                    <th>Type</th>
                                    <th>File Size</th>
                                    <th>Downloads</th>
                                    <th style="text-align: right;">Action</th>
                                </tr>
                            </thead>
                            <tbody id="releases-table-body"></tbody>
                        </table>
                    </div>
                </div>

                <!-- VIEW 5: SYSTEM HEALTH -->
                <div class="view-page" id="view-system-health">
                    <div class="page-subheader">
                        <div class="page-title-wrap">
                            <h1>System Health & Microservices</h1>
                            <p>Operational latency, uptime metrics, and service node health</p>
                        </div>
                    </div>
                    <div class="card">
                        <div class="card-header">
                            <span class="card-title">Microservice Cluster (8 Nodes)</span>
                            <span class="badge badge-recommended">99.98% 30-Day SLA</span>
                        </div>
                        <div id="full-system-health-list"></div>
                    </div>
                </div>

                <!-- VIEW 6: WEBSITE CMS MANAGER -->
                <div class="view-page" id="view-website-manager">
                    <div class="page-subheader">
                        <div class="page-title-wrap">
                            <h1>Website Content & Landing Page CMS</h1>
                            <p>Synchronize landing page copy, hero announcements, and release notes</p>
                        </div>
                        <div class="subheader-actions">
                            <a href="<?php echo esc_url(home_url('/')); ?>" target="_blank" class="btn btn-secondary">
                                <i data-lucide="external-link" style="width: 14px; height: 14px;"></i> View Public Site
                            </a>
                            <button class="btn btn-primary" onclick="window.edmApp.showToast('Published updates to live website!', 'success')">
                                <i data-lucide="upload-cloud" style="width: 14px; height: 14px;"></i> Publish to Live
                            </button>
                        </div>
                    </div>
                    <div style="display: grid; grid-template-columns: 1.2fr 1fr; gap: 16px;">
                        <div class="card">
                            <span class="card-title"><i data-lucide="layout"></i> Public Landing Hero Editor</span>
                            <div style="display: flex; flex-direction: column; gap: 14px; margin-top: 14px;">
                                <div class="form-group">
                                    <label class="form-label">Top Banner Text</label>
                                    <input type="text" class="form-input-full" value="NEW RELEASE v1.3.0 — 32x Multi-Thread Download Acceleration Available Now">
                                </div>
                                <div class="form-group">
                                    <label class="form-label">Headline Title</label>
                                    <input type="text" class="form-input-full" value="The Fastest Download Manager for Windows">
                                </div>
                                <div class="form-group">
                                    <label class="form-label">Hero Description</label>
                                    <textarea class="form-input-full" rows="3">Experience blistering 32-stream socket acceleration, automatic 4K/8K video stream capture, and smart resume resilience.</textarea>
                                </div>
                                <button class="btn btn-primary" onclick="window.edmApp.showToast('Hero draft saved locally', 'success')">Save Hero Draft</button>
                            </div>
                        </div>
                        <div class="card">
                            <div class="card-header">
                                <span class="card-title"><i data-lucide="smartphone"></i> Live Landing Preview</span>
                                <a href="<?php echo esc_url(home_url('/')); ?>" target="_blank" class="btn-ghost">Open Fullscreen</a>
                            </div>
                            <div style="border: 1px solid var(--color-border); border-radius: var(--radius-md); overflow: hidden; height: 320px; background: #000;">
                                <iframe src="<?php echo esc_url(home_url('/')); ?>" style="width: 100%; height: 100%; border: none;"></iframe>
                            </div>
                        </div>
                    </div>
                </div>

                <!-- Fallback Container -->
                <div class="view-page" id="view-generic">
                    <div class="page-subheader">
                        <div class="page-title-wrap">
                            <h1 id="generic-view-title">Module View</h1>
                            <p>Integrated EDM Control Plane Enterprise Module</p>
                        </div>
                    </div>
                    <div class="card">
                        <p style="color: var(--color-text-secondary);">This section is fully active and wired to the EDM Design Tokens & State Manager.</p>
                    </div>
                </div>

            </main>

            <!-- Global Footer Bar -->
            <footer class="app-footer-bar">
                <span>© <?php echo date('Y'); ?> EDM - Exclusive Download Manager. All rights reserved.</span>
                <span style="color: var(--color-primary-light);">UI Prototype – Backend Integration Pending</span>
            </footer>
        </div>
    </div>

    <!-- Modals -->
    <div class="modal-backdrop" id="modal-release-wizard">
        <div class="modal-card">
            <div class="modal-header">
                <span class="modal-title"><i data-lucide="package" style="color: var(--color-primary);"></i> Create New Release</span>
                <button class="btn-icon-only" onclick="window.edmApp.closeModal('modal-release-wizard')"><i data-lucide="x"></i></button>
            </div>
            <div class="modal-body">
                <div class="form-grid-2">
                    <div class="form-group">
                        <label class="form-label">Version Number *</label>
                        <input type="text" id="rel-input-version" value="v1.4.0" class="form-input-full">
                    </div>
                    <div class="form-group">
                        <label class="form-label">Release Name *</label>
                        <input type="text" id="rel-input-name" value="Multi-Stream Turbo Patch" class="form-input-full">
                    </div>
                </div>
                <div class="form-group">
                    <label class="form-label">Release Notes</label>
                    <textarea id="rel-input-notes" class="form-input-full" rows="3">• 32x Multi-Thread Acceleration&#10;• 4K Video Grabber fixes</textarea>
                </div>
            </div>
            <div class="modal-footer">
                <button class="btn btn-secondary" onclick="window.edmApp.closeModal('modal-release-wizard')">Cancel</button>
                <button class="btn btn-primary" onclick="window.edmApp.handlePublishRelease()">Publish Release</button>
            </div>
        </div>
    </div>

    <!-- Command Palette (Ctrl + K) -->
    <div class="modal-backdrop" id="cmd-palette" style="align-items: flex-start; padding-top: 10vh;">
        <div class="modal-card" style="max-width: 540px;">
            <div style="padding: 14px 18px; border-bottom: 1px solid var(--color-border); display: flex; align-items: center; gap: 10px;">
                <i data-lucide="search" style="width: 16px; height: 16px; color: var(--color-primary);"></i>
                <input type="text" id="cmd-search-input" placeholder="Type a command or search (e.g., users, release, health)..." style="border: none; background: transparent; width: 100%; outline: none; color: #FFF; font-size: 14px;">
            </div>
            <div style="padding: 10px; display: flex; flex-direction: column; gap: 4px;">
                <div class="nav-item" onclick="window.edmApp.navigateTo('dashboard'); window.edmApp.closeCommandPalette();">Dashboard Overview</div>
                <div class="nav-item" onclick="window.edmApp.navigateTo('users'); window.edmApp.closeCommandPalette();">Users Directory</div>
                <div class="nav-item" onclick="window.edmApp.navigateTo('releases'); window.edmApp.closeCommandPalette();">Release Manager</div>
                <div class="nav-item" onclick="window.edmApp.navigateTo('system-health'); window.edmApp.closeCommandPalette();">System Health</div>
                <div class="nav-item" onclick="window.edmApp.navigateTo('website-manager'); window.edmApp.closeCommandPalette();">Website CMS</div>
            </div>
        </div>
    </div>

    <div class="toast-container" id="toast-container"></div>

<?php
get_footer();
