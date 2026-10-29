namespace Vat.Api;

public sealed class DatabaseSettings
{
    public string VatDatabase { get; set; } = "";
}

public sealed class PostingSettings
{
    public bool Enabled { get; set; }
}
