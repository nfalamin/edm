using System;
using System.IO;
using System.Text.Json;
using EDM.Models;
using EDM.Services.Interfaces;

namespace EDM.Services

{
    public class AppSettings
    {
        public ProxySettings Proxy { get; set; } = new ProxySettings();

        public string DefaultDownloadPath { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        public string FfmpegPath { get; set; } = string.Empty;
        public string YtDlpPath { get; set; } = string.Empty;
        public string Aria2Path { get; set; } = string.Empty;
        public string DefaultFormatArgs { get; set; } = string.Empty;
        public bool AutoConvertToMp3 { get; set; } = false;
        public bool SchedulerEnabled { get; set; } = false;
        public TimeSpan? SchedulerTime { get; set; } = null;
        public System.Collections.Generic.List<string> Categories { get; set; } = new System.Collections.Generic.List<string> { "Programs", "Compressed", "Video", "Music", "Documents", "General" };

        // Network preferences
        public int ConnectionLimitOverride { get; set; } = 0; // 0 = auto-detect based on network type
        public bool WarnOnMeteredNetworks { get; set; } = true;
        public bool ReduceQualityOnMeteredNetworks { get; set; } = true;
        public bool DisableNetworkWarnings { get; set; } = false;
        // Bandwidth limiter in KB/s. 0 = unlimited (default). Stored in KB/s for human readability in settings file.
        public int BandwidthLimitKbps { get; set; } = 0;

        // Smart Download Settings (NEW)
        // Preferred video resolution (1080p, 720p, 480p, 360p, etc.)
        public string PreferredResolution { get; set; } = "720p";
        // Preferred format (mp4 or mp3)
        public string PreferredFormat { get; set; } = "mp4";
        // Auto-use metadata title as filename
        public bool UseMetadataTitle { get; set; } = true;
        // Custom filename pattern (if not using metadata)
        public string CustomFilenamePattern { get; set; } = string.Empty;

        // Bandwidth Scheduling
        // List of time-based bandwidth schedules. Empty by default (no scheduling).
        public System.Collections.Generic.List<BandwidthSchedule> BandwidthSchedules { get; set; } = new System.Collections.Generic.List<BandwidthSchedule>();

        // URL Safety Checking (OFF by default - privacy first)
        public bool EnableUrlSafetyCheck { get; set; } = false;
        // Google Safe Browsing API key (user-provided)
        public string GoogleSafeBrowsingApiKey { get; set; } = string.Empty;

        // Crash reporting preferences - default OFF for privacy
        public bool SendAnonymousCrashReports { get; set; } = false;

        // Clipboard Monitoring preferences (ON by default for seamless out-of-the-box IDM parity)
        public bool EnableClipboardMonitoring { get; set; } = true;
        public bool ClipboardMonitorHttp { get; set; } = true;
        public bool ClipboardMonitorHttps { get; set; } = true;
        public bool ClipboardMonitorFtp { get; set; } = true;
        public ClipboardAction ClipboardAction { get; set; } = ClipboardAction.AskBeforeDownload;
        public bool ClipboardIgnoreDuplicates { get; set; } = true;
        public bool ClipboardShowNotification { get; set; } = true;

        // Browser Integration & Capture preferences
        public bool EnableBrowserIntegration { get; set; } = true;
        public bool BrowserCaptureDownloads { get; set; } = true;
        public bool BrowserShowConfirmation { get; set; } = true;
        public bool BrowserShowNotification { get; set; } = true;
        public string BrowserDownloadMode { get; set; } = "ShowDialog"; // "ShowDialog" (IDM-style) or "StartImmediately"
        public string BrowserInterceptedExtensions { get; set; } = "ZIP RAR 7Z TAR GZ ISO EXE MSI APK BIN MP4 MKV MP3 PDF DOCX XLSX PPTX DMG";

        // Next-Gen Advanced Features
        public bool EnableMultiSourceMirrorAggregation { get; set; } = true;
        public bool EnableSmartFileOrganizer { get; set; } = true;
        public bool EnableSubtitleAutoDownloader { get; set; } = false;
        public System.Collections.Generic.List<string> PreferredSubtitleLanguages { get; set; } = new System.Collections.Generic.List<string> { "en", "bn" };
        public string DefaultCloudUploadProvider { get; set; } = "None";
        public string EncryptedCloudApiToken { get; set; } = string.Empty;

        // P2P LAN Sharing & Instant Streaming
        public bool EnableLanP2PSharing { get; set; } = true;
        public string LanPeerName { get; set; } = Environment.MachineName;

        // Permissioned Auto-Extraction & Analytics
        public bool EnableAutoArchiveExtraction { get; set; } = false; // User explicit permission required
        public bool DeleteArchiveAfterExtraction { get; set; } = false;
        public bool EnableDownloadAnalytics { get; set; } = true;

        // Central Control Plane Integration
        public string ControlPlaneApiUrl { get; set; } = "http://localhost:5000";
        public string InstallationIdString { get; set; } = string.Empty;
        public bool TelemetryOptIn { get; set; } = true;
        public string LastKnownAccountStatus { get; set; } = "Active";

        // Dynamic key-value store fallback
        public System.Collections.Generic.Dictionary<string, string> AdditionalSettings { get; set; } = new System.Collections.Generic.Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    public class SettingsService : ISettingsService
    {
        private readonly string _settingsFile;
        private AppSettings _settings;

        public SettingsService()
        {
            string appDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EDM");
            if (!Directory.Exists(appDataFolder)) Directory.CreateDirectory(appDataFolder);
            _settingsFile = Path.Combine(appDataFolder, "settings.json");
            _settings = Load();
        }

        private AppSettings Load()
        {
            try
            {
                if (File.Exists(_settingsFile))
                {
                    var json = File.ReadAllText(_settingsFile);
                    var s = JsonSerializer.Deserialize<AppSettings>(json);
                    if (s != null)
                    {
                        // Backward compatibility: ensure BandwidthSchedules is initialized
                        if (s.BandwidthSchedules == null)
                        {
                            s.BandwidthSchedules = new System.Collections.Generic.List<BandwidthSchedule>();
                        }
                        return s;
                    }
                }
            }
            catch (Exception ex) { LoggingService.Log($"[SettingsService] Load settings failed: {ex.Message}"); }
            return new AppSettings();
        }

        public void Save()
        {
            try
            {
                var json = JsonSerializer.Serialize(_settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_settingsFile, json);
            }
            catch (Exception ex) { LoggingService.Log($"[SettingsService] Save settings failed: {ex.Message}"); }
        }

        public string GetDefaultDownloadPath() => _settings.DefaultDownloadPath;
        public void SetDefaultDownloadPath(string path)
        {
            _settings.DefaultDownloadPath = path;
            Save();
        }

        public System.Collections.Generic.List<string> GetCategories() => _settings.Categories ?? new System.Collections.Generic.List<string> { "Programs", "Compressed", "Video", "Music", "Documents", "General" };

        public void AddCategory(string category)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(category)) return;
                if (_settings.Categories == null) _settings.Categories = new System.Collections.Generic.List<string>();
                if (!_settings.Categories.Contains(category, StringComparer.OrdinalIgnoreCase))
                {
                    _settings.Categories.Add(category);
                    Save();
                }
            }
            catch (Exception ex) { LoggingService.Log($"[SettingsService] AddCategory failed: {ex.Message}"); }
        }

        public string GetFfmpegPath() => _settings.FfmpegPath;
        public void SetFfmpegPath(string path) { _settings.FfmpegPath = path; Save(); }
        public string GetYtDlpPath() => _settings.YtDlpPath;
        public void SetYtDlpPath(string path) { _settings.YtDlpPath = path; Save(); }
        public string GetAria2Path() => _settings.Aria2Path;
        public void SetAria2Path(string path) { _settings.Aria2Path = path; Save(); }
        public string GetDefaultFormatArgs() => _settings.DefaultFormatArgs;
        public void SetDefaultFormatArgs(string args) { _settings.DefaultFormatArgs = args; Save(); }
        public bool GetAutoConvertToMp3() => _settings.AutoConvertToMp3;
        public void SetAutoConvertToMp3(bool v) { _settings.AutoConvertToMp3 = v; Save(); }

        public bool GetSchedulerEnabled() => _settings.SchedulerEnabled;
        public TimeSpan? GetSchedulerTime() => _settings.SchedulerTime;
        public void SetScheduler(bool enabled, TimeSpan? time) { _settings.SchedulerEnabled = enabled; _settings.SchedulerTime = time; Save(); }

        // Network preferences accessors
        /// <summary>Gets the user-specified connection limit override (0 = auto-detect).</summary>
        public int GetConnectionLimitOverride() => _settings.ConnectionLimitOverride;
        public void SetConnectionLimitOverride(int limit) { _settings.ConnectionLimitOverride = Math.Max(0, limit); Save(); }

        /// <summary>Gets whether to warn before downloading on metered networks.</summary>
        public bool GetWarnOnMeteredNetworks() => _settings.WarnOnMeteredNetworks;
        public void SetWarnOnMeteredNetworks(bool warn) { _settings.WarnOnMeteredNetworks = warn; Save(); }

        /// <summary>Gets whether to reduce quality on metered networks.</summary>
        public bool GetReduceQualityOnMeteredNetworks() => _settings.ReduceQualityOnMeteredNetworks;
        public void SetReduceQualityOnMeteredNetworks(bool reduce) { _settings.ReduceQualityOnMeteredNetworks = reduce; Save(); }

        /// <summary>Gets whether network warnings are disabled globally.</summary>
        public bool GetDisableNetworkWarnings() => _settings.DisableNetworkWarnings;
        public void SetDisableNetworkWarnings(bool disable) { _settings.DisableNetworkWarnings = disable; Save(); }

        /// <summary>Gets or sets the global bandwidth limit in KB/s. 0 = unlimited.</summary>
        public int GetBandwidthLimitKbps() => Math.Max(0, _settings.BandwidthLimitKbps);
        public void SetBandwidthLimitKbps(int kbps) { _settings.BandwidthLimitKbps = Math.Max(0, kbps); Save(); }

        /// <summary>Gets the persisted proxy settings (password remains DPAPI-encrypted; decrypt via ProxyService).</summary>
        public ProxySettings GetProxySettings() => _settings.Proxy ?? new ProxySettings();

        /// <summary>
        /// Persists proxy settings. Pass the plain-text password in <paramref name="plainPassword"/>
        /// (or null to keep the existing stored password unchanged) - it will be DPAPI-encrypted before saving.
        /// </summary>
        public void SetProxySettings(ProxySettings settings, string? plainPassword = null)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));

            if (plainPassword != null)
            {
                settings.EncryptedPassword = string.IsNullOrEmpty(plainPassword)
                    ? string.Empty
                    : ProxyService.EncryptPassword(plainPassword);
            }
            else if (_settings.Proxy != null && string.Equals(_settings.Proxy.Username, settings.Username, StringComparison.Ordinal))
            {
                // Username unchanged and no new password supplied: keep previously stored encrypted password.
                settings.EncryptedPassword = _settings.Proxy.EncryptedPassword;
            }

            _settings.Proxy = settings;
            Save();
        }

        // Smart Download Settings Methods (NEW)
        /// <summary>
        /// Gets the user's preferred video resolution.
        /// </summary>
        public string GetPreferredResolution() => _settings.PreferredResolution ?? "720p";

        /// <summary>
        /// Sets the user's preferred video resolution.
        /// </summary>
        public void SetPreferredResolution(string resolution)
        {
            if (!string.IsNullOrWhiteSpace(resolution))
            {
                _settings.PreferredResolution = resolution;
                Save();
                LoggingService.Log($"[SettingsService] Preferred resolution set to: {resolution}");
            }
        }

        /// <summary>
        /// Gets the user's preferred download format (mp4 or mp3).
        /// </summary>
        public string GetPreferredFormat() => _settings.PreferredFormat ?? "mp4";

        /// <summary>
        /// Sets the user's preferred download format.
        /// </summary>
        public void SetPreferredFormat(string format)
        {
            if (!string.IsNullOrWhiteSpace(format) && (format.Equals("mp4", StringComparison.OrdinalIgnoreCase) || format.Equals("mp3", StringComparison.OrdinalIgnoreCase)))
            {
                _settings.PreferredFormat = format.ToLower();
                Save();
                LoggingService.Log($"[SettingsService] Preferred format set to: {format}");
            }
        }

        /// <summary>
        /// Gets whether to use video metadata title as filename.
        /// </summary>
        public bool GetUseMetadataTitle() => _settings.UseMetadataTitle;

        /// <summary>
        /// Sets whether to use video metadata title as filename.
        /// </summary>
        public void SetUseMetadataTitle(bool useTitle)
        {
            _settings.UseMetadataTitle = useTitle;
            Save();
            LoggingService.Log($"[SettingsService] Use metadata title: {useTitle}");
        }

        /// <summary>
        /// Gets the custom filename pattern.
        /// </summary>
        public string GetCustomFilenamePattern() => _settings.CustomFilenamePattern ?? string.Empty;

        /// <summary>
        /// Sets the custom filename pattern.
        /// </summary>
        public void SetCustomFilenamePattern(string pattern)
        {
            _settings.CustomFilenamePattern = pattern ?? string.Empty;
            Save();
            LoggingService.Log($"[SettingsService] Custom filename pattern set");
        }

        /// <summary>
        /// Gets the list of bandwidth schedules.
        /// </summary>
        public System.Collections.Generic.List<BandwidthSchedule> GetBandwidthSchedules()
        {
            return _settings.BandwidthSchedules ?? new System.Collections.Generic.List<BandwidthSchedule>();
        }

        /// <summary>
        /// Sets the bandwidth schedules.
        /// </summary>
        public void SetBandwidthSchedules(System.Collections.Generic.List<BandwidthSchedule> schedules)
        {
            _settings.BandwidthSchedules = schedules ?? new System.Collections.Generic.List<BandwidthSchedule>();
            Save();
            LoggingService.Log($"[SettingsService] Bandwidth schedules updated: {_settings.BandwidthSchedules.Count} entries");
        }

        /// <summary>
        /// Gets the active bandwidth limit in KB/s, considering both global limit and time-based schedules.
        /// Returns the most restrictive limit (lowest non-zero value) that applies to the current time.
        /// If multiple schedules are active, the most restrictive one is used.
        /// Returns 0 if no limit applies (unlimited bandwidth).
        /// </summary>
        public int GetActiveBandwidthLimitKbps()
        {
            try
            {
                var schedules = GetBandwidthSchedules();
                if (schedules == null || schedules.Count == 0)
                {
                    return GetBandwidthLimitKbps();
                }

                var activeProfile = BandwidthSchedule.GetActiveProfile(schedules, DateTime.Now);
                if (activeProfile != null && activeProfile.SpeedLimitKbps > 0)
                {
                    return activeProfile.SpeedLimitKbps;
                }

                return GetBandwidthLimitKbps();
            }
            catch (Exception ex)
            {
                LoggingService.Log($"[SettingsService] GetActiveBandwidthLimitKbps failed: {ex.Message}");
                // Fallback to global limit on error
                return GetBandwidthLimitKbps();
            }
        }

        // URL Safety & Post-Download File Scanning
        public bool GetEnableUrlSafetyCheck()
        {
            var val = GetSetting("EnableUrlSafetyCheck");
            if (bool.TryParse(val, out var enabled)) return enabled;
            return false;
        }

        public void SetEnableUrlSafetyCheck(bool enable)
        {
            SaveSetting("EnableUrlSafetyCheck", enable.ToString());
        }

        public bool GetEnablePostDownloadScan()
        {
            var val = GetSetting("EnablePostDownloadScan");
            if (bool.TryParse(val, out var enabled)) return enabled;
            return true; // default enabled
        }

        public void SetEnablePostDownloadScan(bool enable)
        {
            SaveSetting("EnablePostDownloadScan", enable.ToString());
        }

        /// <summary>
        /// Gets the Google Safe Browsing API key.
        /// </summary>
        public string GetGoogleSafeBrowsingApiKey() => _settings.GoogleSafeBrowsingApiKey ?? string.Empty;

        /// <summary>
        /// Sets the Google Safe Browsing API key.
        /// </summary>
        public void SetGoogleSafeBrowsingApiKey(string apiKey)
        {
            _settings.GoogleSafeBrowsingApiKey = apiKey ?? string.Empty;
            Save();
            LoggingService.Log($"[SettingsService] Google Safe Browsing API Key {(string.IsNullOrEmpty(apiKey) ? "cleared" : "updated")}");
        }

        // Crash reporting settings accessors
        public bool GetSendAnonymousCrashReports() => _settings.SendAnonymousCrashReports;
        public void SetSendAnonymousCrashReports(bool enable)
        {
            _settings.SendAnonymousCrashReports = enable;
            Save();
            LoggingService.Log($"[SettingsService] SendAnonymousCrashReports set to {(enable ? "true" : "false")}");
        }

        // Clipboard Monitoring Settings Methods
        public bool GetEnableClipboardMonitoring() => _settings.EnableClipboardMonitoring;
        public void SetEnableClipboardMonitoring(bool enable)
        {
            _settings.EnableClipboardMonitoring = enable;
            Save();
            LoggingService.Log($"[SettingsService] EnableClipboardMonitoring set to {enable}");
        }

        public bool GetClipboardMonitorHttp() => _settings.ClipboardMonitorHttp;
        public void SetClipboardMonitorHttp(bool enable)
        {
            _settings.ClipboardMonitorHttp = enable;
            Save();
        }

        public bool GetClipboardMonitorHttps() => _settings.ClipboardMonitorHttps;
        public void SetClipboardMonitorHttps(bool enable)
        {
            _settings.ClipboardMonitorHttps = enable;
            Save();
        }

        public bool GetClipboardMonitorFtp() => _settings.ClipboardMonitorFtp;
        public void SetClipboardMonitorFtp(bool enable)
        {
            _settings.ClipboardMonitorFtp = enable;
            Save();
        }

        public ClipboardAction GetClipboardAction() => _settings.ClipboardAction;
        public void SetClipboardAction(ClipboardAction action)
        {
            _settings.ClipboardAction = action;
            Save();
            LoggingService.Log($"[SettingsService] ClipboardAction set to {action}");
        }

        public bool GetClipboardIgnoreDuplicates() => _settings.ClipboardIgnoreDuplicates;
        public void SetClipboardIgnoreDuplicates(bool enable)
        {
            _settings.ClipboardIgnoreDuplicates = enable;
            Save();
        }

        public bool GetClipboardShowNotification() => _settings.ClipboardShowNotification;
        public void SetClipboardShowNotification(bool enable)
        {
            _settings.ClipboardShowNotification = enable;
            Save();
        }

        // Browser Integration & Capture settings
        public bool GetEnableBrowserIntegration() => _settings.EnableBrowserIntegration;
        public void SetEnableBrowserIntegration(bool enable)
        {
            _settings.EnableBrowserIntegration = enable;
            Save();
            LoggingService.Log($"[SettingsService] EnableBrowserIntegration set to {enable}");
        }

        public bool GetBrowserCaptureDownloads() => _settings.BrowserCaptureDownloads;
        public void SetBrowserCaptureDownloads(bool enable)
        {
            _settings.BrowserCaptureDownloads = enable;
            Save();
        }

        public bool GetBrowserShowConfirmation() => _settings.BrowserShowConfirmation;
        public void SetBrowserShowConfirmation(bool enable)
        {
            _settings.BrowserShowConfirmation = enable;
            Save();
        }

        public bool GetBrowserShowNotification() => _settings.BrowserShowNotification;
        public void SetBrowserShowNotification(bool enable)
        {
            _settings.BrowserShowNotification = enable;
            Save();
        }

        public string GetBrowserDownloadMode() => _settings.BrowserDownloadMode ?? "ShowDialog";
        public void SetBrowserDownloadMode(string mode)
        {
            _settings.BrowserDownloadMode = string.IsNullOrWhiteSpace(mode) ? "ShowDialog" : mode;
            Save();
        }

        public string GetBrowserInterceptedExtensions() => _settings.BrowserInterceptedExtensions ?? "ZIP RAR 7Z TAR GZ ISO EXE MSI APK BIN MP4 MKV MP3 PDF DOCX XLSX PPTX DMG";
        public void SetBrowserInterceptedExtensions(string extensions)
        {
            _settings.BrowserInterceptedExtensions = extensions ?? string.Empty;
            Save();
        }

        /// <summary>
        /// Get a generic setting value by key
        /// </summary>
        public string? GetSetting(string key)
        {
            try
            {
                var settingsDict = _settings.GetType().GetProperties();
                var prop = System.Array.Find(settingsDict, p => p.Name.Equals(key, StringComparison.OrdinalIgnoreCase));
                if (prop != null)
                {
                    var value = prop.GetValue(_settings);
                    return value?.ToString();
                }
                if (_settings.AdditionalSettings != null && _settings.AdditionalSettings.TryGetValue(key, out var val))
                {
                    return val;
                }
                return null;
            }
            catch (Exception ex)
            {
                LoggingService.LogException($"[SettingsService] GetSetting({key}) failed", ex);
                return null;
            }
        }

        /// <summary>
        /// Save a generic setting value by key
        /// </summary>
        public void SaveSetting(string key, string value)
        {
            try
            {
                var prop = _settings.GetType().GetProperty(key, System.Reflection.BindingFlags.IgnoreCase | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
                if (prop != null && prop.CanWrite)
                {
                    if (prop.PropertyType == typeof(string))
                    {
                        prop.SetValue(_settings, value);
                    }
                    else if (prop.PropertyType == typeof(bool) && bool.TryParse(value, out var boolValue))
                    {
                        prop.SetValue(_settings, boolValue);
                    }
                    else if (prop.PropertyType == typeof(int) && int.TryParse(value, out var intValue))
                    {
                        prop.SetValue(_settings, intValue);
                    }
                    Save();
                    LoggingService.Log($"[SettingsService] SaveSetting({key}, {value}) completed");
                }
                else
                {
                    if (_settings.AdditionalSettings == null)
                    {
                        _settings.AdditionalSettings = new System.Collections.Generic.Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    }
                    _settings.AdditionalSettings[key] = value;
                    Save();
                    LoggingService.Log($"[SettingsService] SaveSetting({key}, {value}) stored in AdditionalSettings");
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogException($"[SettingsService] SaveSetting({key}) failed", ex);
            }
        }

        public void SetSetting(string key, string value) => SaveSetting(key, value);

        public bool GetBoolSetting(string key, bool defaultValue = false)
        {
            var str = GetSetting(key);
            return string.IsNullOrEmpty(str) ? defaultValue : (bool.TryParse(str, out var b) ? b : defaultValue);
        }
    }
}
