using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Vat.Messaging.Http;

public sealed class OutboxServiceAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger, UrlEncoder encoder, IOptions<OutboxHttpServerSettings> settings)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "OutboxService";
    public const string HeaderName = "X-Outbox-Service-Key";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var config = settings.Value;
        // Do not trust forwarded headers to establish TLS or loopback identity.
        var local = Context.Connection.RemoteIpAddress is { } address && System.Net.IPAddress.IsLoopback(address);
        if (!Request.IsHttps && !(config.AllowLoopbackHttp && local))
            return Task.FromResult(AuthenticateResult.Fail("Secure service transport required."));
        var values = Request.Headers[HeaderName];
        if (values.Count != 1 || !Credential.IsValid(values[0]) || !config.IsValid()
            || !CryptographicOperations.FixedTimeEquals(Convert.FromHexString(values[0]!), Convert.FromHexString(config.ServiceKey)))
            return Task.FromResult(AuthenticateResult.Fail("Invalid service credential."));
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "integration-outbox-dispatcher")], SchemeName);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
    }
}
