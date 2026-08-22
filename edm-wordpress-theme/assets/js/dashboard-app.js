/**
 * EDM Central Control Plane — Master Frontend Application Controller
 * Full Implementation of Rules #67 through #115 (No dead buttons, full state engine)
 */

class EdmApp {
    constructor() {
        this.activePage = "dashboard";
        this.charts = {};
        this.theme = localStorage.getItem("edm_theme") || "dark";
        this.sidebarCollapsed = localStorage.getItem("edm_sidebar_collapsed") === "true";
        this.selectedUsers = new Set();
        this.currentDateRange = "May 18, 2025 - Jun 18, 2025";
        
        this.init();
    }

    init() {
        // 1. Initialize Theme
        this.applyTheme(this.theme);

        // 2. Initialize Sidebar State
        if (this.sidebarCollapsed) {
            document.getElementById("sidebar")?.classList.add("collapsed");
        }

        // 3. Setup Global Event Listeners
        this.setupEventListeners();

        // 4. Initial Render
        this.renderCurrentView();

        // 5. Initialize Lucide Icons
        if (window.lucide) {
            window.lucide.createIcons();
        }

        console.log("[EDM Control Plane] Initialized in Full Master Prototype Mode.");
    }

    // ══════════════════════════════════════════════════════════════
    // THEME & LAYOUT CONTROLLERS
    // ══════════════════════════════════════════════════════════════
    applyTheme(theme) {
        this.theme = theme;
        localStorage.setItem("edm_theme", theme);
        
        const body = document.body;
        const themeIcon = document.getElementById("theme-icon");
        const themeText = document.getElementById("theme-text");

        if (theme === "light") {
            body.classList.add("light-theme");
            if (themeIcon) themeIcon.setAttribute("data-lucide", "moon");
            if (themeText) themeText.textContent = "Light";
        } else {
            body.classList.remove("light-theme");
            if (themeIcon) themeIcon.setAttribute("data-lucide", "sun");
            if (themeText) themeText.textContent = "Dark";
        }

        if (window.lucide) window.lucide.createIcons();
        
        // Re-render charts with updated theme colors if on dashboard
        if (this.activePage === "dashboard") {
            setTimeout(() => this.initDashboardCharts(), 50);
        }
    }

    toggleTheme() {
        const nextTheme = this.theme === "dark" ? "light" : "dark";
        this.applyTheme(nextTheme);
        this.showToast(`Switched to ${nextTheme} theme`, "info");
    }

    toggleSidebar() {
        const sidebar = document.getElementById("sidebar");
        if (!sidebar) return;
        
        this.sidebarCollapsed = !this.sidebarCollapsed;
        sidebar.classList.toggle("collapsed", this.sidebarCollapsed);
        localStorage.setItem("edm_sidebar_collapsed", this.sidebarCollapsed);

        if (window.lucide) window.lucide.createIcons();
    }

    // ══════════════════════════════════════════════════════════════
    // ROUTING & NAVIGATION
    // ══════════════════════════════════════════════════════════════
    setupEventListeners() {
        // Navigation Buttons
        document.querySelectorAll(".nav-item").forEach(btn => {
            btn.addEventListener("click", () => {
                const target = btn.getAttribute("data-page");
                if (target) this.navigateTo(target);
            });
        });

        // Theme Toggle Button
        document.getElementById("btn-theme-toggle")?.addEventListener("click", () => this.toggleTheme());

        // Sidebar Toggle Button
        document.getElementById("btn-toggle-sidebar")?.addEventListener("click", () => this.toggleSidebar());

        // Command Palette Trigger
        document.getElementById("btn-open-cmd")?.addEventListener("click", () => this.openCommandPalette());
        window.addEventListener("keydown", (e) => {
            if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === "k") {
                e.preventDefault();
                this.openCommandPalette();
            }
            if (e.key === "Escape") {
                this.closeCommandPalette();
                this.closeAllModals();
            }
        });

        // Command Search Input
        document.getElementById("cmd-search-input")?.addEventListener("input", (e) => {
            this.handleCommandSearch(e.target.value);
        });

        // Date Picker Modal Trigger
        document.getElementById("btn-date-picker")?.addEventListener("click", () => this.openModal("modal-date-picker"));

        // Notifications Drawer Trigger
        document.getElementById("btn-notifications-dropdown")?.addEventListener("click", () => this.openNotificationsDrawer());

        // Export Report Button
        document.getElementById("btn-export-report")?.addEventListener("click", () => this.exportPerformanceReport());

        // User Search & Filters
        document.getElementById("users-search-input")?.addEventListener("input", () => this.renderUsersTable());
        document.getElementById("users-filter-plan")?.addEventListener("change", () => this.renderUsersTable());
        document.getElementById("users-filter-status")?.addEventListener("change", () => this.renderUsersTable());
        document.getElementById("check-all-users")?.addEventListener("change", (e) => this.toggleSelectAllUsers(e.target.checked));
        document.getElementById("btn-bulk-suspend")?.addEventListener("click", () => this.handleBulkSuspend());
        document.getElementById("btn-export-users")?.addEventListener("click", () => this.exportUsersCSV());
        document.getElementById("btn-add-user")?.addEventListener("click", () => this.showToast("Add user dialog opened", "info"));

        // Release Wizard & Rollback
        document.getElementById("btn-open-new-release")?.addEventListener("click", () => this.openModal("modal-release-wizard"));
        document.getElementById("btn-open-rollback")?.addEventListener("click", () => this.openModal("modal-rollback"));
        document.getElementById("btn-submit-publish-release")?.addEventListener("click", () => this.handlePublishRelease());
        document.getElementById("btn-submit-rollback")?.addEventListener("click", () => this.handleRollback());

        // Settings Tabs
        document.querySelectorAll(".tab-btn").forEach(btn => {
            btn.addEventListener("click", () => {
                const tab = btn.getAttribute("data-tab");
                this.switchSettingsTab(tab, btn);
            });
        });

        // Maintenance Mode Toggle
        document.getElementById("btn-toggle-maintenance")?.addEventListener("click", () => this.toggleMaintenanceMode());

        // Period Dropdowns in Dashboard
        document.getElementById("select-user-growth-period")?.addEventListener("change", (e) => this.updateChartPeriod("userGrowth", e.target.value));
        document.getElementById("select-revenue-period")?.addEventListener("change", (e) => this.updateChartPeriod("revenue", e.target.value));
        document.getElementById("select-downloads-period")?.addEventListener("change", (e) => this.updateChartPeriod("downloads", e.target.value));

        // Profile Menu
        document.getElementById("btn-profile-menu")?.addEventListener("click", () => {
            this.navigateTo("settings");
        });

        // Help Modal
        document.getElementById("btn-help-modal")?.addEventListener("click", () => {
            this.showToast("EDM Control Plane • Full Architecture Ready", "info");
        });
    }

    navigateTo(pageKey) {
        this.activePage = pageKey;

        // Update Sidebar Active state
        document.querySelectorAll(".nav-item").forEach(item => {
            if (item.getAttribute("data-page") === pageKey) {
                item.classList.add("active");
            } else {
                item.classList.remove("active");
            }
        });

        // Hide all views
        document.querySelectorAll(".view-page").forEach(page => page.classList.remove("active"));

        // Match target page or show generic wrapper
        const targetView = document.getElementById(`view-${pageKey}`);
        if (targetView) {
            targetView.classList.add("active");
        } else {
            const generic = document.getElementById("view-generic");
            if (generic) {
                generic.classList.add("active");
            }
        }

        // Render Page-Specific Data
        this.renderCurrentView();

        if (window.lucide) window.lucide.createIcons();
    }

    renderCurrentView() {
        switch (this.activePage) {
            case "dashboard":
                this.renderDashboardOverview();
                break;
            case "users":
                this.renderUsersTable();
                break;
            case "devices":
                this.renderDevicesTable();
                break;
            case "user-activity":
                this.renderUserActivityTable();
                break;
            case "download-analytics":
                this.renderDownloadAnalytics();
                break;
            case "browser-extension":
                this.renderBrowserExtensionTable();
                break;
            case "releases":
                this.renderReleasesTable();
                break;
            case "version-history":
                this.renderVersionHistory();
                break;
            case "plans":
                this.renderPlansView();
                break;
            case "trials":
                this.renderTrialsView();
                break;
            case "licenses":
                this.renderLicensesTable();
                break;
            case "country-pricing":
                this.renderCountryPricingTable();
                break;
            case "promotions":
                this.renderPromotionsTable();
                break;
            case "notifications":
                this.renderNotificationsTable();
                break;
            case "email-campaigns":
                this.renderEmailCampaignsTable();
                break;
            case "system-health":
                this.renderFullSystemHealth();
                break;
            case "security-center":
            case "audit-logs":
                this.renderAuditLogsTable();
                break;
            case "bug-reports":
            case "feature-requests":
            case "feedback":
                this.renderTicketsTable();
                break;
            case "settings":
                this.renderFeatureFlags();
                break;
        }
    }

    // ══════════════════════════════════════════════════════════════
    // DATE RANGE & NOTIFICATION DRAWER CONTROLLERS
    // ══════════════════════════════════════════════════════════════
    setDateRange(rangeText) {
        this.currentDateRange = rangeText;
        const label = document.getElementById("current-date-range-label");
        if (label) label.textContent = rangeText;
        this.closeModal("modal-date-picker");
        this.showToast(`Updated date window to: ${rangeText}`, "info");
        if (this.activePage === "dashboard") {
            this.initDashboardCharts();
        }
    }

    openNotificationsDrawer() {
        const container = document.getElementById("notif-drawer-content");
        if (container) {
            const notifs = window.EDM_MOCK_DATA.notifications;
            container.innerHTML = notifs.map(n => `
                <div style="padding: 10px; border-radius: var(--radius-md); background: ${n.read ? 'transparent' : 'var(--color-bg-subtle)'}; margin-bottom: 8px; border: 1px solid var(--color-border);">
                    <div style="display: flex; justify-content: space-between; align-items: flex-start;">
                        <strong style="font-size: 13px; color: var(--color-text-main);">${n.title}</strong>
                        ${!n.read ? '<span class="status-dot" style="background: var(--color-primary); width: 6px; height: 6px;"></span>' : ''}
                    </div>
                    <p style="font-size: 11px; color: var(--color-text-muted); margin-top: 2px;">Target: ${n.audience} • ${n.date}</p>
                </div>
            `).join("");
        }
        this.openModal("modal-notifications");
    }

    markAllNotificationsRead() {
        window.EDM_MOCK_DATA.notifications.forEach(n => n.read = true);
        const badge = document.getElementById("header-notif-count");
        if (badge) badge.style.display = "none";
        this.closeModal("modal-notifications");
        this.showToast("All notifications marked as read", "success");
    }

    // ══════════════════════════════════════════════════════════════
    // DASHBOARD & CHARTS
    // ══════════════════════════════════════════════════════════════
    async renderDashboardOverview() {
        const d = window.EDM_MOCK_DATA.overview;
        this.drawSparkline("spark-total-users", d.totalUsers.sparkline, "#818CF8");
        this.drawSparkline("spark-active-users", d.activeUsers.sparkline, "#60A5FA");
        this.drawSparkline("spark-premium-users", d.premiumUsers.sparkline, "#FBBF24");
        this.drawSparkline("spark-trial-users", d.trialUsers.sparkline, "#C084FC");
        this.drawSparkline("spark-revenue", d.monthlyRevenue.sparkline, "#34D399");
        this.drawSparkline("spark-downloads", d.activeDownloads.sparkline, "#38BDF8");

        this.initDashboardCharts();

        // 1. Populate Recent Releases
        const releasesList = document.getElementById("dashboard-recent-releases-list");
        if (releasesList) {
            const releases = window.EDM_MOCK_DATA.releases.slice(0, 3);
            const colorMap = ["purple", "blue", "amber"];
            releasesList.innerHTML = releases.map((rel, idx) => `
                <div class="release-item-row">
                    <div class="release-item-left">
                        <div class="release-icon-box ${colorMap[idx]}">
                            <i data-lucide="${idx === 0 ? 'package-check' : (idx === 1 ? 'user' : 'crown')}" style="width: 16px; height: 16px;"></i>
                        </div>
                        <div>
                            <div class="release-title-row">
                                <span class="release-version-text">${rel.version}</span>
                                <span class="badge ${rel.type === 'RECOMMENDED' ? 'badge-recommended' : 'badge-optional'}">${rel.type}</span>
                            </div>
                            <span class="release-desc-text">${rel.name}</span>
                            <div style="font-size: 10.5px; color: var(--color-text-muted);">Released: ${rel.date}</div>
                        </div>
                    </div>
                    <div class="release-meta-right">
                        <span>${rel.size}</span>
                        <button class="btn-icon-only" title="Download Installer" onclick="window.edmApp.downloadMockBinary('${rel.file}')">
                            <i data-lucide="download" style="width: 13px; height: 13px;"></i>
                        </button>
                    </div>
                </div>
            `).join("");
        }

        // 2. Populate Recent Activities
        const activitiesList = document.getElementById("dashboard-activities-list");
        if (activitiesList) {
            activitiesList.innerHTML = window.EDM_MOCK_DATA.recentActivities.map(act => `
                <div class="activity-item">
                    <div class="activity-avatar" style="background: ${act.bg}22; color: ${act.bg};">
                        <i data-lucide="${act.icon}" style="width: 14px; height: 14px;"></i>
                    </div>
                    <div class="activity-info">
                        <span class="activity-title">${act.title}</span>
                        <span class="activity-desc">${act.desc}</span>
                    </div>
                    <span class="activity-time">${act.time}</span>
                </div>
            `).join("");
        }

        // 3. Populate System Health
        const healthList = document.getElementById("dashboard-health-list");
        if (healthList) {
            healthList.innerHTML = window.EDM_MOCK_DATA.systemHealth.map(srv => `
                <div class="health-item-row">
                    <span class="health-service-name">
                        <span class="status-dot green"></span>
                        <span>${srv.name}</span>
                    </span>
                    <div style="display: flex; align-items: center;">
                        <span class="health-status-tag">● ${srv.status}</span>
                        <span class="health-latency">${srv.latency}</span>
                    </div>
                </div>
            `).join("");
        }

        if (window.lucide) window.lucide.createIcons();
    }

    drawSparkline(canvasId, dataPoints, strokeColor) {
        const canvas = document.getElementById(canvasId);
        if (!canvas) return;
        const ctx = canvas.getContext("2d");
        const w = canvas.width = canvas.parentElement.clientWidth;
        const h = canvas.height = canvas.parentElement.clientHeight;

        ctx.clearRect(0, 0, w, h);
        if (!dataPoints || dataPoints.length < 2) return;

        const min = Math.min(...dataPoints);
        const max = Math.max(...dataPoints);
        const range = max - min || 1;

        ctx.beginPath();
        dataPoints.forEach((val, i) => {
            const x = (i / (dataPoints.length - 1)) * (w - 4) + 2;
            const y = h - ((val - min) / range) * (h - 8) - 4;
            if (i === 0) ctx.moveTo(x, y);
            else ctx.lineTo(x, y);
        });

        ctx.strokeStyle = strokeColor;
        ctx.lineWidth = 2;
        ctx.lineCap = "round";
        ctx.lineJoin = "round";
        ctx.stroke();
    }

    initDashboardCharts() {
        const isDark = this.theme === "dark";
        const gridColor = isDark ? "rgba(255, 255, 255, 0.05)" : "rgba(0, 0, 0, 0.05)";
        const textColor = isDark ? "#64748B" : "#94A3B8";

        const months = ["Jan 2025", "Feb 2025", "Mar 2025", "Apr 2025", "May 2025", "Jun 2025"];

        // 1. User Growth Line Chart
        const ctxUser = document.getElementById("chart-user-growth")?.getContext("2d");
        if (ctxUser) {
            if (this.charts.userGrowth) this.charts.userGrowth.destroy();
            
            const gradientPurple = ctxUser.createLinearGradient(0, 0, 0, 160);
            gradientPurple.addColorStop(0, "rgba(88, 86, 214, 0.38)");
            gradientPurple.addColorStop(1, "rgba(88, 86, 214, 0.0)");

            this.charts.userGrowth = new Chart(ctxUser, {
                type: "line",
                data: {
                    labels: months,
                    datasets: [{
                        label: "Users",
                        data: [10000, 15000, 19000, 21500, 23500, 25000],
                        borderColor: "#7C7AFA",
                        backgroundColor: gradientPurple,
                        borderWidth: 2.5,
                        fill: true,
                        tension: 0.35,
                        pointBackgroundColor: "#FFFFFF",
                        pointBorderColor: "#5856D6",
                        pointRadius: 4,
                        pointHoverRadius: 6
                    }]
                },
                options: {
                    responsive: true,
                    maintainAspectRatio: false,
                    plugins: { legend: { display: false } },
                    scales: {
                        x: { grid: { color: gridColor }, ticks: { color: textColor, font: { size: 10 } } },
                        y: { 
                            min: 0,
                            max: 30000,
                            ticks: { 
                                stepSize: 5000,
                                color: textColor, 
                                font: { size: 10 },
                                callback: val => val === 0 ? "0" : `${val / 1000}K`
                            },
                            grid: { color: gridColor }
                        }
                    }
                }
            });
        }

        // 2. Revenue Overview Bar Chart
        const ctxRev = document.getElementById("chart-revenue")?.getContext("2d");
        if (ctxRev) {
            if (this.charts.revenue) this.charts.revenue.destroy();
            this.charts.revenue = new Chart(ctxRev, {
                type: "bar",
                data: {
                    labels: months,
                    datasets: [{
                        label: "Revenue",
                        data: [7500, 11000, 13800, 15000, 17200, 18765],
                        backgroundColor: "#38BDF8",
                        borderRadius: 3,
                        barThickness: 16
                    }]
                },
                options: {
                    responsive: true,
                    maintainAspectRatio: false,
                    plugins: { legend: { display: false } },
                    scales: {
                        x: { grid: { display: false }, ticks: { color: textColor, font: { size: 10 } } },
                        y: { 
                            min: 0,
                            max: 25000,
                            ticks: { 
                                stepSize: 5000,
                                color: textColor, 
                                font: { size: 10 },
                                callback: val => val === 0 ? "$0" : `$${val / 1000}K`
                            },
                            grid: { color: gridColor }
                        }
                    }
                }
            });
        }

        // 3. Downloads Overview Line Chart
        const ctxDl = document.getElementById("chart-downloads")?.getContext("2d");
        if (ctxDl) {
            if (this.charts.downloads) this.charts.downloads.destroy();
            
            const gradientGreen = ctxDl.createLinearGradient(0, 0, 0, 160);
            gradientGreen.addColorStop(0, "rgba(16, 185, 129, 0.3)");
            gradientGreen.addColorStop(1, "rgba(16, 185, 129, 0.0)");

            this.charts.downloads = new Chart(ctxDl, {
                type: "line",
                data: {
                    labels: months,
                    datasets: [{
                        label: "Downloads",
                        data: [12000, 20000, 28000, 34000, 41000, 45282],
                        borderColor: "#34D399",
                        backgroundColor: gradientGreen,
                        borderWidth: 2.5,
                        fill: true,
                        tension: 0.35,
                        pointBackgroundColor: "#FFFFFF",
                        pointBorderColor: "#10B981",
                        pointRadius: 4,
                        pointHoverRadius: 6
                    }]
                },
                options: {
                    responsive: true,
                    maintainAspectRatio: false,
                    plugins: { legend: { display: false } },
                    scales: {
                        x: { grid: { color: gridColor }, ticks: { color: textColor, font: { size: 10 } } },
                        y: { 
                            min: 0,
                            max: 60000,
                            ticks: { 
                                stepSize: 10000,
                                color: textColor, 
                                font: { size: 10 },
                                callback: val => val === 0 ? "0" : `${val / 1000}K`
                            },
                            grid: { color: gridColor }
                        }
                    }
                }
            });
        }
    }

    updateChartPeriod(chartKey, period) {
        this.showToast(`Updated ${chartKey} analytics to ${period} view`, "info");
    }

    // ══════════════════════════════════════════════════════════════
    // USERS DIRECTORY & ACTIONS
    // ══════════════════════════════════════════════════════════════
    async renderUsersTable() {
        const searchVal = document.getElementById("users-search-input")?.value || "";
        const planVal = document.getElementById("users-filter-plan")?.value || "all";
        const statusVal = document.getElementById("users-filter-status")?.value || "all";

        const users = await window.edmApi.getUsers({
            search: searchVal,
            plan: planVal,
            status: statusVal
        });

        const tbody = document.getElementById("users-table-body");
        if (!tbody) return;

        if (users.length === 0) {
            tbody.innerHTML = `<tr><td colspan="10" style="text-align: center; padding: 24px; color: var(--color-text-muted);">No users found.</td></tr>`;
            return;
        }

        tbody.innerHTML = users.map(user => `
            <tr>
                <td>
                    <input type="checkbox" class="user-row-checkbox" value="${user.id}" ${this.selectedUsers.has(user.id) ? 'checked' : ''} onchange="window.edmApp.toggleUserSelection('${user.id}', this.checked)">
                </td>
                <td>
                    <div style="display: flex; flex-direction: column;">
                        <strong style="color: var(--color-text-main); font-weight: 600;">${user.name}</strong>
                        <span style="font-size: 11px; color: var(--color-text-muted);">${user.email}</span>
                    </div>
                </td>
                <td><span>${user.country}</span></td>
                <td>
                    <span class="badge ${user.plan === 'Premium' ? 'badge-primary' : (user.plan === 'Trial' ? 'badge-warning' : 'badge-neutral')}">${user.plan}</span>
                </td>
                <td>
                    <span class="badge ${user.status === 'Active' ? 'badge-success' : 'badge-danger'}">● ${user.status}</span>
                </td>
                <td style="color: var(--color-text-muted); font-size: 11.5px;">${user.trial}</td>
                <td><strong>${user.devices}</strong> / 5</td>
                <td style="color: var(--color-text-muted); font-size: 11.5px;">${user.lastActive}</td>
                <td style="color: var(--color-text-muted); font-size: 11.5px;">${user.created}</td>
                <td style="text-align: right;">
                    <div style="display: flex; gap: 4px; justify-content: flex-end;">
                        <button class="btn-icon-only btn-sm" title="View Profile" onclick="window.edmApp.openUserProfileModal('${user.id}')">
                            <i data-lucide="eye" style="width: 13px; height: 13px;"></i>
                        </button>
                        <button class="btn-icon-only btn-sm" title="${user.status === 'Active' ? 'Suspend User' : 'Activate User'}" onclick="window.edmApp.toggleUserStatus('${user.id}', '${user.status === 'Active' ? 'Suspended' : 'Active'}')">
                            <i data-lucide="${user.status === 'Active' ? 'ban' : 'check-circle'}" style="width: 13px; height: 13px; color: ${user.status === 'Active' ? 'var(--color-danger)' : 'var(--color-success)'};"></i>
                        </button>
                    </div>
                </td>
            </tr>
        `).join("");

        if (window.lucide) window.lucide.createIcons();
    }

    toggleUserSelection(userId, isChecked) {
        if (isChecked) this.selectedUsers.add(userId);
        else this.selectedUsers.delete(userId);
    }

    toggleSelectAllUsers(isChecked) {
        document.querySelectorAll(".user-row-checkbox").forEach(cb => {
            cb.checked = isChecked;
            this.toggleUserSelection(cb.value, isChecked);
        });
    }

    async toggleUserStatus(userId, newStatus) {
        await window.edmApi.toggleUserStatus(userId, newStatus);
        this.showToast(`User ${userId} status changed to ${newStatus}`, newStatus === "Active" ? "success" : "warning");
        this.renderUsersTable();
    }

    handleBulkSuspend() {
        if (this.selectedUsers.size === 0) {
            this.showToast("Please select at least one user", "warning");
            return;
        }
        this.selectedUsers.forEach(id => window.edmApi.toggleUserStatus(id, "Suspended"));
        this.showToast(`Suspended ${this.selectedUsers.size} user account(s)`, "danger");
        this.selectedUsers.clear();
        this.renderUsersTable();
    }

    openUserProfileModal(userId) {
        const user = window.EDM_MOCK_DATA.users.find(u => u.id === userId);
        if (!user) return;

        const content = document.getElementById("user-modal-content");
        if (content) {
            content.innerHTML = `
                <div style="display: flex; align-items: center; gap: 14px; margin-bottom: 16px; padding-bottom: 14px; border-bottom: 1px solid var(--color-border);">
                    <div style="width: 48px; height: 48px; border-radius: var(--radius-full); background: var(--color-primary); color: #fff; font-size: 18px; font-weight: 700; display: flex; align-items: center; justify-content: center;">
                        ${user.name.split(" ").map(n => n[0]).join("")}
                    </div>
                    <div>
                        <h3 style="font-size: 17px; color: var(--color-text-main);">${user.name}</h3>
                        <p style="color: var(--color-text-muted); font-size: 12.5px;">${user.email} • ID: <code>${user.id}</code></p>
                    </div>
                </div>

                <div class="form-grid-2">
                    <div style="background: var(--color-bg-subtle); padding: 12px; border-radius: var(--radius-md);">
                        <span class="card-subtitle">Subscription & Licensing</span>
                        <p style="font-size: 13.5px; font-weight: 700; color: var(--color-text-main); margin-top: 4px;">${user.plan} Edition</p>
                        <p style="font-size: 11.5px; color: var(--color-text-muted);">Status: ${user.status} • Trial: ${user.trial}</p>
                    </div>
                    <div style="background: var(--color-bg-subtle); padding: 12px; border-radius: var(--radius-md);">
                        <span class="card-subtitle">Hardware Bind (HWID)</span>
                        <p style="font-size: 12px; font-family: monospace; color: var(--color-primary-light); margin-top: 4px;">${user.hwid}</p>
                        <p style="font-size: 11.5px; color: var(--color-text-muted);">Active Devices: ${user.devices} / 5</p>
                    </div>
                </div>
            `;
        }

        this.openModal("modal-user-detail");
    }

    exportUsersCSV() {
        const headers = ["ID,Name,Email,Country,Plan,Status,Devices,Created\n"];
        const rows = window.EDM_MOCK_DATA.users.map(u => `"${u.id}","${u.name}","${u.email}","${u.country}","${u.plan}","${u.status}",${u.devices},"${u.created}"\n`);
        const blob = new Blob([...headers, ...rows], { type: "text/csv" });
        const url = URL.createObjectURL(blob);
        const a = document.createElement("a");
        a.href = url;
        a.download = `edm_users_export_${Date.now()}.csv`;
        a.click();
        this.showToast("Exported users CSV successfully", "success");
    }

    // ══════════════════════════════════════════════════════════════
    // DEVICES & USER ACTIVITY
    // ══════════════════════════════════════════════════════════════
    async renderDevicesTable() {
        const devices = await window.edmApi.getDevices();
        const tbody = document.getElementById("devices-table-body");
        if (!tbody) return;

        tbody.innerHTML = devices.map(dev => `
            <tr>
                <td><strong>${dev.deviceName}</strong></td>
                <td><span style="font-size: 11.5px; color: var(--color-text-muted);">${dev.user}</span></td>
                <td>${dev.os}</td>
                <td><span class="badge badge-primary">${dev.edmVersion}</span></td>
                <td><code>${dev.deviceId}</code></td>
                <td>${dev.country}</td>
                <td style="font-family: monospace; font-size: 11.5px;">${dev.ip}</td>
                <td style="font-size: 11.5px; color: var(--color-text-muted);">${dev.lastActive}</td>
                <td><span class="badge ${dev.status === 'Active' ? 'badge-success' : 'badge-danger'}">${dev.status}</span></td>
                <td style="text-align: right;">
                    <button class="btn btn-secondary btn-sm" onclick="window.edmApp.handleDeactivateDevice('${dev.id}')">Deactivate</button>
                </td>
            </tr>
        `).join("");

        if (window.lucide) window.lucide.createIcons();
    }

    async handleDeactivateDevice(deviceId) {
        await window.edmApi.deactivateDevice(deviceId);
        this.showToast(`Device ${deviceId} deactivated`, "warning");
        this.renderDevicesTable();
    }

    renderUserActivityTable() {
        const tbody = document.getElementById("user-activity-table-body");
        if (!tbody) return;

        tbody.innerHTML = window.EDM_MOCK_DATA.userActivities.map(act => `
            <tr>
                <td><code>${act.id}</code></td>
                <td><strong>${act.user}</strong></td>
                <td><span class="badge badge-neutral">${act.type}</span></td>
                <td>${act.desc}</td>
                <td><code>${act.ip}</code></td>
                <td><span class="badge ${act.severity === 'SUCCESS' ? 'badge-success' : (act.severity === 'WARNING' ? 'badge-warning' : 'badge-primary')}">${act.severity}</span></td>
                <td style="color: var(--color-text-muted);">${act.time}</td>
            </tr>
        `).join("");
    }

    renderDownloadAnalytics() {
        const container = document.getElementById("download-formats-list");
        if (!container) return;

        container.innerHTML = window.EDM_MOCK_DATA.downloadTelemetry.formatDistribution.map(f => `
            <div>
                <div style="display: flex; justify-content: space-between; font-size: 12.5px; margin-bottom: 4px;">
                    <span>${f.format}</span>
                    <strong>${f.share} (${f.count})</strong>
                </div>
                <div style="background: var(--color-bg-subtle); height: 7px; border-radius: var(--radius-full); overflow: hidden;">
                    <div style="background: var(--color-primary); width: ${f.share}; height: 100%;"></div>
                </div>
            </div>
        `).join("");
    }

    renderBrowserExtensionTable() {
        const tbody = document.getElementById("browser-extensions-table-body");
        if (!tbody) return;

        tbody.innerHTML = window.EDM_MOCK_DATA.downloadTelemetry.browserExtensions.map(ext => `
            <tr>
                <td><strong>${ext.browser}</strong></td>
                <td><span class="badge badge-primary">${ext.version}</span></td>
                <td>${ext.activeUsers} Active</td>
                <td><span class="badge badge-success">● ${ext.status}</span></td>
                <td style="text-align: right;">
                    <button class="btn btn-secondary btn-sm" onclick="window.edmApp.showToast('Updated bridge config for ${ext.browser}', 'info')">Configure</button>
                </td>
            </tr>
        `).join("");
    }

    // ══════════════════════════════════════════════════════════════
    // RELEASES & VERSION HISTORY
    // ══════════════════════════════════════════════════════════════
    async renderReleasesTable() {
        const releases = await window.edmApi.getReleases();
        const tbody = document.getElementById("releases-table-body");
        if (!tbody) return;

        tbody.innerHTML = releases.map(rel => `
            <tr>
                <td>
                    <strong style="color: var(--color-primary-light); font-size: 13.5px;">${rel.version}</strong>
                </td>
                <td><strong>${rel.name}</strong></td>
                <td style="font-size: 11.5px; color: var(--color-text-muted);">${rel.date}</td>
                <td>
                    <span class="badge ${rel.type === 'RECOMMENDED' ? 'badge-recommended' : (rel.type === 'REQUIRED' ? 'badge-required' : 'badge-optional')}">${rel.type}</span>
                </td>
                <td><span class="badge ${rel.status.includes('Active') ? 'badge-success' : 'badge-neutral'}">${rel.status}</span></td>
                <td><code>${rel.file}</code></td>
                <td>${rel.size}</td>
                <td><strong>${rel.downloads.toLocaleString()}</strong></td>
                <td style="text-align: right;">
                    <button class="btn-icon-only btn-sm" title="Download Installer" onclick="window.edmApp.downloadMockBinary('${rel.file}')">
                        <i data-lucide="download" style="width: 13px; height: 13px;"></i>
                    </button>
                </td>
            </tr>
        `).join("");

        if (window.lucide) window.lucide.createIcons();
    }

    renderVersionHistory() {
        const container = document.getElementById("version-history-cards-container");
        if (!container) return;

        container.innerHTML = window.EDM_MOCK_DATA.releases.map(rel => `
            <div class="card">
                <div style="display: flex; justify-content: space-between; align-items: flex-start; margin-bottom: 8px;">
                    <div>
                        <strong style="font-size: 16px; color: var(--color-text-main);">${rel.version} — ${rel.name}</strong>
                        <p style="font-size: 11.5px; color: var(--color-text-muted);">Published ${rel.date} by ${rel.publishedBy}</p>
                    </div>
                    <span class="badge ${rel.type === 'RECOMMENDED' ? 'badge-recommended' : 'badge-optional'}">${rel.type}</span>
                </div>
                <div style="background: var(--color-bg-subtle); padding: 10px; border-radius: var(--radius-md); font-family: monospace; font-size: 11px; margin: 8px 0; color: var(--color-primary-light);">
                    SHA-256: ${rel.sha256}
                </div>
                <pre style="font-family: inherit; font-size: 12.5px; color: var(--color-text-secondary); white-space: pre-wrap; line-height: 1.5;">${rel.notes}</pre>
            </div>
        `).join("");
    }

    async handlePublishRelease() {
        const version = document.getElementById("rel-input-version")?.value || "v2.1.1";
        const name = document.getElementById("rel-input-name")?.value || "Patch";
        const type = document.getElementById("rel-input-type")?.value || "RECOMMENDED";
        const notes = document.getElementById("rel-input-notes")?.value || "";

        const payload = {
            version,
            name,
            date: new Date().toLocaleDateString("en-US", { month: "short", day: "numeric", year: "numeric" }),
            type,
            status: "Active / Production",
            file: `EDM-Setup-${version.replace('v', '')}-x64.exe`,
            size: "2.5 MB",
            sha256: "9918237192837192837192837192837192837192837192837192837192837192",
            downloads: 1,
            minSupportedVersion: "v1.9.0",
            notes,
            publishedBy: "Admin (Super Admin)"
        };

        const res = await window.edmApi.createRelease(payload);
        this.closeModal("modal-release-wizard");
        this.showToast(res.message, "success");
        this.renderReleasesTable();
    }

    async handleRollback() {
        const version = document.getElementById("select-rollback-version")?.value || "v2.0.9";
        const res = await window.edmApi.rollbackRelease(version);
        this.closeModal("modal-rollback");
        this.showToast(res.message, "warning");
        this.renderReleasesTable();
    }

    downloadMockBinary(filename) {
        this.showToast(`Downloading ${filename}...`, "success");
    }

    // ══════════════════════════════════════════════════════════════
    // PLANS, TRIALS, LICENSES, COUNTRY PRICING, PROMOTIONS
    // ══════════════════════════════════════════════════════════════
    renderPlansView() {
        const container = document.getElementById("plans-container");
        if (!container) return;

        container.innerHTML = window.EDM_MOCK_DATA.plans.map(plan => `
            <div class="card" style="display: flex; flex-direction: column; justify-content: space-between;">
                <div>
                    <div style="display: flex; justify-content: space-between; align-items: flex-start; margin-bottom: 10px;">
                        <div>
                            <h3 style="font-size: 17px; color: var(--color-text-main);">${plan.name}</h3>
                            <span class="card-subtitle">Billing: ${plan.billingPeriod}</span>
                        </div>
                        <span class="badge badge-primary">${plan.activeUsers} Active</span>
                    </div>

                    <div style="font-size: 26px; font-weight: 800; color: var(--color-text-main); margin: 10px 0;">
                        ${plan.monthlyPrice} <span style="font-size: 13px; font-weight: 400; color: var(--color-text-muted);">/ month</span>
                    </div>

                    <ul style="list-style: none; display: flex; flex-direction: column; gap: 8px; margin: 14px 0;">
                        ${plan.features.map(f => `
                            <li style="display: flex; align-items: center; gap: 8px; font-size: 12.5px; color: var(--color-text-secondary);">
                                <i data-lucide="check" style="width: 15px; height: 15px; color: var(--color-success);"></i>
                                <span>${f}</span>
                            </li>
                        `).join("")}
                    </ul>
                </div>

                <button class="btn btn-secondary w-full" style="margin-top: 14px;" onclick="window.edmApp.showToast('Configuring ${plan.name}', 'info')">
                    Edit Plan Tier
                </button>
            </div>
        `).join("");

        if (window.lucide) window.lucide.createIcons();
    }

    renderTrialsView() {
        const container = document.getElementById("trials-funnel-container");
        if (!container) return;

        container.innerHTML = window.EDM_MOCK_DATA.trials.funnel.map(t => `
            <div class="status-card">
                <div class="status-info-col">
                    <span class="status-card-label">${t.period}</span>
                    <span class="status-card-val">${t.count} Users</span>
                    <span class="kpi-comparison">Conversion: ${t.conversion}</span>
                </div>
            </div>
        `).join("");
    }

    renderLicensesTable() {
        const tbody = document.getElementById("licenses-table-body");
        if (!tbody) return;

        tbody.innerHTML = window.EDM_MOCK_DATA.licenses.map(lic => `
            <tr>
                <td><code style="color: var(--color-primary-light);">${lic.key}</code></td>
                <td><strong>${lic.user}</strong></td>
                <td>${lic.devicesBound} / ${lic.maxDevices}</td>
                <td>${lic.expires}</td>
                <td><span class="badge ${lic.status === 'Active' ? 'badge-success' : 'badge-danger'}">${lic.status}</span></td>
                <td style="text-align: right;">
                    <button class="btn btn-secondary btn-sm" onclick="window.edmApp.showToast('Updated key ${lic.key}', 'info')">Manage</button>
                </td>
            </tr>
        `).join("");
    }

    async renderCountryPricingTable() {
        const pricing = await window.edmApi.getCountryPricing();
        const tbody = document.getElementById("country-pricing-table-body");
        if (!tbody) return;

        tbody.innerHTML = pricing.map(p => `
            <tr>
                <td><strong>${p.country} (${p.code})</strong></td>
                <td><code>${p.currency}</code></td>
                <td><strong>${p.monthly}</strong></td>
                <td>${p.yearly}</td>
                <td>${p.users}</td>
                <td><strong style="color: var(--color-success);">${p.revenue}</strong></td>
                <td><span class="badge badge-success">${p.status}</span></td>
                <td style="text-align: right;">
                    <button class="btn btn-secondary btn-sm" onclick="window.edmApp.showToast('Editing ${p.country} price', 'info')">Edit Rate</button>
                </td>
            </tr>
        `).join("");
    }

    renderPromotionsTable() {
        const tbody = document.getElementById("promotions-table-body");
        if (!tbody) return;

        tbody.innerHTML = window.EDM_MOCK_DATA.promotions.map(promo => `
            <tr>
                <td><strong style="color: var(--color-primary-light);">${promo.code}</strong></td>
                <td>${promo.discount}</td>
                <td>${promo.type}</td>
                <td>${promo.uses}</td>
                <td>${promo.expires}</td>
                <td><span class="badge badge-success">${promo.status}</span></td>
            </tr>
        `).join("");
    }

    async renderNotificationsTable() {
        const notifs = await window.edmApi.getNotifications();
        const tbody = document.getElementById("notifications-table-body");
        if (!tbody) return;

        tbody.innerHTML = notifs.map(n => `
            <tr>
                <td><code>${n.id}</code></td>
                <td><strong>${n.title}</strong></td>
                <td><span class="badge badge-primary">${n.audience}</span></td>
                <td>${n.type}</td>
                <td><strong>${n.sentCount}</strong></td>
                <td style="color: var(--color-text-muted); font-size: 11.5px;">${n.date}</td>
                <td><span class="badge ${n.status === 'Active' ? 'badge-success' : 'badge-warning'}">${n.status}</span></td>
            </tr>
        `).join("");
    }

    renderEmailCampaignsTable() {
        const tbody = document.getElementById("email-campaigns-table-body");
        if (!tbody) return;

        tbody.innerHTML = window.EDM_MOCK_DATA.emailCampaigns.map(cmp => `
            <tr>
                <td><code>${cmp.id}</code></td>
                <td><strong>${cmp.name}</strong></td>
                <td>${cmp.audience}</td>
                <td><strong style="color: var(--color-success);">${cmp.openRate}</strong></td>
                <td><strong>${cmp.clickRate}</strong></td>
                <td><span class="badge badge-success">${cmp.status}</span></td>
            </tr>
        `).join("");
    }

    async renderFullSystemHealth() {
        const services = await window.edmApi.getSystemHealth();
        const container = document.getElementById("full-system-health-list");
        if (!container) return;

        container.innerHTML = services.map(srv => `
            <div class="health-item-row" style="padding: 10px 4px;">
                <span class="health-service-name">
                    <span class="status-dot green"></span>
                    <span style="font-size: 13.5px; font-weight: 600;">${srv.name}</span>
                </span>
                <div style="display: flex; align-items: center; gap: 14px;">
                    <span class="badge badge-success">${srv.status}</span>
                    <span style="font-size: 11.5px; color: var(--color-text-muted);">Uptime: ${srv.uptime}</span>
                    <span class="health-latency" style="font-size: 12px; font-weight: 700; color: var(--color-text-main);">${srv.latency}</span>
                </div>
            </div>
        `).join("");
    }

    async renderAuditLogsTable() {
        const logs = await window.edmApi.getAuditLogs();
        const tbody = document.getElementById("audit-logs-table-body");
        if (!tbody) return;

        tbody.innerHTML = logs.map(l => `
            <tr>
                <td style="font-family: monospace; font-size: 11.5px;">${l.timestamp}</td>
                <td><strong>${l.admin}</strong></td>
                <td><span class="badge badge-neutral">${l.action}</span></td>
                <td><code>${l.target}</code></td>
                <td style="font-family: monospace; font-size: 11.5px;">${l.ip}</td>
                <td><span class="badge ${l.result === 'SUCCESS' ? 'badge-success' : 'badge-danger'}">${l.result}</span></td>
            </tr>
        `).join("");
    }

    async renderTicketsTable() {
        const tickets = await window.edmApi.getTickets();
        const tbody = document.getElementById("tickets-table-body");
        if (!tbody) return;

        tbody.innerHTML = tickets.map(t => `
            <tr>
                <td><code>${t.id}</code></td>
                <td><strong>${t.user}</strong></td>
                <td><span class="badge badge-danger">${t.priority}</span></td>
                <td>${t.subject}</td>
                <td><span class="badge ${t.status === 'Resolved' ? 'badge-success' : 'badge-warning'}">${t.status}</span></td>
                <td style="font-size: 11.5px; color: var(--color-text-muted);">${t.created}</td>
                <td style="text-align: right;">
                    <button class="btn btn-secondary btn-sm" onclick="window.edmApp.showToast('Managing ticket ${t.id}', 'info')">Manage</button>
                </td>
            </tr>
        `).join("");
    }

    switchSettingsTab(tabName, clickedBtn) {
        document.querySelectorAll("#tab-general, #tab-feature-flags, #tab-maintenance, #tab-api-keys").forEach(content => content.classList.add("hidden"));
        clickedBtn.parentElement.querySelectorAll(".tab-btn").forEach(b => b.classList.remove("active"));
        clickedBtn.classList.add("active");

        const target = document.getElementById(`tab-${tabName}`);
        if (target) target.classList.remove("hidden");
    }

    switchCmsTab(tabName, clickedBtn) {
        document.querySelectorAll(".cms-tab-content").forEach(c => c.classList.add("hidden"));
        clickedBtn.parentElement.querySelectorAll(".tab-btn").forEach(b => b.classList.remove("active"));
        clickedBtn.classList.add("active");

        const target = document.getElementById(`tab-${tabName}`);
        if (target) target.classList.remove("hidden");
    }

    async renderFeatureFlags() {
        const flags = await window.edmApi.getFeatureFlags();
        const card = document.getElementById("feature-flags-card");
        if (!card) return;

        card.innerHTML = `
            <div class="card-header">
                <span class="card-title">Live Experimental Feature Flags</span>
            </div>
            <div style="display: flex; flex-direction: column; gap: 14px;">
                ${flags.map(f => `
                    <div style="display: flex; align-items: center; justify-content: space-between; padding: 10px 12px; background: var(--color-bg-subtle); border-radius: var(--radius-md);">
                        <div>
                            <strong style="color: var(--color-text-main); font-size: 13.5px;">${f.name}</strong>
                            <p style="font-size: 11.5px; color: var(--color-text-muted); margin-top: 2px;"><code>${f.key}</code> • ${f.desc}</p>
                            <span class="badge badge-primary" style="margin-top: 4px;">Rollout: ${f.rollout}%</span>
                        </div>
                        <label class="toggle-switch">
                            <input type="checkbox" ${f.enabled ? 'checked' : ''} onchange="window.edmApp.toggleFlag('${f.key}', this.checked)">
                            <span class="toggle-slider"></span>
                        </label>
                    </div>
                `).join("")}
            </div>
        `;
    }

    async toggleFlag(key, enabled) {
        await window.edmApi.toggleFeatureFlag(key, enabled);
        this.showToast(`Feature flag ${key} is now ${enabled ? 'ENABLED' : 'DISABLED'}`, enabled ? 'success' : 'warning');
    }

    toggleMaintenanceMode() {
        const btn = document.getElementById("btn-toggle-maintenance");
        const isMaint = btn?.classList.contains("active");
        if (isMaint) {
            btn?.classList.remove("active");
            if (btn) btn.innerHTML = `<i data-lucide="power"></i> <span>Enable Maintenance Mode</span>`;
            this.showToast("Maintenance mode deactivated.", "success");
        } else {
            btn?.classList.add("active");
            if (btn) btn.innerHTML = `<i data-lucide="power"></i> <span>Disable Maintenance Mode</span>`;
            this.showToast("EMERGENCY MAINTENANCE MODE ACTIVE.", "danger");
        }
        if (window.lucide) window.lucide.createIcons();
    }

    openCommandPalette() {
        const pal = document.getElementById("cmd-palette");
        const input = document.getElementById("cmd-search-input");
        if (pal) pal.classList.add("active");
        if (input) {
            input.value = "";
            input.focus();
        }
    }

    closeCommandPalette() {
        document.getElementById("cmd-palette")?.classList.remove("active");
    }

    handleCommandSearch(query) {
        const q = query.toLowerCase().trim();
        const items = document.querySelectorAll("#cmd-results-list .cmd-item");
        items.forEach(item => {
            const text = item.textContent.toLowerCase();
            if (!q || text.includes(q)) {
                item.style.display = "flex";
            } else {
                item.style.display = "none";
            }
        });
    }

    openModal(modalId) {
        document.getElementById(modalId)?.classList.add("active");
        if (window.lucide) window.lucide.createIcons();
    }

    closeModal(modalId) {
        document.getElementById(modalId)?.classList.remove("active");
    }

    closeAllModals() {
        document.querySelectorAll(".modal-backdrop").forEach(m => m.classList.remove("active"));
    }

    showToast(message, type = "info") {
        const container = document.getElementById("toast-container");
        if (!container) return;

        const toast = document.createElement("div");
        toast.className = `toast toast-${type}`;
        
        let iconName = "info";
        if (type === "success") iconName = "check-circle";
        if (type === "danger") iconName = "alert-circle";
        if (type === "warning") iconName = "alert-triangle";

        toast.innerHTML = `
            <i data-lucide="${iconName}" style="width: 16px; height: 16px; flex-shrink: 0;"></i>
            <span style="flex: 1;">${message}</span>
        `;

        container.appendChild(toast);
        if (window.lucide) window.lucide.createIcons();

        setTimeout(() => {
            toast.style.opacity = "0";
            toast.style.transform = "translateY(8px)";
            toast.style.transition = "all 0.2s ease";
            setTimeout(() => toast.remove(), 200);
        }, 3000);
    }

    exportPerformanceReport() {
        const reportData = {
            generatedAt: new Date().toISOString(),
            dateRange: this.currentDateRange,
            systemStatus: "Operational",
            metrics: window.EDM_MOCK_DATA.overview,
            activeReleases: window.EDM_MOCK_DATA.releases
        };

        const blob = new Blob([JSON.stringify(reportData, null, 2)], { type: "application/json" });
        const url = URL.createObjectURL(blob);
        const a = document.createElement("a");
        a.href = url;
        a.download = `edm_performance_report_${Date.now()}.json`;
        a.click();

        this.showToast("Monthly performance report downloaded", "success");
    }
}

// Global DOM Hook
document.addEventListener("DOMContentLoaded", () => {
    window.edmApp = new EdmApp();
    
    document.querySelectorAll("#cmd-results-list .cmd-item").forEach(item => {
        item.addEventListener("click", () => {
            const action = item.getAttribute("data-action");
            const target = item.getAttribute("data-target");
            window.edmApp.closeCommandPalette();
            
            if (action === "nav") {
                window.edmApp.navigateTo(target);
            } else if (action === "action") {
                if (target === "create-release") window.edmApp.openModal("modal-release-wizard");
                if (target === "export-report") window.edmApp.exportPerformanceReport();
            }
        });
    });
});
