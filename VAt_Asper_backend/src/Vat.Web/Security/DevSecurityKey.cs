using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Vat.Api.Security;

public static class DevSecurityKey
{
    public const string KeyId = "enterprise-vat-local-rsa";
    public const string Issuer = "https://localhost:5080";
    public const string Audience = "vat-api";

    public static bool IsEnabled(IHostEnvironment environment, IConfiguration configuration) =>
        environment.IsDevelopment() && configuration["Authentication:Authority"]?.TrimEnd('/') == Issuer;

    public static RSA CreateRsa()
    {
        // All local service hosts share a generated, untracked key. Never use it outside Development.
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root != null && !File.Exists(Path.Combine(root.FullName, "EnterpriseVat.sln"))) root = root.Parent;
        if (root == null) throw new InvalidOperationException("Local authentication requires the backend source workspace.");
        var directory = Path.Combine(root.FullName, ".local");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "development-signing-key.pem");
        for (var attempt = 0; attempt < 50; attempt++)
        {
            try
            {
                if (!File.Exists(path))
                {
                    using var generated = RSA.Create(2048);
                    using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                    using var writer = new StreamWriter(stream);
                    writer.Write(generated.ExportRSAPrivateKeyPem());
                }
                var rsa = RSA.Create();
                try { rsa.ImportFromPem(File.ReadAllText(path)); return rsa; }
                catch { rsa.Dispose(); throw; }
            }
            catch (IOException) when (attempt < 49) { Thread.Sleep(100); }
        }
        throw new InvalidOperationException("Could not load the local signing key.");
    }

    public static RsaSecurityKey CreateSigningKey() => new(CreateRsa()) { KeyId = KeyId };
}
