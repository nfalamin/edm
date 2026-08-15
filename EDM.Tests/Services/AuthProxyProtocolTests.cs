using System;
using EDM.Models;
using EDM.Services;
using FluentAssertions;
using Xunit;

namespace EDM.Tests.Services
{
    public class AuthProxyProtocolTests : TestBase
    {
        [Fact]
        public void ProxyService_EncryptAndDecryptPassword_DPAPIWorksCorrectly()
        {
            // Arrange
            string originalPassword = "SecretPassword123!";

            // Act
            string encrypted = ProxyService.EncryptPassword(originalPassword);
            string decrypted = ProxyService.DecryptPassword(encrypted);

            // Assert
            encrypted.Should().NotBe(originalPassword);
            decrypted.Should().Be(originalPassword);
        }

        [Fact]
        public void ProxyService_BuildWebProxy_CreatesProxyObjectWithBypass()
        {
            // Arrange
            var settings = new ProxySettings
            {
                Enabled = true,
                Host = "127.0.0.1",
                Port = 8080,
                Type = ProxyType.Http,
                BypassLocalAddresses = true,
                BypassList = "localhost, *.internal"
            };

            // Act
            var proxy = ProxyService.BuildWebProxy(settings);

            // Assert
            proxy.Should().NotBeNull();
            var webProxy = proxy as System.Net.WebProxy;
            webProxy.Should().NotBeNull();
            webProxy.BypassList.Should().NotBeNull();
            webProxy.BypassList.Length.Should().BeGreaterThan(0);
        }
    }
}
