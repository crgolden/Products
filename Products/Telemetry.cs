namespace Products;

using System.Diagnostics;

public static class Telemetry
{
    public static readonly ActivitySource ActivitySource = new(nameof(Products), "1.0.0");
}
