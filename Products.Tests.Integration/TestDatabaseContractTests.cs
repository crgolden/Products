namespace Products.Tests.Integration;

using Products.Tests.Integration.Infrastructure;

[Trait("Category", "Integration")]
public sealed class TestDatabaseContractTests
{
    [Fact]
    public void IsDisposableDatabase_TriageSuffix_ReturnsTrue()
    {
        var triageDatabase = Generated.NewDatabaseName() + TestDatabaseContractConstants.TriageDatabaseSuffix;

        Assert.True(TestDatabaseContract.IsDisposableDatabase(triageDatabase));
    }

    [Fact]
    public void IsDisposableDatabase_TestSuffix_ReturnsTrue()
    {
        var testDatabase = Generated.NewDatabaseName() + TestDatabaseContractConstants.TestDatabaseSuffix;

        Assert.True(TestDatabaseContract.IsDisposableDatabase(testDatabase));
    }

    [Fact]
    public void IsDisposableDatabase_NeitherSuffix_ReturnsFalse()
    {
        var productionLikeDatabase = Generated.NewDatabaseName();

        Assert.False(TestDatabaseContract.IsDisposableDatabase(productionLikeDatabase));
    }

    [Fact]
    public void IsDisposableDatabase_TriageSuffixBeforeTheEnd_ReturnsFalse()
    {
        var triageMidNameDatabase = TestDatabaseContractConstants.TriageDatabaseSuffix + Generated.NewDatabaseName();

        Assert.False(TestDatabaseContract.IsDisposableDatabase(triageMidNameDatabase));
    }

    [Fact]
    public void IsDisposableDatabase_LowercaseTriageSuffix_ReturnsFalse()
    {
        var lowercaseSuffixDatabase = Generated.NewDatabaseName() + TestDatabaseContractConstants.TriageDatabaseSuffix.ToLowerInvariant();

        Assert.False(TestDatabaseContract.IsDisposableDatabase(lowercaseSuffixDatabase));
    }

    [Fact]
    public void IsDisposableDatabase_Null_ReturnsFalse()
    {
        Assert.False(TestDatabaseContract.IsDisposableDatabase(null));
    }
}
