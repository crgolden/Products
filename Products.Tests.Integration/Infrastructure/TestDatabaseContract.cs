namespace Products.Tests.Integration.Infrastructure;

internal static class TestDatabaseContract
{
    internal static bool IsDisposableDatabase(string? databaseName) =>
        databaseName is not null
        && (databaseName.EndsWith(TestDatabaseContractConstants.TestDatabaseSuffix, StringComparison.Ordinal)
            || databaseName.EndsWith(TestDatabaseContractConstants.TriageDatabaseSuffix, StringComparison.Ordinal));
}
