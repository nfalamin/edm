using System;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace EDM.ControlPlane.Api.Middleware
{
    public class GlobalExceptionHandlingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<GlobalExceptionHandlingMiddleware> _logger;

        public GlobalExceptionHandlingMiddleware(RequestDelegate next, ILogger<GlobalExceptionHandlingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                string correlationId = context.TraceIdentifier;
                _logger.LogError(ex, "Unhandled exception caught by GlobalExceptionHandlingMiddleware. CorrelationId: {CorrelationId}", correlationId);

                await HandleExceptionAsync(context, ex, correlationId);
            }
        }

        private static Task HandleExceptionAsync(HttpContext context, Exception exception, string correlationId)
        {
            context.Response.ContentType = "application/json";

            var statusCode = exception switch
            {
                ArgumentException => HttpStatusCode.BadRequest,
                InvalidOperationException => HttpStatusCode.BadRequest,
                UnauthorizedAccessException => HttpStatusCode.Unauthorized,
                KeyNotFoundException => HttpStatusCode.NotFound,
                _ => HttpStatusCode.InternalServerError
            };

            context.Response.StatusCode = (int)statusCode;

            string errorCode = exception switch
            {
                ArgumentException => "INVALID_ARGUMENT",
                InvalidOperationException => "INVALID_OPERATION",
                UnauthorizedAccessException => "UNAUTHORIZED",
                KeyNotFoundException => "RESOURCE_NOT_FOUND",
                _ => "INTERNAL_SERVER_ERROR"
            };

            string message = statusCode == HttpStatusCode.InternalServerError
                ? "An unexpected internal error occurred. Please provide the correlation ID if reporting this issue."
                : exception.Message;

            var errorResponse = new
            {
                error = errorCode,
                message,
                correlationId,
                timestampUtc = DateTime.UtcNow
            };

            var json = JsonSerializer.Serialize(errorResponse);
            return context.Response.WriteAsync(json);
        }
    }
}
