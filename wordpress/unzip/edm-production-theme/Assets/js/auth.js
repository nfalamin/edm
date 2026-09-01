/**
 * EDM Control Plane — Master Super Admin Authentication & Security Engine
 * Supports Passwords, RFC 6238 TOTP 2FA, Backup Recovery Codes, Google Sign-In, and Passkeys
 * Hardened: Pure HttpOnly Cookie-based Authentication + CSRF Header Validation (Zero LocalStorage Token Dependency)
 */

class EdmAuthService {
    constructor() {
        this.currentUser = null;
        this.pending2FaTicket = null;
        this.csrfToken = null;
    }

    getCurrentUser() {
        return this.currentUser;
    }

    async getCsrfToken() {
        if (this.csrfToken) return this.csrfToken;
        if (window.edmApi && window.edmApi.csrfToken) {
            this.csrfToken = window.edmApi.csrfToken;
            return this.csrfToken;
        }
        try {
            const res = await fetch("/api/v1/auth/csrf-token", { credentials: "include" });
            if (res.ok) {
                const data = await res.json();
                this.csrfToken = data.csrfToken;
                return this.csrfToken;
            }
        } catch (e) {}
        return null;
    }

    async checkAuth() {
        try {
            const res = await fetch("/api/v1/auth/me", {
                headers: { "Accept": "application/json" },
                credentials: "include"
            });

            if (res.ok) {
                const user = await res.json();
                if (user && user.role && (user.role === "SUPER_ADMIN" || user.role === "ADMIN" || user.role === "SUPPORT" || user.role === "RELEASE_MANAGER" || user.role === "ANALYST")) {
                    this.currentUser = user;
                    this.updateUserUI();
                    this.hideAuthModal();
                    await this.getCsrfToken();
                    return true;
                }
            }
        } catch (e) {
            console.error("[Auth] Session validation failed:", e);
        }

        this.currentUser = null;
        this.showAuthModal("Authentication required to access EDM Admin Control Plane.");
        return false;
    }

    async login(usernameOrEmail, password, rememberDevice = true) {
        try {
            const res = await fetch("/api/v1/auth/login", {
                method: "POST",
                headers: { "Content-Type": "application/json", "Accept": "application/json" },
                credentials: "include",
                body: JSON.stringify({ usernameOrEmail, password, rememberDevice })
            });

            const data = await res.json();

            if (!res.ok) {
                throw new Error(data.message || "Invalid credentials.");
            }

            if (data.requires2FA) {
                this.pending2FaTicket = data.twoFactorTicket;
                this.show2FaStep();
                return { requires2FA: true, message: data.message };
            }

            if (data.csrfToken) {
                this.csrfToken = data.csrfToken;
                if (window.edmApi) window.edmApi.csrfToken = data.csrfToken;
            }

            this.currentUser = data.user;
            this.updateUserUI();
            this.hideAuthModal();

            if (window.edmApp) {
                window.edmApp.showToast(`👑 Welcome back, Super Admin ${this.currentUser.username}!`, "success");
                window.edmApp.renderCurrentView();
            }
            return { success: true };
        } catch (err) {
            throw err;
        }
    }

    async verify2Fa(code, isRecoveryCode = false) {
        if (!this.pending2FaTicket) {
            throw new Error("No active 2FA challenge found. Please log in again.");
        }

        try {
            const res = await fetch("/api/v1/auth/2fa/verify", {
                method: "POST",
                headers: { "Content-Type": "application/json", "Accept": "application/json" },
                credentials: "include",
                body: JSON.stringify({
                    twoFactorTicket: this.pending2FaTicket,
                    code: code.trim(),
                    isRecoveryCode: isRecoveryCode
                })
            });

            const data = await res.json();
            if (!res.ok) {
                throw new Error(data.message || "Invalid 2FA code.");
            }

            this.pending2FaTicket = null;
            if (data.csrfToken) {
                this.csrfToken = data.csrfToken;
                if (window.edmApi) window.edmApi.csrfToken = data.csrfToken;
            }

            this.currentUser = data.user;
            this.updateUserUI();
            this.hideAuthModal();

            if (window.edmApp) {
                window.edmApp.showToast(`🛡️ 2FA Verified. Control Plane Unlocked for ${this.currentUser.username}!`, "success");
                window.edmApp.renderCurrentView();
            }

            return { success: true };
        } catch (err) {
            throw err;
        }
    }

    async loginWithGoogle(idToken) {
        try {
            const res = await fetch("/api/v1/auth/google", {
                method: "POST",
                headers: { "Content-Type": "application/json", "Accept": "application/json" },
                credentials: "include",
                body: JSON.stringify({ idToken })
            });

            const data = await res.json();
            if (!res.ok) {
                throw new Error(data.message || "Google authentication failed.");
            }

            if (data.requires2FA) {
                this.pending2FaTicket = data.twoFactorTicket;
                this.show2FaStep();
                return { requires2FA: true };
            }

            if (data.csrfToken) {
                this.csrfToken = data.csrfToken;
                if (window.edmApi) window.edmApi.csrfToken = data.csrfToken;
            }

            this.currentUser = data.user;
            this.updateUserUI();
            this.hideAuthModal();

            if (window.edmApp) {
                window.edmApp.showToast(`Google Sign-In successful for ${this.currentUser.username}`, "success");
                window.edmApp.renderCurrentView();
            }

            return { success: true };
        } catch (err) {
            throw err;
        }
    }

    async loginWithFirebase(idToken, installationId = null) {
        try {
            const res = await fetch("/api/v1/auth/firebase", {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify({ idToken, installationId, clientType: "WebDashboard" }),
                credentials: "include"
            });

            const data = await res.json();
            if (!res.ok) {
                throw new Error(data.message || "Firebase authentication failed.");
            }

            if (data.requires2FA) {
                this.pending2FaTicket = data.twoFactorTicket;
                this.show2FaStep();
                return { requires2FA: true };
            }

            if (data.csrfToken) {
                this.csrfToken = data.csrfToken;
                if (window.edmApi) window.edmApi.csrfToken = data.csrfToken;
            }

            this.currentUser = data.user;
            this.updateUserUI();
            this.hideAuthModal();

            if (window.edmApp) {
                window.edmApp.showToast(`Firebase authentication successful for ${this.currentUser.username}`, "success");
                window.edmApp.renderCurrentView();
            }

            return { success: true };
        } catch (err) {
            throw err;
        }
    }

    async loginWithPasskey() {
        if (!window.PublicKeyCredential) {
            throw new Error("WebAuthn / Passkeys are not supported on this browser.");
        }

        try {
            const optRes = await fetch("/api/v1/auth/passkey/login-options", { credentials: "include" });
            const options = await optRes.json();

            // Convert challenge base64url to Uint8Array
            options.challenge = this.base64UrlToBuffer(options.challenge);

            const credential = await navigator.credentials.get({ publicKey: options });
            if (!credential) throw new Error("Passkey authentication was cancelled.");

            const clientDataJson = this.bufferToBase64Url(credential.response.clientDataJSON);
            const authenticatorData = this.bufferToBase64Url(credential.response.authenticatorData);
            const signature = this.bufferToBase64Url(credential.response.signature);
            const credentialId = credential.id;

            const res = await fetch("/api/v1/auth/passkey/login-verify", {
                method: "POST",
                headers: { "Content-Type": "application/json", "Accept": "application/json" },
                credentials: "include",
                body: JSON.stringify({ credentialId, clientDataJson, authenticatorData, signature })
            });

            const data = await res.json();
            if (!res.ok) throw new Error(data.message || "Passkey verification failed.");

            if (data.csrfToken) {
                this.csrfToken = data.csrfToken;
                if (window.edmApi) window.edmApi.csrfToken = data.csrfToken;
            }

            this.currentUser = data.user;
            this.updateUserUI();
            this.hideAuthModal();

            if (window.edmApp) {
                window.edmApp.showToast(`🔑 Passkey Verified. Welcome ${this.currentUser.username}!`, "success");
                window.edmApp.renderCurrentView();
            }

            return { success: true };
        } catch (err) {
            throw err;
        }
    }

    async registerPasskey(deviceName = "My Passkey") {
        if (!window.PublicKeyCredential) {
            throw new Error("WebAuthn / Passkeys are not supported on this browser.");
        }
        const csrf = await this.getCsrfToken();
        const headers = { "Accept": "application/json" };
        if (csrf) headers["X-CSRF-Token"] = csrf;

        const optRes = await fetch("/api/v1/auth/passkey/register-options", {
            headers,
            credentials: "include"
        });
        if (!optRes.ok) throw new Error("Failed to initialize passkey registration.");
        const options = await optRes.json();

        // Convert challenge and user.id to ArrayBuffer
        options.challenge = this.base64UrlToBuffer(options.challenge);
        options.user.id = this.base64UrlToBuffer(options.user.id);

        const credential = await navigator.credentials.create({ publicKey: options });
        if (!credential) throw new Error("Passkey registration was cancelled.");

        const clientDataJson = this.bufferToBase64Url(credential.response.clientDataJSON);
        const attestationObject = this.bufferToBase64Url(credential.response.attestationObject);

        const verifyHeaders = { "Content-Type": "application/json", "Accept": "application/json" };
        if (csrf) verifyHeaders["X-CSRF-Token"] = csrf;

        const res = await fetch("/api/v1/auth/passkey/register-verify", {
            method: "POST",
            headers: verifyHeaders,
            credentials: "include",
            body: JSON.stringify({ clientDataJson, attestationObject, deviceName })
        });
        const data = await res.json();
        if (!res.ok) throw new Error(data.message || "Failed to register passkey.");
        return data;
    }

    async listPasskeys() {
        const csrf = await this.getCsrfToken();
        const headers = { "Accept": "application/json" };
        if (csrf) headers["X-CSRF-Token"] = csrf;

        const res = await fetch("/api/v1/auth/passkeys", {
            headers,
            credentials: "include"
        });
        if (!res.ok) throw new Error("Failed to load passkeys.");
        return await res.json();
    }

    async deletePasskey(passkeyId) {
        const csrf = await this.getCsrfToken();
        const headers = { "Accept": "application/json" };
        if (csrf) headers["X-CSRF-Token"] = csrf;

        const res = await fetch(`/api/v1/auth/passkeys/${passkeyId}`, {
            method: "DELETE",
            headers,
            credentials: "include"
        });
        const data = await res.json();
        if (!res.ok) throw new Error(data.message || "Failed to delete passkey.");
        return data;
    }

    async renamePasskey(passkeyId, newName) {
        const csrf = await this.getCsrfToken();
        const headers = { "Content-Type": "application/json", "Accept": "application/json" };
        if (csrf) headers["X-CSRF-Token"] = csrf;

        const res = await fetch(`/api/v1/auth/passkeys/${passkeyId}/rename`, {
            method: "POST",
            headers,
            credentials: "include",
            body: JSON.stringify({ newName })
        });
        const data = await res.json();
        if (!res.ok) throw new Error(data.message || "Failed to rename passkey.");
        return data;
    }

    async forgotPassword(email) {
        const res = await fetch("/api/v1/auth/forgot-password", {
            method: "POST",
            headers: { "Content-Type": "application/json", "Accept": "application/json" },
            body: JSON.stringify({ email })
        });
        const data = await res.json();
        return data;
    }

    async resetPassword(token, newPassword, twoFactorCode = null, isRecoveryCode = false) {
        const res = await fetch("/api/v1/auth/reset-password", {
            method: "POST",
            headers: { "Content-Type": "application/json", "Accept": "application/json" },
            body: JSON.stringify({
                token,
                newPassword,
                twoFactorCode,
                isRecoveryCode
            })
        });
        const data = await res.json();
        if (!res.ok) throw new Error(data.message || "Password reset failed.");
        return data;
    }

    async setup2Fa() {
        const csrf = await this.getCsrfToken();
        const headers = { "Accept": "application/json" };
        if (csrf) headers["X-CSRF-Token"] = csrf;

        const res = await fetch("/api/v1/auth/2fa/setup", {
            method: "POST",
            headers,
            credentials: "include"
        });
        const data = await res.json();
        if (!res.ok) throw new Error(data.message || "Failed to initialize 2FA setup.");
        return data;
    }

    async confirm2Fa(code) {
        const csrf = await this.getCsrfToken();
        const headers = { "Content-Type": "application/json", "Accept": "application/json" };
        if (csrf) headers["X-CSRF-Token"] = csrf;

        const res = await fetch("/api/v1/auth/2fa/confirm", {
            method: "POST",
            headers,
            credentials: "include",
            body: JSON.stringify({ code })
        });
        const data = await res.json();
        if (!res.ok) throw new Error(data.message || "Invalid 2FA code.");
        if (this.currentUser) this.currentUser.twoFactorEnabled = true;
        return data;
    }

    async logout() {
        try {
            const csrf = await this.getCsrfToken();
            const headers = {};
            if (csrf) headers["X-CSRF-Token"] = csrf;

            await fetch("/api/v1/auth/logout", {
                method: "POST",
                headers,
                credentials: "include"
            });
        } catch (e) {}

        this.currentUser = null;
        this.csrfToken = null;
        if (window.edmApi) window.edmApi.csrfToken = null;
        this.showAuthModal("You have been signed out.");
        if (window.edmApp) {
            window.edmApp.showToast("Signed out successfully.", "info");
        }
    }

    async disable2Fa(password) {
        const csrf = await this.getCsrfToken();
        const headers = { "Content-Type": "application/json", "Accept": "application/json" };
        if (csrf) headers["X-CSRF-Token"] = csrf;

        const res = await fetch("/api/v1/auth/2fa/disable", {
            method: "POST",
            headers,
            credentials: "include",
            body: JSON.stringify({ password })
        });

        const data = await res.json();
        if (!res.ok) throw new Error(data.message || "Failed to disable 2FA.");
        if (this.currentUser) this.currentUser.twoFactorEnabled = false;
        return data;
    }

    async regenerateRecoveryCodes(password) {
        const csrf = await this.getCsrfToken();
        const headers = { "Content-Type": "application/json", "Accept": "application/json" };
        if (csrf) headers["X-CSRF-Token"] = csrf;

        const res = await fetch("/api/v1/auth/2fa/regenerate-recovery-codes", {
            method: "POST",
            headers,
            credentials: "include",
            body: JSON.stringify({ password })
        });

        const data = await res.json();
        if (!res.ok) throw new Error(data.message || "Failed to regenerate recovery codes.");
        return data;
    }

    async requestRecoveryEmail(password, newRecoveryEmail) {
        const csrf = await this.getCsrfToken();
        const headers = { "Content-Type": "application/json", "Accept": "application/json" };
        if (csrf) headers["X-CSRF-Token"] = csrf;

        const res = await fetch("/api/v1/auth/recovery-email/request", {
            method: "POST",
            headers,
            credentials: "include",
            body: JSON.stringify({ password, newRecoveryEmail })
        });

        const data = await res.json();
        if (!res.ok) throw new Error(data.message || "Failed to request recovery email update.");
        return data;
    }

    async confirmRecoveryEmail(tokenCode) {
        const csrf = await this.getCsrfToken();
        const headers = { "Content-Type": "application/json", "Accept": "application/json" };
        if (csrf) headers["X-CSRF-Token"] = csrf;

        const res = await fetch("/api/v1/auth/recovery-email/confirm", {
            method: "POST",
            headers,
            credentials: "include",
            body: JSON.stringify({ token: tokenCode })
        });

        const data = await res.json();
        if (!res.ok) throw new Error(data.message || "Failed to confirm recovery email.");
        return data;
    }

    async getSecurityOverview() {
        const res = await fetch("/api/v1/auth/security-overview", {
            headers: { "Accept": "application/json" },
            credentials: "include"
        });

        if (!res.ok) throw new Error("Failed to load security overview.");
        return await res.json();
    }

    updateUserUI() {
        if (!this.currentUser) return;
        const nameEl = document.getElementById("admin-user-name");
        const roleEl = document.getElementById("admin-user-role");
        const badgeEl = document.getElementById("admin-role-badge");

        if (nameEl) nameEl.textContent = this.currentUser.username;
        if (roleEl) roleEl.textContent = this.currentUser.role.replace("_", " ");
        if (badgeEl) badgeEl.textContent = this.currentUser.role;
    }

    showAuthModal(msg = null) {
        const modal = document.getElementById("modal-admin-auth");
        if (modal) {
            modal.style.display = "flex";
            modal.classList.add("active");
            
            // Reset to Step 1
            const step1 = document.getElementById("auth-step-login");
            const step2 = document.getElementById("auth-step-2fa");
            const stepForgot = document.getElementById("auth-step-forgot");
            if (step1) step1.style.display = "block";
            if (step2) step2.style.display = "none";
            if (stepForgot) stepForgot.style.display = "none";

            const msgEl = document.getElementById("auth-error-banner");
            if (msgEl) {
                if (msg) {
                    msgEl.textContent = msg;
                    msgEl.style.display = "block";
                } else {
                    msgEl.style.display = "none";
                }
            }
        }
    }

    hideAuthModal() {
        const modal = document.getElementById("modal-admin-auth");
        if (modal) {
            modal.style.display = "none";
            modal.classList.remove("active");
        }
    }

    show2FaStep() {
        const step1 = document.getElementById("auth-step-login");
        const step2 = document.getElementById("auth-step-2fa");
        const stepForgot = document.getElementById("auth-step-forgot");
        if (step1) step1.style.display = "none";
        if (step2) step2.style.display = "block";
        if (stepForgot) stepForgot.style.display = "none";
        const codeInput = document.getElementById("auth-2fa-input");
        if (codeInput) {
            codeInput.value = "";
            codeInput.focus();
        }
    }

    base64UrlToBuffer(base64url) {
        const padding = "=".repeat((4 - (base64url.length % 4)) % 4);
        const base64 = (base64url + padding).replace(/-/g, "+").replace(/_/g, "/");
        const rawData = window.atob(base64);
        const outputArray = new Uint8Array(rawData.length);
        for (let i = 0; i < rawData.length; ++i) {
            outputArray[i] = rawData.charCodeAt(i);
        }
        return outputArray.buffer;
    }

    bufferToBase64Url(buffer) {
        const bytes = new Uint8Array(buffer);
        let binary = "";
        for (let i = 0; i < bytes.byteLength; i++) {
            binary += String.fromCharCode(bytes[i]);
        }
        return window.btoa(binary).replace(/\+/g, "-").replace(/\//g, "_").replace(/=+$/, "");
    }
}

// Global Auth instance
window.edmAuth = new EdmAuthService();
