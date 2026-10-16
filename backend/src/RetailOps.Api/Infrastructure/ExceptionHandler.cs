using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using RetailOps.Application.Common;
using RetailOps.Domain.Common;

namespace RetailOps.Api.Infrastructure;

/// <summary>Maps application/domain exceptions to RFC 9457 problem details so every client gets one error shape.</summary>
public sealed class ExceptionHandler(IProblemDetailsService problemDetails, ILogger<ExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        var problem = exception switch
        {
            ValidationException v => new ValidationProblemDetails(
                v.Errors.GroupBy(e => ToCamel(e.PropertyName))
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray()))
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Validation failed"
            },
            NotFoundException => Problem(StatusCodes.Status404NotFound, "Not found", exception.Message),
            UnauthorizedException => Problem(StatusCodes.Status401Unauthorized, "Unauthorized", exception.Message),
            ForbiddenException => Problem(StatusCodes.Status403Forbidden, "Forbidden", exception.Message),
            ConflictException => Problem(StatusCodes.Status409Conflict, "Conflict", exception.Message),
            DomainException d => Problem(StatusCodes.Status422UnprocessableEntity, "Business rule violated", d.Message, d.Code),
            _ => null
        };

        if (problem is null)
        {
            logger.LogError(exception, "Unhandled exception on {Method} {Path}", context.Request.Method, context.Request.Path);
            problem = Problem(StatusCodes.Status500InternalServerError, "Unexpected error", "Something went wrong. The error was logged.");
        }

        context.Response.StatusCode = problem.Status!.Value;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext { HttpContext = context, ProblemDetails = problem, Exception = exception });
    }

    private static ProblemDetails Problem(int status, string title, string detail, string? code = null)
    {
        var p = new ProblemDetails { Status = status, Title = title, Detail = detail };
        if (code is not null) p.Extensions["code"] = code;
        return p;
    }

    private static string ToCamel(string name) =>
        string.IsNullOrEmpty(name) ? name : char.ToLowerInvariant(name[0]) + name[1..];
}
