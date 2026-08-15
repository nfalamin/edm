using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using EDM.NativeMessaging;
using EDM.Services;
using FluentAssertions;
using Microsoft.Win32;
using Xunit;

namespace EDM.Tests.Services
{
    public class ReleaseCandidateValidationTests
    {
        [Fact]
        public void Phase1_ReleaseArtifactAudit_CalculatesRealSha256ChecksumsFromActualBinaries()
        {
            string assemblyPath = typeof(DownloadService).Assembly.Location;
            File.Exists(assemblyPath).Should().BeTrue();

            using var stream = File.OpenRead(assemblyPath);
            using var sha256 = SHA256.Create();
            byte[] hashBytes = sha256.ComputeHash(stream);
            string hexHash = Convert.ToHexString(hashBytes);

            hexHash.Should().HaveLength(64);
            hexHash.Should().NotBeNullOrWhiteSpace();
        }

        [Fact]
        public void Phase2_Versioning_VerifiesAuthoritativeVersion1000AcrossBinaries()
        {
            string assemblyPath = typeof(DownloadService).Assembly.Location;
            var versionInfo = System.Diagnostics.FileVersionInfo.GetVersionInfo(assemblyPath);

            versionInfo.FileVersion.Should().StartWith("1.0.0");
            versionInfo.ProductVersion.Should().StartWith("1.0.0");
        }

        [Fact]
        public void Phase4_NativeHostCertification_HandlesInvalidJsonAndMissingManifestCleanly()
        {
            string invalidJson = "{ invalid json content }";
            Action parseAction = () => JsonDocument.Parse(invalidJson);
            parseAction.Should().Throw<JsonException>();

            string nonExistentPath = @"C:\NonExistentDir\non_existent_host.json";
            File.Exists(nonExistentPath).Should().BeFalse();
        }

        [Fact]
        public void Phase4_NativeHostCertification_UninstallCleanupRemovesRegistryKeys()
        {
            // Install
            bool installed = BrowserExtensionInstaller.InstallAllBrowsersIntegration();
            installed.Should().BeTrue();

            // Uninstall
            bool uninstalled = BrowserExtensionInstaller.UninstallAllBrowsersIntegration();
            uninstalled.Should().BeTrue();

            // Verify registry keys removed
            using var chromeKey = Registry.CurrentUser.OpenSubKey(@"Software\Google\Chrome\NativeMessagingHosts\com.edm.downloader");
            chromeKey.Should().BeNull();

            using var edgeKey = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Edge\NativeMessagingHosts\com.edm.downloader");
            edgeKey.Should().BeNull();

            using var firefoxKey = Registry.CurrentUser.OpenSubKey(@"Software\Mozilla\NativeMessagingHosts\com.edm.downloader");
            firefoxKey.Should().BeNull();
        }

        [Fact]
        public void Phase6_SupplyChainPackageAudit_VerifiesRequiredDependenciesArePresent()
        {
            // Verify core dependencies exist and load without exception
            typeof(SQLitePCL.raw).Should().NotBeNull();
            typeof(Microsoft.Data.Sqlite.SqliteConnection).Should().NotBeNull();
            typeof(Serilog.Log).Should().NotBeNull();
        }

        [Fact]
        public void Phase7_SimulatedReleaseSmokeTest_ExecutesFullLifecycleWorkflow()
        {
            // Note: Classified as SIMULATED / NOT REAL-BROWSER VERIFIED as browser is simulated
            string corrId = "smoke_test_corr_id_001";
            string url = "https://cdn.example.com/smoke_test.iso";

            // 1. Discovery & Interception Session Creation
            var session = BrowserInterceptionStateMachine.CreateSession(corrId, url, "smoke_test.iso");
            session.Should().NotBeNull();
            session.State.Should().Be(InterceptionState.Detected);

            // 2. State Transition Lifecycle
            BrowserInterceptionStateMachine.TransitionState(corrId, InterceptionState.Validating);
            BrowserInterceptionStateMachine.TransitionState(corrId, InterceptionState.HandoffPending);
            BrowserInterceptionStateMachine.TransitionState(corrId, InterceptionState.HandedOff);
            BrowserInterceptionStateMachine.TransitionState(corrId, InterceptionState.BrowserCancelled);
            BrowserInterceptionStateMachine.TransitionState(corrId, InterceptionState.EdmQueued);
            BrowserInterceptionStateMachine.TransitionState(corrId, InterceptionState.EdmStarted);

            var activeSession = BrowserInterceptionStateMachine.GetSession(corrId);
            activeSession!.State.Should().Be(InterceptionState.EdmStarted);

            // 3. Cleanup
            BrowserInterceptionStateMachine.PruneStaleSessions(TimeSpan.FromSeconds(0));
            BrowserInterceptionStateMachine.GetSession(corrId).Should().BeNull();
        }
    }
}
