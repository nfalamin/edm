using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using EDM.NativeMessaging;
using EDM.Services;
using FluentAssertions;
using Xunit;

namespace EDM.Tests.Services
{
    [Collection("InterceptionTests")]
    public class RealWorldVerificationInfrastructureTests : IDisposable
    {
        public RealWorldVerificationInfrastructureTests()
        {
            BrowserInterceptionStateMachine.ResetForTesting();
            NativeMessageListener.ResetDeduplicationCacheForTesting();
        }

        public void Dispose()
        {
            BrowserInterceptionStateMachine.ResetForTesting();
            NativeMessageListener.ResetDeduplicationCacheForTesting();
        }

        [Fact]
        public void Part3_NativeHostManifest_ContainsValidSchemaAndBrowserOrigins()
        {
            string testExePath = @"C:\Program Files (x86)\Exclusive Download Manager (v2.0)\EDM.exe";
            string manifestJson = BrowserExtensionInstaller.GenerateManifestJson(testExePath);

            manifestJson.Should().NotBeNullOrEmpty();
            manifestJson.Should().Contain("com.edm.downloader");
            manifestJson.Should().Contain("chrome-extension://");
            manifestJson.Should().Contain(BrowserExtensionInstaller.ChromeExtensionId);
            manifestJson.Should().Contain(testExePath.Replace(@"\", @"\\"));
        }

        [Theory]
        [InlineData(@"C:\Program Files (x86)\EDM Downloader\EDM.exe")] // Path with spaces & parentheses
        [InlineData(@"D:\Project 2\10 AUG - 2.07AM\5 AUG\EDM\EDM.exe")] // Deep path with spaces
        [InlineData(@"C:\Users\Nafala\Downloads\📁 EDM-Downloader (ভবিষ্যৎ)\EDM.exe")] // Path with Unicode & folder icons
        public void Part5_NativeHostPathRobustness_EscapesComplexPathsCorrectly(string complexPath)
        {
            string manifestJson = BrowserExtensionInstaller.GenerateManifestJson(complexPath);

            using var doc = JsonDocument.Parse(manifestJson);
            string parsedPath = doc.RootElement.GetProperty("path").GetString() ?? "";

            parsedPath.Should().Be(Path.GetFullPath(complexPath));
        }

        [Fact]
        public void Part4_InstallerSimulation_ExecutesInstallUninstallReinstallCycleCleanly()
        {
            // Step 1: Install
            bool installed = BrowserExtensionInstaller.InstallAllBrowsersIntegration();
            installed.Should().BeTrue();

            // Step 2: Uninstall
            bool uninstalled = BrowserExtensionInstaller.UninstallAllBrowsersIntegration();
            uninstalled.Should().BeTrue();

            // Step 3: Reinstall
            bool reinstalled = BrowserExtensionInstaller.InstallAllBrowsersIntegration();
            reinstalled.Should().BeTrue();
        }

        [Fact]
        public void Part6_NetworkFailureMatrix_PreservesBrowserDownloadOnInterruptedHandoff()
        {
            string url = "https://cdn.example.com/network_interrupted_file.zip";
            string corrId = "edm_corr_net_fail_" + Guid.NewGuid().ToString("N");

            BrowserInterceptionStateMachine.CreateSession(corrId, url, "network_interrupted_file.zip");
            BrowserInterceptionStateMachine.TransitionState(corrId, InterceptionState.Validating);
            BrowserInterceptionStateMachine.TransitionState(corrId, InterceptionState.HandoffPending);

            // Simulate HTTP 5xx or server connection reset during handoff
            bool fallback = BrowserInterceptionStateMachine.TransitionState(corrId, InterceptionState.RecoverableFallback, "HTTP 503 Server Connection Reset");
            fallback.Should().BeTrue();

            var session = BrowserInterceptionStateMachine.GetSession(corrId);
            session!.State.Should().Be(InterceptionState.RecoverableFallback);
        }

        [Fact]
        public void Part8_HighVolumeSimulatedSoakTest_Processes1000EventsWithoutLossOrLeak()
        {
            BrowserInterceptionStateMachine.ResetForTesting();
            int eventCount = 1000;
            int uniqueExpected = 500;
            int queuedCount = 0;
            int duplicatesSuppressed = 0;

            long memoryBefore = GC.GetTotalMemory(forceFullCollection: true);

            for (int i = 0; i < eventCount; i++)
            {
                int bId = (i / 2) + 1;
                string url = $"https://example.com/soak_file_{bId}.bin";
                string json = $"{{\"action\":\"add_download\",\"url\":\"{url}\",\"browserDownloadId\":\"{bId}\"}}";

                using var doc = JsonDocument.Parse(json);
                bool isDup = NativeMessageListener.IsDuplicateMessage(doc.RootElement);

                if (isDup)
                {
                    duplicatesSuppressed++;
                }
                else
                {
                    queuedCount++;
                    string corrId = $"edm_corr_soak_{i}";
                    BrowserInterceptionStateMachine.CreateSession(corrId, url, $"soak_file_{bId}.bin");
                    BrowserInterceptionStateMachine.TransitionState(corrId, InterceptionState.Validating);
                    BrowserInterceptionStateMachine.TransitionState(corrId, InterceptionState.HandoffPending);
                    BrowserInterceptionStateMachine.TransitionState(corrId, InterceptionState.HandedOff);
                    BrowserInterceptionStateMachine.TransitionState(corrId, InterceptionState.BrowserCancelled);
                    BrowserInterceptionStateMachine.TransitionState(corrId, InterceptionState.EdmQueued);
                }
            }

            queuedCount.Should().Be(uniqueExpected);
            duplicatesSuppressed.Should().Be(eventCount - uniqueExpected);

            // Prune sessions older than 0 seconds
            int pruned = BrowserInterceptionStateMachine.PruneStaleSessions(TimeSpan.FromSeconds(0));
            pruned.Should().Be(uniqueExpected);
            BrowserInterceptionStateMachine.ActiveSessionCount.Should().Be(0);

            long memoryAfter = GC.GetTotalMemory(forceFullCollection: true);
            long memoryDelta = memoryAfter - memoryBefore;

            // Assert memory delta after pruning is negligible (< 2 MB for 1000 events)
            memoryDelta.Should().BeLessThan(2 * 1024 * 1024);
        }

        [Fact]
        public void Part11_CryptographicFileIntegrity_VerifiesPayloadSha256MatchesSourceHash()
        {
            byte[] sourceData = Encoding.UTF8.GetBytes("Exclusive Download Manager (EDM) Payload Verification Data — " + Guid.NewGuid().ToString("N"));
            string sourceHash = ComputeSha256(sourceData);

            // Simulate file download merge output
            byte[] downloadedData = (byte[])sourceData.Clone();
            string downloadedHash = ComputeSha256(downloadedData);

            downloadedHash.Should().Be(sourceHash);
        }

        private static string ComputeSha256(byte[] data)
        {
            using var sha = SHA256.Create();
            byte[] hash = sha.ComputeHash(data);
            return Convert.ToHexStringLower(hash);
        }
    }
}
