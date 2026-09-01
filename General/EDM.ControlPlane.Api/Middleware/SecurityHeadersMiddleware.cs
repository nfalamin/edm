using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace EDM.ControlPlane.Api.Middleware
{
    public class SecurityHeadersMiddleware
    {
        private readonly RequestDelegate _next;

        public SecurityHeadersMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            context.Response.Headers["X-Content-Type-Options"] = "nosniff";
            context.Response.Headers["X-Frame-Options"] = "DENY";
            context.Response.Headers["X-XSS-Protection"] = "1; mode=block";
            context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
            context.Response.Headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
            context.Response.Headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";
            
            // Content Security Policy permitting local assets, Lucide CDN, Chart.js CDN, and Google Auth
            context.Response.Headers["Content-Security-Policy"] = 
                "default-src 'self'; " +
                "script-src 'self' 'unsafe-inline' https://unpkg.com https://cdn.jsdelivr.net https://accounts.google.com; " +
                "style-src 'self' 'unsafe-inline' https://fonts.googleapis.com; " +
                "img-src 'self' data: https:; " +
                "font-src 'self' https://fonts.gstatic.com data:; " +
                "connect-src 'self' https://cdn.jsdelivr.net https://accounts.google.com; " +
                "frame-ancestors 'none';";

            await _next(context);
        }
    }
}
