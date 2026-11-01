using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Vat.Api.Security;
using Vat.Application.Security;
using Vat.Services.Identity.Infrastructure;
using Vat.Services.Identity.Domain;
namespace Vat.Services.Identity.Controllers;

public sealed record CreateLocalAccountRequest(string Username, string Password, string FullName, string? FullNameBn,
    string Email, string? Phone, string? Role, string[]? Roles, Guid CompanyId, Guid BranchId, string[]? AssignedModuleCodes);
[ApiController]
[Route("api/v1/companies/{companyId:guid}/identity/users")]
public sealed class LocalAccountsController(IdentityDbContext db, IWebHostEnvironment environment, IConfiguration configuration) : ControllerBase
{
    private static object View(LocalAccount u) => new { u.Id, u.Username, u.FullName, u.FullNameBn, u.Email, u.Phone,
        u.CompanyId, u.BranchId, u.Roles, Role = u.Roles.FirstOrDefault(), u.AssignedModuleCodes, u.IsActive, u.CreatedAt,
        CompanyName = "", BranchName = "", AccessibleCompanyIds = new[] { u.CompanyId },
        Permissions = LocalAccountPolicy.PermissionsFor(u.Roles, u.AssignedModuleCodes) };
    [HttpGet]
    [Authorize(Policy = Permissions.IdentityRead)]
    public async Task<IActionResult> List(Guid companyId, CancellationToken ct, [FromQuery] int skip = 0, [FromQuery] int take = 100)
    {
        if (!DevSecurityKey.IsEnabled(environment, configuration)) return NotFound();
        var actor = HttpContext.GetActor(); actor.RequireCompany(companyId);
        if (skip < 0 || skip > 10000 || take < 1 || take > 100) return BadRequest();
        var rows = await db.Set<LocalAccount>().AsNoTracking().Where(u => u.TenantId == actor.TenantId && u.CompanyId == companyId)
            .OrderBy(u => u.Username).Skip(skip).Take(take).ToArrayAsync(ct);
        return Ok(rows.Select(View));
    }
    [HttpPost]
    [Authorize(Policy = Permissions.IdentityManage)]
    public async Task<IActionResult> Create(Guid companyId, CreateLocalAccountRequest request, CancellationToken ct)
    {
        if (!DevSecurityKey.IsEnabled(environment, configuration)) return NotFound();
        var actor = HttpContext.GetActor(); actor.RequireCompany(companyId);
        var username = request.Username?.Trim().ToLowerInvariant() ?? "";
        var roles = (request.Roles ?? (request.Role is null ? [] : [request.Role])).Distinct().ToArray();
        var modules = (request.AssignedModuleCodes ?? []).Distinct().ToArray();
        if (request.CompanyId != companyId || request.BranchId == Guid.Empty ||
            !System.Text.RegularExpressions.Regex.IsMatch(username, @"^[a-z0-9][a-z0-9._@-]{2,99}$") || LocalAccountPolicy.Reserved(username) ||
            string.IsNullOrWhiteSpace(request.FullName) || request.FullName.Length > 200 ||
            (request.FullNameBn?.Length ?? 0) > 200 || (request.Phone?.Length ?? 0) > 50 ||
            string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 8 || request.Password.Length > 128 ||
            string.IsNullOrWhiteSpace(request.Email) || request.Email.Length > 254 || !System.Net.Mail.MailAddress.TryCreate(request.Email, out _) ||
            roles.Length == 0 || roles.Any(r => !LocalAccountPolicy.Roles.ContainsKey(r)) || modules.Any(m => !LocalAccountPolicy.Modules.Contains(m)))
            return BadRequest(new ProblemDetails { Status = 400, Title = "Invalid user details", Detail = "Provide a unique username (3-100 characters), password (8-128 characters), name, email, company, branch, valid roles and modules." });
        if (await db.Set<LocalAccount>().AnyAsync(u => u.Username == username, ct))
            return Conflict(new ProblemDetails { Status = 409, Title = "Username already exists" });
        var account = new LocalAccount { Id = Guid.CreateVersion7(), TenantId = actor.TenantId, CompanyId = companyId,
            BranchId = request.BranchId, Username = username, FullName = request.FullName.Trim(), FullNameBn = request.FullNameBn?.Trim() ?? request.FullName.Trim(),
            Email = request.Email.Trim(), Phone = request.Phone?.Trim() ?? "", Roles = roles, AssignedModuleCodes = modules,
            CreatedAt = DateTimeOffset.UtcNow, CreatedBy = actor.Subject };
        account.PasswordHash = new PasswordHasher<LocalAccount>().HashPassword(account, request.Password);
        db.Add(account);
        db.Add(new SecurityAudit { Id = Guid.CreateVersion7(), TenantId = actor.TenantId, CompanyId = companyId,
            CreatedAt = account.CreatedAt, UpdatedAt = account.CreatedAt, CreatedBy = actor.Subject,
            Action = "LocalAccountCreated", Resource = account.Id.ToString(), OccurredAt = account.CreatedAt,
            CorrelationId = HttpContext.TraceIdentifier, Outcome = "Success" });
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: "23505" })
        { return Conflict(new ProblemDetails { Status = 409, Title = "Username already exists" }); }
        return StatusCode(201, View(account));
    }
}
