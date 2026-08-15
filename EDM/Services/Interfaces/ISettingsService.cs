using System;
using System.Collections.Generic;
using EDM.Models;

namespace EDM.Services.Interfaces
{
    public interface ISettingsService
    {
        string GetDefaultDownloadPath();
        void SetDefaultDownloadPath(string path);

        List<string> GetCategories();
        void AddCategory(string category);

        string GetFfmpegPath();
        void SetFfmpegPath(string path);

        string GetYtDlpPath();
        void SetYtDlpPath(string path);

        string GetAria2Path();
        void SetAria2Path(string path);

        string GetDefaultFormatArgs();
        void SetDefaultFormatArgs(string args);

        bool GetAutoConvertToMp3();
        void SetAutoConvertToMp3(bool v);

        bool GetSchedulerEnabled();
        TimeSpan? GetSchedulerTime();
        void SetScheduler(bool enabled, TimeSpan? time);

        // Network-related settings
        int GetConnectionLimitOverride();
        bool GetReduceQualityOnMeteredNetworks();
        int GetBandwidthLimitKbps();
        int GetActiveBandwidthLimitKbps();  // Consider schedules
        ProxySettings GetProxySettings();
        void SetProxySettings(ProxySettings settings, string? plainPassword = null);

        // Bandwidth scheduling
        List<BandwidthSchedule> GetBandwidthSchedules();
        void SetBandwidthSchedules(List<BandwidthSchedule> schedules);

        // URL Safety & Post-Download File Scanning
        bool GetEnableUrlSafetyCheck();
        void SetEnableUrlSafetyCheck(bool enable);
        bool GetEnablePostDownloadScan();
        void SetEnablePostDownloadScan(bool enable);
        string GetGoogleSafeBrowsingApiKey();
        void SetGoogleSafeBrowsingApiKey(string apiKey);

        // Crash reporting preferences
        bool GetSendAnonymousCrashReports();
        void SetSendAnonymousCrashReports(bool enable);

        // Generic key-value settings (for theme, general preferences)
        string? GetSetting(string key);
        void SaveSetting(string key, string value);
        void SetSetting(string key, string value);
        bool GetBoolSetting(string key, bool defaultValue = false);
    }
}
