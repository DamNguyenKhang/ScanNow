using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Diagnostics;
using FluentValidation;

namespace ScanNow.Web.ExceptionHandler
{
    public class ValidationExceptionHandler(IProblemDetailsService problemDetailsService, ILogger<ValidationExceptionHandler> logger) : IExceptionHandler
    {
        public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
        {
            if (exception is not ValidationException validationException)
            {
                return false;
            }

            httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;

            var context = new ProblemDetailsContext
            {
                HttpContext = httpContext,
                Exception = exception,
                ProblemDetails = new ProblemDetails
                {
                    Detail = "One or more validation errors occured",
                    Status = httpContext.Response.StatusCode
                }
            };

            var errors = validationException.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(
                    g => g.Key.ToLowerInvariant(),
                    g => g.Select(e => e.ErrorMessage).ToArray()
                );
            context.ProblemDetails.Extensions.Add("errors", errors);
            return await problemDetailsService.TryWriteAsync(context);
        }
    }
}
