using System;
using System.IO;
using System.Text.Json;
using Microsoft.Win32;

namespace EDM.Services
{
    /// <summary>
    /// Universal Browser Extension Native Messaging Host Installer.
    /// Configures compliant manifests and registry entries for Chrome, Edge, Firefox, Brave, Opera, and Vivaldi.
    /// </summary>
    public static class BrowserExtensionInstaller
    {
        public const string NativeHostName = "com.edm.downloader";
        public const string ChromeExtensionId = "knldjmfmopnpolahpmmgbagdohdnhkda";
        public const string FirefoxExtensionId = "edm@exclusive-download-manager.com";
        public const string LegacyFirefoxExtensionId = "edm-extension@edm.app";

        public static string ResolveNativeHostExecutable()
        {
            string appDir = AppDomain.CurrentDomain.BaseDirectory;
            string nativeHostExe = Path.Combine(appDir, "EDM.NativeHost.exe");
            if (File.Exists(nativeHostExe)) return Path.GetFullPath(nativeHostExe);

            string edmExe = Path.Combine(appDir, "EDM.exe");
            if (File.Exists(edmExe)) return Path.GetFullPath(edmExe);

            return Path.GetFullPath(nativeHostExe);
        }

        public static bool InstallAllBrowsersIntegration()
        {
            try
            {
                string exePath = ResolveNativeHostExecutable();
                string nativeHostDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EDM", "NativeHost");
                Directory.CreateDirectory(nativeHostDir);

                string chromiumManifestPath = Path.Combine(nativeHostDir, $"{NativeHostName}.json");
                string firefoxManifestPath = Path.Combine(nativeHostDir, $"{NativeHostName}-firefox.json");

                // 1. Write Chromium Native Messaging Host Manifest
                string chromiumJson = GenerateChromiumManifestJson(exePath);
                File.WriteAllText(chromiumManifestPath, chromiumJson);
                LoggingService.Log($"[BrowserExtensionInstaller] Chromium manifest created at {chromiumManifestPath}");

                // 2. Write Firefox Native Messaging Host Manifest
                string firefoxJson = GenerateFirefoxManifestJson(exePath);
                File.WriteAllText(firefoxManifestPath, firefoxJson);
                LoggingService.Log($"[BrowserExtensionInstaller] Firefox manifest created at {firefoxManifestPath}");

                // 3. Register Chromium Browsers
                RegisterChromiumBrowser("Google Chrome", @"Software\Google\Chrome\NativeMessagingHosts", chromiumManifestPath);
                RegisterChromiumBrowser("Microsoft Edge", @"Software\Microsoft\Edge\NativeMessagingHosts", chromiumManifestPath);
                RegisterChromiumBrowser("Brave", @"Software\BraveSoftware\Brave-Browser\NativeMessagingHosts", chromiumManifestPath);
                RegisterChromiumBrowser("Opera", @"Software\Opera Software\NativeMessagingHosts", chromiumManifestPath);
                RegisterChromiumBrowser("Vivaldi", @"Software\Vivaldi\NativeMessagingHosts", chromiumManifestPath);

                // 4. Register Mozilla Firefox
                RegisterFirefox(firefoxManifestPath);

                LoggingService.Log("[BrowserExtensionInstaller] All browsers Native Messaging registered successfully.");
                return true;
            }
            catch (Exception ex)
            {
                LoggingService.LogException("[BrowserExtensionInstaller] Failed to register browser extensions", ex);
                return false;
            }
        }

        public static string GenerateChromiumManifestJson(string exePath)
        {
            if (string.IsNullOrWhiteSpace(exePath)) throw new ArgumentNullException(nameof(exePath));
            exePath = Path.GetFullPath(exePath);

            var manifestObj = new
            {
                name = NativeHostName,
                description = "Exclusive Download Manager Native Host Agent",
                path = exePath,
                type = "stdio",
                allowed_origins = new[]
                {
                    "chrome-extension://fgnkgamjcmfccjmkifdhipjgnagfgioe/",
                    $"chrome-extension://{ChromeExtensionId}/",
                    "chrome-extension://knldjmfmopnpolahpmmgbagdohdnhkda/",
                    "chrome-extension://lhfkofephegnnhpcfkffnflfobafpaoe/",
                    "chrome-extension://pjnefijmagpdjfhhkpljicbbpicelgko/",
                    "chrome-extension://agionbommeaifngbhincahgmoflcikhm/",
                    "chrome-extension://aapbdbdomjkkjkaonfhkkikfgjllcleb/",
                    "chrome-extension://eppiocemhmnlbhjplcgkofciiegomcon/",
                    "chrome-extension://aicmkgpgakddgnaphhhpliifpcfhicfo/",
                    "chrome-extension://ghbmnnjooekpmoecnnnilnnbdlolhkhi/",
                    "chrome-extension://ngpampappnmepgilojfohadhhmbhlaek/",
                    "chrome-extension://joalfcmoabjccbphlngocfcpkglmalkj/",
                    "chrome-extension://omfoimoadhlddiepbagphpoccblokgem/",
                    "chrome-extension://nmmhkkegccagdldgiimedpiccmgmieda/",
                    "chrome-extension://bcmmjkglicliekcndffbfgcfopnidllp/",
                    "chrome-extension://caidcmannjgahlnbpmidmiecjcoiiigg/",
                    "chrome-extension://aohghmighlieiainnegkcijnfilokake/",
                    "chrome-extension://aapocclcgogkmnckokdopfmhonfmgoek/",
                    "chrome-extension://felcaaldnbdncclmgdcncolpebgiejap/",
                    "chrome-extension://apdfllckaahabafndbhieahigkjlhalf/",
                    "chrome-extension://pjkljhegncpnkpknbcohdijeoejaedia/",
                    "chrome-extension://blpcfgokakmgnkcojhhkbfbldkacnbeo/"
                }
            };

            return JsonSerializer.Serialize(manifestObj, new JsonSerializerOptions { WriteIndented = true });
        }

        public static string GenerateFirefoxManifestJson(string exePath)
        {
            if (string.IsNullOrWhiteSpace(exePath)) throw new ArgumentNullException(nameof(exePath));
            exePath = Path.GetFullPath(exePath);

            var manifestObj = new
            {
                name = NativeHostName,
                description = "Exclusive Download Manager Native Host Agent",
                path = exePath,
                type = "stdio",
                allowed_extensions = new[]
                {
                    FirefoxExtensionId,
                    LegacyFirefoxExtensionId
                }
            };

            return JsonSerializer.Serialize(manifestObj, new JsonSerializerOptions { WriteIndented = true });
        }

        public static string GenerateManifestJson(string exePath) => GenerateChromiumManifestJson(exePath);

        private static void RegisterChromiumBrowser(string browserName, string subKeyPath, string manifestPath)
        {
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey($@"{subKeyPath}\{NativeHostName}");
                key?.SetValue("", manifestPath);
                LoggingService.Log($"[BrowserExtensionInstaller] Registered for {browserName}");
            }
            catch (Exception ex)
            {
                LoggingService.Log($"[BrowserExtensionInstaller] Error registering {browserName}: {ex.Message}");
            }
        }

        private static void RegisterFirefox(string manifestPath)
        {
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey($@"Software\Mozilla\NativeMessagingHosts\{NativeHostName}");
                key?.SetValue("", manifestPath);
                LoggingService.Log("[BrowserExtensionInstaller] Registered for Mozilla Firefox");
            }
            catch (Exception ex)
            {
                LoggingService.Log($"[BrowserExtensionInstaller] Error registering Firefox: {ex.Message}");
            }
        }

        public static bool UninstallAllBrowsersIntegration()
        {
            try
            {
                UnregisterChromiumBrowser("Google Chrome", @"Software\Google\Chrome\NativeMessagingHosts");
                UnregisterChromiumBrowser("Microsoft Edge", @"Software\Microsoft\Edge\NativeMessagingHosts");
                UnregisterChromiumBrowser("Brave", @"Software\BraveSoftware\Brave-Browser\NativeMessagingHosts");
                UnregisterChromiumBrowser("Opera", @"Software\Opera Software\NativeMessagingHosts");
                UnregisterChromiumBrowser("Vivaldi", @"Software\Vivaldi\NativeMessagingHosts");
                UnregisterFirefox();

                string nativeHostDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EDM", "NativeHost");
                string chromiumManifestPath = Path.Combine(nativeHostDir, $"{NativeHostName}.json");
                string firefoxManifestPath = Path.Combine(nativeHostDir, $"{NativeHostName}-firefox.json");

                if (File.Exists(chromiumManifestPath)) File.Delete(chromiumManifestPath);
                if (File.Exists(firefoxManifestPath)) File.Delete(firefoxManifestPath);

                LoggingService.Log("[BrowserExtensionInstaller] All browsers Native Messaging unregistered cleanly.");
                return true;
            }
            catch (Exception ex)
            {
                LoggingService.LogException("[BrowserExtensionInstaller] Error during uninstall cleanup", ex);
                return false;
            }
        }

        private static void UnregisterChromiumBrowser(string browserName, string subKeyPath)
        {
            try
            {
                Registry.CurrentUser.DeleteSubKeyTree($@"{subKeyPath}\{NativeHostName}", throwOnMissingSubKey: false);
                LoggingService.Log($"[BrowserExtensionInstaller] Unregistered for {browserName}");
            }
            catch (Exception ex)
            {
                LoggingService.Log($"[BrowserExtensionInstaller] Error unregistering {browserName}: {ex.Message}");
            }
        }

        private static void UnregisterFirefox()
        {
            try
            {
                Registry.CurrentUser.DeleteSubKeyTree($@"Software\Mozilla\NativeMessagingHosts\{NativeHostName}", throwOnMissingSubKey: false);
                LoggingService.Log("[BrowserExtensionInstaller] Unregistered for Mozilla Firefox");
            }
            catch (Exception ex)
            {
                LoggingService.Log($"[BrowserExtensionInstaller] Error unregistering Firefox: {ex.Message}");
            }
        }

        public static bool IsBrowserRegistered(string browserName)
        {
            try
            {
                string subKey = browserName.ToLowerInvariant() switch
                {
                    "google chrome" or "chrome" => @"Software\Google\Chrome\NativeMessagingHosts\" + NativeHostName,
                    "microsoft edge" or "edge" => @"Software\Microsoft\Edge\NativeMessagingHosts\" + NativeHostName,
                    "brave" => @"Software\BraveSoftware\Brave-Browser\NativeMessagingHosts\" + NativeHostName,
                    "opera" => @"Software\Opera Software\NativeMessagingHosts\" + NativeHostName,
                    "vivaldi" => @"Software\Vivaldi\NativeMessagingHosts\" + NativeHostName,
                    "firefox" or "mozilla firefox" => @"Software\Mozilla\NativeMessagingHosts\" + NativeHostName,
                    _ => string.Empty
                };

                if (string.IsNullOrEmpty(subKey)) return false;
                using var key = Registry.CurrentUser.OpenSubKey(subKey);
                var val = key?.GetValue("") as string;
                return !string.IsNullOrEmpty(val) && File.Exists(val);
            }
            catch
            {
                return false;
            }
        }
    }
}
