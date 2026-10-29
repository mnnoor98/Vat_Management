using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Vat.Domain;

namespace Vat.Api.Errors;

public sealed class ApiExceptionHandler(IProblemDetailsService problems, ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        var (status, code, detail) = exception switch
        {
            DomainException e => (e.Status, e.Code, e.Message),
            BadHttpRequestException => (400, "invalid_request", "The request body or parameters are invalid."),
            DbUpdateConcurrencyException => (409, "version_conflict", "The resource changed. Reload it before retrying."),
            DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } }
                => (409, "duplicate_reference", "A resource with this business reference already exists."),
            DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.ForeignKeyViolation } }
                => (409, "reference_conflict", "A referenced resource is unavailable."),
            NpgsqlException => (503, "database_unavailable", "The database is temporarily unavailable."),
            _ => (500, "internal_error", "An unexpected error occurred.")
        };
        if (status >= 500)
            logger.LogError(exception, "Request failed with {ErrorCode}; correlation {CorrelationId}", code, context.TraceIdentifier);
        context.Response.StatusCode = status;
        return await problems.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = new()
            {
                Status = status,
                Title = code,
                Detail = detail,
                Extensions = { ["code"] = code, ["correlationId"] = context.TraceIdentifier }
            }
        });
    }
}
