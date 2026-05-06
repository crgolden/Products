namespace Products.Extensions;

using Azure.Security.KeyVault.Secrets;

public static class SecretClientExtensions
{
    extension(SecretClient secretClient)
    {
#pragma warning disable SA1009
        public (
            KeyVaultSecret ElasticsearchUsername,
            KeyVaultSecret ElasticsearchPassword,
            KeyVaultSecret MongoDbUsername,
            KeyVaultSecret MongoDbPassword
        ) GetProductsSecrets()
        {
            var elasticsearchUsername = secretClient.GetSecret("ElasticsearchUsername");
            var elasticsearchPassword = secretClient.GetSecret("ElasticsearchPassword");
            var mongoDbUsername = secretClient.GetSecret("MongoDbUsername");
            var mongoDbPassword = secretClient.GetSecret("MongoDbPassword");
            return (
                elasticsearchUsername.Value,
                elasticsearchPassword.Value,
                mongoDbUsername.Value,
                mongoDbPassword.Value
            );
        }
#pragma warning restore SA1009
    }
}