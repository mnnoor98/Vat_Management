using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vat.Api.Security;
using Vat.Application.Security;
using Vat.Application;
using Vat.Application.Contracts;
using Vat.Infrastructure.Persistence;

namespace Vat.Coordinated.Company.Controllers;

public static class CompanyApiCommands
{
    public static async Task<IActionResult> Execute(HttpContext context, ICommandExecutor commands,
        Guid companyId, string operation, object request, Func<Task<CommandOutcome>> action, CancellationToken ct)
    {
        var response = await commands.Execute(context.GetActor(), companyId, operation,
            context.Request.Headers["Idempotency-Key"].ToString(), request, action, ct);
        if (response.Location is not null) context.Response.Headers.Location = response.Location;
        context.Response.Headers["Idempotency-Replayed"] = response.Replayed ? "true" : "false";
        return new ContentResult
        {
            StatusCode = response.StatusCode,
            ContentType = "application/json",
            Content = response.Json
        };
    }
}

[ApiController]
[Tags("Company")]
public sealed class CompanyController : ControllerBase
{
    [HttpPost("/api/v1/companies", Name = "CreateCompany")]
    [Authorize(Policy = Permissions.CompanyManage)]
    [ProducesResponseType(typeof(CompanyResponse), 201)]
    public async Task<IActionResult> CreateCompany([FromBody] CreateCompanyRequest request, [FromServices] CompanyService service, [FromServices] ICommandExecutor commands, CancellationToken ct)
    {
        var ctx = HttpContext;
        return await CompanyApiCommands.Execute(ctx, commands, request.Id, "company.create", request,
            () => service.Create(ctx.GetActor(), request, ctx.TraceIdentifier), ct);
    }

    [HttpGet("/api/v1/companies/{companyId:guid}", Name = "GetCompany")]
    [Authorize(Policy = Permissions.CompanyRead)]
    public async Task<IActionResult> GetCompany([FromRoute] Guid companyId, [FromServices] CompanyService service, CancellationToken ct)
    {
        var ctx = HttpContext;
        return Ok(await service.Get(ctx.GetActor(), companyId, ct));
    }

    [HttpPost("/api/v1/companies/{companyId:guid}/branches", Name = "CreateBranch")]
    [Authorize(Policy = Permissions.CompanyManage)]
    [ProducesResponseType(typeof(BranchResponse), 201)]
    public async Task<IActionResult> CreateBranch([FromRoute] Guid companyId, [FromBody] CreateBranchRequest request, [FromServices] CompanyService service, [FromServices] ICommandExecutor commands, CancellationToken ct)
    {
        var ctx = HttpContext;
        return await CompanyApiCommands.Execute(ctx, commands, companyId, "branch.create", request,
            () => service.CreateBranch(ctx.GetActor(), companyId, request, ctx.TraceIdentifier, ct), ct);
    }

    [HttpGet("/api/v1/companies/{companyId:guid}/branches/{branchId:guid}", Name = "GetBranch")]
    [Authorize(Policy = Permissions.CompanyRead)]
    public async Task<IActionResult> GetBranch([FromRoute] Guid companyId, [FromRoute] Guid branchId, [FromServices] CompanyService service, CancellationToken ct)
    {
        var ctx = HttpContext;
        return Ok(await service.GetBranch(ctx.GetActor(), companyId, branchId, ct));
    }

    [HttpGet("/api/v1/companies/{companyId:guid}/branches", Name = "ListBranches")]
    [Authorize(Policy = Permissions.CompanyRead)]
    public async Task<IActionResult> ListBranches([FromRoute] Guid companyId, [FromServices] CompanyService service, CancellationToken ct)
    {
        var ctx = HttpContext;
        return Ok(await service.Branches(ctx.GetActor(), companyId, ct));
    }
}
