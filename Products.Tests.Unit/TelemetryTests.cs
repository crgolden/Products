namespace Products.Tests.Unit;

using Products.Tests.Unit.TestSupport;

[Trait("Category", "Unit")]
public sealed class TelemetryTests : IDisposable
{
    private readonly TelemetryHarness _harness = new();

    [Fact]
    public void IndexCreationFailureCounter_CarriesTheConfiguredDescription()
    {
        // Arrange
        Dictionary<string, string?> expected = new(StringComparer.Ordinal)
        {
            [Telemetry.Metrics.IndexCreationFailureCounterName] = _harness.Descriptions.IndexCreationFailureDescription,
        };

        // Act
        var actual = _harness.PublishedDescriptions();

        // Assert
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void IndexCreationFailed_EmitsOneMeasurementTaggedWithTheExceptionType()
    {
        // Arrange
        var failure = new InvalidOperationException(Generated.NewDescription());
        long captured = 0;
        KeyValuePair<string, object?>[] capturedTags = [];
        using var listener = _harness.ListenTo(
            Telemetry.Metrics.IndexCreationFailureCounterName,
            (value, tags) =>
            {
                captured = value;
                capturedTags = tags;
            });

        // Act
        _harness.Telemetry.IndexCreationFailed(failure);

        // Assert
        Assert.Equal(1, captured);
        Assert.Contains(
            capturedTags,
            t => string.Equals(t.Key, Telemetry.Metrics.ExceptionTypeTagName, StringComparison.Ordinal) &&
                string.Equals(t.Value as string, typeof(InvalidOperationException).FullName, StringComparison.Ordinal));
    }

    public void Dispose() => _harness.Dispose();
}
