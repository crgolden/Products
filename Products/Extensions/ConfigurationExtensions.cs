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
            return configuration.GetValue<T?>(key) ?? throw new InvalidOperationException($"Invalid '{key}'.");
        }

#pragma warning disable SA1009
        internal (
            string MongoDbUsername,
            string MongoDbPassword
        ) GetProductsSecrets()
        {
            var mongoDbUsername = configuration.GetRequired<string>("MongoDbUsername");
            var mongoDbPassword = configuration.GetRequired<string>("MongoDbPassword");
            return (
                mongoDbUsername,
                mongoDbPassword
            );
        }
#pragma warning restore SA1009
    }
}
