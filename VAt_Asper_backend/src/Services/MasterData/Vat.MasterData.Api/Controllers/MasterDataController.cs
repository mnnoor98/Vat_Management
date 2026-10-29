using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vat.Api.Security;
using Vat.Application.Security;
using Vat.Services.MasterData.Contracts;

namespace Vat.Services.MasterData.Controllers;

[ApiController]
[Tags("MasterData")]
public sealed class MasterDataController : ControllerBase
{
    [HttpPost("/api/v1/companies/{companyId:guid}/products", Name = "CreateProduct")]
    [Authorize(Policy = Permissions.MasterDataManage)]
    [ProducesResponseType(typeof(ProductResponse), 201)]
    public async Task<IActionResult> CreateProduct([FromRoute] Guid companyId, [FromBody] CreateProductRequest request, [FromServices] MasterDataProductService service, [FromServices] ICommandExecutor commands, CancellationToken ct)
    {
        var ctx = HttpContext;
        return await MasterDataApiCommands.Execute(ctx, commands, companyId, "product.create", request,
                () => service.CreateProduct(ctx.GetActor(), companyId, request, ctx.TraceIdentifier, ct), ct);
    }

    [HttpGet("/api/v1/companies/{companyId:guid}/products", Name = "ListProducts")]
    [Authorize(Policy = Permissions.MasterDataRead)]
    public async Task<IActionResult> ListProducts([FromRoute] Guid companyId, [FromServices] MasterDataProductService service, CancellationToken ct)
    {
        var ctx = HttpContext;
        return Ok(await service.ListProducts(ctx.GetActor(), companyId, ct));
    }

    [HttpGet("/api/v1/companies/{companyId:guid}/products/{id:guid}", Name = "GetProduct")]
    [Authorize(Policy = Permissions.MasterDataRead)]
    public async Task<IActionResult> GetProduct([FromRoute] Guid companyId, [FromRoute] Guid id, [FromServices] MasterDataProductService service, CancellationToken ct)
    {
        var ctx = HttpContext;
        return Ok(await service.GetProduct(ctx.GetActor(), companyId, id, ct));
    }

    [HttpPost("/api/v1/companies/{companyId:guid}/suppliers", Name = "CreateSupplier")]
    [Authorize(Policy = Permissions.MasterDataManage)]
    [ProducesResponseType(typeof(SupplierResponse), 201)]
    public async Task<IActionResult> CreateSupplier([FromRoute] Guid companyId, [FromBody] CreateSupplierRequest request, [FromServices] MasterDataProductService service, [FromServices] ICommandExecutor commands, CancellationToken ct)
    {
        var ctx = HttpContext;
        return await MasterDataApiCommands.Execute(ctx, commands, companyId, "supplier.create", request,
                () => service.CreateSupplier(ctx.GetActor(), companyId, request, ctx.TraceIdentifier, ct), ct);
    }

    [HttpGet("/api/v1/companies/{companyId:guid}/suppliers", Name = "ListSuppliers")]
    [Authorize(Policy = Permissions.MasterDataRead)]
    public async Task<IActionResult> ListSuppliers([FromRoute] Guid companyId, [FromServices] MasterDataProductService service, CancellationToken ct)
    {
        var ctx = HttpContext;
        return Ok(await service.ListSuppliers(ctx.GetActor(), companyId, ct));
    }

    [HttpGet("/api/v1/companies/{companyId:guid}/suppliers/{id:guid}", Name = "GetSupplier")]
    [Authorize(Policy = Permissions.MasterDataRead)]
    public async Task<IActionResult> GetSupplier([FromRoute] Guid companyId, [FromRoute] Guid id, [FromServices] MasterDataProductService service, CancellationToken ct)
    {
        var ctx = HttpContext;
        return Ok(await service.GetSupplier(ctx.GetActor(), companyId, id, ct));
    }
}
