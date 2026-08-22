using System;
using System.Buffers.Binary;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using EDM.NativeMessaging;
using FluentAssertions;
using Xunit;

namespace EDM.Tests.NativeMessaging
{
    public class NativeMessageListenerTests
    {
        private static byte[] PackNativeMessage(object payload)
        {
            byte[] jsonBytes = JsonSerializer.SerializeToUtf8Bytes(payload);
            byte[] result = new byte[4 + jsonBytes.Length];
            BinaryPrimitives.WriteInt32LittleEndian(result, jsonBytes.Length);
            Buffer.BlockCopy(jsonBytes, 0, result, 4, jsonBytes.Length);
            return result;
        }

        private static JsonElement UnpackNativeMessage(MemoryStream stdoutStream)
        {
            byte[] bytes = stdoutStream.ToArray();
            bytes.Length.Should().BeGreaterThanOrEqualTo(4);
            int len = BinaryPrimitives.ReadInt32LittleEndian(bytes);
            bytes.Length.Should().Be(4 + len);

            var jsonBytes = new byte[len];
            Buffer.BlockCopy(bytes, 4, jsonBytes, 0, len);
            using var doc = JsonDocument.Parse(jsonBytes);
            return doc.RootElement.Clone();
        }

        [Fact]
        public async Task NativeMessageListener_PingAction_ReturnsPongResponse()
        {
            var requestPayload = PackNativeMessage(new { action = "ping" });
            using var stdinStream = new MemoryStream(requestPayload);
            using var stdoutStream = new MemoryStream();

            await using var listener = new NativeMessageListener(stdinStream, stdoutStream);
            listener.Start();

            // Allow time for channel processor
            await Task.Delay(150);
            listener.Stop();

            var response = UnpackNativeMessage(stdoutStream);
            response.GetProperty("success").GetBoolean().Should().BeTrue();
            response.GetProperty("action").GetString().Should().Be("pong");
            response.GetProperty("version").GetString().Should().Be("1.0");
        }

        [Fact]
        public async Task NativeMessageListener_CustomAction_InvokesHandlerAndReturnsData()
        {
            var requestPayload = PackNativeMessage(new { action = "query_status", downloadId = "d-123" });
            using var stdinStream = new MemoryStream(requestPayload);
            using var stdoutStream = new MemoryStream();

            await using var listener = new NativeMessageListener(stdinStream, stdoutStream);

            listener.MessageReceivedWithResult += elem =>
            {
                string id = elem.GetProperty("downloadId").GetString()!;
                return Task.FromResult<object?>(new { status = "downloading", speed = 1048576, id });
            };

            listener.Start();
            await Task.Delay(150);
            listener.Stop();

            var response = UnpackNativeMessage(stdoutStream);
            response.GetProperty("success").GetBoolean().Should().BeTrue();
            response.GetProperty("action").GetString().Should().Be("query_status");

            var data = response.GetProperty("data");
            data.GetProperty("status").GetString().Should().Be("downloading");
            data.GetProperty("id").GetString().Should().Be("d-123");
        }
    }
}
