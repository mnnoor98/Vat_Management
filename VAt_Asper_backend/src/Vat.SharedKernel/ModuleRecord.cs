namespace Vat.Application;

public sealed record ModuleRecord(Guid Id, string? Reference, string? State, DateTimeOffset CreatedAt, int Version);
