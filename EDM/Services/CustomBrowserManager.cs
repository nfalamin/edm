using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace EDM.Services
{
    public class CustomBrowserInfo
    {
        public string BrowserName { get; set; } = string.Empty;
        public string ExecutablePath { get; set; } = string.Empty;
        public string BrowserType { get; set; } = "Chromium"; // Chromium or Firefox
        public bool IsRegistered { get; set; }
    }

    /// <summary>
    /// Custom Browser Association & Native Messaging Host Manager for EDM.
    /// Allows users to register arbitrary custom / portable browsers with EDM.
    /// </summary>
    public class CustomBrowserManager
    {
        private static readonly Lazy<CustomBrowserManager> _instance = new(() => new CustomBrowserManager());
        public static CustomBrowserManager Instance => _instance.Value;

        private readonly List<CustomBrowserInfo> _registeredBrowsers = new();

        public IReadOnlyList<CustomBrowserInfo> GetCustomBrowsers() => _registeredBrowsers.AsReadOnly();

        public bool RegisterBrowser(string executablePath, string? customName = null, string browserType = "Chromium")
        {
            if (!File.Exists(executablePath)) return false;

            string name = customName ?? Path.GetFileNameWithoutExtension(executablePath);

            var info = new CustomBrowserInfo
            {
                BrowserName = name,
                ExecutablePath = executablePath,
                BrowserType = browserType,
                IsRegistered = true
            };

            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    // Register native messaging host manifest for the custom browser
                    RegisterNativeHostForCustomBrowser(name, browserType);
                }

                _registeredBrowsers.RemoveAll(b => b.ExecutablePath.Equals(executablePath, StringComparison.OrdinalIgnoreCase));
                _registeredBrowsers.Add(info);
                LoggingService.Log($"[CustomBrowserManager] Successfully registered browser: {name} ({executablePath})");
                return true;
            }
            catch (Exception ex)
            {
                LoggingService.LogException($"[CustomBrowserManager] Failed to register browser: {name}", ex);
                return false;
            }
        }

        private void RegisterNativeHostForCustomBrowser(string browserName, string browserType)
        {
            string hostName = "com.edm.native_host";
            string appDir = AppDomain.CurrentDomain.BaseDirectory;
            string hostExePath = Path.Combine(appDir, "EDM.NativeMessagingHost.exe");
            if (!File.Exists(hostExePath))
            {
                hostExePath = Path.Combine(appDir, "EDM.exe");
            }

            string manifestPath = Path.Combine(appDir, $"edm_native_host_{browserName.ToLowerInvariant()}.json");
            string manifestJson = $@"{{
  ""name"": ""{hostName}"",
  ""description"": ""EDM Native Messaging Host for {browserName}"",
  ""path"": ""{hostExePath.Replace("\\", "\\\\")}"",
  ""type"": ""stdio"",
  ""allowed_origins"": [
    ""chrome-extension://*/*""
  ]
}}";
            File.WriteAllText(manifestPath, manifestJson);

            // Register in Windows Registry
            string registryKey = $@"Software\Google\Chrome\NativeMessagingHosts\{hostName}";
            if (browserType.Equals("Firefox", StringComparison.OrdinalIgnoreCase))
            {
                registryKey = $@"Software\Mozilla\NativeMessagingHosts\{hostName}";
            }

            using var key = Registry.CurrentUser.CreateSubKey(registryKey);
            key?.SetValue(string.Empty, manifestPath);
        }
    }
}
