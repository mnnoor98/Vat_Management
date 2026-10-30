using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;

namespace Vat.Api.Security;

public static class DevSecurityKey
{
    public const string KeyId = "enterprise-vat-dev-rsa-2026";
    public const string Issuer = "https://localhost:5080";
    public const string Audience = "vat-api";

    private const string RsaXml =
        "<RSAKeyValue><Modulus>vqY3O4uadM4Y4Hz+kJ/+HZegUNz4vXViMhFw2qj5Io6lRYQKPFfqAf69BlmvilBNt8KNagwW2Y5RLV3OFUDrHYTlGCtFi+KwfxBQljmxIksx5KeFDl1N14+iQRoBycYngWj+tIKYDAnNKbxVkiezFy+eoIm8aCTHygaVlphNQJWsqv3ysPuiYVqZeQiEpVD4fO4fbarZkP5c52POu4RV55mPM5yDlb6sWPAwv6M20yX4eXxBGjyGNQRye/vPSyTx7CElzqcx+OtM1MGRkmrF/QzBgjW7VjQ/OxrSDOud2dV+5z4JNAUwa8o7cvt0dvq/rQ/a0zmTYOj6PT5tMiuN1Q==</Modulus><Exponent>AQAB</Exponent><P>1dqVpvujLT+d2+ETS29d94nWB1rNyKk2CWGUYOPuFsLlgKCLnggjq3iD0SXQoR26nrMpf6B+4SgvgGJ9EMiQT8vXrQLReu9bCtZ7r61osCeC4bcu9Z3F/HyT12e6NLRJDeuZkGgAn0RXPJ0RP+TMA7h0LrKq9MHjYVEhcJ0ONUs=</P><Q>5DjoZI0zW6dy4Z333k3Y+W2I9V2sRW8KstKaOCggvk091Z3LLMche4fIIuVHKSYGERItbqGr26/yWdf1wWast7DzOCKAT1Ohfzph5hAieVsYd6IC4xKVTrqrL23xSMAGRXSL8OQgI7gBUAaaMTk9jphCbkYca39kYepc8iSP9V8=</Q><DP>q654t/6KaHgK/gt6VvQIJwLT/UGLtQ0LIYrh+MrjSAidAJ9ilrMIF89/W3eVPtNED8XbiedKFO4S/GmU59Vr7XPDKNT7YUso9tFrqIR5Z/nAW/6Yx3wrBuDoO2S9mzQk9fTvanvkC/hsZG/f+kBu/ImnVBoBns7NAUKFqwea8XM=</DP><DQ>cBC1pLJlNqtZ0QnVrfaCBbDQoNOXhRzKnxalHdAui+O36k4qd1lsypDZKLF3KPB/6CAJ6RpAHoceNRnDlm1JgIXGWJkwckN5uNS9P9BKbVbbA9utGT2FzXwCFS8iXPvV2IMrOGV8lq2DD32+d09JN9zyOd35lJugeigv7cMgYXc=</DQ><InverseQ>sxEn0OWaOUm9AaI893kpbvh3S6Wi1CgCMINxV71tSTLELj/6Yun0ogBrvNkDn4eYfgUfmA9lSrQU+47DUpC6Cah90LX7fU1D9I0vy6SL0l1s3RyPJ1MASJWHFrXfP/FPEbbzKZdiwD1JZmDcnt2CiXDAbUCpwvT7l6O5WYpnbIo=</InverseQ><D>G3eTCY8pZ56x0tXvTyLd16GaxUuwwosA5LnCip5Blor6oLNPTjZUTolG7n1JXyIGsGzlNtwbBqkMZP7YzBMYTJ5y6zKAyJCWYRgWpC4XqRswSwMfllw29NGKzNpUW5+i3XAlEhimk0VuO48YvP/fO33u2q49aLuz1RVxw8qZhq5KCy2wPgZqT0YI3VF1IW9umf1vQK+RAQpA9tC5tVe2glRUDHJJFr83SPgQjupvb5FmEh42e07Etg8nZHm6DaUyD2xb0ImvcXS+tr5H7/lvb+ya1x3qL368WTn3YzRn9niIGOSd3/j3E1FLoC6k7NGCeYWZ9bjySKJPbH8ggeq82Q==</D></RSAKeyValue>";

    public static RSA CreateRsa()
    {
        var rsa = RSA.Create();
        rsa.FromXmlString(RsaXml);
        return rsa;
    }

    public static RsaSecurityKey CreateSigningKey() => new(CreateRsa()) { KeyId = KeyId };
}
