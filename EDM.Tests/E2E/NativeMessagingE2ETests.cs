using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EDM.NativeMessaging;
using EDM.Services;
using FluentAssertions;
using Xunit;

namespace EDM.Tests.E2E
{
    [Trait("Category", "RealE2E")]
    public class NativeMessagingE2ETests
    {
        [Fact]
        public async Task NativeIpcServer_ReceivesHandoffAndReturnsAccepted()
        {
            IpcHandoffPayload? received = null;
            var handoffEvent = new TaskCompletionSource<bool>();

            string testPipeName = "EDM_TestPipe_" + Guid.NewGuid().ToString("N");
            var server = new NativeIpcServer(payload =>
            {
                received = payload;
                handoffEvent.TrySetResult(true);
                return Task.FromResult(true);
            }, testPipeName);

            try
            {
                server.Start();
                await Task.Delay(200);

                var sendPayload = new IpcHandoffPayload
                {
                    Url = "https://example.com/test_video.mp4",
                    Filename = "test_video.mp4",
                    Quality = "1080p",
                    Browser = "Chrome",
                    CorrelationId = "test-corr-123"
                };

                using var client = new NamedPipeClientStream(".", testPipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
                await client.ConnectAsync(3000);

                using var writer = new StreamWriter(client, Encoding.UTF8, leaveOpen: true) { AutoFlush = true };
                using var reader = new StreamReader(client, Encoding.UTF8, leaveOpen: true);

                await writer.WriteLineAsync(JsonSerializer.Serialize(sendPayload));
                string? responseJson = await reader.ReadLineAsync();

                responseJson.Should().NotBeNullOrWhiteSpace();
                using var respDoc = JsonDocument.Parse(responseJson!);
                respDoc.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();
                respDoc.RootElement.GetProperty("status").GetString().Should().Be("accepted");

                var handoffCompleted = await Task.WhenAny(handoffEvent.Task, Task.Delay(2000));
                handoffCompleted.Should().Be(handoffEvent.Task);

                received.Should().NotBeNull();
                received!.Url.Should().Be("https://example.com/test_video.mp4");
                received.Filename.Should().Be("test_video.mp4");
                received.Quality.Should().Be("1080p");
            }
            finally
            {
                await server.DisposeAsync();
            }
        }

        [Fact]
        public void BrowserExtensionInstaller_Manifests_MatchBrowserRequirements()
        {
            string fakeExe = @"C:\Program Files\EDM\EDM.NativeHost.exe";

            string chromiumManifest = BrowserExtensionInstaller.GenerateChromiumManifestJson(fakeExe);
            using var cDoc = JsonDocument.Parse(chromiumManifest);
            var cRoot = cDoc.RootElement;
            cRoot.GetProperty("name").GetString().Should().Be("com.edm.downloader");
            cRoot.GetProperty("type").GetString().Should().Be("stdio");
            cRoot.GetProperty("allowed_origins").GetArrayLength().Should().BeGreaterThan(0);
            cRoot.GetProperty("allowed_origins")[0].GetString().Should().Contain("chrome-extension://");

            string firefoxManifest = BrowserExtensionInstaller.GenerateFirefoxManifestJson(fakeExe);
            using var fDoc = JsonDocument.Parse(firefoxManifest);
            var fRoot = fDoc.RootElement;
            fRoot.GetProperty("name").GetString().Should().Be("com.edm.downloader");
            fRoot.GetProperty("type").GetString().Should().Be("stdio");
            fRoot.GetProperty("allowed_extensions").GetArrayLength().Should().Be(1);
            fRoot.GetProperty("allowed_extensions")[0].GetString().Should().Be("edm-extension@edm.app");
        }

        [Fact]
        public async Task NativeMessageListener_ProcessesPingMessage()
        {
            using var inStream = new MemoryStream();
            using var outStream = new MemoryStream();

            // Prepare 32-bit LE length + ping JSON
            byte[] pingJson = Encoding.UTF8.GetBytes("{\"action\":\"ping\"}");
            byte[] lenBuf = new byte[4];
            System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(lenBuf, pingJson.Length);

            inStream.Write(lenBuf);
            inStream.Write(pingJson);
            inStream.Position = 0;

            await using var listener = new NativeMessageListener(inStream, outStream);
            listener.Start();

            // Allow listener to process message
            await Task.Delay(300);

            outStream.Position = 0;
            outStream.Length.Should().BeGreaterThan(4);

            byte[] respLenBuf = new byte[4];
            outStream.Read(respLenBuf, 0, 4);
            int respLen = System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(respLenBuf);
            respLen.Should().BeGreaterThan(0);

            byte[] respPayload = new byte[respLen];
            outStream.Read(respPayload, 0, respLen);
            string respJson = Encoding.UTF8.GetString(respPayload);

            using var doc = JsonDocument.Parse(respJson);
            doc.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();
            doc.RootElement.GetProperty("action").GetString().Should().Be("pong");
        }

        [Fact]
        public void BrowserExtensionInstaller_InstallsRegistryAndManifestsPermanently()
        {
            bool ok = BrowserExtensionInstaller.InstallAllBrowsersIntegration();
            ok.Should().BeTrue();
        }
    }
}
