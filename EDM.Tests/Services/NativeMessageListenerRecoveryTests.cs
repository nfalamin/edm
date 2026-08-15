using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EDM.NativeMessaging;
using FluentAssertions;
using Xunit;

namespace EDM.Tests.Services
{
    public class NativeMessageListenerRecoveryTests
    {
        private class FailsOnFirstReadStream : Stream
        {
            private int _readAttempt = 0;
            private readonly MemoryStream _dataStream;

            public FailsOnFirstReadStream(byte[] data)
            {
                _dataStream = new MemoryStream(data);
            }

            public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                int attempt = Interlocked.Increment(ref _readAttempt);
                if (attempt == 1)
                {
                    throw new IOException("Simulated transient pipe read error");
                }
                return _dataStream.ReadAsync(buffer, offset, count, cancellationToken);
            }

            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => _dataStream.Length;
            public override long Position { get => 0; set { } }
            public override void Flush() { }
            public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        }

        private class AlwaysFailsStream : Stream
        {
            public int ReadAttempts = 0;

            public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                Interlocked.Increment(ref ReadAttempts);
                throw new IOException("Persistent pipe failure");
            }

            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => 0;
            public override long Position { get => 0; set { } }
            public override void Flush() { }
            public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        }

        [Fact]
        public async Task ReadLoop_RecoversFromTransientIOExceptionAndProcessesMessage()
        {
            string json = "{\"action\":\"test_download\",\"url\":\"https://example.com/file.zip\"}";
            byte[] payload = Encoding.UTF8.GetBytes(json);
            byte[] lengthHeader = BitConverter.GetBytes(payload.Length);
            byte[] fullData = new byte[4 + payload.Length];
            Buffer.BlockCopy(lengthHeader, 0, fullData, 0, 4);
            Buffer.BlockCopy(payload, 0, fullData, 4, payload.Length);

            using var inStream = new FailsOnFirstReadStream(fullData);
            using var outStream = new MemoryStream();

            var listener = new NativeMessageListener(inStream, outStream);
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            listener.MessageReceived += (msg) =>
            {
                tcs.TrySetResult(true);
                return Task.CompletedTask;
            };

            listener.Start();
            var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(2000));
            bool pingProcessed = completedTask == tcs.Task && await tcs.Task;
            await listener.DisposeAsync();

            pingProcessed.Should().BeTrue("Listener must recover from transient IOException and process subsequent stream payload");
        }

        [Fact]
        public async Task ReadLoop_StopsAfterMaxTransientRetriesWithoutCpuSpinning()
        {
            using var inStream = new AlwaysFailsStream();
            using var outStream = new MemoryStream();

            var listener = new NativeMessageListener(inStream, outStream);
            listener.Start();

            await Task.Delay(1000); // Allow retry loop to run bounded retries
            await listener.DisposeAsync();

            inStream.ReadAttempts.Should().BeLessOrEqualTo(NativeMessageListener.MaxTransientRetries + 2,
                "Listener must stop after bounded retries rather than CPU spinning indefinitely");
        }

        [Fact]
        public async Task DisposeAsync_HandlesObjectDisposedExceptionCleanly()
        {
            using var inStream = new MemoryStream();
            using var outStream = new MemoryStream();

            var listener = new NativeMessageListener(inStream, outStream);
            listener.Start();

            // Simulate stream disposal while listener is running
            inStream.Dispose();
            outStream.Dispose();

            var disposeTask = listener.DisposeAsync().AsTask();
            var completed = await Task.WhenAny(disposeTask, Task.Delay(2000));

            completed.Should().Be(disposeTask, "DisposeAsync must complete cleanly when streams are disposed");
        }

        [Fact]
        public void ScrubPayloadForLogs_RedactsSensitiveInformation()
        {
            string rawJson = "{\"action\":\"add_download\",\"token\":\"secret123\",\"password\":\"myPass\",\"url\":\"https://example.com/file.zip\"}";
            string scrubbed = NativeMessageListener.ScrubPayloadForLogs(rawJson);

            scrubbed.Should().Contain("[REDACTED]");
            scrubbed.Should().NotContain("secret123");
            scrubbed.Should().NotContain("myPass");
            scrubbed.Should().Contain("https://example.com/file.zip");
        }
    }
}
