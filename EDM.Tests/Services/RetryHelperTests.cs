using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using EDM.Services;

namespace EDM.Tests.Services
{
    public class RetryHelperTests : TestBase
    {
        [Fact]
        public async Task RetryAsync_SuccessfulOperation_ReturnsResultFirstTry()
        {
            // Arrange
            int attempts = 0;
            Func<Task<string>> operation = () =>
            {
                attempts++;
                return Task.FromResult("Success");
            };

            // Act
            var result = await RetryHelper.RetryAsync(operation, maxAttempts: 3, backoff: a => TimeSpan.Zero, cancellationToken: CancellationToken.None);

            // Assert
            result.Should().Be("Success");
            attempts.Should().Be(1);
        }

        [Fact]
        public async Task RetryAsync_FailsTwiceThenSucceeds_RetriesAndReturnsResult()
        {
            // Arrange
            int attempts = 0;
            Func<Task<int>> operation = () =>
            {
                attempts++;
                if (attempts < 3)
                {
                    throw new InvalidOperationException($"Attempt {attempts} failed");
                }
                return Task.FromResult(42);
            };

            // Act
            var result = await RetryHelper.RetryAsync(operation, maxAttempts: 5, backoff: a => TimeSpan.FromMilliseconds(1), cancellationToken: CancellationToken.None);

            // Assert
            result.Should().Be(42);
            attempts.Should().Be(3);
        }

        [Fact]
        public async Task RetryAsync_ExceedsMaxAttempts_ThrowsOriginalException()
        {
            // Arrange
            int attempts = 0;
            Func<Task<int>> operation = () =>
            {
                attempts++;
                throw new InvalidOperationException("Persistent failure");
            };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                RetryHelper.RetryAsync(operation, maxAttempts: 3, backoff: a => TimeSpan.FromMilliseconds(1), cancellationToken: CancellationToken.None));

            ex.Message.Should().Be("Persistent failure");
            attempts.Should().Be(3);
        }

        [Fact]
        public async Task RetryAsync_CancellationRequested_ThrowsOperationCanceledException()
        {
            // Arrange
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            Func<Task<int>> operation = () => Task.FromResult(100);

            // Act & Assert
            await Assert.ThrowsAsync<OperationCanceledException>(() =>
                RetryHelper.RetryAsync(operation, maxAttempts: 3, backoff: a => TimeSpan.Zero, cancellationToken: cts.Token));
        }
    }
}
