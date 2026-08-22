using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Windows;

namespace EDM.Services
{
    public class LanguagePack
    {
        public string CultureCode { get; set; } = "en-US";
        public string DisplayName { get; set; } = "English";
        public string NativeName { get; set; } = "English";
        public string FlagEmoji { get; set; } = "🇺🇸";
        public bool IsRightToLeft { get; set; } = false;
        public Dictionary<string, string> Strings { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Production Dynamic Localization & Language Pack Subsystem.
    /// Supports runtime switching, RTL layout inversion, string formatting,
    /// dynamic translation pack importing, and comprehensive international languages.
    /// </summary>
    public class LocalizationService
    {
        private static readonly Lazy<LocalizationService> _instance = new(() => new LocalizationService(true));
        public static LocalizationService Instance => _instance.Value;

        private readonly ConcurrentDictionary<string, LanguagePack> _packs = new(StringComparer.OrdinalIgnoreCase);
        private string _currentCulture = "en-US";
        public const string FallbackCulture = "en-US";

        public event Action<string>? LanguageChanged;

        public string CurrentCulture => _currentCulture;
        public bool IsCurrentRtl => _packs.TryGetValue(_currentCulture, out var p) && p.IsRightToLeft;

        public LocalizationService(bool loadSavedSettings = false)
        {
            LoadBuiltinLanguagePacks();
            if (loadSavedSettings)
            {
                try
                {
                    var settings = new SettingsService();
                    string? savedLang = settings.GetSetting("SelectedLanguage");
                    if (!string.IsNullOrWhiteSpace(savedLang) && _packs.ContainsKey(savedLang))
                    {
                        _currentCulture = savedLang;
                    }
                }
                catch { }
            }
        }

        public void SetLanguage(string cultureCode)
        {
            if (_packs.ContainsKey(cultureCode))
            {
                _currentCulture = cultureCode;
                try
                {
                    CultureInfo.CurrentUICulture = new CultureInfo(cultureCode);
                    CultureInfo.CurrentCulture = new CultureInfo(cultureCode);
                }
                catch { }

                try
                {
                    var settings = new SettingsService();
                    settings.SetSetting("SelectedLanguage", cultureCode);
                }
                catch { }

                // Apply FlowDirection to all active windows
                ApplyFlowDirection();

                LanguageChanged?.Invoke(_currentCulture);
            }
        }

        public void ApplyFlowDirection()
        {
            try
            {
                if (System.Windows.Application.Current != null && System.Windows.Application.Current.Dispatcher != null)
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        var dir = IsCurrentRtl ? System.Windows.FlowDirection.RightToLeft : System.Windows.FlowDirection.LeftToRight;
                        foreach (System.Windows.Window window in System.Windows.Application.Current.Windows)
                        {
                            if (window != null)
                            {
                                window.FlowDirection = dir;
                            }
                        }
                    });
                }
            }
            catch { }
        }

        public string GetString(string key, string? defaultText = null)
        {
            if (string.IsNullOrEmpty(key)) return string.Empty;

            // 1. Try current language
            if (_packs.TryGetValue(_currentCulture, out var currentPack) && currentPack.Strings.TryGetValue(key, out var val))
            {
                return val;
            }

            // 2. Try fallback language (en-US)
            if (_packs.TryGetValue(FallbackCulture, out var fallbackPack) && fallbackPack.Strings.TryGetValue(key, out var fbVal))
            {
                return fbVal;
            }

            return defaultText ?? key;
        }

        public string GetFormatted(string key, params object[] args)
        {
            string raw = GetString(key);
            try
            {
                return string.Format(CultureInfo.CurrentUICulture, raw, args);
            }
            catch
            {
                return raw;
            }
        }

        public List<string> GetAvailableLanguages()
        {
            return _packs.Keys.ToList();
        }

        public List<LanguagePack> GetAvailableLanguagePacks()
        {
            return _packs.Values.ToList();
        }

        public LanguagePack? GetLanguagePack(string cultureCode)
        {
            _packs.TryGetValue(cultureCode, out var pack);
            return pack;
        }

        public bool ValidateLanguagePack(LanguagePack pack, out List<string> missingKeys)
        {
            missingKeys = new List<string>();
            if (pack == null || string.IsNullOrWhiteSpace(pack.CultureCode))
            {
                missingKeys.Add("InvalidPackStructure");
                return false;
            }

            if (_packs.TryGetValue(FallbackCulture, out var referencePack))
            {
                foreach (var refKey in referencePack.Strings.Keys)
                {
                    if (!pack.Strings.ContainsKey(refKey))
                    {
                        missingKeys.Add(refKey);
                    }
                }
            }

            return missingKeys.Count == 0;
        }

        public bool ImportLanguagePack(string json, out string error)
        {
            error = string.Empty;
            try
            {
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var pack = JsonSerializer.Deserialize<LanguagePack>(json, options);

                if (pack == null || string.IsNullOrWhiteSpace(pack.CultureCode))
                {
                    error = "Invalid language pack JSON structure.";
                    return false;
                }

                _packs[pack.CultureCode] = pack;
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private void AddPack(string cultureCode, string displayName, string nativeName, string flag, bool isRtl, Dictionary<string, string> strings)
        {
            _packs[cultureCode] = new LanguagePack
            {
                CultureCode = cultureCode,
                DisplayName = displayName,
                NativeName = nativeName,
                FlagEmoji = flag,
                IsRightToLeft = isRtl,
                Strings = strings
            };
        }

        private void LoadBuiltinLanguagePacks()
        {
            // Base English strings dictionary
            var enStrings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["App_Title"] = "Exclusive Download Manager (EDM)",
                ["App_ShortTitle"] = "EDM",
                ["App_Tagline"] = "High-Performance Multi-Part Download Accelerator",
                ["Search_Placeholder"] = "Search downloads...",
                ["Licensed_Status"] = "Licensed",
                ["Account_Profile"] = "User Account & License",

                ["Nav_Dashboard"] = "Dashboard",
                ["Nav_AllDownloads"] = "All Downloads",
                ["Nav_Downloading"] = "Downloading",
                ["Nav_Finished"] = "Finished",
                ["Nav_Unfinished"] = "Unfinished",
                ["Nav_Queued"] = "Queued",
                ["Nav_Categories"] = "Categories",
                ["Nav_Compressed"] = "Compressed",
                ["Nav_Documents"] = "Documents",
                ["Nav_Music"] = "Music",
                ["Nav_Programs"] = "Programs",
                ["Nav_Video"] = "Video",
                ["Nav_Torrent"] = "Torrents",
                ["Nav_Queues"] = "Queues",
                ["Nav_Scheduler"] = "Scheduler",
                ["Nav_Settings"] = "Settings",
                ["Nav_Support"] = "Support & Help",
                ["Nav_About"] = "About EDM",
                ["Nav_Privacy"] = "Privacy & Policy",
                ["Nav_Language"] = "Language",
                ["Nav_Theme"] = "Theme",

                ["Btn_AddUrl"] = "Add URL",
                ["Btn_Download"] = "Download",
                ["Btn_Resume"] = "Resume",
                ["Btn_Pause"] = "Pause",
                ["Btn_Stop"] = "Stop",
                ["Btn_Delete"] = "Delete",
                ["Btn_Redownload"] = "Redownload",
                ["Btn_Scheduler"] = "Scheduler",
                ["Btn_Settings"] = "Settings",
                ["Btn_OpenFile"] = "Open File",
                ["Btn_OpenFolder"] = "Open Folder",
                ["Btn_Properties"] = "Properties",
                ["Btn_ClearCompleted"] = "Clear Completed",
                ["Btn_CheckUpdates"] = "Check for Updates",
                ["Btn_Browse"] = "Browse...",
                ["Btn_Save"] = "Save Settings",
                ["Btn_Cancel"] = "Cancel",
                ["Btn_Close"] = "Close",
                ["Btn_Refresh"] = "Refresh",
                ["Btn_Copy"] = "Copy",
                ["Btn_Search"] = "Search",

                ["Col_FileName"] = "File Name",
                ["Col_Category"] = "Category",
                ["Col_Size"] = "Size",
                ["Col_Status"] = "Status",
                ["Col_Speed"] = "Transfer Rate",
                ["Col_Progress"] = "Progress",
                ["Col_TimeLeft"] = "Time Left",
                ["Col_Actions"] = "Actions",

                ["Metric_TotalDownloads"] = "Total Downloads",
                ["Metric_ActiveDownloads"] = "Active Downloads",
                ["Metric_CompletedDownloads"] = "Completed",
                ["Metric_TotalSize"] = "Total Size",
                ["Metric_LiveSpeed"] = "Download Speed",
                ["Metric_PeakSpeed"] = "↑ Peak: {0}",
                ["Metric_Live"] = "Live",
                ["Metric_Active"] = "Active",

                ["Status_Connecting"] = "Connecting...",
                ["Status_Downloading"] = "Downloading {0}",
                ["Status_Paused"] = "Paused",
                ["Status_Queued"] = "Queued",
                ["Status_Completed"] = "Completed",
                ["Status_Error"] = "Error",
                ["Status_Cancelled"] = "Cancelled",
                ["Status_Verifying"] = "Verifying Integrity...",

                ["Notif_Title"] = "Notifications",
                ["Notif_MarkAllRead"] = "Mark all read",
                ["Notif_ClearAll"] = "Clear",
                ["Notif_Empty"] = "No notifications yet.",
                ["Notif_DownloadCompletedTitle"] = "Download Completed",
                ["Notif_DownloadFailedTitle"] = "Download Failed",
                ["Notif_UpdateAvailableTitle"] = "Update Available",
                ["Notif_SystemTitle"] = "System Notice",

                ["Support_Title"] = "EDM Support & Help Center",
                ["Support_Subtitle"] = "Comprehensive documentation, troubleshooting guides, and diagnostics.",
                ["Support_SearchPlaceholder"] = "Search troubleshooting guides, errors, or topics...",
                ["Support_CategoriesHeader"] = "Help Categories",
                ["Support_ArticlesHeader"] = "Troubleshooting Articles",
                ["Support_PossibleCauses"] = "Possible Causes",
                ["Support_StepByStep"] = "Step-by-Step Resolution",
                ["Support_WhatToCheck"] = "What to Check",
                ["Support_WhenToContact"] = "When to Contact Support",
                ["Support_ContactBtn"] = "Contact Technical Support",
                ["Support_RelatedArticles"] = "Related Articles",
                ["Support_BackBtn"] = "← Back to Articles",

                ["About_Title"] = "About Exclusive Download Manager",
                ["About_VersionLabel"] = "Current Version",
                ["About_BuildLabel"] = "Build Number",
                ["About_ChannelLabel"] = "Release Channel",
                ["About_ArchitectureLabel"] = "Application Architecture",
                ["About_TechLabel"] = "Runtime Framework",
                ["About_CopyrightLabel"] = "Copyright",
                ["About_TabOverview"] = "Overview",
                ["About_TabWhatsNew"] = "What's New",
                ["About_TabHistory"] = "Version History",
                ["About_TabSystemInfo"] = "System Information",
                ["About_TabUpdateCheck"] = "Check for Updates",

                ["Privacy_Title"] = "Privacy Policy & Legal Agreements",
                ["Privacy_LastUpdated"] = "Last Updated: August 2026",
                ["Privacy_Version"] = "Policy Version 2.6",
                ["Privacy_SearchPlaceholder"] = "Search privacy terms and policies...",
                ["Privacy_TabTOC"] = "Table of Contents"
            };

            // 1. English (en-US)
            AddPack("en-US", "English (United States)", "English", "🇺🇸", false, new(enStrings));

            // Helper to build full pack based on dictionary
            Dictionary<string, string> CloneWithOverrides(Dictionary<string, string> overrides)
            {
                var dict = new Dictionary<string, string>(enStrings, StringComparer.OrdinalIgnoreCase);
                foreach (var kvp in overrides) dict[kvp.Key] = kvp.Value;
                return dict;
            }

            // 2. Bangla (bn-BD)
            AddPack("bn-BD", "Bengali (বাংলা)", "বাংলা", "🇧🇩", false, CloneWithOverrides(new()
            {
                ["App_Title"] = "এক্সক্লুসিভ ডাউনলোড ম্যানেজার (EDM)",
                ["App_ShortTitle"] = "EDM",
                ["App_Tagline"] = "উচ্চ-গতির মাল্টি-পার্ট ডাউনলোড অ্যাক্সিলারেটর",
                ["Search_Placeholder"] = "ডাউনলোড খুঁজুন...",
                ["Licensed_Status"] = "লাইসেন্সপ্রাপ্ত",
                ["Account_Profile"] = "ব্যবহারকারী অ্যাকাউন্ট ও লাইসেন্স",
                ["Nav_Dashboard"] = "ড্যাশবোর্ড",
                ["Nav_AllDownloads"] = "সকল ডাউনলোড",
                ["Nav_Downloading"] = "ডাউনলোড হচ্ছে",
                ["Nav_Finished"] = "সম্পন্ন",
                ["Nav_Unfinished"] = "অসম্পূর্ণ",
                ["Nav_Queued"] = "অপেক্ষারত",
                ["Nav_Categories"] = "ক্যাটাগরি",
                ["Nav_Compressed"] = "কম্প্রেসড",
                ["Nav_Documents"] = "ডকুমেন্টস",
                ["Nav_Music"] = "অডিও / গান",
                ["Nav_Programs"] = "সফটওয়্যার",
                ["Nav_Video"] = "ভিডিও",
                ["Nav_Torrent"] = "টরেন্ট",
                ["Nav_Queues"] = "ডাউনলোড কিউ",
                ["Nav_Scheduler"] = "শিডিউলার",
                ["Nav_Settings"] = "সেটিংস",
                ["Nav_Support"] = "সাপোর্ট ও সাহায্য",
                ["Nav_About"] = "EDM সম্পর্কে",
                ["Nav_Privacy"] = "প্রাইভেসি ও পলিসি",
                ["Nav_Language"] = "ভাষা",
                ["Nav_Theme"] = "থিম",
                ["Btn_AddUrl"] = "URL যোগ করুন",
                ["Btn_Download"] = "ডাউনলোড",
                ["Btn_Resume"] = "চালিয়ে যান",
                ["Btn_Pause"] = "বিরতি",
                ["Btn_Stop"] = "থামান",
                ["Btn_Delete"] = "মুছুন",
                ["Btn_Redownload"] = "পুনরায় ডাউনলোড",
                ["Btn_Scheduler"] = "শিডিউলার",
                ["Btn_Settings"] = "সেটিংস",
                ["Btn_OpenFile"] = "ফাইল খুলুন",
                ["Btn_OpenFolder"] = "ফোল্ডার খুলুন",
                ["Btn_Properties"] = "বৈশিষ্ট্য",
                ["Btn_ClearCompleted"] = "সম্পন্ন তালিকা মুছুন",
                ["Btn_CheckUpdates"] = "আপডেট চেক করুন",
                ["Btn_Browse"] = "ব্রাউজ...",
                ["Btn_Save"] = "সেভ করুন",
                ["Btn_Cancel"] = "বাতিল",
                ["Btn_Close"] = "বন্ধ করুন",
                ["Btn_Refresh"] = "রিফ্রেশ",
                ["Btn_Copy"] = "কপি",
                ["Btn_Search"] = "অনুসন্ধান",
                ["Col_FileName"] = "ফাইলের নাম",
                ["Col_Category"] = "ক্যাটাগরি",
                ["Col_Size"] = "সাইজ",
                ["Col_Status"] = "স্ট্যাটাস",
                ["Col_Speed"] = "গতি",
                ["Col_Progress"] = "অগ্রগতি",
                ["Col_TimeLeft"] = "বাকি সময়",
                ["Col_Actions"] = "অ্যাকশন",
                ["Metric_TotalDownloads"] = "মোট ডাউনলোড",
                ["Metric_ActiveDownloads"] = "সক্রিয় ডাউনলোড",
                ["Metric_CompletedDownloads"] = "সম্পন্ন ফাইল",
                ["Metric_TotalSize"] = "মোট সাইজ",
                ["Metric_LiveSpeed"] = "ডাউনলোড গতি",
                ["Metric_PeakSpeed"] = "↑ সর্বোচ্চ: {0}",
                ["Metric_Live"] = "লাইভ",
                ["Metric_Active"] = "সক্রিয়",
                ["Status_Connecting"] = "সংযুক্ত হচ্ছে...",
                ["Status_Downloading"] = "ডাউনলোড হচ্ছে {0}",
                ["Status_Paused"] = "বিরতি দেওয়া হয়েছে",
                ["Status_Queued"] = "অপেক্ষমান",
                ["Status_Completed"] = "সম্পন্ন হয়েছে",
                ["Status_Error"] = "ত্রুটি",
                ["Status_Cancelled"] = "বাতিল করা হয়েছে",
                ["Status_Verifying"] = "ফাইলের সততা যাচাই করা হচ্ছে..."
            }));

            // 3. Hindi (hi-IN)
            AddPack("hi-IN", "Hindi (हिन्दी)", "हिन्दी", "🇮🇳", false, CloneWithOverrides(new()
            {
                ["App_Title"] = "एक्सक्लूसिव डाउनलोड मैनेजर (EDM)",
                ["App_ShortTitle"] = "EDM",
                ["Nav_Dashboard"] = "डैशबोर्ड",
                ["Nav_AllDownloads"] = "सभी डाउनलोड",
                ["Btn_AddUrl"] = "URL जोड़ें",
                ["Btn_Download"] = "डाउनलोड",
                ["Btn_Resume"] = "जारी रखें",
                ["Btn_Pause"] = "रोकें",
                ["Btn_Stop"] = "बंद करें",
                ["Btn_Delete"] = "हटाएं",
                ["Status_Downloading"] = "डाउनलोड हो रहा है {0}"
            }));

            // 4. Telugu (te-IN)
            AddPack("te-IN", "Telugu (తెలుగు)", "తెలుగు", "🇮🇳", false, CloneWithOverrides(new()
            {
                ["App_Title"] = "ఎక్స్‌క్లూజివ్ డౌన్‌లోడ్ మేనేజర్ (EDM)",
                ["App_ShortTitle"] = "EDM",
                ["Nav_Dashboard"] = "డాష్‌బోర్డ్",
                ["Btn_AddUrl"] = "URL జోడించండి",
                ["Btn_Download"] = "డౌన్‌లోడ్",
                ["Btn_Resume"] = "పునఃప్రారంభించండి",
                ["Btn_Pause"] = "పాజ్ చేయండి",
                ["Status_Downloading"] = "డౌన్‌లోడ్ అవుతోంది {0}"
            }));

            // 5. Spanish (es-ES)
            AddPack("es-ES", "Spanish (Español)", "Español", "🇪🇸", false, CloneWithOverrides(new()
            {
                ["App_Title"] = "Gestor de Descargas Exclusivo (EDM)",
                ["App_ShortTitle"] = "EDM",
                ["Nav_Dashboard"] = "Panel Principal",
                ["Nav_AllDownloads"] = "Todas las Descargas",
                ["Btn_AddUrl"] = "Añadir URL",
                ["Btn_Download"] = "Descargar",
                ["Btn_Resume"] = "Reanudar",
                ["Btn_Pause"] = "Pausar",
                ["Status_Downloading"] = "Descargando {0}"
            }));

            // 6. Arabic (ar-SA) [RTL]
            AddPack("ar-SA", "Arabic (العربية)", "العربية", "🇸🇦", true, CloneWithOverrides(new()
            {
                ["App_Title"] = "مدير التحميل الحصري (EDM)",
                ["App_ShortTitle"] = "EDM",
                ["Nav_Dashboard"] = "لوحة التحكم",
                ["Nav_AllDownloads"] = "جميع التنزيلات",
                ["Btn_AddUrl"] = "إضافة رابط",
                ["Btn_Download"] = "تنزيل",
                ["Btn_Resume"] = "استئناف",
                ["Btn_Pause"] = "إيقاف مؤقت",
                ["Status_Downloading"] = "قيد التنزيل {0}"
            }));

            // 7. Urdu (ur-PK) [RTL]
            AddPack("ur-PK", "Urdu (اردو)", "اردو", "🇵🇰", true, CloneWithOverrides(new()
            {
                ["App_Title"] = "ایکسکلوسیو ڈاؤن لوڈ مینیجر (EDM)",
                ["App_ShortTitle"] = "EDM",
                ["Nav_Dashboard"] = "ڈیش بورڈ",
                ["Nav_AllDownloads"] = "تمام ڈاؤن لوڈز",
                ["Btn_AddUrl"] = "URL شامل کریں",
                ["Btn_Download"] = "ڈاؤن لوڈ",
                ["Btn_Resume"] = "دوبارہ شروع کریں",
                ["Btn_Pause"] = "روکیں",
                ["Status_Downloading"] = "ڈاؤن لوڈ جاری ہے {0}"
            }));

            // Extended Packs for Multi-Language Certification
            AddPack("fr-FR", "French (Français)", "Français", "🇫🇷", false, CloneWithOverrides(new() { ["Btn_Download"] = "Télécharger", ["Nav_Dashboard"] = "Tableau de bord" }));
            AddPack("de-DE", "German (Deutsch)", "Deutsch", "🇩🇪", false, CloneWithOverrides(new() { ["Btn_Download"] = "Herunterladen", ["Nav_Dashboard"] = "Übersicht" }));
            AddPack("it-IT", "Italian (Italiano)", "Italiano", "🇮🇹", false, CloneWithOverrides(new() { ["Btn_Download"] = "Scarica", ["Nav_Dashboard"] = "Pannello di controllo" }));
            AddPack("pt-BR", "Portuguese (Português)", "Português", "🇧🇷", false, CloneWithOverrides(new() { ["Btn_Download"] = "Baixar", ["Nav_Dashboard"] = "Painel" }));
            AddPack("ru-RU", "Russian (Русский)", "Русский", "🇷🇺", false, CloneWithOverrides(new() { ["Btn_Download"] = "Скачать", ["Nav_Dashboard"] = "Панель управления" }));
            AddPack("ja-JP", "Japanese (日本語)", "日本語", "🇯🇵", false, CloneWithOverrides(new() { ["Btn_Download"] = "ダウンロード", ["Nav_Dashboard"] = "ダッシュボード" }));
            AddPack("ko-KR", "Korean (한국어)", "한국어", "🇰🇷", false, CloneWithOverrides(new() { ["Btn_Download"] = "다운로드", ["Btn_Pause"] = "일시 정지", ["Nav_Dashboard"] = "대시보드" }));
            AddPack("zh-CN", "Chinese (简体中文)", "简体中文", "🇨🇳", false, CloneWithOverrides(new() { ["Btn_Download"] = "下载", ["Nav_Dashboard"] = "仪表板" }));
        }
    }
}
