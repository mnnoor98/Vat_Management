namespace Vat.Messaging.Http;

public sealed class OutboxHttpServerSettings
{
    // A dedicated, randomly generated 256-bit credential, supplied through secret configuration.
    public string ServiceKey { get; set; } = "";
    public bool AllowLoopbackHttp { get; set; }
    public bool IsValid() => Credential.IsValid(ServiceKey);
}

public sealed class OutboxHttpClientSettings
{
    public string BaseAddress { get; set; } = "";
    public string ServiceKey { get; set; } = "";
    public string SourceOwner { get; set; } = "financial-coordinated";
    public int TimeoutSeconds { get; set; } = 5;
    public bool AllowLoopbackHttp { get; set; }
    public bool IsValid() => Uri.TryCreate(BaseAddress, UriKind.Absolute, out var uri)
        && (uri.Scheme == "https" || (AllowLoopbackHttp && uri.Scheme == "http" && uri.IsLoopback))
        && string.IsNullOrEmpty(uri.UserInfo) && uri.AbsolutePath == "/"
        && string.IsNullOrEmpty(uri.Query) && string.IsNullOrEmpty(uri.Fragment)
        && Credential.IsValid(ServiceKey) && SourceOwner == "financial-coordinated"
        && TimeoutSeconds is >= 1 and <= 30;
}

internal static class Credential
{
    internal static bool IsValid(string? value) => value is { Length: 64 }
        && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F');
}
