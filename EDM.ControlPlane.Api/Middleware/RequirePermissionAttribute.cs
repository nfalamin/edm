using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using EDM.ControlPlane.Api.Services;

namespace EDM.ControlPlane.Api.Middleware
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true)]
    public class RequirePermissionAttribute : TypeFilterAttribute
    {
        public RequirePermissionAttribute(string permissionCode) : base(typeof(PermissionAuthorizationFilter))
        {
            Arguments = new object[] { permissionCode };
        }
    }

    public class PermissionAuthorizationFilter : IAsyncAuthorizationFilter
    {
        private readonly string _permissionCode;

        public PermissionAuthorizationFilter(string permissionCode)
        {
            _permissionCode = permissionCode ?? throw new ArgumentNullException(nameof(permissionCode));
        }

        public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            var user = context.HttpContext.User;
            if (user?.Identity == null || !user.Identity.IsAuthenticated)
            {
                context.Result = new UnauthorizedObjectResult(new
                {
                    error = "UNAUTHORIZED",
                    message = "Authentication is required to access this administrative resource.",
                    timestampUtc = DateTime.UtcNow
                });
                return;
            }

            var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier) ?? user.FindFirst("sub");
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
            {
                context.Result = new UnauthorizedObjectResult(new
                {
                    error = "INVALID_TOKEN_SUBJECT",
                    message = "User context could not be determined from the provided token.",
                    timestampUtc = DateTime.UtcNow
                });
                return;
            }

            var permissionService = context.HttpContext.RequestServices.GetRequiredService<IPermissionService>();
            bool hasPerm = await permissionService.HasPermissionAsync(userId, _permissionCode);

            if (!hasPerm)
            {
                var auditLogger = context.HttpContext.RequestServices.GetRequiredService<IAuditLoggingService>();
                await auditLogger.LogActionAsync(
                    actorId: userId,
                    actorUsername: user.Identity.Name ?? "UNKNOWN",
                    action: "UNAUTHORIZED_ACCESS_DENIED",
                    targetEntity: "Permission",
                    targetId: _permissionCode,
                    detailsJson: $"{{\"requiredPermission\":\"{_permissionCode}\",\"path\":\"{context.HttpContext.Request.Path}\"}}",
                    correlationId: context.HttpContext.TraceIdentifier,
                    resultStatus: "DENIED",
                    rawIpAddress: context.HttpContext.Connection.RemoteIpAddress?.ToString());

                context.Result = new ObjectResult(new
                {
                    error = "FORBIDDEN_INSUFFICIENT_PERMISSIONS",
                    message = $"You lack the required permission '{_permissionCode}' to perform this action.",
                    requiredPermission = _permissionCode,
                    timestampUtc = DateTime.UtcNow
                })
                {
                    StatusCode = 403
                };
            }
        }
    }
}
