namespace Vat.Domain;

public sealed class DomainException(string code, string message, int status = 422) : Exception(message)
{
    public string Code { get; } = code;
    public int Status { get; } = status;
}

public static class Guard
{
    public static string Text(string? value, string field, int maxLength)
    {
        var text = value?.Trim();
        if (string.IsNullOrEmpty(text) || text.Length > maxLength)
            throw new DomainException("validation_failed", $"{field} must contain 1-{maxLength} characters.");
        return text;
    }

    public static Guid Id(Guid value, string field) => value != Guid.Empty
        ? value : throw new DomainException("validation_failed", $"{field} is required.");
}
