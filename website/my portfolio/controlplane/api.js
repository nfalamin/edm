/**
 * EDM Central Control Plane - Centralized API Layer & Live Integration Interface
 * Connected to WordPress REST API (/wp-json/edm-api/v1) and ASP.NET Core Web API (/api/v1)
 * Built with Dynamic Route Resolvers, CSRF Security, Bearer Session Auth, and Offline Mock Fallbacks
 */

const EDM_API_CONFIG = {
    API_BASE_URL: "/api/v1",
    REQUEST_TIMEOUT_MS: 8000
};

class EdmApiService {
    constructor(config = EDM_API_CONFIG) {
        this.config = config;
        this.csrfToken = null;
        this.isOfflineMode = false;
        this.initCsrfToken();
    }

    getBaseUrl() {
        if (typeof window !== "undefined") {
            if (window.edmDashboardSettings && window.edmDashboardSettings.apiBase) {
                return window.edmDashboardSettings.apiBase.replace(/\/$/, "");
            }
            if (window.location && window.location.origin && window.location.origin !== "null" && window.location.protocol.startsWith("http")) {
                return window.location.origin + "/api/v1";
            }
        }
        return "http://localhost:5000/api/v1";
    }

    async initCsrfToken() {
        try {
            const baseUrl = this.getBaseUrl();
            const res = await fetch(`${baseUrl}/auth/csrf-token`, { credentials: "include" });
            if (res.ok) {
                const data = await res.json();
                this.csrfToken = data.csrfToken;
            }
        } catch (e) {
            this.csrfToken = "edm_live_session_token_" + Date.now();
        }
    }

    async _request(endpoint, method = "GET", body = null) {
        if (!this.csrfToken && (method === "POST" || method === "PUT" || method === "PATCH" || method === "DELETE")) {
            await this.initCsrfToken();
        }

        const headers = {
            "Content-Type": "application/json",
            "Accept": "application/json"
        };

        const token = localStorage.getItem("edm_token") || sessionStorage.getItem("edm_token");
        if (token) {
            headers["Authorization"] = `Bearer ${token}`;
        }

        if (this.csrfToken && (method === "POST" || method === "PUT" || method === "PATCH" || method === "DELETE")) {
            headers["X-CSRF-Token"] = this.csrfToken;
        }

        const options = {
            method,
            headers,
            credentials: "include"
        };

        if (body && (method === "POST" || method === "PUT" || method === "PATCH")) {
            options.body = JSON.stringify(body);
        }

        const baseUrl = this.getBaseUrl();
        const controller = new AbortController();
        const timeoutId = setTimeout(() => controller.abort(), this.config.REQUEST_TIMEOUT_MS || 8000);
        options.signal = controller.signal;

        try {
            const response = await fetch(`${baseUrl}${endpoint}`, options);
            clearTimeout(timeoutId);
            
            const newCsrf = response.headers?.get?.("X-CSRF-Token");
            if (newCsrf) this.csrfToken = newCsrf;

            if (response.status === 401) {
                if (window.edmAuth && typeof window.edmAuth.handleSessionExpired === "function") {
                    window.edmAuth.handleSessionExpired();
                } else if (window.edmApp && typeof window.edmApp.showAuthModal === "function") {
                    window.edmApp.showAuthModal("Session Expired. Please authenticate.");
                }
                throw new Error("Authentication required (HTTP 401). Please sign in.");
            }

            if (response.status === 403) {
                if (window.edmApp && typeof window.edmApp.showToast === "function") {
                    window.edmApp.showToast("Access Denied: You do not have administrator privileges for this action.", "danger");
                }
                throw new Error("Access Denied: Administrator role required (HTTP 403).");
            }

            if (response.status === 429) {
                if (window.edmApp && typeof window.edmApp.showToast === "function") {
                    window.edmApp.showToast("Rate limit reached. Please wait a moment before sending more requests.", "warning");
                }
                throw new Error("Rate limit exceeded (HTTP 429).");
            }

            if (!response.ok) {
                let errorMsg = `Server request failed (HTTP ${response.status})`;
                try {
                    const errData = await response.json();
                    if (errData && errData.message) errorMsg = errData.message;
                    else if (errData && errData.error) errorMsg = errData.error;
                } catch (_) {}
                throw new Error(errorMsg);
            }

            this.isOfflineMode = false;
            return await response.json();
        } catch (error) {
            clearTimeout(timeoutId);
            this.isOfflineMode = true;
            if (error.name === "AbortError") {
                throw new Error("Request timed out after 8s (HTTP 408 / Timeout).");
            }
            throw error;
        }
    }

    // ══════════════════════════════════════════════════════════════
    // DASHBOARD & ANALYTICS API METHODS
    // ══════════════════════════════════════════════════════════════
    async getDashboardMetrics(filters = {}) {
        const clean = {};
        for (const [k, v] of Object.entries(filters || {})) {
            if (v !== undefined && v !== null && v !== "" && v !== "undefined" && v !== "null") {
                clean[k] = v;
            }
        }
        const qs = new URLSearchParams(clean).toString();
        return this._request('/admin/dashboard/summary' + (qs ? '?' + qs : ''));
    }

    async getAnalyticsData(range = '30d') {
        return this._request(`/admin/analytics/website?range=${encodeURIComponent(range)}`);
    }

    async getUserCohortAnalytics(range = '30d') {
        return this._request(`/admin/analytics/user-cohorts?range=${encodeURIComponent(range)}`);
    }

    async getUserGrowthAnalytics(period = 'monthly', range = '30d', startDate = null, endDate = null, filter = 'all') {
        let url = `/admin/analytics/user-growth?period=${encodeURIComponent(period)}&range=${encodeURIComponent(range)}`;
        if (startDate && startDate !== 'undefined') url += `&startDate=${encodeURIComponent(startDate)}`;
        if (endDate && endDate !== 'undefined') url += `&endDate=${encodeURIComponent(endDate)}`;
        if (filter && filter !== 'all' && filter !== 'undefined') url += `&filter=${encodeURIComponent(filter)}`;
        return this._request(url);
    }

    async getDownloadAnalytics(range = '7d', startDate = null, endDate = null, filter = 'all') {
        let url = `/admin/analytics/downloads?range=${encodeURIComponent(range)}`;
        if (startDate && startDate !== 'undefined') url += `&startDate=${encodeURIComponent(startDate)}`;
        if (endDate && endDate !== 'undefined') url += `&endDate=${encodeURIComponent(endDate)}`;
        if (filter && filter !== 'all' && filter !== 'undefined') url += `&filter=${encodeURIComponent(filter)}`;
        return this._request(url);
    }

    async getTrialConversion(range = '30d', startDate = null, endDate = null, filter = 'all') {
        let url = `/admin/analytics/trial-conversion?range=${encodeURIComponent(range)}`;
        if (startDate && startDate !== 'undefined') url += `&startDate=${encodeURIComponent(startDate)}`;
        if (endDate && endDate !== 'undefined') url += `&endDate=${encodeURIComponent(endDate)}`;
        if (filter && filter !== 'all' && filter !== 'undefined') url += `&filter=${encodeURIComponent(filter)}`;
        return this._request(url);
    }

    async getTopCountries(range = '30d', startDate = null, endDate = null, filter = 'all') {
        let url = `/admin/analytics/countries?range=${encodeURIComponent(range)}`;
        if (startDate && startDate !== 'undefined') url += `&startDate=${encodeURIComponent(startDate)}`;
        if (endDate && endDate !== 'undefined') url += `&endDate=${encodeURIComponent(endDate)}`;
        if (filter && filter !== 'all' && filter !== 'undefined') url += `&filter=${encodeURIComponent(filter)}`;
        return this._request(url);
    }

    async getSystemHealth() {
        return this._request('/health/diagnostics');
    }

    async getApiStatus() {
        return this._request('/admin/system/api-status');
    }

    async getStorageQuota() {
        return this._request('/storage/quota');
    }

    async getAllWebsiteContent(locale = "en") {
        try {
            const sections = await this._request(`/content?locale=${encodeURIComponent(locale)}`);
            const content = {};
            if (Array.isArray(sections)) {
                sections.forEach(s => {
                    try {
                        content[s.sectionKey] = typeof s.contentJson === "string" ? JSON.parse(s.contentJson) : s.contentJson;
                    } catch {
                        content[s.sectionKey] = { title: s.title };
                    }
                });
            }
            return content;
        } catch {
            return {
                hero: { title: "The Fastest Download Manager for Windows", description: "Accelerate your downloads up to 32x with smart multi-threaded segmentation." }
            };
        }
    }

    async getRecentActivities(limit = 10) {
        return this._request(`/admin/audit-logs?limit=${limit}`);
    }

    async getAuditLogs(params = {}) {
        const qs = new URLSearchParams(params).toString();
        return this._request('/admin/audit-logs' + (qs ? '?' + qs : ''));
    }

    async getLoginActivity(params = {}) {
        const qs = new URLSearchParams(params).toString();
        return this._request('/admin/login-activity' + (qs ? '?' + qs : ''));
    }

    async getUserActivity(params = {}) {
        const qs = new URLSearchParams(params).toString();
        return this._request('/telemetry/events' + (qs ? '?' + qs : ''));
    }

    async getUsers(params = {}) {
        const qs = new URLSearchParams(params).toString();
        return this._request('/admin/users' + (qs ? '?' + qs : ''));
    }

    async getDevices(params = {}) {
        const qs = new URLSearchParams(params).toString();
        return this._request('/admin/devices' + (qs ? '?' + qs : ''));
    }

    async getReleases() {
        return this._request('/admin/releases');
    }

    async getBrowserExtensions() {
        return this._request('/admin/browser-extensions');
    }

    async pingBrowserExtension(browser) {
        return this._request(`/admin/browser-extensions/ping/${encodeURIComponent(browser)}`, 'POST');
    }

    async getLicenses(params = {}) {
        const qs = new URLSearchParams(params).toString();
        return this._request('/admin/licenses' + (qs ? '?' + qs : ''));
    }

    async getSubscriptions(params = {}) {
        const qs = new URLSearchParams(params).toString();
        return this._request('/admin/subscriptions' + (qs ? '?' + qs : ''));
    }

    async extendTrial(installationId, additionalDays = 10, reason = "Admin extension") {
        return this._request(`/admin/subscriptions/${installationId}/extend-trial`, 'POST', { additionalDays, reason });
    }

    async blockDevice(installationId, reason = "Administrative policy restriction") {
        return this._request(`/admin/devices/${installationId}/block`, 'POST', { reason });
    }

    async unblockDevice(installationId) {
        return this._request(`/admin/devices/${installationId}/unblock`, 'POST');
    }

    async getNotifications() {
        return this._request('/admin/notifications');
    }

    async getPricingRules() {
        return this._request('/admin/pricing/rules');
    }

    async getCountryPricing() {
        return this.getPricingRules();
    }

    // User CRUD
    async updateUser(id, userData) {
        return this._request(`/admin/users/${id}`, 'PUT', userData);
    }

    async deleteUser(id) {
        return this._request(`/admin/users/${id}`, 'DELETE');
    }

    async toggleUserStatus(id) {
        return this._request(`/admin/users/${id}/toggle-status`, 'POST');
    }

    // Devices & Sessions
    async getRemoteDevices() {
        return this._request('/admin/devices');
    }

    async getRemoteDownloads(deviceId = null) {
        const query = deviceId ? `?deviceId=${encodeURIComponent(deviceId)}` : '';
        return this._request('/admin/downloads/activity' + query);
    }

    async sendRemoteCommand(deviceId, commandType, payload = {}) {
        return this._request('/remote/commands', 'POST', {
            deviceId,
            commandType,
            payloadJson: typeof payload === "string" ? payload : JSON.stringify(payload)
        });
    }

    async revokeDevice(deviceId, reason = "Revoked by Administrator") {
        return this._request(`/admin/devices/${deviceId}/block`, 'POST', { reason });
    }

    async revokeSession(sessionId) {
        return this._request(`/admin/devices/sessions/${sessionId}`, 'DELETE');
    }

    // Licenses CRUD
    async createLicense(licenseData) {
        return this._request('/admin/licenses', 'POST', licenseData);
    }

    async generateLicense(licenseData) {
        return this.createLicense(licenseData);
    }

    async revokeLicense(id) {
        return this._request(`/admin/licenses/${id}/revoke`, 'POST');
    }

    async extendLicense(id, additionalDays = 30) {
        return this._request(`/admin/licenses/${id}/extend`, 'POST', { additionalDays, reason: "Admin extension" });
    }

    // Plans CRUD
    async getPlans() {
        return this._request('/admin/plans');
    }

    async createPlan(planData) {
        return this._request('/admin/plans', 'POST', planData);
    }

    async updatePlan(id, planData) {
        return this._request(`/admin/plans/${id}`, 'PUT', planData);
    }

    async deletePlan(id) {
        return this._request(`/admin/plans/${id}`, 'DELETE');
    }

    // Transactions & Ledger
    async getTransactions(params = {}) {
        const qs = new URLSearchParams(params).toString();
        return this._request('/admin/transactions' + (qs ? '?' + qs : ''));
    }

    async getTransactionReceipt(id) {
        return this._request(`/admin/transactions/${id}`);
    }

    // Coupons
    async getCoupons() {
        return this._request('/admin/coupons');
    }

    async createCoupon(couponData) {
        return this._request('/admin/coupons', 'POST', couponData);
    }

    async deleteCoupon(id) {
        return this._request(`/admin/coupons/${id}`, 'DELETE');
    }

    // Country Pricing
    async savePricingRule(ruleData) {
        return this._request('/admin/pricing/rules', 'POST', ruleData);
    }

    async deletePricingRule(id) {
        return this._request(`/admin/pricing/rules/${id}`, 'DELETE');
    }

    // Subscription & Entitlement Controls
    async getGlobalSubscriptionConfig() {
        return this._request('/admin/subscriptions/config');
    }

    async updateGlobalSubscriptionConfig(config) {
        return this._request('/admin/subscriptions/config', 'POST', config);
    }

    async setGlobalSubscriptionSwitch(isEnabled, reason = "Dashboard master toggle") {
        return this._request('/admin/subscriptions/global-switch', 'POST', { isEnabled, reason });
    }

    async setAsiaSubscriptionSwitch(isEnabled, reason = "Dashboard Asia regional toggle") {
        return this._request('/admin/subscriptions/asia-switch', 'POST', { isEnabled, reason });
    }

    async getRegionPolicies() {
        return this._request('/admin/subscriptions/regions');
    }

    async saveRegionPolicy(policy) {
        return this._request('/admin/subscriptions/regions', 'POST', policy);
    }

    // Email Campaigns
    async getEmailCampaigns() {
        return this._request('/admin/email-campaigns');
    }

    async createEmailCampaign(campaignData) {
        return this._request('/admin/email-campaigns', 'POST', campaignData);
    }

    // Deep-Dive Analytics & Download Monitoring
    async getDownloadMetrics() {
        return this._request('/admin/downloads/metrics');
    }

    async getDownloadDeepDive(range = '30d', period = 'daily') {
        return this._request(`/admin/downloads/deep-dive?range=${encodeURIComponent(range)}&period=${encodeURIComponent(period)}`);
    }

    async getDownloadActivity(params = {}) {
        const qs = new URLSearchParams(params).toString();
        return this._request('/admin/downloads/activity' + (qs ? '?' + qs : ''));
    }

    async getRevenueAnalytics(range = '30d', period = 'monthly') {
        return this._request(`/admin/analytics/revenue?range=${encodeURIComponent(range)}&period=${encodeURIComponent(period)}`);
    }

    async getFeatureAnalytics(range = '30d') {
        return this._request(`/admin/analytics/features?range=${encodeURIComponent(range)}`);
    }

    async getNotificationsUnreadCount() {
        return this._request('/admin/notifications/unread-count');
    }

    async markNotificationRead(id) {
        return this._request(`/admin/notifications/${id}/read`, 'POST');
    }

    async markAllNotificationsRead() {
        return this._request('/admin/notifications/mark-read', 'POST');
    }

    subscribeToEventStream(onMessage, onError) {
        const baseUrl = this.getBaseUrl();
        const streamUrl = `${baseUrl}/admin/events/stream`;
        
        let eventSource = null;
        let retryTimeout = null;
        let isClosed = false;
        let retryCount = 0;

        const connect = () => {
            if (isClosed) return;
            try {
                eventSource = new EventSource(streamUrl, { withCredentials: true });
                
                eventSource.onopen = () => {
                    if (typeof onMessage === 'function') {
                        onMessage({ type: 'stream_connected', data: { connected: true } });
                    }
                    retryCount = 0;
                    if (typeof onStateChange === 'function') onStateChange('connected', 0);
                };

                eventSource.onmessage = (e) => {
                    try {
                        const data = JSON.parse(e.data);
                        if (typeof onMessage === 'function') onMessage({ type: 'message', data });
                    } catch (err) {
                        if (typeof onMessage === 'function') onMessage({ type: 'message', data: e.data });
                    }
                };

                const eventTypes = ['download_progress', 'download_started', 'download_completed', 'download_failed', 'download_cancelled', 'notification_created', 'audit_event', 'health_heartbeat', 'connected'];
                eventTypes.forEach(evtType => {
                    eventSource.addEventListener(evtType, (e) => {
                        try {
                            const data = JSON.parse(e.data);
                            if (typeof onMessage === 'function') onMessage({ type: evtType, data });
                        } catch (err) {
                            if (typeof onMessage === 'function') onMessage({ type: evtType, data: e.data });
                        }
                    });
                });

                eventSource.onerror = (err) => {
                    if (eventSource) eventSource.close();
                    retryCount++;
                    const delay = Math.min(30000, 1000 * Math.pow(1.5, retryCount));
                    if (typeof onStateChange === 'function') onStateChange('reconnecting', retryCount, delay);
                    if (typeof onError === 'function') onError(err);
                    if (!isClosed) {
                        retryTimeout = setTimeout(connect, delay);
                    }
                };
            } catch (err) {
                retryCount++;
                const delay = Math.min(30000, 1000 * Math.pow(1.5, retryCount));
                if (typeof onStateChange === 'function') onStateChange('reconnecting', retryCount, delay);
                if (typeof onError === 'function') onError(err);
                if (!isClosed) {
                    retryTimeout = setTimeout(connect, delay);
                }
            }
        };

        connect();

        return () => {
            isClosed = true;
            if (retryTimeout) clearTimeout(retryTimeout);
            if (eventSource) eventSource.close();
        };
    }

    // Promotions
    async getPromotions() {
        return this._request('/admin/promotions');
    }

    async createPromotion(promoData) {
        return this._request('/admin/promotions', 'POST', promoData);
    }

    async deletePromotion(id) {
        return this._request(`/admin/promotions/${id}`, 'DELETE');
    }

    // Announcements
    async getAnnouncements() {
        try {
            const res = await this._request('/admin/announcements');
            return Array.isArray(res) ? res : (res?.announcements || []);
        } catch {
            return [];
        }
    }

    async createAnnouncement(data) {
        return this._request('/admin/announcements', 'POST', data);
    }

    // ══════════════════════════════════════════════════════════════
    // GOOGLE DATABASE (FIREBASE / FIRESTORE) API METHODS
    // ══════════════════════════════════════════════════════════════
    async getGoogleDatabaseConfig() {
        return this._request('/admin/database/google-config');
    }

    async saveGoogleDatabaseConfig(config) {
        return this._request('/admin/database/google-config', 'POST', config);
    }

    async testGoogleDatabaseConnection(projectId = 'edm-download-manager-live') {
        return this._request('/admin/database/test-connection', 'POST', { projectId });
    }

    async syncGoogleDatabase() {
        return this._request('/admin/database/sync', 'POST', {});
    }

    async getGoogleDatabaseCollections() {
        return this._request('/admin/database/collections');
    }

    async validateCoupon(couponCode, planCode, userId = null, installationId = null, userEmail = null) {
        return this.fetchJson('/pricing/validate-coupon', {
            method: 'POST',
            body: JSON.stringify({ couponCode, planCode, userId, installationId, userEmail })
        });
    }

    async getLivePulse() {
        return this._request('/admin/telemetry/live-pulse');
    }

    // Generic POST helper
    async post(url, body) {
        return this._request(url, 'POST', body);
    }

    // Support Tickets & Customer Care
    async getSupportTickets(params = {}) {
        const qs = new URLSearchParams(params).toString();
        return this._request('/admin/support/tickets' + (qs ? '?' + qs : ''));
    }

    async getTicketDetails(ticketId) {
        return this._request(`/admin/support/tickets/${ticketId}`);
    }

    async replyTicket(ticketId, message) {
        return this._request(`/admin/support/tickets/${ticketId}/reply`, 'POST', { message });
    }

    async updateTicketStatus(ticketId, status) {
        return this._request(`/admin/support/tickets/${ticketId}/status`, 'PATCH', { status });
    }

    async getFeatureRequests() {
        return this._request('/admin/support/feature-requests');
    }

    async getUserFeedback() {
        return this._request('/admin/support/feedback');
    }

    // User & Device Security Operations
    async getUserDetails(userId) {
        return this._request(`/admin/users/${userId}`);
    }

    async banUser(userId, reason = "Manual administrator ban") {
        return this._request(`/admin/users/${userId}/ban`, 'POST', { reason });
    }

    async unbanUser(userId) {
        return this._request(`/admin/users/${userId}/unban`, 'POST');
    }

    async banDevice(deviceId, reason = "Manual administrator device ban") {
        return this._request(`/admin/devices/${deviceId}/ban`, 'POST', { reason });
    }

    async revokeUserSessions(userId) {
        return this._request(`/admin/users/${userId}/revoke-sessions`, 'POST');
    }

    // File Storage & Cloud Sync Operations
    async getSyncedFiles(params = {}) {
        const qs = new URLSearchParams(params).toString();
        return this._request('/storage/files' + (qs ? '?' + qs : ''));
    }

    async getDownloadUrl(fileId) {
        return `/api/v1/storage/files/${fileId}/download`;
    }

    async getFilePreview(fileId) {
        return this._request(`/storage/files/${fileId}/preview`);
    }

    async getPreviewMediaUrl(fileId) {
        return `/api/v1/storage/files/${fileId}/download`;
    }

    async renameFile(fileId, newFileName) {
        return this._request(`/storage/files/${fileId}/rename`, 'POST', { newFileName });
    }

    async moveFile(fileId, targetFolder) {
        return this._request(`/storage/files/${fileId}/move`, 'POST', { targetFolder });
    }

    async deleteSyncedFile(fileId) {
        return this._request(`/storage/files/${fileId}`, 'DELETE');
    }

    async restoreSyncedFile(fileId) {
        return this._request(`/storage/files/${fileId}/restore`, 'POST');
    }

    async permanentlyDeleteFile(fileId) {
        return this._request(`/storage/files/${fileId}/permanent`, 'DELETE');
    }

    async resolveFileConflict(fileId, body) {
        return this._request(`/storage/files/${fileId}/resolve-conflict`, 'POST', body);
    }

    async uploadFile(formData) {
        return this._request('/storage/upload', 'POST', formData);
    }

    async registerFileMetadata(metadata) {
        return this._request('/storage/files/register-metadata', 'POST', metadata);
    }

    // Release Management Lifecycle
    async createRelease(releaseData) {
        return this._request('/admin/releases', 'POST', releaseData);
    }

    async updateRelease(id, releaseData) {
        return this._request(`/admin/releases/${id}`, 'PUT', releaseData);
    }

    async publishRelease(id) {
        return this._request(`/admin/releases/${id}/publish`, 'POST');
    }

    async unpublishRelease(id) {
        return this._request(`/admin/releases/${id}/unpublish`, 'POST');
    }

    async archiveRelease(id) {
        return this._request(`/admin/releases/${id}/archive`, 'PUT');
    }

    async rollbackRelease(id, targetVersion, reason = "Automated rollback") {
        return this._request(`/admin/releases/${id}/rollback`, 'POST', { targetVersion, reason });
    }

    async uploadReleaseArtifact(releaseId, formData) {
        return this._request(`/admin/releases/${releaseId}/artifacts/upload`, 'POST', formData);
    }

    // License Operations
    async suspendLicense(id) {
        return this._request(`/admin/licenses/${id}/suspend`, 'POST');
    }

    async reactivateLicense(id) {
        return this._request(`/admin/licenses/${id}/reactivate`, 'POST');
    }

    // Website Content & Analytics
    async getWebsiteAnalytics(range = '30d') {
        return this._request(`/admin/analytics/website?range=${encodeURIComponent(range)}`);
    }

    async updateWebsiteContent(sectionKey, contentData) {
        return this._request(`/admin/website/content/${sectionKey}`, 'PUT', contentData);
    }
}

// Global API Fetch helper for direct calls
async function apiFetch(endpoint, options = {}) {
    const token = localStorage.getItem('edm_token') || sessionStorage.getItem('edm_token');
    const headers = {
        'Accept': 'application/json',
        ...(options.headers || {})
    };

    if (!(options.body instanceof FormData)) {
        headers['Content-Type'] = 'application/json';
    }

    if (token) {
        headers['Authorization'] = `Bearer ${token}`;
    }

    const res = await fetch(endpoint, {
        ...options,
        headers
    });

    if (!res.ok) {
        let errMessage = `HTTP ${res.status}: ${res.statusText}`;
        try {
            const errData = await res.json();
            if (errData && errData.message) errMessage = errData.message;
            else if (errData && errData.error) errMessage = errData.error;
        } catch { }
        throw new Error(errMessage);
    }

    const contentType = res.headers.get('content-type');
    if (contentType && contentType.includes('application/json')) {
        return await res.json();
    }
    return await res.text();
}

function showToast(message, type = 'info') {
    if (window.edmApp && typeof window.edmApp.showToast === 'function') {
        window.edmApp.showToast(message, type);
    } else {
        console.log(`[${type.toUpperCase()}] ${message}`);
    }
}

// Expose globally to window
if (typeof window !== "undefined") {
    window.EdmApiService = EdmApiService;
    window.edmApi = new EdmApiService();
    window.apiFetch = apiFetch;
    window.showToast = showToast;
}
