using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace EDM
{
    public enum ClientSubscriptionState
    {
        TrialActive,
        TrialExpired,
        GracePeriod,
        FreeRestricted,
        Subscribed,
        SubscriptionExpired,
        Suspended,
        Blocked,
        AdminOverride
    }

    public record ClientEntitlementPolicy(
        Guid InstallationId,
        Guid? UserId,
        string State,
        string PlanCode,
        string PlanTier,
        int MaxConnections,
        int MaxConcurrentDownloads,
        int TrialDaysRemaining,
        int GraceDaysRemaining,
        DateTime? ExpiresAtUtc,
        Dictionary<string, bool>? FeatureFlags,
        bool IsBlocked,
        string? BlockReason,
        string StatusMessage,
        string CountryCode,
        string Currency,
        decimal MonthlyPrice,
        string FormattedPrice,
        int PolicyVersion,
        int OfflineGraceHours,
        DateTime ServerTimeUtc,
        string Signature);

    public interface ISubscriptionEntitlementClient
    {
        Guid InstallationId { get; }
        ClientSubscriptionState CurrentState { get; }
        int MaxAllowedConnections { get; }
        int MaxConcurrentDownloads { get; }
        int TrialDaysRemaining { get; }
        int GraceDaysRemaining { get; }
        bool IsBlocked { get; }
        string StatusMessage { get; }
        string FormattedPrice { get; }
        bool IsFeatureEnabled(string featureKey);
        Task<ClientEntitlementPolicy> SyncPolicyAsync(CancellationToken cancellationToken = default);
        void StartBackgroundSync(TimeSpan interval);
        void StopBackgroundSync();
    }

    public class SubscriptionEntitlementClient : ISubscriptionEntitlementClient
    {
        private const string SIGNING_KEY = "EDM_CONTROL_PLANE_SECRET_SIGNING_KEY_2026_V210";
        private readonly HttpClient _httpClient;
        private readonly string _cacheFilePath;
        private readonly object _syncLock = new();

        private ClientEntitlementPolicy? _currentPolicy;
        private System.Threading.Timer? _syncTimer;
        private DateTime _lastSyncLocalTime = DateTime.MinValue;
        private DateTime _lastKnownServerTime = DateTime.MinValue;

        public Guid InstallationId { get; private set; }
        public ClientSubscriptionState CurrentState { get; private set; } = ClientSubscriptionState.TrialActive;
        public int MaxAllowedConnections { get; private set; } = 64;
        public int MaxConcurrentDownloads { get; private set; } = 8;
        public int TrialDaysRemaining { get; private set; } = 10;
        public int GraceDaysRemaining { get; private set; } = 5;
        public bool IsBlocked { get; private set; } = false;
        public string StatusMessage { get; private set; } = "Your free trial is active — 10 days remaining.";
        public string FormattedPrice { get; private set; } = "$4.99 / mo";

        public SubscriptionEntitlementClient(HttpClient? httpClient = null, string? appDataDirectory = null)
        {
            _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            
            string baseDir = appDataDirectory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EDM");
            Directory.CreateDirectory(baseDir);
            _cacheFilePath = Path.Combine(baseDir, "entitlement.policy");
            
            InstallationId = LoadOrCreateInstallationId(baseDir);
            LoadCachedPolicy();
        }

        public bool IsFeatureEnabled(string featureKey)
        {
            lock (_syncLock)
            {
                if (IsBlocked) return false;
                if (_currentPolicy?.FeatureFlags != null && _currentPolicy.FeatureFlags.TryGetValue(featureKey, out bool enabled))
                {
                    return enabled;
                }
                // Safe default features
                return true;
            }
        }

        public async Task<ClientEntitlementPolicy> SyncPolicyAsync(CancellationToken cancellationToken = default)
        {
            var requestPayload = new
            {
                InstallationId,
                AppVersion = "2.1.0",
                OsVersion = Environment.OSVersion.VersionString,
                DeviceName = Environment.MachineName
            };

            try
            {
                var response = await _httpClient.PostAsJsonAsync("http://localhost:5000/api/v1/entitlements/sync", requestPayload, cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    var policy = await response.Content.ReadFromJsonAsync<ClientEntitlementPolicy>(cancellationToken: cancellationToken);
                    if (policy != null && ValidatePolicySignature(policy))
                    {
                        ApplyPolicy(policy);
                        SavePolicyCache(policy);
                        return policy;
                    }
                }
            }
            catch
            {
                // Fallback to offline cache
            }

            return ApplyOfflineFallback();
        }

        public void StartBackgroundSync(TimeSpan interval)
        {
            _syncTimer?.Dispose();
            _syncTimer = new System.Threading.Timer(async _ =>
            {
                try { await SyncPolicyAsync(); } catch { }
            }, null, interval, interval);
        }

        public void StopBackgroundSync()
        {
            _syncTimer?.Dispose();
            _syncTimer = null;
        }

        private void ApplyPolicy(ClientEntitlementPolicy policy)
        {
            lock (_syncLock)
            {
                _currentPolicy = policy;
                _lastSyncLocalTime = DateTime.UtcNow;
                _lastKnownServerTime = policy.ServerTimeUtc;

                CurrentState = Enum.TryParse<ClientSubscriptionState>(policy.State.Replace("_", ""), true, out var parsedState)
                    ? parsedState
                    : ClientSubscriptionState.TrialActive;

                MaxAllowedConnections = policy.MaxConnections;
                EDM.Services.GlobalConnectionGovernor.Instance.GlobalMaxConnections = policy.MaxConnections;
                MaxConcurrentDownloads = policy.MaxConcurrentDownloads;
                TrialDaysRemaining = policy.TrialDaysRemaining;
                GraceDaysRemaining = policy.GraceDaysRemaining;
                IsBlocked = policy.IsBlocked;
                StatusMessage = policy.StatusMessage;
                FormattedPrice = policy.FormattedPrice;
            }
        }

        private ClientEntitlementPolicy ApplyOfflineFallback()
        {
            lock (_syncLock)
            {
                var now = DateTime.UtcNow;

                // Check for obvious clock manipulation (e.g. system clock set behind last known server time)
                if (_lastKnownServerTime > DateTime.MinValue && now < _lastKnownServerTime.AddHours(-1))
                {
                    // Clock set back detected
                    CurrentState = ClientSubscriptionState.FreeRestricted;
                    MaxAllowedConnections = 16;
                    StatusMessage = "System clock anomaly detected. Operating in restricted mode until synchronized.";
                }
                else if (_currentPolicy != null && (now - _lastSyncLocalTime).TotalHours <= (_currentPolicy.OfflineGraceHours > 0 ? _currentPolicy.OfflineGraceHours : 72))
                {
                    // Within 72-hour offline grace window
                    ApplyPolicy(_currentPolicy);
                }
                else
                {
                    // Exceeded offline window
                    CurrentState = ClientSubscriptionState.GracePeriod;
                    MaxAllowedConnections = 32;
                    StatusMessage = "Offline grace period active. Connect to internet to verify full subscription.";
                }

                return _currentPolicy ?? new ClientEntitlementPolicy(
                    InstallationId: InstallationId,
                    UserId: null,
                    State: CurrentState.ToString(),
                    PlanCode: "offline_fallback",
                    PlanTier: "Trial",
                    MaxConnections: MaxAllowedConnections,
                    MaxConcurrentDownloads: MaxConcurrentDownloads,
                    TrialDaysRemaining: TrialDaysRemaining,
                    GraceDaysRemaining: GraceDaysRemaining,
                    ExpiresAtUtc: null,
                    FeatureFlags: null,
                    IsBlocked: IsBlocked,
                    BlockReason: null,
                    StatusMessage: StatusMessage,
                    CountryCode: "GLOBAL",
                    Currency: "USD",
                    MonthlyPrice: 4.99m,
                    FormattedPrice: FormattedPrice,
                    PolicyVersion: 1,
                    OfflineGraceHours: 72,
                    ServerTimeUtc: DateTime.UtcNow,
                    Signature: "");
            }
        }

        private bool ValidatePolicySignature(ClientEntitlementPolicy policy)
        {
            if (string.IsNullOrWhiteSpace(policy.Signature)) return false;
            string raw = $"{policy.InstallationId}|{policy.State}|{policy.MaxConnections}|{policy.ExpiresAtUtc:O}|{policy.ServerTimeUtc:O}";
            
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(SIGNING_KEY));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(raw));
            string expected = Convert.ToHexString(hash).ToLowerInvariant();

            return string.Equals(expected, policy.Signature, StringComparison.OrdinalIgnoreCase);
        }

        private Guid LoadOrCreateInstallationId(string baseDir)
        {
            string idFile = Path.Combine(baseDir, "installation.id");
            if (File.Exists(idFile))
            {
                string text = File.ReadAllText(idFile).Trim();
                if (Guid.TryParse(text, out var guid)) return guid;
            }

            var newGuid = Guid.NewGuid();
            try { File.WriteAllText(idFile, newGuid.ToString()); } catch { }
            return newGuid;
        }

        private void LoadCachedPolicy()
        {
            try
            {
                if (File.Exists(_cacheFilePath))
                {
                    string json = File.ReadAllText(_cacheFilePath);
                    var policy = JsonSerializer.Deserialize<ClientEntitlementPolicy>(json);
                    if (policy != null) ApplyPolicy(policy);
                }
            }
            catch { }
        }

        private void SavePolicyCache(ClientEntitlementPolicy policy)
        {
            try
            {
                string json = JsonSerializer.Serialize(policy);
                File.WriteAllText(_cacheFilePath, json);
            }
            catch { }
        }
    }
}
