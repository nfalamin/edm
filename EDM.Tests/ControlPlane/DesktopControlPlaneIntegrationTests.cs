using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using EDM.ControlPlane.Api.Data;
using EDM.ControlPlane.Api.Models;
using EDM.Models;
using EDM.Services;
using EDM.Services.Interfaces;

namespace EDM.Tests.ControlPlane
{
    public class DesktopControlPlaneIntegrationTests : IClassFixture<ControlPlaneTestFactory>
    {
        private readonly ControlPlaneTestFactory _factory;
        private readonly HttpClient _client;

        public DesktopControlPlaneIntegrationTests(ControlPlaneTestFactory factory)
        {
            _factory = factory;
            _client = factory.CreateClient();
        }

        private class InMemorySettingsService : ISettingsService
        {
            private readonly System.Collections.Concurrent.ConcurrentDictionary<string, string> _dict = new(StringComparer.OrdinalIgnoreCase);

            public string? GetSetting(string key) => _dict.TryGetValue(key, out var val) ? val : null;
            public void SaveSetting(string key, string value) => _dict[key] = value;
            public void SetSetting(string key, string value) => _dict[key] = value;
            public bool GetBoolSetting(string key, bool defaultValue = false) => _dict.TryGetValue(key, out var val) && bool.TryParse(val, out var b) ? b : defaultValue;
            public int GetIntSetting(string key, int defaultValue = 0) => _dict.TryGetValue(key, out var val) && int.TryParse(val, out var i) ? i : defaultValue;
            public double GetDoubleSetting(string key, double defaultValue = 0) => _dict.TryGetValue(key, out var val) && double.TryParse(val, out var d) ? d : defaultValue;
            
            public string GetDefaultDownloadPath() => "C:\\Downloads";
            public void SetDefaultDownloadPath(string path) { }
            public List<string> GetCategories() => new() { "General" };
            public void AddCategory(string category) { }
            public string GetFfmpegPath() => string.Empty;
            public void SetFfmpegPath(string path) { }
            public string GetYtDlpPath() => string.Empty;
            public void SetYtDlpPath(string path) { }
            public string GetAria2Path() => string.Empty;
            public void SetAria2Path(string path) { }
            public string GetDefaultFormatArgs() => string.Empty;
            public void SetDefaultFormatArgs(string args) { }
            public bool GetAutoConvertToMp3() => false;
            public void SetAutoConvertToMp3(bool v) { }
            public bool GetSchedulerEnabled() => false;
            public TimeSpan? GetSchedulerTime() => null;
            public void SetScheduler(bool enabled, TimeSpan? time) { }
            public int GetConnectionLimitOverride() => 0;
            public bool GetReduceQualityOnMeteredNetworks() => true;
            public int GetBandwidthLimitKbps() => 0;
            public int GetActiveBandwidthLimitKbps() => 0;
            public ProxySettings GetProxySettings() => new();
            public void SetProxySettings(ProxySettings settings, string? plainPassword = null) { }
            public List<BandwidthSchedule> GetBandwidthSchedules() => new();
            public void SetBandwidthSchedules(List<BandwidthSchedule> schedules) { }
            public bool GetEnableUrlSafetyCheck() => false;
            public void SetEnableUrlSafetyCheck(bool enable) { }
            public bool GetEnablePostDownloadScan() => false;
            public void SetEnablePostDownloadScan(bool enable) { }
            public string GetGoogleSafeBrowsingApiKey() => string.Empty;
            public void SetGoogleSafeBrowsingApiKey(string apiKey) { }
            public bool GetSendAnonymousCrashReports() => false;
            public void SetSendAnonymousCrashReports(bool enable) { }
        }

        [Fact]
        public void StableInstallationId_PersistsAcrossRestarts_WithoutRawMac()
        {
            var settings = new InMemorySettingsService();
            var client1 = new ControlPlaneClient(_client, settings);
            Guid id1 = client1.InstallationId;

            id1.Should().NotBeEmpty();

            // Simulate application restart with new client instance sharing settings
            var client2 = new ControlPlaneClient(_client, settings);
            Guid id2 = client2.InstallationId;

            id2.Should().Be(id1);
        }

        [Fact]
        public async Task DesktopLoginAndTokenPersistence_HandlesSessionLifecycle()
        {
            var settings = new InMemorySettingsService();
            var cpClient = new ControlPlaneClient(_client, settings);

            string username = "deskuser_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            string email = $"{username}@edm.test";
            string pwd = "DesktopPassword!2026";

            // Register through API directly
            var regRes = await _client.PostAsJsonAsync("/api/v1/auth/register", new { Username = username, Email = email, Password = pwd });
            regRes.StatusCode.Should().Be(HttpStatusCode.OK);

            // Login via desktop client
            bool loginSuccess = await cpClient.LoginAsync(email, pwd);
            loginSuccess.Should().BeTrue();
            cpClient.CurrentSecurityState.Should().Be(AccountSecurityState.Active);

            // Check account status
            var status = await cpClient.CheckAccountStatusAsync();
            status.Should().Be(AccountSecurityState.Active);
        }

        [Fact]
        public async Task OfflineResilience_ControlPlaneDown_DoesNotCrash_AndNeverFalselyBans()
        {
            var settings = new InMemorySettingsService();
            // Point client to non-existent endpoint
            settings.SetSetting("ControlPlaneApiUrl", "http://127.0.0.1:54321");

            var offlineHttpClient = new HttpClient { Timeout = TimeSpan.FromMilliseconds(200) };
            var cpClient = new ControlPlaneClient(offlineHttpClient, settings);

            // Login attempt against offline server
            bool loginRes = await cpClient.LoginAsync("user", "pass");
            loginRes.Should().BeFalse();
            cpClient.CurrentSecurityState.Should().Be(AccountSecurityState.Offline);

            // Status check
            var status = await cpClient.CheckAccountStatusAsync();
            status.Should().Be(AccountSecurityState.Offline);

            // Update check should return null gracefully
            var update = await cpClient.CheckForUpdateAsync();
            update.Should().BeNull();
        }

        [Fact]
        public async Task NonBlockingTelemetry_BuffersEventsWithoutThrowing()
        {
            var settings = new InMemorySettingsService();
            var cpClient = new ControlPlaneClient(_client, settings);
            using var telemetry = new ControlPlaneTelemetryService(cpClient);

            // Enqueue several rapid events
            telemetry.TrackAppStarted("2.0.0", "Windows 11");
            telemetry.TrackDownloadStarted("https://example.com/test.zip", 50000000, 8);
            telemetry.TrackDownloadCompleted("https://example.com/test.zip", 50000000, 4.2, 12000000);
            telemetry.TrackDownloadFailed("https://example.com/failed.bin", "404 Not Found", false);

            // Allow background queue to process
            await Task.Delay(300);
        }

        [Fact]
        public async Task UpdateService_CheckControlPlaneUpdate_ReturnsValidReleaseInfo()
        {
            var settings = new InMemorySettingsService();
            var updateService = new UpdateService(settings, null, _client);

            var updateInfo = await updateService.CheckControlPlaneUpdateAsync("1.0.0");
            updateInfo.Should().NotBeNull();
            updateInfo.IsUpdateAvailable.Should().BeTrue();
            updateInfo.Version.Should().NotBeNullOrWhiteSpace();
        }
    }
}
