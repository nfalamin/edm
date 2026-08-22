using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using EDM.ControlPlane.Api.Services;

namespace EDM.ControlPlane.Api.Middleware
{
    /// <summary>
    /// Cryptographic Anti-Forgery CSRF Protection Middleware.
    /// Intercepts and strictly validates CSRF tokens on state-changing HTTP operations (POST, PUT, PATCH, DELETE).
    /// </summary>
    public class CsrfProtectionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ICsrfProtectionService _csrfService;

        private static readonly HashSet<string> SafeMethods = new(StringComparer.OrdinalIgnoreCase)
        {
            "GET", "HEAD", "OPTIONS", "TRACE"
        };

        private static readonly string[] PublicAuthBypassEndpoints = new[]
        {
            "/api/v1/auth/login",
            "/api/v1/auth/register",
            "/api/v1/auth/setup-initial-admin",
            "/api/v1/auth/refresh",
            "/api/v1/auth/google",
            "/api/v1/auth/google/login",
            "/api/v1/auth/firebase",
            "/api/v1/auth/firebase/login",
            "/api/v1/auth/passkey/login-options",
            "/api/v1/auth/passkey/login-verify",
            "/api/v1/auth/passkey/register-options",
            "/api/v1/auth/passkey/register-verify",
            "/api/v1/auth/2fa/verify",
            "/api/v1/auth/recovery-email/request",
            "/api/v1/auth/recovery-email/confirm",
            "/api/v1/auth/forgot-password",
            "/api/v1/auth/reset-password",
            "/api/v1/auth/csrf-token",
            "/api/v1/support/tickets",
            "/api/v1/entitlements/sync",
            "/api/v1/licenses/activate",
            "/api/v1/licenses/verify",
            "/api/v1/licenses/deactivate",
            "/api/v1/licenses/heartbeat",
            "/api/v1/releases/check",
            "/api/v1/updates/check",
            "/api/v1/health",
            "/api/v1/telemetry",
            "/api/v1/telemetry/event",
            "/api/v1/analytics/event",
            "/api/v1/analytics/events"
        };

        public CsrfProtectionMiddleware(RequestDelegate next, ICsrfProtectionService csrfService)
        {
            _next = next ?? throw new ArgumentNullException(nameof(next));
            _csrfService = csrfService ?? throw new ArgumentNullException(nameof(csrfService));
        }

        public async Task InvokeAsync(HttpContext context)
        {
            string method = context.Request.Method;

            // 1. Safe HTTP methods bypass CSRF validation
            if (SafeMethods.Contains(method))
            {
                await _next(context);
                return;
            }

            string path = context.Request.Path.Value ?? string.Empty;

            // 2. Non-API paths (e.g. static assets, frontend files) bypass
            if (!path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase))
            {
                await _next(context);
                return;
            }

            // 3. Public login / register / recovery / license activation endpoints bypass
            foreach (var bypass in PublicAuthBypassEndpoints)
            {
                if (path.Equals(bypass, StringComparison.OrdinalIgnoreCase))
                {
                    await _next(context);
                    return;
                }
            }

            // 4. API clients with explicit Authorization Bearer token bypass CSRF
            string? authHeader = context.Request.Headers.Authorization.ToString();
            if (!string.IsNullOrWhiteSpace(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                await _next(context);
                return;
            }

            // 5. Extract CSRF token from request headers
            string? token = context.Request.Headers["X-CSRF-Token"].ToString();
            if (string.IsNullOrWhiteSpace(token))
            {
                token = context.Request.Headers["X-XSRF-Token"].ToString();
            }

            // 6. Validate CSRF token
            if (string.IsNullOrWhiteSpace(token) || !_csrfService.ValidateCsrfToken(context, token))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                context.Response.ContentType = "application/json";

                var errorResponse = new
                {
                    success = false,
                    error = "CSRF_VALIDATION_FAILED",
                    message = "Invalid or missing Anti-Forgery CSRF token. Please include a valid X-CSRF-Token header on state-changing operations."
                };

                await context.Response.WriteAsync(JsonSerializer.Serialize(errorResponse));
                return;
            }

            await _next(context);
        }
    }
}
