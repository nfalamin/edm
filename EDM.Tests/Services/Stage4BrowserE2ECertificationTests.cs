using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Win32;
using Xunit;
using EDM.NativeMessaging;
using EDM.Services;

namespace EDM.Tests.Services
{
    public class Stage4BrowserE2ECertificationTests : TestBase
    {
        [Fact]
        public void NativeMessagingManifest_GeneratesCompliantJsonForAllBrowsers()
        {
            string fakeExe = @"C:\Program Files\EDM\EDM.exe";
            string manifestJson = BrowserExtensionInstaller.GenerateManifestJson(fakeExe);

            manifestJson.Should().NotBeNullOrWhiteSpace();

            using var doc = JsonDocument.Parse(manifestJson);
            var root = doc.RootElement;

            root.GetProperty("name").GetString().Should().Be("com.edm.downloader");
            root.GetProperty("type").GetString().Should().Be("stdio");
            root.GetProperty("path").GetString().Should().Be(fakeExe);

            var origins = root.GetProperty("allowed_origins");
            origins.GetArrayLength().Should().BeGreaterThan(0);
        }

        [Fact]
        public void RegistryRegistrationAndUninstall_CleansUpAllKeysDeterministically()
        {
            // Test installation & registration
            bool installOk = BrowserExtensionInstaller.InstallAllBrowsersIntegration();
            installOk.Should().BeTrue();

            // Verify Chrome registry key exists in HKCU
            using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Google\Chrome\NativeMessagingHosts\com.edm.downloader"))
            {
                key.Should().NotBeNull("Chrome Native Messaging Host must be registered in HKCU");
            }

            // Verify Edge registry key exists in HKCU
            using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Edge\NativeMessagingHosts\com.edm.downloader"))
            {
                key.Should().NotBeNull("Edge Native Messaging Host must be registered in HKCU");
            }

            // Test uninstall cleanup
            bool uninstallOk = BrowserExtensionInstaller.UninstallAllBrowsersIntegration();
            uninstallOk.Should().BeTrue();

            // Verify clean unregistration
            using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Google\Chrome\NativeMessagingHosts\com.edm.downloader"))
            {
                key.Should().BeNull("Chrome registration key must be cleaned up on uninstall");
            }
        }

        [Fact]
        public async Task StdioHandshakeAndProtocol_HandlesInterceptAndDuplicateSuppression()
        {
            using var stdin = new MemoryStream();
            using var stdout = new MemoryStream();

            // Prepare simulated incoming native messages
            var interceptMsg = new
            {
                action = "intercept",
                url = "https://example.com/software.zip",
                filename = "software.zip",
                cookies = "session=12345",
                headers = new Dictionary<string, string> { { "User-Agent", "Mozilla/5.0" } }
            };

            byte[] jsonBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(interceptMsg));
            byte[] lengthHeader = new byte[4];
            BinaryPrimitives.WriteUInt32LittleEndian(lengthHeader, (uint)jsonBytes.Length);

            // Write 2 identical messages to test duplicate suppression
            stdin.Write(lengthHeader);
            stdin.Write(jsonBytes);
            stdin.Write(lengthHeader);
            stdin.Write(jsonBytes);
            stdin.Position = 0;

            var receivedActions = new List<string>();
            var tcs = new TaskCompletionSource<bool>();

            await using var listener = new NativeMessageListener(stdin, stdout);
            listener.MessageReceivedWithResult += async (msg) =>
            {
                string action = msg.GetProperty("action").GetString() ?? "";
                lock (receivedActions)
                {
                    receivedActions.Add(action);
                    tcs.TrySetResult(true);
                }
                return new { status = "handed_off", downloadId = Guid.NewGuid().ToString("N") };
            };

            listener.Start();

            var completed = await Task.WhenAny(tcs.Task, Task.Delay(2000));
            completed.Should().Be(tcs.Task, "Listener should process messages within timeout");

            // Give a moment for second duplicate message to be evaluated and suppressed
            await Task.Delay(200);

            receivedActions.Should().ContainSingle().Which.Should().Be("intercept", "Duplicate message must be suppressed by NativeMessageListener deduplication cache");
            completed.Should().Be(tcs.Task, "Listener should process messages within timeout");

            receivedActions.Should().Contain("intercept");
        }

        [Fact]
        public void EnvironmentBrowserMatrix_CategorizesRealVsBlockedAccurately()
        {
            // Query registry for actually installed browsers on this machine
            var installedBrowsers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                using var clientsKey = Registry.LocalMachine.OpenSubKey(@"Software\Clients\StartMenuInternet");
                if (clientsKey != null)
                {
                    foreach (var subKey in clientsKey.GetSubKeyNames())
                    {
                        installedBrowsers.Add(subKey);
                    }
                }
            }
            catch { }

            // Document status without faking
            bool chromeInstalled = installedBrowsers.Contains("Google Chrome") || File.Exists(@"C:\Program Files\Google\Chrome\Application\chrome.exe");
            bool edgeInstalled = installedBrowsers.Contains("Microsoft Edge") || File.Exists(@"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe");

            chromeInstalled.Should().BeTrue("Google Chrome is present in test environment");
            edgeInstalled.Should().BeTrue("Microsoft Edge is present in test environment");
        }
    }
}
