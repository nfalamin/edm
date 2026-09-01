using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace EDM.Services
{
    /// <summary>
    /// Online-only Auto Country Language Detect + Translate Service.
    /// Uses ip-api.com for geolocation (no API key required).
    /// Uses Google Translate unofficial endpoint for dynamic translation.
    /// No translation data is ever stored on disk.
    /// </summary>
    public class AutoTranslateService
    {
        private static readonly Lazy<AutoTranslateService> _instance = new(() => new AutoTranslateService());
        public static AutoTranslateService Instance => _instance.Value;

        private static readonly HttpClient _http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };

        // Map ISO-3166-1 alpha-2 country codes to BCP-47 language codes (built-in packs)
        private static readonly Dictionary<string, string> _countryToBuiltin = new(StringComparer.OrdinalIgnoreCase)
        {
            ["BD"] = "bn-BD", ["IN"] = "hi-IN", ["SA"] = "ar-SA", ["AE"] = "ar-SA",
            ["EG"] = "ar-SA", ["PK"] = "ur-PK", ["ES"] = "es-ES", ["MX"] = "es-ES",
            ["AR"] = "es-ES", ["FR"] = "fr-FR", ["DE"] = "de-DE", ["AT"] = "de-DE",
            ["IT"] = "it-IT", ["BR"] = "pt-BR", ["PT"] = "pt-BR", ["RU"] = "ru-RU",
            ["JP"] = "ja-JP", ["KR"] = "ko-KR", ["CN"] = "zh-CN", ["TW"] = "zh-CN",
            ["US"] = "en-US", ["GB"] = "en-US", ["AU"] = "en-US", ["CA"] = "en-US",
            ["NZ"] = "en-US",
        };

        // Countries that need Google Translate (no built-in pack)
        private static readonly Dictionary<string, string> _countryToGoogleLang = new(StringComparer.OrdinalIgnoreCase)
        {
            ["TH"] = "th", ["VN"] = "vi", ["ID"] = "id", ["MY"] = "ms",
            ["TR"] = "tr", ["PL"] = "pl", ["NL"] = "nl", ["SE"] = "sv",
            ["NO"] = "no", ["DK"] = "da", ["FI"] = "fi", ["UA"] = "uk",
            ["IL"] = "he", ["GR"] = "el", ["HU"] = "hu", ["CZ"] = "cs",
            ["RO"] = "ro", ["BG"] = "bg", ["HR"] = "hr", ["SK"] = "sk",
            ["NP"] = "ne", ["LK"] = "si", ["MM"] = "my", ["KH"] = "km",
            ["MN"] = "mn", ["ET"] = "am", ["KE"] = "sw", ["NG"] = "yo",
            ["ZA"] = "zu", ["AF"] = "ps", ["IR"] = "fa",
        };

        private AutoTranslateService() { }

        /// <summary>
        /// Detects user country via IP and applies best matching language.
        /// Returns (success, countryName, languageCode, errorMessage).
        /// </summary>
        public async Task<(bool Success, string CountryName, string LanguageCode, string ErrorMessage)>
            DetectAndApplyAsync(CancellationToken ct = default)
        {
            try
            {
                var (country, countryCode) = await GetCountryFromIpAsync(ct).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(countryCode))
                    return (false, "", "en-US", "ইন্টারনেট সংযোগ পাওয়া যায়নি। ইংরেজিতে রাখা হয়েছে।");

                // Built-in pack exists
                if (_countryToBuiltin.TryGetValue(countryCode, out var builtinCode))
                {
                    LocalizationService.Instance.SetLanguage(builtinCode);
                    return (true, country, builtinCode, string.Empty);
                }

                // Try online translation
                if (_countryToGoogleLang.TryGetValue(countryCode, out var googleLang))
                {
                    var pack = await BuildTranslatedPackAsync(googleLang, countryCode, country, ct).ConfigureAwait(false);
                    if (pack != null)
                    {
                        LocalizationService.Instance.ImportLanguagePack(
                            System.Text.Json.JsonSerializer.Serialize(pack), out _);
                        LocalizationService.Instance.SetLanguage(pack.CultureCode);
                        return (true, country, pack.CultureCode, string.Empty);
                    }
                }

                // Fallback
                LocalizationService.Instance.SetLanguage("en-US");
                return (true, country, "en-US",
                    $"আপনার দেশ ({country}) এর ভাষা সাপোর্টে নেই। ইংরেজিতে রাখা হয়েছে।");
            }
            catch (TaskCanceledException)
            {
                return (false, "", "en-US", "সময় শেষ হয়ে গেছে। ইন্টারনেট পরীক্ষা করুন।");
            }
            catch (Exception ex)
            {
                LoggingService.Log($"[AutoTranslate] Failed: {ex.Message}");
                return (false, "", "en-US", "ভাষা শনাক্ত করতে সমস্যা হয়েছে।");
            }
        }

        private static async Task<(string Country, string CountryCode)> GetCountryFromIpAsync(CancellationToken ct)
        {
            try
            {
                string json = await _http.GetStringAsync("http://ip-api.com/json/?fields=country,countryCode", ct)
                                         .ConfigureAwait(false);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                string country = root.TryGetProperty("country", out var c) ? c.GetString() ?? "" : "";
                string code = root.TryGetProperty("countryCode", out var cc) ? cc.GetString() ?? "" : "";
                return (country, code);
            }
            catch { return (string.Empty, string.Empty); }
        }

        private static async Task<LanguagePack?> BuildTranslatedPackAsync(
            string googleLang, string countryCode, string countryName, CancellationToken ct)
        {
            var enPack = LocalizationService.Instance.GetLanguagePack("en-US");
            if (enPack == null) return null;

            var translated = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var keys = new List<string>(enPack.Strings.Keys);
            const int batchSize = 8;

            for (int i = 0; i < keys.Count; i += batchSize)
            {
                if (ct.IsCancellationRequested) return null;
                var batch = keys.GetRange(i, Math.Min(batchSize, keys.Count - i));
                string combined = string.Join(" ||| ", batch.ConvertAll(k => enPack.Strings[k]));
                string result = await TranslateAsync(combined, googleLang, ct).ConfigureAwait(false);

                if (string.IsNullOrWhiteSpace(result))
                {
                    foreach (var k in batch) translated[k] = enPack.Strings[k];
                    continue;
                }

                var parts = result.Split(new[] { "|||" }, StringSplitOptions.None);
                for (int j = 0; j < batch.Count; j++)
                    translated[batch[j]] = j < parts.Length ? parts[j].Trim() : enPack.Strings[batch[j]];

                await Task.Delay(100, ct).ConfigureAwait(false);
            }

            bool isRtl = googleLang is "he" or "ar" or "ur" or "fa" or "ps";
            return new LanguagePack
            {
                CultureCode = $"{googleLang}-{countryCode}",
                DisplayName = countryName,
                NativeName = countryName,
                FlagEmoji = GetFlagEmoji(countryCode),
                IsRightToLeft = isRtl,
                Strings = translated
            };
        }

        private static async Task<string> TranslateAsync(string text, string targetLang, CancellationToken ct)
        {
            try
            {
                string encoded = HttpUtility.UrlEncode(text);
                string url = $"https://translate.googleapis.com/translate_a/single?client=gtx&sl=en&tl={targetLang}&dt=t&q={encoded}";

                using var req = new HttpRequestMessage(HttpMethod.Get, url);
                req.Headers.UserAgent.TryParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64)");
                using var resp = await _http.SendAsync(req, ct).ConfigureAwait(false);
                string body = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

                using var doc = JsonDocument.Parse(body);
                var sb = new System.Text.StringBuilder();
                foreach (var seg in doc.RootElement[0].EnumerateArray())
                {
                    if (seg.ValueKind == JsonValueKind.Array && seg.GetArrayLength() > 0)
                    {
                        var part = seg[0].GetString();
                        if (!string.IsNullOrEmpty(part)) sb.Append(part);
                    }
                }
                return sb.ToString();
            }
            catch { return string.Empty; }
        }

        private static string GetFlagEmoji(string code)
        {
            if (string.IsNullOrWhiteSpace(code) || code.Length != 2) return "🌍";
            int off = 0x1F1E6 - 'A';
            return char.ConvertFromUtf32(off + char.ToUpperInvariant(code[0])) +
                   char.ConvertFromUtf32(off + char.ToUpperInvariant(code[1]));
        }
    }
}
