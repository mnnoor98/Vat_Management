using Vat.Api.Security;
using Vat.Application;

namespace Vat.Hosting;

public static class ApiCommands
{
    public static async Task<IResult> Execute(HttpContext context, ICommandExecutor commands,
        Guid companyId, string operation, object request, Func<Task<CommandOutcome>> action, CancellationToken ct)
    {
        var response = await commands.Execute(context.GetActor(), companyId, operation,
            context.Request.Headers["Idempotency-Key"].ToString(), request, action, ct);
        if (response.Location is not null) context.Response.Headers.Location = response.Location;
        context.Response.Headers["Idempotency-Replayed"] = response.Replayed ? "true" : "false";
        return Results.Content(response.Json, "application/json", statusCode: response.StatusCode);
    }
}
