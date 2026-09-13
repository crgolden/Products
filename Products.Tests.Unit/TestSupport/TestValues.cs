namespace Products.Tests.Unit.TestSupport;

internal static class TestValues
{
    internal static string LowercaseToken(int length) =>
        string.Concat(Enumerable.Range(0, length).Select(_ => (char)Random.Shared.Next('a', 'z' + 1)));

    internal static string NewProductName() => $"{LowercaseToken(5)} {LowercaseToken(7)}";

    internal static decimal NewPrice() => Math.Round((decimal)(Random.Shared.NextDouble() * 1000.0), 2);

    internal static Uri NewManualUrl() => new($"https://{LowercaseToken(12)}.example/{LowercaseToken(6)}");

    internal static string NewBrand() => LowercaseToken(6);

    internal static string NewModelNumber() => $"{LowercaseToken(3)}-{LowercaseToken(5)}";

    internal static string NewModelErrorKey() => LowercaseToken(8);

    internal static string NewModelErrorMessage() => $"invalid-{LowercaseToken(10)}";

    internal static string NewOpenApiDocumentName() => LowercaseToken(4);

    internal static string NewBlank() => new string(' ', Random.Shared.Next(1, 4));

    internal static string NewMalformedGuid() => $"{LowercaseToken(8)}-{LowercaseToken(4)}";

    internal static string NewUppercaseMarker() => LowercaseToken(4).ToUpperInvariant();

    internal static string NewTimeoutMessage() => $"timeout-{LowercaseToken(10)}";

    internal static string NewUppercaseSortingName() => $"Z{NewProductName()}";

    internal static string NewLowercaseSortingName() => $"a{NewProductName()}";
}
