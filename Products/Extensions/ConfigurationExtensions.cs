namespace Products.Extensions;

using System;
using Microsoft.Extensions.Configuration;

public static class ConfigurationExtensions
{
    extension(IConfiguration configuration)
    {
        public T GetRequired<T>(string key)
            where T : notnull
        {
            if (configuration[key] is null)
            {
                throw new InvalidOperationException($"Invalid '{key}'.");
            }

            return configuration.GetValue<T?>(key) ?? throw new InvalidOperationException($"Invalid '{key}'.");
        }
    }
}
