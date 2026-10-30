using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Vat.Api.Security;
using Vat.Application.Security;

namespace Vat.Services.Identity.Controllers;

public sealed record LoginRequest(string Username, string Password);

public sealed record UserProfileDto(
    Guid Id,
    string Username,
    string DisplayName,
    Guid CompanyId,
    Guid TenantId,
    string[] Roles,
    string[] Permissions
);

public sealed record LoginResponse(
    string AccessToken,
    string TokenType,
    int ExpiresIn,
    UserProfileDto User
);

[ApiController]
[Tags("Authentication")]
[Route("api/v1/auth")]
public sealed class AuthController : ControllerBase
{
    private static readonly Guid TenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid CompanyId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [HttpPost("login")]
    [AllowAnonymous]
    public IActionResult Login([FromBody] LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request?.Username) || string.IsNullOrWhiteSpace(request?.Password))
        {
            return BadRequest(new ProblemDetails
            {
                Status = 400,
                Title = "invalid_credentials",
                Detail = "Username and password are required."
            });
        }

        var uname = request.Username.Trim().ToLowerInvariant();
        var pwd = request.Password;

        // Supported seeded accounts: admin, maker, checker, auditor
        string displayName;
        string[] roles;
        string[] permissions;
        Guid userId;

        if (uname == "admin" && (pwd == "Admin@123456" || pwd == "admin123" || pwd == "123456"))
        {
            userId = Guid.Parse("99999999-1111-1111-1111-111111111111");
            displayName = "System Administrator";
            roles = ["Admin"];
            permissions = Permissions.All;
        }
        else if (uname == "maker" && (pwd == "Maker@123456" || pwd == "maker123" || pwd == "123456"))
        {
            userId = Guid.Parse("99999999-2222-2222-2222-222222222222");
            displayName = "Rahim Ahmed (VAT Maker)";
            roles = ["Maker"];
            permissions = [
                Permissions.PurchaseRead, Permissions.PurchaseCreate, Permissions.PurchaseSubmit, Permissions.PurchasePrepare, Permissions.PurchaseCancel,
                Permissions.SalesInvoiceRead, Permissions.SalesInvoiceCreate, Permissions.SalesInvoiceSubmit, Permissions.SalesInvoiceCancel, Permissions.SalesReserve,
                Permissions.SalesDebitRequest, Permissions.SalesCreditRequest,
                Permissions.PaymentRead, Permissions.PaymentRequest, Permissions.PaymentAllocate, Permissions.PaymentReverseRequest, Permissions.PaymentRefundRequest,
                Permissions.InventoryRead, Permissions.InventoryManage,
                Permissions.ManufacturingRead, Permissions.ManufacturingRequest,
                Permissions.MasterDataRead, Permissions.MasterDataManage,
                Permissions.CompanyRead, Permissions.TaxRead, Permissions.PeriodRead, Permissions.ReportRead,
                Permissions.ComplianceRead, Permissions.WorkflowRead, Permissions.IdentityRead
            ];
        }
        else if (uname == "checker" && (pwd == "Checker@123456" || pwd == "checker123" || pwd == "123456"))
        {
            userId = Guid.Parse("99999999-3333-3333-3333-333333333333");
            displayName = "Karim Ullah (VAT Checker)";
            roles = ["Checker"];
            permissions = [
                Permissions.PurchaseRead, Permissions.PurchaseApprove, Permissions.PurchasePost, Permissions.PurchaseCancel, Permissions.PurchaseReverseApprove,
                Permissions.SalesInvoiceRead, Permissions.SalesInvoiceApprove, Permissions.SalesPost, Permissions.SalesInvoiceCancel,
                Permissions.SalesDebitApprove, Permissions.SalesCreditApprove,
                Permissions.PaymentRead, Permissions.PaymentApprove, Permissions.PaymentReverseApprove, Permissions.PaymentRefundApprove,
                Permissions.InventoryRead, Permissions.InventoryApprove, Permissions.InventoryPost,
                Permissions.ManufacturingRead, Permissions.ManufacturingApprove,
                Permissions.PeriodRead, Permissions.PeriodCloseApprove,
                Permissions.ComplianceRead, Permissions.ComplianceManage, Permissions.ComplianceSubmit,
                Permissions.MasterDataRead, Permissions.CompanyRead, Permissions.TaxRead, Permissions.ReportRead, Permissions.WorkflowRead, Permissions.IdentityRead
            ];
        }
        else if (uname == "auditor" && (pwd == "Auditor@123456" || pwd == "auditor123" || pwd == "123456"))
        {
            userId = Guid.Parse("99999999-4444-4444-4444-444444444444");
            displayName = "Nasreen Akter (Auditor)";
            roles = ["Auditor"];
            permissions = [
                Permissions.CompanyRead, Permissions.PeriodRead, Permissions.MasterDataRead, Permissions.PurchaseRead, Permissions.SalesInvoiceRead,
                Permissions.InventoryRead, Permissions.ManufacturingRead, Permissions.TaxRead, Permissions.ComplianceRead, Permissions.ReportRead,
                Permissions.PaymentRead, Permissions.WorkflowRead, Permissions.IdentityRead, Permissions.IntegrationRead, Permissions.MessagingRead
            ];
        }
        else
        {
            return Unauthorized(new ProblemDetails
            {
                Status = 401,
                Title = "unauthorized",
                Detail = "Invalid username or password. Check credentials and try again."
            });
        }

        // Generate signed JWT token with RSA key
        var key = DevSecurityKey.CreateSigningKey();
        var credentials = new SigningCredentials(key, SecurityAlgorithms.RsaSha256);

        var claims = new List<Claim>
        {
            new("sub", uname),
            new("name", displayName),
            new("tenant_id", TenantId.ToString()),
            new("company_id", CompanyId.ToString())
        };

        foreach (var perm in permissions)
        {
            claims.Add(new Claim("permission", perm));
        }

        var expires = DateTime.UtcNow.AddDays(7);
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = expires,
            Issuer = DevSecurityKey.Issuer,
            Audience = DevSecurityKey.Audience,
            SigningCredentials = credentials
        };

        var handler = new JwtSecurityTokenHandler();
        var token = handler.CreateToken(tokenDescriptor);
        var jwtString = handler.WriteToken(token);

        return Ok(new LoginResponse(
            AccessToken: jwtString,
            TokenType: "Bearer",
            ExpiresIn: (int)(expires - DateTime.UtcNow).TotalSeconds,
            User: new UserProfileDto(
                Id: userId,
                Username: uname,
                DisplayName: displayName,
                CompanyId: CompanyId,
                TenantId: TenantId,
                Roles: roles,
                Permissions: permissions
            )
        ));
    }
}
