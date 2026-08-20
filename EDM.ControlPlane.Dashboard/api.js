/**
 * EDM Central Control Plane — Centralized API Layer & Live Integration Interface
 * Connected directly to ASP.NET Core Web API (/api/v1) with CSRF, Bearer Session Auth, and Error Interceptors
 */

const EDM_API_CONFIG = {
    API_BASE_URL: "/api/v1",
    REQUEST_TIMEOUT_MS: 15000
};

class EdmApiService {
    constructor(config) {
        this.config = config;
        this.csrfToken = null;
        this.initCsrfToken();
    }

    async initCsrfToken() {
        try {
            const res = await fetch(`${this.config.API_BASE_URL}/auth/csrf-token`, { credentials: "include" });
            if (res.ok) {
                const data = await res.json();
                this.csrfToken = data.csrfToken;
            }
        } catch (e) {
            // CSRF fetch fallback
        }
    }

    /**
     * Internal request wrapper handling authentication cookies, CSRF, & error envelopes
     */
    async _request(endpoint, method = "GET", body = null) {
        if (!this.csrfToken && (method === "POST" || method === "PUT" || method === "PATCH" || method === "DELETE")) {
            await this.initCsrfToken();
        }

        const headers = {
            "Content-Type": "application/json",
            "Accept": "application/json"
        };

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

        // Add timeout controller
        const controller = new AbortController();
        const timeoutId = setTimeout(() => controller.abort(), this.config.REQUEST_TIMEOUT_MS);
        options.signal = controller.signal;

        try {
            const response = await fetch(`${this.config.API_BASE_URL}${endpoint}`, options);
            clearTimeout(timeoutId);
            
            // Capture updated CSRF token from headers if present
            const newCsrf = response.headers.get("X-CSRF-Token");
            if (newCsrf) this.csrfToken = newCsrf;

            if (response.status === 401) {
                if (window.edmAuth && typeof window.edmAuth.handleSessionExpired === "function") {
                    window.edmAuth.handleSessionExpired();
                } else if (window.edmApp && typeof window.edmApp.showAuthModal === "function") {
                    window.edmApp.showAuthModal("Session Expired. Please authenticate.");
                }
                throw new Error("Authentication required: Session expired or invalid.");
            }

            if (response.status === 403) {
                const forbiddenData = await response.json().catch(() => ({}));
                const msg = forbiddenData.message || "Access Denied: Insufficient administrative permissions.";
                if (window.edmApp && typeof window.edmApp.showToast === "function") {
                    window.edmApp.showToast(msg, "danger");
                }
                throw new Error(msg);
            }

            if (!response.ok) {
                const errData = await response.json().catch(() => ({}));
                const errMsg = errData.message || errData.error || `HTTP ${response.status}: ${response.statusText}`;
                throw new Error(errMsg);
            }

            // Return json if content exists
            const text = await response.text();
            return text ? JSON.parse(text) : { success: true };
        } catch (error) {
            clearTimeout(timeoutId);
            if (error.name === "AbortError") {
                throw new Error("Request timed out. Please verify your connection.");
            }
            throw error;
        }
    }

    // ══════════════════════════════════════════════════════════════
    // 1. DASHBOARD & ANALYTICS
    // ══════════════════════════════════════════════════════════════
    async getDashboardMetrics() {
        const data = await this._request("/admin/dashboard/summary");
        return {
            totalUsers: data.totalUsers || 0,
            activeUsers: data.activeUsers || 0,
            totalDownloads: data.totalDownloads || 0,
            downloadsToday: data.downloadsToday || 0,
            currentVersion: data.currentRelease || "v2.1.0",
            registeredDevices: data.registeredDevices || 0,
            activeSessions: data.activeSessions || 0,
            securityEvents: data.securityEvents || 0,
            bannedAccounts: data.bannedAccounts || 0,
            serverTime: data.serverTimeUtc
        };
    }

    async getAnalyticsData(period = "7d") {
        const [dlData, usrData, platData, verData] = await Promise.all([
            this._request(`/admin/analytics/downloads?range=${encodeURIComponent(period)}`),
            this._request(`/admin/analytics/users?range=${encodeURIComponent(period)}`),
            this._request("/admin/analytics/platforms"),
            this._request("/admin/analytics/versions")
        ]);
        return {
            downloads: dlData,
            users: usrData,
            platforms: platData,
            versions: verData
        };
    }

    // ══════════════════════════════════════════════════════════════
    // 2. USER MANAGEMENT
    // ══════════════════════════════════════════════════════════════
    async getUsers(filters = {}) {
        let url = `/admin/users?page=${filters.page || 1}&pageSize=${filters.pageSize || 50}`;
        if (filters.search) url += `&search=${encodeURIComponent(filters.search)}`;
        if (filters.role !== undefined && filters.role !== "all") url += `&role=${encodeURIComponent(filters.role)}`;
        if (filters.status === "active") url += `&isActive=true`;
        if (filters.status === "suspended") url += `&isActive=false`;

        const res = await this._request(url);
        return {
            totalCount: res.totalCount || 0,
            page: res.page || 1,
            pageSize: res.pageSize || 50,
            users: (res.users || []).map(u => ({
                id: u.id,
                name: u.username,
                email: u.email,
                role: u.role,
                status: u.isActive ? "Active" : "Suspended",
                devices: u.deviceCount || 0,
                sessions: u.sessionCount || 0,
                joined: new Date(u.createdAtUtc).toLocaleDateString(),
                lastSeen: u.lastSeenAtUtc ? new Date(u.lastSeenAtUtc).toLocaleString() : "Never",
                plan: u.planName || "Standard",
                twoFactorEnabled: u.twoFactorEnabled,
                emailVerified: u.emailVerified
            }))
        };
    }

    async getUserDetails(userId) {
        return this._request(`/admin/users/${encodeURIComponent(userId)}`);
    }

    async banUser(userId, reason, durationDays = null) {
        return this._request("/admin/ban", "POST", {
            targetType: 0, // UserId
            targetValue: userId.toString(),
            reason: reason || "Administrative sanction",
            durationDays: durationDays
        });
    }

    async unbanUser(userId) {
        return this._request("/admin/unban", "POST", {
            targetType: 0, // UserId
            targetValue: userId.toString()
        });
    }

    async revokeUserSessions(userId) {
        return this._request(`/admin/revoke-user-sessions/${encodeURIComponent(userId)}`, "POST");
    }

    async grantUserPermission(userId, permissionCode) {
        return this._request(`/admin/users/${encodeURIComponent(userId)}/permissions/grant`, "POST", {
            permissionCode: permissionCode
        });
    }

    async revokeUserPermission(userId, permissionCode) {
        return this._request(`/admin/users/${encodeURIComponent(userId)}/permissions/revoke`, "POST", {
            permissionCode: permissionCode
        });
    }

    // ══════════════════════════════════════════════════════════════
    // 3. DEVICE & SESSION MANAGEMENT
    // ══════════════════════════════════════════════════════════════
    async getDevices(filters = {}) {
        let url = `/admin/devices?page=${filters.page || 1}&pageSize=${filters.pageSize || 50}`;
        if (filters.search) url += `&search=${encodeURIComponent(filters.search)}`;
        
        const res = await this._request(url);
        return {
            totalCount: res.totalCount || 0,
            devices: (res.devices || []).map(d => ({
                id: d.id,
                installationId: d.installationId,
                clientType: d.clientType,
                os: d.osVersion || "Windows",
                version: d.appVersion || "v2.1.0",
                country: d.coarseCountryCode || "Global",
                status: d.isBanned ? "Banned" : "Active",
                sessionCount: d.sessionCount || 0,
                lastSeen: new Date(d.lastSeenAtUtc).toLocaleString()
            }))
        };
    }

    async banDevice(installationId, reason) {
        return this._request("/admin/ban", "POST", {
            targetType: 2, // InstallationId
            targetValue: installationId.toString(),
            reason: reason || "Hardware device ban"
        });
    }

    async getSessions(filters = {}) {
        let url = `/admin/sessions?page=${filters.page || 1}&pageSize=${filters.pageSize || 50}`;
        const res = await this._request(url);
        return {
            totalCount: res.totalCount || 0,
            sessions: (res.sessions || []).map(s => ({
                id: s.id,
                userId: s.userId,
                username: s.username,
                email: s.email,
                installationId: s.installationId,
                clientType: s.clientType,
                osVersion: s.osVersion,
                appVersion: s.appVersion,
                coarseIp: s.coarseIpAddress || "Internal",
                isRevoked: s.isRevoked,
                lastActivity: new Date(s.lastActivityAtUtc).toLocaleString(),
                expiresAt: new Date(s.expiresAtUtc).toLocaleString()
            }))
        };
    }

    async revokeSession(sessionId) {
        return this._request(`/admin/revoke-session/${encodeURIComponent(sessionId)}`, "POST");
    }

    // ══════════════════════════════════════════════════════════════
    // 4. LICENSES, PLANS & PRICING
    // ══════════════════════════════════════════════════════════════
    async getLicenses(filters = {}) {
        let url = `/licenses?page=${filters.page || 1}&pageSize=${filters.pageSize || 50}`;
        if (filters.search) url += `&search=${encodeURIComponent(filters.search)}`;
        if (filters.status) url += `&status=${encodeURIComponent(filters.status)}`;
        return this._request(url);
    }

    async getPlans() {
        return this._request("/licenses/plans");
    }

    async generateLicense(planId, userId = null, maxActivations = 3, durationDays = null) {
        return this._request("/licenses/generate", "POST", {
            planId,
            userId,
            maxActivations: parseInt(maxActivations, 10) || 3,
            durationDays: durationDays ? parseInt(durationDays, 10) : null
        });
    }

    async suspendLicense(licenseId, reason) {
        return this._request(`/licenses/${encodeURIComponent(licenseId)}/suspend`, "POST", {
            reason: reason || "Administrative suspension"
        });
    }

    async reactivateLicense(licenseId) {
        return this._request(`/licenses/${encodeURIComponent(licenseId)}/reactivate`, "POST");
    }

    async revokeLicense(licenseId, reason) {
        return this._request(`/licenses/${encodeURIComponent(licenseId)}/revoke`, "POST", {
            reason: reason || "Administrative revocation"
        });
    }

    async getPricingTiers(activeOnly = true) {
        return this._request(`/pricing?activeOnly=${activeOnly}`);
    }

    async upsertPricingTier(tierPayload) {
        return this._request("/pricing", "POST", tierPayload);
    }

    // ══════════════════════════════════════════════════════════════
    // 5. RELEASES & UPDATE CENTER
    // ══════════════════════════════════════════════════════════════
    async getReleases() {
        const res = await this._request("/admin/releases");
        return (res || []).map(r => ({
            id: r.id,
            version: r.version,
            title: r.title || `EDM ${r.version}`,
            notes: r.releaseNotes || "",
            status: r.isWithdrawn ? "Archived / Withdrawn" : (r.isPublished ? "Active / Production" : "Draft"),
            type: r.isMandatory ? "CRITICAL" : "RECOMMENDED",
            channel: r.channel || "stable",
            platform: r.platform,
            date: new Date(r.publishedAtUtc || r.createdAtUtc).toLocaleDateString(),
            rollbackTargetVersion: r.rollbackTargetVersion,
            rollbackReason: r.rollbackReason,
            artifacts: r.artifacts || []
        }));
    }

    async createRelease(releasePayload) {
        const payload = {
            platform: parseInt(releasePayload.platform, 10) || 0, // 0 = DesktopWindows
            version: releasePayload.version,
            channel: releasePayload.channel || "stable",
            minimumSupportedVersion: releasePayload.minimumSupportedVersion || "1.0.0",
            title: releasePayload.title || `EDM ${releasePayload.version} Turbo Release`,
            releaseNotes: releasePayload.notes || "",
            isMandatory: releasePayload.type === "CRITICAL",
            severity: releasePayload.type === "CRITICAL" ? 2 : 0,
            artifacts: releasePayload.artifacts || [
                {
                    artifactName: `EDM-Setup-${releasePayload.version}.exe`,
                    architecture: "x64",
                    downloadUrl: `https://releases.edm-download.org/desktop/EDM-Setup-${releasePayload.version}.exe`,
                    sha256Hash: "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
                    fileSizeBytes: 2500000
                }
            ]
        };

        const res = await this._request("/admin/releases", "POST", payload);

        // Broadcast to public website
        try {
            const bus = new BroadcastChannel("edm_product_state_bus");
            bus.postMessage({ type: "PRODUCT_STATE_CHANGED", latestVersion: releasePayload.version });
        } catch (e) {}

        return res;
    }

    async uploadReleaseArtifact(releaseId, file, architecture = "x64", expectedSha256 = null, onProgress = null) {
        return new Promise((resolve, reject) => {
            const xhr = new XMLHttpRequest();
            xhr.open("POST", `${this.baseUrl}/admin/releases/${encodeURIComponent(releaseId)}/artifacts/upload`, true);

            const token = localStorage.getItem("edm_admin_jwt") || sessionStorage.getItem("edm_admin_jwt");
            if (token) xhr.setRequestHeader("Authorization", `Bearer ${token}`);

            const csrf = this._getCsrfToken();
            if (csrf) xhr.setRequestHeader("X-CSRF-TOKEN", csrf);

            if (xhr.upload && onProgress) {
                xhr.upload.onprogress = (e) => {
                    if (e.lengthComputable) {
                        const percent = Math.round((e.loaded / e.total) * 100);
                        onProgress(percent, e.loaded, e.total);
                    }
                };
            }

            xhr.onload = () => {
                try {
                    const data = JSON.parse(xhr.responseText);
                    if (xhr.status >= 200 && xhr.status < 300) {
                        resolve(data);
                    } else {
                        reject(new Error(data.message || data.error || `HTTP ${xhr.status}: Upload failed`));
                    }
                } catch (err) {
                    reject(new Error(`Upload failed with status ${xhr.status}`));
                }
            };

            xhr.onerror = () => reject(new Error("Network error during file upload."));

            const formData = new FormData();
            formData.append("file", file);
            formData.append("architecture", architecture);
            if (expectedSha256) formData.append("expectedSha256", expectedSha256);

            xhr.send(formData);
        });
    }

    async updateRelease(releaseId, releasePayload) {
        const res = await this._request(`/admin/releases/${encodeURIComponent(releaseId)}`, "PUT", releasePayload);
        try {
            const bus = new BroadcastChannel("edm_product_state_bus");
            bus.postMessage({ type: "PRODUCT_STATE_CHANGED" });
        } catch (e) {}
        return res;
    }

    async publishRelease(releaseId) {
        const res = await this._request(`/admin/releases/${encodeURIComponent(releaseId)}/publish`, "POST");
        try {
            const bus = new BroadcastChannel("edm_product_state_bus");
            bus.postMessage({ type: "PRODUCT_STATE_CHANGED" });
        } catch (e) {}
        return res;
    }

    async unpublishRelease(releaseId) {
        const res = await this._request(`/admin/releases/${encodeURIComponent(releaseId)}/unpublish`, "POST");
        try {
            const bus = new BroadcastChannel("edm_product_state_bus");
            bus.postMessage({ type: "PRODUCT_STATE_CHANGED" });
        } catch (e) {}
        return res;
    }

    async deleteReleaseArtifact(releaseId, artifactId) {
        return this._request(`/admin/releases/${encodeURIComponent(releaseId)}/artifacts/${encodeURIComponent(artifactId)}`, "DELETE");
    }

    async rollbackRelease(releaseId, targetVersion, reason) {
        const res = await this._request(`/admin/releases/${encodeURIComponent(releaseId)}/rollback`, "POST", {
            targetVersion,
            reason: reason || "Administrative safety rollback"
        });
        try {
            const bus = new BroadcastChannel("edm_product_state_bus");
            bus.postMessage({ type: "PRODUCT_STATE_CHANGED" });
        } catch (e) {}
        return res;
    }

    async archiveRelease(releaseId) {
        return this._request(`/admin/releases/${encodeURIComponent(releaseId)}/archive`, "PUT");
    }

    // ══════════════════════════════════════════════════════════════
    // 6. WEBSITE CONTENT MANAGEMENT
    // ══════════════════════════════════════════════════════════════
    async getAllWebsiteContent(locale = "en") {
        return this._request(`/content?locale=${encodeURIComponent(locale)}`);
    }

    async getWebsiteContent(sectionKey, locale = "en") {
        return this._request(`/content/${encodeURIComponent(sectionKey)}?locale=${encodeURIComponent(locale)}`);
    }

    async updateWebsiteContent(sectionKey, title, contentJson, locale = "en") {
        return this._request(`/content/${encodeURIComponent(sectionKey)}`, "PUT", {
            title,
            contentJson,
            locale
        });
    }

    // ══════════════════════════════════════════════════════════════
    // 7. SUPPORT TICKETS
    // ══════════════════════════════════════════════════════════════
    async getSupportTickets(filters = {}) {
        let url = `/support/tickets?page=${filters.page || 1}&pageSize=${filters.pageSize || 50}`;
        if (filters.status) url += `&status=${encodeURIComponent(filters.status)}`;
        if (filters.priority) url += `&priority=${encodeURIComponent(filters.priority)}`;
        if (filters.category) url += `&category=${encodeURIComponent(filters.category)}`;
        return this._request(url);
    }

    async getTicketDetails(ticketId) {
        return this._request(`/support/tickets/${encodeURIComponent(ticketId)}`);
    }

    async replyTicket(ticketId, messageContent) {
        return this._request(`/support/tickets/${encodeURIComponent(ticketId)}/reply`, "POST", {
            messageContent
        });
    }

    async updateTicketStatus(ticketId, status) {
        return this._request(`/support/tickets/${encodeURIComponent(ticketId)}/status`, "PUT", {
            status: parseInt(status, 10)
        });
    }

    // ══════════════════════════════════════════════════════════════
    // 8. NOTIFICATIONS & ANNOUNCEMENTS
    // ══════════════════════════════════════════════════════════════
    async getNotifications(unreadOnly = false) {
        return this._request(`/admin/notifications?unreadOnly=${unreadOnly}`);
    }

    async markNotificationsRead() {
        return this._request("/admin/notifications/mark-read", "POST");
    }

    async getAnnouncements() {
        return this._request("/admin/announcements");
    }

    async createAnnouncement(announcement) {
        return this._request("/admin/announcements", "POST", announcement);
    }

    // ══════════════════════════════════════════════════════════════
    // 9. AUDIT LOGS & SECURITY
    // ══════════════════════════════════════════════════════════════
    async getAuditLogs(filters = {}) {
        let url = `/admin/audit-logs?page=${filters.page || 1}&pageSize=${filters.pageSize || 50}`;
        if (filters.action) url += `&action=${encodeURIComponent(filters.action)}`;
        const res = await this._request(url);
        return {
            totalCount: res.totalCount || 0,
            logs: (res.logs || []).map(l => ({
                id: l.id,
                actor: l.actorUsername || "System",
                action: l.action,
                target: `${l.targetEntity}: ${l.targetId || ""}`,
                status: l.resultStatus,
                ip: l.coarseIpAddress || l.rawIpAddress || "Internal",
                time: new Date(l.timestampUtc).toLocaleString(),
                details: l.detailsJson
            }))
        };
    }

    async getSecurityOverview() {
        return this._request("/auth/security-overview");
    }

    // ══════════════════════════════════════════════════════════════
    // 10. SYSTEM HEALTH & DIAGNOSTICS
    // ══════════════════════════════════════════════════════════════
    async getSystemHealth() {
        return this._request("/health/diagnostics");
    }

    async getWebsiteAnalytics(range = "7d") {
        return this._request(`/admin/analytics/website?range=${encodeURIComponent(range)}`);
    }

    async getDownloadAnalyticsOverview(range = "30d") {
        return this._request(`/admin/analytics/downloads/overview?range=${encodeURIComponent(range)}`);
    }

    // ══════════════════════════════════════════════════════════════
    // 12. STORAGE & LOCAL HDD FILE SYNC & FILE EXPLORER
    // ══════════════════════════════════════════════════════════════
    async getSyncedFiles(options = {}) {
        let params = new URLSearchParams();
        if (typeof options === "string") {
            params.append("category", options);
        } else {
            if (options.folder !== undefined && options.folder !== null) params.append("folder", options.folder);
            if (options.search) params.append("search", options.search);
            if (options.category) params.append("category", options.category);
            if (options.includeDeleted) params.append("includeDeleted", "true");
        }
        const qs = params.toString();
        const url = `/storage/files${qs ? "?" + qs : ""}`;
        return this._request(url);
    }

    async uploadFile(file, targetFolder = "", category = "Uploads") {
        if (!this.csrfToken) await this.initCsrfToken();
        const formData = new FormData();
        formData.append("file", file);
        if (targetFolder) formData.append("targetFolder", targetFolder);
        if (category) formData.append("category", category);

        const headers = {};
        if (this.csrfToken) headers["X-CSRF-Token"] = this.csrfToken;

        const res = await fetch(`${this.config.API_BASE_URL}/storage/upload`, {
            method: "POST",
            headers,
            credentials: "include",
            body: formData
        });

        if (!res.ok) {
            const err = await res.json().catch(() => ({ message: `Upload failed (Status ${res.status})` }));
            throw new Error(err.message || "Upload failed");
        }

        return res.json();
    }

    async getFilePreview(fileId) {
        return this._request(`/storage/files/${fileId}/preview`);
    }

    getDownloadUrl(fileId) {
        return `${this.config.API_BASE_URL}/storage/files/${fileId}/download`;
    }

    getPreviewMediaUrl(fileId) {
        return `${this.config.API_BASE_URL}/storage/files/${fileId}/preview`;
    }

    async renameFile(fileId, newFileName) {
        return this._request(`/storage/files/${fileId}/rename`, "POST", { newFileName });
    }

    async moveFile(fileId, targetFolder) {
        return this._request(`/storage/files/${fileId}/move`, "POST", { targetFolder });
    }

    async registerFileMetadata(payload) {
        return this._request("/storage/files", "POST", payload);
    }

    async resolveFileConflict(fileId, strategy, resolvedHash = null, resolvedSize = null) {
        return this._request(`/storage/files/${fileId}/resolve-conflict`, "POST", {
            strategy,
            resolvedHash,
            resolvedSize
        });
    }

    async deleteSyncedFile(fileId) {
        return this._request(`/storage/files/${fileId}`, "DELETE");
    }

    async restoreSyncedFile(fileId) {
        return this._request(`/storage/files/${fileId}/restore`, "POST", {});
    }

    async permanentlyDeleteFile(fileId) {
        return this._request(`/storage/files/${fileId}/permanent`, "DELETE");
    }

    async getStorageQuota() {
        return this._request("/storage/quota");
    }

    // ══════════════════════════════════════════════════════════════
    // REMOTE CONTROL & LIVE TELEMETRY
    // ══════════════════════════════════════════════════════════════
    async getRemoteDevices() {
        return this._request("/remote/devices");
    }

    async getRemoteDownloads(deviceId = null) {
        const query = deviceId ? `?deviceId=${encodeURIComponent(deviceId)}` : "";
        return this._request(`/remote/downloads${query}`);
    }

    async sendRemoteCommand(deviceId, commandType, targetDownloadId = null, payload = null) {
        return this._request("/remote/commands", "POST", {
            deviceId,
            commandType,
            targetDownloadId,
            payload
        });
    }

    async getRemoteCommandStatus(commandId) {
        return this._request(`/remote/commands/${commandId}`);
    }
}

// Global live API instance
window.edmApi = new EdmApiService(EDM_API_CONFIG);
