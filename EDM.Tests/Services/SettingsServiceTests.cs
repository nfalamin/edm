using System;
using Xunit;
using FluentAssertions;
using EDM.Services;
using EDM.Services.Interfaces;

namespace EDM.Tests.Services
{
    /// <summary>
    /// Tests for SettingsService to verify settings persistence and retrieval.
    /// </summary>
    public class SettingsServiceTests : TestBase
    {
        private readonly ISettingsService _settingsService;

        public SettingsServiceTests()
        {
            _settingsService = new SettingsService();
        }

        [Fact]
        public void GetSetting_WithValidKey_ReturnsValue()
        {
            // Arrange
            var key = "TestKey";
            var expectedValue = "TestValue";
            _settingsService.SaveSetting(key, expectedValue);

            // Act
            var result = _settingsService.GetSetting(key);

            // Assert
            result.Should().Be(expectedValue);
        }

        [Fact]
        public void GetSetting_WithNonExistentKey_ReturnsEmpty()
        {
            // Arrange
            var key = "NonExistentKey_" + Guid.NewGuid();

            // Act
            var result = _settingsService.GetSetting(key);

            // Assert
            result.Should().BeNullOrEmpty();
        }

        [Fact]
        public void SaveSetting_WithValidKeyValue_PersistsValue()
        {
            // Arrange
            var key = "PersistKey_" + Guid.NewGuid();
            var value = "PersistValue_" + DateTime.UtcNow.Ticks;

            // Act
            _settingsService.SaveSetting(key, value);
            var result = _settingsService.GetSetting(key);

            // Assert
            result.Should().Be(value);
        }

        [Fact]
        public void SaveSetting_OverwritesExistingValue()
        {
            // Arrange
            var key = "OverwriteKey_" + Guid.NewGuid();
            var originalValue = "Original";
            var newValue = "Updated";
            _settingsService.SaveSetting(key, originalValue);

            // Act
            _settingsService.SaveSetting(key, newValue);
            var result = _settingsService.GetSetting(key);

            // Assert
            result.Should().Be(newValue);
        }
    }
}
