namespace Products;

using System.Diagnostics;
using System.Diagnostics.Metrics;

public static class Telemetry
{
    public static readonly ActivitySource ActivitySource = new(nameof(Products), "1.0.0");

    public static class Metrics
    {
        public const string MeterName = nameof(Products);

        public const string IndexCreationFailureCounterName = "products.index_creation.failures";

        public const string ExceptionTypeTagName = "exception.type";

        private static readonly Meter Meter = new(MeterName, "1.0.0");

        private static readonly Counter<long> IndexCreationFailureCounter =
            Meter.CreateCounter<long>(
                IndexCreationFailureCounterName,
                description: "Failed attempts to create the Product collection's indexes at startup, before the initializer retries.");

        public static void IndexCreationFailed(Exception exception) =>
            IndexCreationFailureCounter.Add(
                1,
                new TagList { { ExceptionTypeTagName, exception.GetType().FullName } });
    }
}
