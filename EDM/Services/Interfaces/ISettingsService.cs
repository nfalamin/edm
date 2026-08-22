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

        // Clipboard Monitoring Settings
        bool GetEnableClipboardMonitoring();
        void SetEnableClipboardMonitoring(bool enable);
        bool GetClipboardMonitorHttp();
        void SetClipboardMonitorHttp(bool enable);
        bool GetClipboardMonitorHttps();
        void SetClipboardMonitorHttps(bool enable);
        bool GetClipboardMonitorFtp();
        void SetClipboardMonitorFtp(bool enable);
        ClipboardAction GetClipboardAction();
        void SetClipboardAction(ClipboardAction action);
        bool GetClipboardIgnoreDuplicates();
        void SetClipboardIgnoreDuplicates(bool enable);
        bool GetClipboardShowNotification();
        void SetClipboardShowNotification(bool enable);

        // Browser Integration & Capture Settings
        bool GetEnableBrowserIntegration();
        void SetEnableBrowserIntegration(bool enable);
        bool GetBrowserCaptureDownloads();
        void SetBrowserCaptureDownloads(bool enable);
        bool GetBrowserShowConfirmation();
        void SetBrowserShowConfirmation(bool enable);
        bool GetBrowserShowNotification();
        void SetBrowserShowNotification(bool enable);

        // Generic key-value settings (for theme, general preferences)
        string? GetSetting(string key);
        void SaveSetting(string key, string value);
        void SetSetting(string key, string value);
        bool GetBoolSetting(string key, bool defaultValue = false);
    }
}
