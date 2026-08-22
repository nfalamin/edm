using System;
using System.Net;
using System.Runtime.Versioning;
using Xunit;
using FluentAssertions;
using EDM.Models;
using EDM.Services;

namespace EDM.Tests.Services
{
    public class ProxyServiceTests : TestBase
    {
        [Fact]
        public void BuildWebProxy_WithDisabledProxy_ReturnsNull()
        {
            // Arrange
            var settings = new ProxySettings
            {
                Enabled = false,
                Host = "127.0.0.1",
                Port = 8080
            };

            // Act
            var proxy = ProxyService.BuildWebProxy(settings);

            // Assert
            proxy.Should().BeNull();
        }

        [Fact]
        public void BuildWebProxy_WithNullSettings_ReturnsNull()
        {
            // Act
            var proxy = ProxyService.BuildWebProxy(null);

            // Assert
            proxy.Should().BeNull();
        }

        [Fact]
        public void BuildWebProxy_WithInvalidHostOrPort_ReturnsNull()
        {
            // Arrange - invalid port
            var settings1 = new ProxySettings { Enabled = true, Host = "127.0.0.1", Port = 0 };
            // Arrange - empty host
            var settings2 = new ProxySettings { Enabled = true, Host = "", Port = 8080 };

            // Act & Assert
            ProxyService.BuildWebProxy(settings1).Should().BeNull();
            ProxyService.BuildWebProxy(settings2).Should().BeNull();
        }

        [Fact]
        public void BuildWebProxy_WithValidHttpProxy_ReturnsWebProxyInstance()
        {
            // Arrange
            var settings = new ProxySettings
            {
                Enabled = true,
                Type = ProxyType.Http,
                Host = "proxy.example.com",
                Port = 8080,
                BypassLocalAddresses = true
            };

            // Act
            var proxy = ProxyService.BuildWebProxy(settings) as WebProxy;

            // Assert
            proxy.Should().NotBeNull();
            proxy!.Address.Should().Be(new Uri("http://proxy.example.com:8080"));
            proxy.BypassProxyOnLocal.Should().BeTrue();
        }

        [SupportedOSPlatform("windows")]
        [Fact]
        public void EncryptAndDecryptPassword_RoundtripsSuccessfullyOnWindows()
        {
            if (!OperatingSystem.IsWindows()) return;

            // Arrange
            var plainPassword = "SecretPassword123!";

            // Act
            var encrypted = ProxyService.EncryptPassword(plainPassword);
            var decrypted = ProxyService.DecryptPassword(encrypted);

            // Assert
            encrypted.Should().NotBeNullOrEmpty();
            encrypted.Should().NotBe(plainPassword);
            decrypted.Should().Be(plainPassword);
        }

        [SupportedOSPlatform("windows")]
        [Fact]
        public void BuildWebProxy_WithCredentials_AttachesCredentials()
        {
            if (!OperatingSystem.IsWindows()) return;

            // Arrange
            var plainPassword = "MyProxyPassword";
            var encrypted = ProxyService.EncryptPassword(plainPassword);

            var settings = new ProxySettings
            {
                Enabled = true,
                Host = "10.0.0.1",
                Port = 3128,
                Username = "proxyUser",
                EncryptedPassword = encrypted
            };

            // Act
            var proxy = ProxyService.BuildWebProxy(settings) as WebProxy;

            // Assert
            proxy.Should().NotBeNull();
            proxy!.Credentials.Should().NotBeNull();
            var cred = proxy.Credentials!.GetCredential(new Uri("http://10.0.0.1:3128"), "Basic");
            cred.Should().NotBeNull();
            cred!.UserName.Should().Be("proxyUser");
            cred.Password.Should().Be(plainPassword);
        }
    }
}
