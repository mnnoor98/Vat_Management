namespace Vat.Domain;

/// <summary>Fail closed until the extracted financial participants have a verified commit/recovery contract.</summary>
public static class FinancialBoundary
{
    public static void RequireAvailable() => throw new DomainException(
        "financial_boundary_unavailable",
        "Financial execution is unavailable while stock, tax, period and event coordination is being reconciled.", 503);
}
