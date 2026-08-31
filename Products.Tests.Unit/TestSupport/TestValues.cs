namespace Products.Tests.Unit.TestSupport;

internal static class TestValues
{
    internal static string LowercaseToken(int length) =>
        string.Concat(Enumerable.Range(0, length).Select(_ => (char)Random.Shared.Next('a', 'z' + 1)));

    internal static string NewProductName() => $"{LowercaseToken(5)} {LowercaseToken(7)}";

    internal static decimal NewPrice() => Math.Round((decimal)(Random.Shared.NextDouble() * 1000.0), 2);

    internal static Uri NewManualUrl() => new($"https://{LowercaseToken(12)}.example/{LowercaseToken(6)}");

    internal static string NewModelErrorKey() => LowercaseToken(8);

    internal static string NewModelErrorMessage() => $"invalid-{LowercaseToken(10)}";
}
