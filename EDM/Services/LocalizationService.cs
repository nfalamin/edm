using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;

namespace EDM.Services
{
    public class LanguagePack
    {
        public string CultureCode { get; set; } = "en-US";
        public string DisplayName { get; set; } = "English";
        public bool IsRightToLeft { get; set; } = false;
        public Dictionary<string, string> Strings { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Enterprise Dynamic Localization & Language Pack Subsystem.
    /// Supports runtime switching, fallbacks, RTL flags, string interpolation,
    /// 12+ built-in international languages, and community translation pack validation/importing.
    /// </summary>
    public class LocalizationService
    {
        private static readonly Lazy<LocalizationService> _instance = new(() => new LocalizationService());
        public static LocalizationService Instance => _instance.Value;

        private readonly ConcurrentDictionary<string, LanguagePack> _packs = new(StringComparer.OrdinalIgnoreCase);
        private string _currentCulture = "en-US";
        private const string FallbackCulture = "en-US";

        public event Action<string>? LanguageChanged;

        public string CurrentCulture => _currentCulture;
        public bool IsCurrentRtl => _packs.TryGetValue(_currentCulture, out var p) && p.IsRightToLeft;

        public LocalizationService()
        {
            LoadBuiltinLanguagePacks();
        }

        public void SetLanguage(string cultureCode)
        {
            if (_packs.ContainsKey(cultureCode))
            {
                _currentCulture = cultureCode;
                try
                {
                    CultureInfo.CurrentUICulture = new CultureInfo(cultureCode);
                }
                catch { }

                LanguageChanged?.Invoke(_currentCulture);
            }
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

        public bool ValidateLanguagePack(LanguagePack pack, out List<string> missingKeys)
        {
            missingKeys = new List<string>();
            if (pack == null || string.IsNullOrWhiteSpace(pack.CultureCode))
            {
                missingKeys.Add("Missing CultureCode");
                return false;
            }

            if (_packs.TryGetValue(FallbackCulture, out var fallbackPack))
            {
                foreach (var k in fallbackPack.Strings.Keys)
                {
                    if (!pack.Strings.ContainsKey(k))
                    {
                        missingKeys.Add(k);
                    }
                }
            }

            return missingKeys.Count == 0;
        }

        public bool ImportLanguagePack(string jsonContent, out string errorMessage)
        {
            errorMessage = string.Empty;
            try
            {
                var pack = JsonSerializer.Deserialize<LanguagePack>(jsonContent);
                if (pack == null || string.IsNullOrWhiteSpace(pack.CultureCode))
                {
                    errorMessage = "Invalid language pack format: missing CultureCode.";
                    return false;
                }

                _packs[pack.CultureCode] = pack;
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = $"JSON parsing failed: {ex.Message}";
                return false;
            }
        }

        public List<string> GetAvailableLanguages()
        {
            return new List<string>(_packs.Keys);
        }

        public IReadOnlyCollection<LanguagePack> GetAvailableLanguagePacks()
        {
            return new List<LanguagePack>(_packs.Values);
        }

        public LanguagePack? GetLanguagePack(string cultureCode)
        {
            _packs.TryGetValue(cultureCode, out var pack);
            return pack;
        }

        private void LoadBuiltinLanguagePacks()
        {
            // 1. English (en-US)
            AddPack("en-US", "English (United States)", false, new()
            {
                ["App_Title"] = "Enhanced Download Manager (EDM)",
                ["Btn_Download"] = "Download",
                ["Btn_Pause"] = "Pause",
                ["Btn_Resume"] = "Resume",
                ["Btn_Cancel"] = "Cancel",
                ["Status_Connecting"] = "Connecting...",
                ["Status_Downloading"] = "Downloading at {0} MB/s",
                ["Status_Complete"] = "Download completed successfully.",
                ["Status_Error"] = "Download error: {0}",
                ["Menu_AddUrl"] = "Add URL...",
                ["Menu_SiteGrabber"] = "Site Grabber",
                ["Menu_Settings"] = "Settings"
            });

            // 2. Bengali (bn-BD)
            AddPack("bn-BD", "বাংলা (Bengali)", false, new()
            {
                ["App_Title"] = "এনহ্যান্সড ডাউনলোড ম্যানেজার (EDM)",
                ["Btn_Download"] = "ডাউনলোড",
                ["Btn_Pause"] = "বিরতি",
                ["Btn_Resume"] = "চালিয়ে যান",
                ["Btn_Cancel"] = "বাতিল",
                ["Status_Connecting"] = "সংযুক্ত হচ্ছে...",
                ["Status_Downloading"] = "{0} MB/s গতিতে ডাউনলোড হচ্ছে",
                ["Status_Complete"] = "ডাউনলোড সফলভাবে সম্পন্ন হয়েছে।",
                ["Status_Error"] = "ডাউনলোড ত্রুটি: {0}",
                ["Menu_AddUrl"] = "URL যোগ করুন...",
                ["Menu_SiteGrabber"] = "সাইট গ্র্যাবার",
                ["Menu_Settings"] = "সেটিংস"
            });

            // 3. Spanish (es-ES)
            AddPack("es-ES", "Español (Spanish)", false, new()
            {
                ["App_Title"] = "Gestor de Descargas Mejorado (EDM)",
                ["Btn_Download"] = "Descargar",
                ["Btn_Pause"] = "Pausar",
                ["Btn_Resume"] = "Reanudar",
                ["Btn_Cancel"] = "Cancelar",
                ["Status_Connecting"] = "Conectando...",
                ["Status_Downloading"] = "Descargando a {0} MB/s",
                ["Status_Complete"] = "Descarga completada con éxito.",
                ["Status_Error"] = "Error de descarga: {0}",
                ["Menu_AddUrl"] = "Añadir URL...",
                ["Menu_SiteGrabber"] = "Capturador de Sitio",
                ["Menu_Settings"] = "Configuración"
            });

            // 4. French (fr-FR)
            AddPack("fr-FR", "Français (French)", false, new()
            {
                ["App_Title"] = "Gestionnaire de Téléchargement Amélioré (EDM)",
                ["Btn_Download"] = "Télécharger",
                ["Btn_Pause"] = "Pause",
                ["Btn_Resume"] = "Reprendre",
                ["Btn_Cancel"] = "Annuler",
                ["Status_Connecting"] = "Connexion...",
                ["Status_Downloading"] = "Téléchargement à {0} Mo/s",
                ["Status_Complete"] = "Téléchargement terminé avec succès.",
                ["Status_Error"] = "Erreur de téléchargement : {0}",
                ["Menu_AddUrl"] = "Ajouter URL...",
                ["Menu_SiteGrabber"] = "Aspirateur de Site",
                ["Menu_Settings"] = "Paramètres"
            });

            // 5. German (de-DE)
            AddPack("de-DE", "Deutsch (German)", false, new()
            {
                ["App_Title"] = "Erweiterter Download-Manager (EDM)",
                ["Btn_Download"] = "Herunterladen",
                ["Btn_Pause"] = "Pause",
                ["Btn_Resume"] = "Fortsetzen",
                ["Btn_Cancel"] = "Abbrechen",
                ["Status_Connecting"] = "Verbinde...",
                ["Status_Downloading"] = "Herunterladen mit {0} MB/s",
                ["Status_Complete"] = "Download erfolgreich abgeschlossen.",
                ["Status_Error"] = "Download-Fehler: {0}",
                ["Menu_AddUrl"] = "URL hinzufügen...",
                ["Menu_SiteGrabber"] = "Site-Grabber",
                ["Menu_Settings"] = "Einstellungen"
            });

            // 6. Italian (it-IT)
            AddPack("it-IT", "Italiano (Italian)", false, new()
            {
                ["App_Title"] = "Gestore Download Avanzato (EDM)",
                ["Btn_Download"] = "Scarica",
                ["Btn_Pause"] = "Pausa",
                ["Btn_Resume"] = "Riprendi",
                ["Btn_Cancel"] = "Annulla",
                ["Status_Connecting"] = "Connessione in corso...",
                ["Status_Downloading"] = "Download a {0} MB/s",
                ["Status_Complete"] = "Download completato con successo.",
                ["Status_Error"] = "Errore di download: {0}",
                ["Menu_AddUrl"] = "Aggiungi URL...",
                ["Menu_SiteGrabber"] = "Site Grabber",
                ["Menu_Settings"] = "Impostazioni"
            });

            // 7. Portuguese (pt-BR)
            AddPack("pt-BR", "Português (Portuguese)", false, new()
            {
                ["App_Title"] = "Gerenciador de Downloads Aprimorado (EDM)",
                ["Btn_Download"] = "Baixar",
                ["Btn_Pause"] = "Pausar",
                ["Btn_Resume"] = "Continuar",
                ["Btn_Cancel"] = "Cancelar",
                ["Status_Connecting"] = "Conectando...",
                ["Status_Downloading"] = "Baixando a {0} MB/s",
                ["Status_Complete"] = "Download concluído com sucesso.",
                ["Status_Error"] = "Erro no download: {0}",
                ["Menu_AddUrl"] = "Adicionar URL...",
                ["Menu_SiteGrabber"] = "Capturador de Sites",
                ["Menu_Settings"] = "Configurações"
            });

            // 8. Russian (ru-RU)
            AddPack("ru-RU", "Русский (Russian)", false, new()
            {
                ["App_Title"] = "Улучшенный менеджер загрузок (EDM)",
                ["Btn_Download"] = "Скачать",
                ["Btn_Pause"] = "Пауза",
                ["Btn_Resume"] = "Возобновить",
                ["Btn_Cancel"] = "Отмена",
                ["Status_Connecting"] = "Подключение...",
                ["Status_Downloading"] = "Загрузка: {0} МБ/с",
                ["Status_Complete"] = "Загрузка успешно завершена.",
                ["Status_Error"] = "Ошибка загрузки: {0}",
                ["Menu_AddUrl"] = "Добавить URL...",
                ["Menu_SiteGrabber"] = "Граббер сайтов",
                ["Menu_Settings"] = "Настройки"
            });

            // 9. Japanese (ja-JP)
            AddPack("ja-JP", "日本語 (Japanese)", false, new()
            {
                ["App_Title"] = "拡張ダウンロードマネージャー (EDM)",
                ["Btn_Download"] = "ダウンロード",
                ["Btn_Pause"] = "一時停止",
                ["Btn_Resume"] = "再開",
                ["Btn_Cancel"] = "キャンセル",
                ["Status_Connecting"] = "接続中...",
                ["Status_Downloading"] = "{0} MB/s でダウンロード中",
                ["Status_Complete"] = "ダウンロードが正常に完了しました。",
                ["Status_Error"] = "ダウンロードエラー: {0}",
                ["Menu_AddUrl"] = "URLを追加...",
                ["Menu_SiteGrabber"] = "サイトグラバー",
                ["Menu_Settings"] = "設定"
            });

            // 10. Simplified Chinese (zh-CN)
            AddPack("zh-CN", "简体中文 (Simplified Chinese)", false, new()
            {
                ["App_Title"] = "增强型下载管理器 (EDM)",
                ["Btn_Download"] = "下载",
                ["Btn_Pause"] = "暂停",
                ["Btn_Resume"] = "继续",
                ["Btn_Cancel"] = "取消",
                ["Status_Connecting"] = "连接中...",
                ["Status_Downloading"] = "下载中 {0} MB/s",
                ["Status_Complete"] = "下载成功完成。",
                ["Status_Error"] = "下载错误: {0}",
                ["Menu_AddUrl"] = "添加链接...",
                ["Menu_SiteGrabber"] = "站点抓取器",
                ["Menu_Settings"] = "设置"
            });

            // 11. Arabic (ar-SA) [Right-to-Left]
            AddPack("ar-SA", "العربية (Arabic)", true, new()
            {
                ["App_Title"] = "مدير التنزيل المتقدم (EDM)",
                ["Btn_Download"] = "تحميل",
                ["Btn_Pause"] = "إيقاف مؤقت",
                ["Btn_Resume"] = "استئناف",
                ["Btn_Cancel"] = "إلغاء",
                ["Status_Connecting"] = "جارٍ الاتصال...",
                ["Status_Downloading"] = "جارٍ التحميل بسرعة {0} م.ب/ث",
                ["Status_Complete"] = "اكتمل التحميل بنجاح.",
                ["Status_Error"] = "خطأ في التحميل: {0}",
                ["Menu_AddUrl"] = "إضافة رابط...",
                ["Menu_SiteGrabber"] = "ساحب المواقع",
                ["Menu_Settings"] = "الإعدادات"
            });

            // 12. Hindi (hi-IN)
            AddPack("hi-IN", "हिन्दी (Hindi)", false, new()
            {
                ["App_Title"] = "एन्हांस्ड डाउनलोड मैनेजर (EDM)",
                ["Btn_Download"] = "डाउनलोड करें",
                ["Btn_Pause"] = "रोकें",
                ["Btn_Resume"] = "जारी रखें",
                ["Btn_Cancel"] = "रद्द करें",
                ["Status_Connecting"] = "कनेक्ट हो रहा है...",
                ["Status_Downloading"] = "{0} MB/s पर डाउनलोड हो रहा है",
                ["Status_Complete"] = "डाउनलोड सफलतापूर्वक पूरा हुआ।",
                ["Status_Error"] = "डाउनलोड त्रुटि: {0}",
                ["Menu_AddUrl"] = "URL जोड़ें...",
                ["Menu_SiteGrabber"] = "साइट ग्रैबर",
                ["Menu_Settings"] = "सेटिंग्स"
            });

            // 13. Korean (ko-KR)
            AddPack("ko-KR", "한국어 (Korean)", false, new()
            {
                ["App_Title"] = "EDM 다운로드 관리자",
                ["Btn_Download"] = "다운로드",
                ["Btn_Pause"] = "일시 정지",
                ["Btn_Resume"] = "다시 시작",
                ["Btn_Cancel"] = "취소",
                ["Status_Connecting"] = "연결 중...",
                ["Status_Downloading"] = "{0} MB/s 속도로 다운로드 중",
                ["Status_Complete"] = "다운로드가 성공적으로 완료되었습니다.",
                ["Status_Error"] = "다운로드 오류: {0}",
                ["Menu_AddUrl"] = "URL 추가...",
                ["Menu_SiteGrabber"] = "사이트 그래버",
                ["Menu_Settings"] = "설정"
            });
        }

        private void AddPack(string cultureCode, string displayName, bool isRtl, Dictionary<string, string> strings)
        {
            _packs[cultureCode] = new LanguagePack
            {
                CultureCode = cultureCode,
                DisplayName = displayName,
                IsRightToLeft = isRtl,
                Strings = strings
            };
        }
    }
}
