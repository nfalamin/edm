using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Threading.Tasks;
using EDM.Models;
using EDM.Services.Cloud;
using Xunit;

namespace EDM.Tests.Services
{
    public class CloudSyncAndBackupTestSuite
    {
        [Fact]
        public void CloudVaultEncryption_EncryptAndDecryptString_RoundtripMatchesOriginal()
        {
            string secretUrl = "https://private-server.internal/files/confidential_dataset.tar.gz?token=secret123";
            string passphrase = "User-Master-Cloud-Passphrase-2026";

            string encryptedBase64 = CloudVaultEncryption.EncryptString(secretUrl, passphrase);
            Assert.False(string.IsNullOrWhiteSpace(encryptedBase64));
            Assert.NotEqual(secretUrl, encryptedBase64);

            string decrypted = CloudVaultEncryption.DecryptString(encryptedBase64, passphrase);
            Assert.Equal(secretUrl, decrypted);
        }

        [Fact]
        public void CloudVaultEncryption_WrongPassphrase_ThrowsCryptographicException()
        {
            string secret = "Sensitive download credentials";
            string correctPass = "CorrectKey123";
            string wrongPass = "WrongKey456";

            string encrypted = CloudVaultEncryption.EncryptString(secret, correctPass);

            Assert.ThrowsAny<CryptographicException>(() =>
            {
                CloudVaultEncryption.DecryptString(encrypted, wrongPass);
            });
        }

        [Fact]
        public async Task CloudSyncService_GuestAndSignInFlow_TogglesAuthenticationCorrectly()
        {
            var service = CloudSyncService.Instance;
            await service.SignOutAsync();

            Assert.False(service.Account.IsAuthenticated);
            Assert.Equal("Free Cloud Vault", service.Account.PlanTier);

            // Sign in
            bool signedIn = await service.SignInWithPasskeyOrMagicLinkAsync("pro-user@enterprise.org");
            Assert.True(signedIn);
            Assert.True(service.Account.IsAuthenticated);
            Assert.Equal("pro-user@enterprise.org", service.Account.Email);
            Assert.Contains("Pro", service.Account.PlanTier);
            Assert.True(service.Account.LinkedDevices.Count >= 1);

            // Sign out
            await service.SignOutAsync();
            Assert.False(service.Account.IsAuthenticated);
        }

        [Fact]
        public async Task CloudSyncService_CreateAndRestoreSnapshot_PreservesDataIntegrity()
        {
            var service = CloudSyncService.Instance;
            await service.SignInWithPasskeyOrMagicLinkAsync("backup-tester@edm.net");

            var sampleDownloads = new List<DownloadItem>
            {
                new DownloadItem { FileName = "LinuxKernel_6.10.tar.xz", Url = "https://kernel.org/v6.10.tar.xz", Size = "140 MB", Category = "Compressed", Status = "Completed" },
                new DownloadItem { FileName = "Visual_Studio_Code_x64.msi", Url = "https://code.visualstudio.com/x64.msi", Size = "95 MB", Category = "Programs", Status = "Downloading" }
            };

            // Create Snapshot
            var snapshot = await service.CreateBackupSnapshotAsync(sampleDownloads, "Test Automated Snapshot");
            Assert.NotNull(snapshot);
            Assert.Equal(2, snapshot.TotalDownloadsCount);
            Assert.False(string.IsNullOrWhiteSpace(snapshot.EncryptedPayloadBase64));

            // Restore Snapshot
            var restored = await service.RestoreFromSnapshotAsync(snapshot.SnapshotId);
            Assert.NotNull(restored);
            Assert.Equal(2, restored.Count);
            Assert.Equal("LinuxKernel_6.10.tar.xz", restored[0].FileName);
            Assert.Equal("Visual_Studio_Code_x64.msi", restored[1].FileName);
        }

        [Fact]
        public void WebhookNotificationService_BuildsValidDiscordAndGenericPayloads()
        {
            var service = WebhookNotificationService.Instance;
            var item = new DownloadItem
            {
                FileName = "TensorFlow-2.18-GPU.whl",
                Size = "480 MB",
                Category = "Programs",
                Url = "https://pypi.org/tensorflow"
            };

            // Discord format
            service.Config.ServiceType = "Discord";
            string discordJson = service.BuildPayload(item, true, null);
            Assert.Contains("EDM Download Manager", discordJson);
            Assert.Contains("Download Completed", discordJson);
            Assert.Contains("TensorFlow-2.18-GPU.whl", discordJson);

            // Generic JSON format
            service.Config.ServiceType = "CustomJson";
            string genericJson = service.BuildPayload(item, false, "HTTP 403 Forbidden");
            Assert.Contains("DOWNLOAD_FAILED", genericJson);
            Assert.Contains("HTTP 403 Forbidden", genericJson);
        }

        [Fact]
        public void RemotePushService_EnqueueUrl_TriggersRemoteUrlReceivedEvent()
        {
            var service = RemotePushService.Instance;
            RemotePushItem? received = null;

            service.RemoteUrlReceived += item => received = item;

            service.EnqueueRemotePush("https://dist.torproject.org/torbrowser.exe", "torbrowser.exe", "Mobile Companion", true);

            Assert.NotNull(received);
            Assert.Equal("https://dist.torproject.org/torbrowser.exe", received!.Url);
            Assert.Equal("torbrowser.exe", received.SuggestedFileName);
            Assert.Equal("Mobile Companion", received.DeviceSource);
            Assert.True(received.AutoStart);
        }
    }
}
