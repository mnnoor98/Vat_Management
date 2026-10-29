using Xunit;

namespace Vat.Tests;

// Keep disposable-database create/drop operations isolated; individual tests still send concurrent commands.
[CollectionDefinition("PostgreSQL integration", DisableParallelization = true)]
public sealed class PostgresIntegrationCollection;
