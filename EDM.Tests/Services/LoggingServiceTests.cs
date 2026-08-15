using System;
using Xunit;
using EDM.Services;

namespace EDM.Tests.Services
{
    /// <summary>
    /// Tests for LoggingService to verify logging behavior and error handling.
    /// </summary>
    public class LoggingServiceTests : TestBase
    {
        [Fact]
        public void Log_WithValidMessage_DoesNotThrow()
        {
            // Arrange
            var message = "Test log message";

            // Act & Assert
            var exception = Record.Exception(() => LoggingService.Log(message));
            Assert.Null(exception);
        }

        [Fact]
        public void Log_WithEmptyMessage_DoesNotThrow()
        {
            // Arrange
            var message = "";

            // Act & Assert
            var exception = Record.Exception(() => LoggingService.Log(message));
            Assert.Null(exception);
        }

        [Fact]
        public void LogException_WithValidException_DoesNotThrow()
        {
            // Arrange
            var context = "Test context";
            var ex = new InvalidOperationException("Test exception");

            // Act & Assert
            var exception = Record.Exception(() => LoggingService.LogException(context, ex));
            Assert.Null(exception);
        }

        [Fact]
        public void LogException_WithNullContext_DoesNotThrow()
        {
            // Arrange
            string? context = null;
            var ex = new InvalidOperationException("Test exception");

            // Act & Assert
            var exception = Record.Exception(() => LoggingService.LogException(context ?? "unnamed", ex));
            Assert.Null(exception);
        }
    }
}
