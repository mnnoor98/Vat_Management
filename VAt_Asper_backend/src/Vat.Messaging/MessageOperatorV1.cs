namespace Vat.Messaging.Contracts.V1;

// The authenticated bearer token carries the actor and scope. They are never accepted in a request body.
public static class MessageOperatorV1
{
    public const string Prefix = "/internal/v1/message-operations/companies/{companyId:guid}";
    public static string CompanyPath(Guid companyId) => $"/internal/v1/message-operations/companies/{companyId}";
}
public sealed record RetryMessageV1(string Reason);
