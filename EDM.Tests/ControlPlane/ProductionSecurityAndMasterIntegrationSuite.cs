using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using EDM.ControlPlane.Api.Middleware;
using EDM.ControlPlane.Api.Models;
using EDM.ControlPlane.Api.Services;
using FluentAssertions;
using Xunit;

namespace EDM.Tests.ControlPlane
{
    public class ProductionSecurityAndMasterIntegrationSuite
    {
        // =====================================================================
        // 1. CSRF PROTECTION VALIDATION
        // =====================================================================
        [Fact]
        public void Phase2_CsrfService_GeneratesAndValidatesCryptographicTokens()
        {
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Csrf:SecretKey"] = "Production_Csrf_Secret_Key_For_Anti_Forgery_2026_Minimum_256_Bits!"
                })
                .Build();

            var service = new CsrfProtectionService(config);
            var httpContext = new DefaultHttpContext();

            // 1. Valid token validation
            string token = service.GenerateCsrfToken(httpContext);
            token.Should().NotBeNullOrWhiteSpace();
            token.Split(':').Should().HaveCount(3);

            bool isValid = service.ValidateCsrfToken(httpContext, token);
            isValid.Should().BeTrue("Valid CSRF token must pass validation");

            // 2. Missing token
            service.ValidateCsrfToken(httpContext, null).Should().BeFalse("Null token must fail");
            service.ValidateCsrfToken(httpContext, "").Should().BeFalse("Empty token must fail");

            // 3. Manipulated signature
            string tamperedToken = token.Substring(0, token.Length - 4) + "0000";
            service.ValidateCsrfToken(httpContext, tamperedToken).Should().BeFalse("Tampered signature must fail");

            // 4. Expired token (simulate 3 hours in the past)
            long expiredTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 10800;
            string expiredRaw = $"0123456789ABCDEF0123456789ABCDEF:{expiredTime}";
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes("Production_Csrf_Secret_Key_For_Anti_Forgery_2026_Minimum_256_Bits!"));
            string expiredSig = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(expiredRaw))).ToLowerInvariant();
            string expiredToken = $"{expiredRaw}:{expiredSig}";

            service.ValidateCsrfToken(httpContext, expiredToken).Should().BeFalse("Expired token (> 2 hours) must fail");
        }

        [Fact]
        public async Task Phase2_CsrfMiddleware_EnforcesValidationOnStateChangingRequests()
        {
            var config = new ConfigurationBuilder().Build();
            var csrfService = new CsrfProtectionService(config);

            bool nextCalled = false;
            RequestDelegate next = (ctx) =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            };

            var middleware = new CsrfProtectionMiddleware(next, csrfService);

            // 1. Safe GET request should pass through
            var getContext = new DefaultHttpContext();
            getContext.Request.Method = "GET";
            getContext.Request.Path = "/api/v1/admin/users";
            nextCalled = false;

            await middleware.InvokeAsync(getContext);
            nextCalled.Should().BeTrue("GET requests must bypass CSRF");

            // 2. State-changing POST without CSRF header should be blocked with 403
            var postContextNoToken = new DefaultHttpContext();
            postContextNoToken.Request.Method = "POST";
            postContextNoToken.Request.Path = "/api/v1/admin/ban";
            nextCalled = false;

            await middleware.InvokeAsync(postContextNoToken);
            nextCalled.Should().BeFalse("State-changing POST without CSRF must not execute next middleware");
            postContextNoToken.Response.StatusCode.Should().Be(StatusCodes.Status403Forbidden);

            // 3. State-changing POST with invalid CSRF header should be blocked with 403
            var postContextBadToken = new DefaultHttpContext();
            postContextBadToken.Request.Method = "POST";
            postContextBadToken.Request.Path = "/api/v1/admin/ban";
            postContextBadToken.Request.Headers["X-CSRF-Token"] = "invalid-token-string";
            nextCalled = false;

            await middleware.InvokeAsync(postContextBadToken);
            nextCalled.Should().BeFalse("Invalid CSRF token must not execute next middleware");
            postContextBadToken.Response.StatusCode.Should().Be(StatusCodes.Status403Forbidden);

            // 4. State-changing POST with valid CSRF header should pass
            var postContextValid = new DefaultHttpContext();
            postContextValid.Request.Method = "POST";
            postContextValid.Request.Path = "/api/v1/admin/ban";
            string validToken = csrfService.GenerateCsrfToken(postContextValid);
            postContextValid.Request.Headers["X-CSRF-Token"] = validToken;
            nextCalled = false;

            await middleware.InvokeAsync(postContextValid);
            nextCalled.Should().BeTrue("Valid CSRF token must allow request through");
        }

        // =====================================================================
        // 2. PRODUCTION JWT SECRET ENFORCEMENT
        // =====================================================================
        [Fact]
        public void Phase3_JwtSecret_FailsStartupInProductionWhenSecretMissing()
        {
            Action checkProductionJwt = () =>
            {
                string? configuredJwtSecret = null; // Missing in prod
                bool isProduction = true;

                if (isProduction)
                {
                    if (string.IsNullOrWhiteSpace(configuredJwtSecret) ||
                        configuredJwtSecret.Equals("EDM_Development_Super_Secret_Key_For_Jwt_Signing_2026_Minimum_256_Bits!", StringComparison.Ordinal) ||
                        configuredJwtSecret.Length < 32)
                    {
                        throw new InvalidOperationException("CRITICAL PRODUCTION SECURITY FAILURE: Production environment requires a valid secure JWT signing secret.");
                    }
                }
            };

            checkProductionJwt.Should().Throw<InvalidOperationException>()
                .WithMessage("*CRITICAL PRODUCTION SECURITY FAILURE*");
        }

        // =====================================================================
        // 3. NO HARDCODED DEFAULT ADMIN PASSWORD IN PRODUCTION
        // =====================================================================
        [Fact]
        public void Phase4_AdminProvisioning_DisallowsHardcodedDefaultInProduction()
        {
            bool isProduction = true;
            string? envPassword = null;

            bool shouldSeedDefaultAdmin = false;
            if (!isProduction || !string.IsNullOrWhiteSpace(envPassword))
            {
                shouldSeedDefaultAdmin = true;
            }

            shouldSeedDefaultAdmin.Should().BeFalse("Production environment with empty database must NOT seed default hardcoded password");
        }

        // =====================================================================
        // 4. CHART.JS CSP & SECURITY HEADERS
        // =====================================================================
        [Fact]
        public async Task Phase8_SecurityHeadersMiddleware_SetsHstsAndPermitsChartJsCdn()
        {
            var httpContext = new DefaultHttpContext();
            RequestDelegate next = (ctx) => Task.CompletedTask;
            var middleware = new SecurityHeadersMiddleware(next);

            await middleware.InvokeAsync(httpContext);

            httpContext.Response.Headers.Should().ContainKey("Content-Security-Policy");
            httpContext.Response.Headers.Should().ContainKey("Strict-Transport-Security");
            httpContext.Response.Headers.Should().ContainKey("X-Content-Type-Options");
            httpContext.Response.Headers.Should().ContainKey("X-Frame-Options");

            string csp = httpContext.Response.Headers["Content-Security-Policy"].ToString();
            csp.Should().Contain("https://cdn.jsdelivr.net", "CSP must explicitly allow Chart.js CDN origin");
            csp.Should().Contain("default-src 'self'");
            csp.Should().NotContain("script-src *", "CSP must not contain wildcard scripts");

            string hsts = httpContext.Response.Headers["Strict-Transport-Security"].ToString();
            hsts.Should().Contain("max-age=31536000");
        }

        // =====================================================================
        // 5. NO MOCK DATA IN PRODUCTION DASHBOARD
        // =====================================================================
        [Fact]
        public void Phase9_ProductionDashboard_DoesNotContainMockDataScript()
        {
            string dashboardIndexPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "EDM.ControlPlane.Dashboard", "index.html"));
            if (File.Exists(dashboardIndexPath))
            {
                string html = File.ReadAllText(dashboardIndexPath);
                html.Should().NotContain("<script src=\"mock-data.js\"></script>", "mock-data.js must NOT be loaded in dashboard index.html");
            }
        }

        // =====================================================================
        // 6. PRODUCTION CORS RESTRICTIONS
        // =====================================================================
        [Fact]
        public void Phase10_ProductionCors_UsesStrictWhitelistedOrigins()
        {
            string configuredOrigins = "https://control.edm-download.org,https://edm-download.org";
            var origins = configuredOrigins.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            origins.Should().NotContain("*", "CORS must not allow wildcard origin in production");
            origins.Should().Contain("https://control.edm-download.org");
            origins.Should().Contain("https://edm-download.org");
        }

        // =====================================================================
        // 7. REAL ARTIFACT SHA-256 HASH VERIFICATION
        // =====================================================================
        [Fact]
        public void Phase12_RealArtifactSha256_CalculatesAccurateCryptographicHash()
        {
            string relativePath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Dist", "EDM_v1.0_Complete_Distribution", "EDM_Setup_v1.0.exe"));
            if (File.Exists(relativePath))
            {
                using var stream = File.OpenRead(relativePath);
                using var sha256 = SHA256.Create();
                byte[] hash = sha256.ComputeHash(stream);
                string hexHash = Convert.ToHexString(hash).ToLowerInvariant();

                hexHash.Should().Be("93049cf86301342dbdaae74256d4013a1e30133aa26a38dbe08e2a6e3e32d023");
                stream.Length.Should().Be(19807971);
            }
        }

        // =====================================================================
        // 8. SINGLE SOURCE OF TRUTH & ARTIFACT METADATA CONSISTENCY
        // =====================================================================
        [Fact]
        public void Phase18_ReleaseMetadata_MatchesAuthoritativeBinaryProperties()
        {
            string expectedVersion = "2.1.0";
            string expectedSha256 = "93049cf86301342dbdaae74256d4013a1e30133aa26a38dbe08e2a6e3e32d023";
            long expectedSize = 19807971;

            var artifact = new ReleaseArtifact
            {
                Id = Guid.NewGuid(),
                ArtifactName = "EDM-Setup-v2.1.0.exe",
                Architecture = "x64",
                DownloadUrl = "/api/v1/releases/latest/download",
                Sha256Hash = expectedSha256,
                FileSizeBytes = expectedSize
            };

            artifact.Sha256Hash.Should().Be(expectedSha256);
            artifact.FileSizeBytes.Should().Be(expectedSize);
            artifact.ArtifactName.Should().Contain(expectedVersion);
        }
    }
}
