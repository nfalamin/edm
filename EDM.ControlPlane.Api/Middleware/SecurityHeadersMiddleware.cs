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
            context.Response.Headers["X-Frame-Options"] = "SAMEORIGIN";
            context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
            context.Response.Headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
            
            if (context.Request.IsHttps)
            {
                context.Response.Headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";
            }

            // Prevent caching of sensitive API responses
            if (context.Request.Path.StartsWithSegments("/api"))
            {
                context.Response.Headers["Cache-Control"] = "no-store, no-cache, must-revalidate, max-age=0";
                context.Response.Headers["Pragma"] = "no-cache";
            }
            
            // Content Security Policy permitting local assets, Lucide CDN, Chart.js CDN, and Google Auth
            context.Response.Headers["Content-Security-Policy"] = 
                "default-src 'self'; " +
                "script-src 'self' 'unsafe-inline' https://unpkg.com https://cdn.jsdelivr.net https://accounts.google.com; " +
                "style-src 'self' 'unsafe-inline' https://fonts.googleapis.com; " +
                "img-src 'self' data: https:; " +
                "font-src 'self' https://fonts.gstatic.com data:; " +
                "connect-src 'self' https://cdn.jsdelivr.net https://accounts.google.com; " +
                "object-src 'none'; " +
                "base-uri 'self'; " +
                "form-action 'self'; " +
                "frame-ancestors 'self';";

            await _next(context);
        }
    }
}
