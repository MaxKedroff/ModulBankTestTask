using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace CandidateService.API.Middlewares
{
    public class GlobalExceptionHandler : IExceptionHandler
    {
        private readonly ILogger<GlobalExceptionHandler> _logger;

        public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
        {
            _logger = logger;
        }

        public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
        {
            var problemDetails = new ProblemDetails
            {
                Instance = httpContext.Request.Path
            };

            switch (exception)
            {
                case ConflictException conflict:
                    problemDetails.Title = "Conflict";
                    problemDetails.Status = StatusCodes.Status409Conflict;
                    problemDetails.Detail = conflict.Message;
                    break;
                case NotFoundException notFound:
                    problemDetails.Title = "Not Found";
                    problemDetails.Status = StatusCodes.Status404NotFound;
                    problemDetails.Detail = notFound.Message;
                    break;
                case ValidationException validation:
                    problemDetails.Title = "Validation Error";
                    problemDetails.Status = StatusCodes.Status400BadRequest;
                    problemDetails.Detail = validation.Message;
                    break;
                default:
                    problemDetails.Title = "Internal Server Error";
                    problemDetails.Status = StatusCodes.Status500InternalServerError;
                    problemDetails.Detail = "An unexpected error occurred";
                    _logger.LogError(exception, "Unhandled exception");
                    break;
            }

            httpContext.Response.StatusCode = problemDetails.Status.Value;
            await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);
            return true;
        }
    }
}
