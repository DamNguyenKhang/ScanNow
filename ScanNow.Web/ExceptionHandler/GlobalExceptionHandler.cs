using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using ScanNow.Application.Exceptions;
using ScanNow.Domain.Exceptions;

namespace ScanNow.Web.ExceptionHandler
{
    public sealed class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
    {
        public async ValueTask<bool> TryHandleAsync(
            HttpContext httpContext,
            Exception exception,
            CancellationToken cancellationToken)
        {
            // Log based on severity
            LogException(exception);

            // Set status code
            httpContext.Response.StatusCode = exception switch
            {
                BaseException baseEx => (int)baseEx.StatusCode,
                _ => StatusCodes.Status500InternalServerError
            };

            // Create problem details
            var problemDetails = CreateProblemDetails(httpContext, exception);

            // Write response
            return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
            {
                HttpContext = httpContext,
                Exception = exception,
                ProblemDetails = problemDetails
            });
        }

        private void LogException(Exception exception)
        {
            switch (exception)
            {
                case ValidationException:
                case NotFoundException:
                    // Client errors - log as warning
                    logger.LogWarning(exception, "Client error occurred: {Message}", exception.Message);
                    break;

                case BusinessRuleException:
                case ConflictException:
                    // Business logic errors - log as warning with more detail
                    logger.LogWarning(exception, "Business rule violation: {Message}", exception.Message);
                    break;

                case UnauthorizedException:
                case ForbiddenException:
                    // Auth errors - log as warning
                    logger.LogWarning(exception, "Authentication/Authorization error: {Message}", exception.Message);
                    break;

                case ExternalServiceException externalEx:
                    // External service errors - log as error
                    logger.LogError(exception, "External service '{ServiceName}' failed: {Message}",
                        externalEx.ServiceName, exception.Message);
                    break;

                case BaseException:
                    // Other custom exceptions - log as error
                    logger.LogError(exception, "Application error occurred: {Message}", exception.Message);
                    break;

                default:
                    // Unhandled exceptions - log as critical
                    logger.LogCritical(exception, "Unhandled exception occurred: {Message}", exception.Message);
                    break;
            }
        }

        private static ProblemDetails CreateProblemDetails(HttpContext httpContext, Exception exception)
        {
            var statusCode = httpContext.Response.StatusCode;

            var problemDetails = new ProblemDetails
            {
                Status = statusCode,
                Title = GetTitle(exception, statusCode),
                Detail = GetDetail(exception),
                Type = GetType(statusCode),
                Instance = httpContext.Request.Path
            };

            // Add additional data for specific exceptions
            switch (exception)
            {
                case ValidationException validationEx when validationEx.Errors != null:
                    problemDetails.Extensions["errors"] = validationEx.Errors;
                    break;

                case ExternalServiceException externalEx:
                    problemDetails.Extensions["service"] = externalEx.ServiceName;
                    break;
            }

            // Add trace ID for debugging
            problemDetails.Extensions["traceId"] = httpContext.TraceIdentifier;

            return problemDetails;
        }

        private static string GetTitle(Exception exception, int statusCode)
        {
            return exception switch
            {
                ValidationException => "Validation Error",
                NotFoundException => "Resource Not Found",
                BusinessRuleException => "Business Rule Violation",
                ConflictException => "Conflict",
                UnauthorizedException => "Unauthorized",
                ForbiddenException => "Forbidden",
                ExternalServiceException => "External Service Error",
                BaseException => "Application Error",
                _ => "An error occurred"
            };
        }

        private static string GetDetail(Exception exception)
        {
            return exception switch
            {
                // For production, you might want to hide detailed error messages
                // But for development, showing the actual message is helpful
                BaseException => exception.Message,
                _ => "An unexpected error occurred. Please contact support if the problem persists."
            };
        }

        private static string GetType(int statusCode)
        {
            // RFC 7807 problem type URIs
            return statusCode switch
            {
                400 => "https://tools.ietf.org/html/rfc7231#section-6.5.1",
                401 => "https://tools.ietf.org/html/rfc7235#section-3.1",
                403 => "https://tools.ietf.org/html/rfc7231#section-6.5.3",
                404 => "https://tools.ietf.org/html/rfc7231#section-6.5.4",
                409 => "https://tools.ietf.org/html/rfc7231#section-6.5.8",
                422 => "https://tools.ietf.org/html/rfc4918#section-11.2",
                500 => "https://tools.ietf.org/html/rfc7231#section-6.6.1",
                502 => "https://tools.ietf.org/html/rfc7231#section-6.6.3",
                _ => "https://tools.ietf.org/html/rfc7231#section-6.6.1"
            };
        }
    }

}
