using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EDM.Services.Interfaces;

namespace EDM.Services
{
    public enum AccountSecurityState
    {
        Active,
        Suspended,
        Revoked,
        Offline
    }

    public record UpdateCheckResult(
        bool UpdateAvailable,
        string CurrentVersion,
        string LatestVersion,
        string MinimumSupportedVersion,
        bool IsMandatory,
        string Severity,
        string Title,
        string ReleaseNotes,
        string? DownloadUrl,
        string? Sha256Hash,
        long FileSizeBytes,
        string? SignatureBase64);

    public class ControlPlaneClient
    {
        private readonly HttpClient _httpClient;
        private readonly ISettingsService _settingsService;
        private Guid _installationId;
        private string? _cachedAccessToken;
        private string? _cachedRefreshToken;
        private AccountSecurityState _currentState = AccountSecurityState.Active;
        private readonly SemaphoreSlim _authLock = new(1, 1);

        public AccountSecurityState CurrentSecurityState => _currentState;
        public Guid InstallationId => _installationId;

        public event Action<AccountSecurityState>? SecurityStateChanged;

        public ControlPlaneClient(HttpClient? httpClient = null, ISettingsService? settingsService = null)
        {
            _settingsService = settingsService ?? new SettingsService();
            _httpClient = httpClient ?? SharedHttpClient.Instance;
            EnsureInstallationId();
            LoadPersistedTokens();
        }

        public Guid EnsureInstallationId()
        {
            if (_installationId != Guid.Empty) return _installationId;

            string? savedId = _settingsService.GetSetting("InstallationIdString");
            if (!string.IsNullOrWhiteSpace(savedId) && Guid.TryParse(savedId, out var parsed))
            {
                _installationId = parsed;
            }
            else
            {
                byte[] randomBytes = new byte[16];
                RandomNumberGenerator.Fill(randomBytes);
                _installationId = new Guid(randomBytes);
                _settingsService.SetSetting("InstallationIdString", _installationId.ToString());
            }

            return _installationId;
        }

        private string GetApiBaseUrl()
        {
            if (_httpClient.BaseAddress != null)
            {
                return _httpClient.BaseAddress.ToString().TrimEnd('/');
            }

            string? url = _settingsService.GetSetting("ControlPlaneApiUrl");
            return string.IsNullOrWhiteSpace(url) ? "http://localhost:5000" : url.TrimEnd('/');
        }

        private void LoadPersistedTokens()
        {
            try
            {
                string? encryptedAccess = _settingsService.GetSetting("EncryptedAccessToken");
                string? encryptedRefresh = _settingsService.GetSetting("EncryptedRefreshToken");

                if (!string.IsNullOrWhiteSpace(encryptedAccess))
                {
                    _cachedAccessToken = SecureCredentialVault.DecryptSecret(encryptedAccess);
                }

                if (!string.IsNullOrWhiteSpace(encryptedRefresh))
                {
                    _cachedRefreshToken = SecureCredentialVault.DecryptSecret(encryptedRefresh);
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogException("[ControlPlaneClient] Failed to load persisted DPAPI tokens", ex);
            }
        }

        private void PersistTokens(string? access, string? refresh)
        {
            _cachedAccessToken = access;
            _cachedRefreshToken = refresh;

            try
            {
                if (!string.IsNullOrEmpty(access))
                {
                    _settingsService.SetSetting("EncryptedAccessToken", SecureCredentialVault.EncryptSecret(access));
                }
                else
                {
                    _settingsService.SetSetting("EncryptedAccessToken", string.Empty);
                }

                if (!string.IsNullOrEmpty(refresh))
                {
                    _settingsService.SetSetting("EncryptedRefreshToken", SecureCredentialVault.EncryptSecret(refresh));
                }
                else
                {
                    _settingsService.SetSetting("EncryptedRefreshToken", string.Empty);
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogException("[ControlPlaneClient] Failed to save DPAPI tokens", ex);
            }
        }

        public async Task<bool> LoginAsync(string usernameOrEmail, string password, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(usernameOrEmail) || string.IsNullOrWhiteSpace(password)) return false;

            await _authLock.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                string endpoint = $"{GetApiBaseUrl()}/api/v1/auth/login";
                var payload = new
                {
                    UsernameOrEmail = usernameOrEmail,
                    Password = password,
                    InstallationId = _installationId
                };

                using var response = await _httpClient.PostAsJsonAsync(endpoint, payload, ct).ConfigureAwait(false);
                if (response.IsSuccessStatusCode)
                {
                    var doc = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct).ConfigureAwait(false);
                    string? access = doc.GetProperty("accessToken").GetString();
                    string? refresh = doc.GetProperty("refreshToken").GetString();

                    PersistTokens(access, refresh);
                    SetSecurityState(AccountSecurityState.Active);
                    return true;
                }
                else if (response.StatusCode == HttpStatusCode.Forbidden)
                {
                    SetSecurityState(AccountSecurityState.Suspended);
                }
            }
            catch (Exception ex)
            {
                LoggingService.Log($"[ControlPlaneClient] Login failed / server offline: {ex.Message}");
                SetSecurityState(AccountSecurityState.Offline);
            }
            finally
            {
                _authLock.Release();
            }

            return false;
        }

        public async Task<bool> RefreshSessionAsync(CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(_cachedRefreshToken)) return false;

            await _authLock.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                string endpoint = $"{GetApiBaseUrl()}/api/v1/auth/refresh";
                var payload = new
                {
                    RefreshToken = _cachedRefreshToken,
                    InstallationId = _installationId
                };

                using var response = await _httpClient.PostAsJsonAsync(endpoint, payload, ct).ConfigureAwait(false);
                if (response.IsSuccessStatusCode)
                {
                    var doc = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct).ConfigureAwait(false);
                    string? access = doc.GetProperty("accessToken").GetString();
                    string? refresh = doc.GetProperty("refreshToken").GetString();

                    PersistTokens(access, refresh);
                    SetSecurityState(AccountSecurityState.Active);
                    return true;
                }
                else if (response.StatusCode == HttpStatusCode.Forbidden)
                {
                    SetSecurityState(AccountSecurityState.Suspended);
                }
                else if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    PersistTokens(null, null);
                    SetSecurityState(AccountSecurityState.Revoked);
                }
            }
            catch
            {
                SetSecurityState(AccountSecurityState.Offline);
            }
            finally
            {
                _authLock.Release();
            }

            return false;
        }

        public async Task<AccountSecurityState> CheckAccountStatusAsync(CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(_cachedAccessToken))
            {
                return _currentState; // Default to current state (Active or Offline if offline error occurred)
            }

            try
            {
                string endpoint = $"{GetApiBaseUrl()}/api/v1/auth/me";
                using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _cachedAccessToken);

                using var response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);
                if (response.IsSuccessStatusCode)
                {
                    SetSecurityState(AccountSecurityState.Active);
                    return AccountSecurityState.Active;
                }
                else if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    // Attempt token refresh
                    bool refreshed = await RefreshSessionAsync(ct).ConfigureAwait(false);
                    return refreshed ? AccountSecurityState.Active : AccountSecurityState.Revoked;
                }
                else if (response.StatusCode == HttpStatusCode.Forbidden)
                {
                    SetSecurityState(AccountSecurityState.Suspended);
                    return AccountSecurityState.Suspended;
                }
            }
            catch (Exception ex)
            {
                LoggingService.Log($"[ControlPlaneClient] CheckAccountStatus failed (Offline): {ex.Message}");
                SetSecurityState(AccountSecurityState.Offline);
                return AccountSecurityState.Offline;
            }

            return _currentState;
        }

        public async Task<UpdateCheckResult?> CheckForUpdateAsync(string currentVersion = "2.0.0", CancellationToken ct = default)
        {
            try
            {
                string endpoint = $"{GetApiBaseUrl()}/api/v1/updates/check";
                var payload = new
                {
                    Platform = 0, // DesktopWindows
                    CurrentVersion = currentVersion,
                    InstallationId = _installationId
                };

                using var response = await _httpClient.PostAsJsonAsync(endpoint, payload, ct).ConfigureAwait(false);
                if (response.IsSuccessStatusCode)
                {
                    var doc = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct).ConfigureAwait(false);
                    string? rawDl = doc.TryGetProperty("downloadUrl", out var du) ? du.GetString() : null;
                    string? resolvedDl = rawDl;
                    if (!string.IsNullOrWhiteSpace(rawDl) && rawDl.StartsWith("/"))
                    {
                        resolvedDl = $"{GetApiBaseUrl()}{rawDl}";
                    }

                    return new UpdateCheckResult(
                        UpdateAvailable: doc.GetProperty("updateAvailable").GetBoolean(),
                        CurrentVersion: doc.GetProperty("currentVersion").GetString() ?? currentVersion,
                        LatestVersion: doc.GetProperty("latestVersion").GetString() ?? currentVersion,
                        MinimumSupportedVersion: doc.GetProperty("minimumSupportedVersion").GetString() ?? "1.0.0",
                        IsMandatory: doc.GetProperty("isMandatory").GetBoolean(),
                        Severity: doc.GetProperty("severity").GetString() ?? "OPTIONAL",
                        Title: doc.GetProperty("title").GetString() ?? string.Empty,
                        ReleaseNotes: doc.GetProperty("releaseNotes").GetString() ?? string.Empty,
                        DownloadUrl: resolvedDl,
                        Sha256Hash: doc.TryGetProperty("sha256Hash", out var sh) ? sh.GetString() : null,
                        FileSizeBytes: doc.TryGetProperty("fileSizeBytes", out var fs) ? fs.GetInt64() : 0,
                        SignatureBase64: doc.TryGetProperty("signatureBase64", out var sb) ? sb.GetString() : null);
                }
            }
            catch (Exception ex)
            {
                LoggingService.Log($"[ControlPlaneClient] Update check failed (Offline fallback): {ex.Message}");
            }

            return null;
        }

        public async Task<bool> SendTelemetryEventAsync(string eventName, object payload, CancellationToken ct = default)
        {
            bool optIn = _settingsService.GetBoolSetting("TelemetryOptIn", true);
            if (!optIn) return true;

            try
            {
                string endpoint = $"{GetApiBaseUrl()}/api/v1/telemetry/event";
                var requestBody = new
                {
                    InstallationId = _installationId,
                    EventName = eventName,
                    Payload = payload
                };

                using var response = await _httpClient.PostAsJsonAsync(endpoint, requestBody, ct).ConfigureAwait(false);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                // Telemetry must degrade gracefully without throwing
                return false;
            }
        }

        private void SetSecurityState(AccountSecurityState newState)
        {
            if (_currentState != newState)
            {
                _currentState = newState;
                _settingsService.SetSetting("LastKnownAccountStatus", newState.ToString());
                try { SecurityStateChanged?.Invoke(newState); } catch { }
            }
        }
    }
}
