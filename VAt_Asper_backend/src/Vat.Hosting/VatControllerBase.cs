using Microsoft.AspNetCore.Mvc;

namespace Vat.Hosting;

public abstract class VatControllerBase : ControllerBase
{
    protected IActionResult Respond(object? value) => value is IResult result ? new HttpResultAdapter(result) : Ok(value);

    private sealed class HttpResultAdapter(IResult result) : IActionResult
    {
        public Task ExecuteResultAsync(ActionContext context) => result.ExecuteAsync(context.HttpContext);
    }
}
