using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using EDM.ControlPlane.Api.Services;

namespace EDM.ControlPlane.Api.Middleware
{
    public class BanEnforcementMiddleware
    {
        private readonly RequestDelegate _next;

        public BanEnforcementMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // If request is authenticated, check if user or device is banned
            if (context.User.Identity?.IsAuthenticated == true)
            {
                var banService = context.RequestServices.GetRequiredService<IBanEnforcementService>();
                var authService = context.RequestServices.GetRequiredService<IAuthService>();

                Guid? userId = null;
                var subClaim = context.User.FindFirst(ClaimTypes.NameIdentifier) ?? context.User.FindFirst("sub");
                if (subClaim != null && Guid.TryParse(subClaim.Value, out var uId))
                {
                    userId = uId;
                }

                Guid? installId = null;
                var installClaim = context.User.FindFirst("installation_id");
                if (installClaim != null && Guid.TryParse(installClaim.Value, out var iId))
                {
                    installId = iId;
                }

                string? clientIp = context.Connection.RemoteIpAddress?.ToString();

                if (await banService.IsRequestBannedAsync(userId, installId, clientIp))
                {
                    // Revoke current session if session_id is present
                    var sessionClaim = context.User.FindFirst("session_id");
                    if (sessionClaim != null && Guid.TryParse(sessionClaim.Value, out var sId))
                    {
                        await authService.LogoutAsync(sId, "ACCOUNT_BANNED");
                    }

                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsync("{\"error\":\"ACCESS_DENIED\",\"message\":\"Access is forbidden due to an active ban or suspension.\"}");
                    return;
                }

                // Verify session validity
                var sessClaim = context.User.FindFirst("session_id");
                if (sessClaim != null && Guid.TryParse(sessClaim.Value, out var sessionId))
                {
                    var session = await authService.ValidateSessionAsync(sessionId);
                    if (session == null)
                    {
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        context.Response.ContentType = "application/json";
                        await context.Response.WriteAsync("{\"error\":\"SESSION_REVOKED\",\"message\":\"Session is invalid, expired, or revoked.\"}");
                        return;
                    }
                }
            }

            await _next(context);
        }
    }
}
