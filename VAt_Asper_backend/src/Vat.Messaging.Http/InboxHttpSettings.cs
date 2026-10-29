namespace Vat.Messaging.Http;

public sealed class InboxHttpServerSettings
{
    public string ServiceKey { get; set; } = "";
    public bool AllowLoopbackHttp { get; set; }
    public bool IsValid() => Credential.IsValid(ServiceKey);
}

public sealed class InboxHttpClientSettings
{
    public string BaseAddress { get; set; } = "";
    public string ServiceKey { get; set; } = "";
    public int TimeoutSeconds { get; set; } = 10;
    public bool AllowLoopbackHttp { get; set; }
    public bool IsValid() => Uri.TryCreate(BaseAddress, UriKind.Absolute, out var uri)
        && (uri.Scheme == "https" || (AllowLoopbackHttp && uri.Scheme == "http" && uri.IsLoopback))
        && string.IsNullOrEmpty(uri.UserInfo) && uri.AbsolutePath == "/"
        && string.IsNullOrEmpty(uri.Query) && string.IsNullOrEmpty(uri.Fragment)
        && Credential.IsValid(ServiceKey) && TimeoutSeconds is >= 1 and <= 30;
}
