namespace Products;

using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Options;

public sealed class Telemetry
{
    private readonly Counter<long> _indexCreationFailureCounter;

    public Telemetry(IMeterFactory meterFactory, IOptions<TelemetryOptions> telemetryOptions)
    {
        var meter = meterFactory.Create(Metrics.MeterName, typeof(Telemetry).Assembly.GetName().Version?.ToString());
        _indexCreationFailureCounter = meter.CreateCounter<long>(
            Metrics.IndexCreationFailureCounterName,
            description: telemetryOptions.Value.IndexCreationFailureDescription);
    }

    public void IndexCreationFailed(Exception exception) =>
        _indexCreationFailureCounter.Add(
            1,
            new TagList { { Metrics.ExceptionTypeTagName, exception.GetType().FullName } });

    public static class Metrics
    {
        public const string MeterName = nameof(Products);

        public const string IndexCreationFailureCounterName = "products.index_creation.failures";

        public const string ExceptionTypeTagName = "exception.type";
    }
}
