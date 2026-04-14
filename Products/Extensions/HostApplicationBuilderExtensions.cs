namespace Products.Extensions;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Azure.Core;
using Azure.Monitor.OpenTelemetry.AspNetCore;
using Azure.Security.KeyVault.Secrets;
using Elastic.Ingest.Elasticsearch;
using Elastic.Ingest.Elasticsearch.DataStreams;
using Elastic.Serilog.Sinks;
using Elastic.Transport;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Models;
using MongoDB.Driver;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;

public static class HostApplicationBuilderExtensions
{
    extension(IHostApplicationBuilder builder)
    {
        public async Task<IHostApplicationBuilder> AddObservabilityAsync(SecretClient secretClient, CancellationToken cancellationToken = default)
        {
            var applicationName = builder.Configuration["WEBSITE_SITE_NAME"];
            var elasticsearchNode = builder.Configuration.GetValue<Uri?>("ElasticsearchNode") ?? throw new InvalidOperationException("Invalid 'ElasticsearchNode'.");
            var tasks = new[]
            {
                secretClient.GetSecretAsync("ElasticsearchUsername", cancellationToken: cancellationToken),
                secretClient.GetSecretAsync("ElasticsearchPassword", cancellationToken: cancellationToken),
            };
            var result = await Task.WhenAll(tasks);
            builder.Logging.AddOpenTelemetry(openTelemetryLoggerOptions =>
            {
                openTelemetryLoggerOptions.IncludeFormattedMessage = true;
                openTelemetryLoggerOptions.IncludeScopes = true;
            });
            var otelBuilder = builder.Services
                .AddOpenTelemetry()
                .ConfigureResource(resourceBuilder =>
                {
                    var serviceName = applicationName ?? builder.Environment.ApplicationName;
                    resourceBuilder.AddService(
                        serviceName: serviceName,
                        serviceVersion: typeof(Program).Assembly.GetName().Version?.ToString() ?? "0.0.0");
                    resourceBuilder.AddAttributes(new Dictionary<string, object>
                    {
                        ["deployment.environment"] = builder.Environment.EnvironmentName.ToLowerInvariant()
                    });
                })
                .WithMetrics(meterProviderBuilder =>
                {
                    meterProviderBuilder
                        .AddAspNetCoreInstrumentation()
                        .AddHttpClientInstrumentation()
                        .AddRuntimeInstrumentation();
                })
                .WithTracing(tracerProviderBuilder =>
                {
                    tracerProviderBuilder
                        .SetSampler(new AlwaysOnSampler())
                        .AddSource(nameof(Products))
                        .AddAspNetCoreInstrumentation(aspNetCoreTraceInstrumentationOptions =>
                        {
                            aspNetCoreTraceInstrumentationOptions.Filter = context =>
                            {
                                return !context.Request.Path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase);
                            };
                        })
                        .AddHttpClientInstrumentation();
                    if (builder.Environment.IsDevelopment())
                    {
                        tracerProviderBuilder.AddConsoleExporter();
                    }
                });

            if (builder.Environment.IsProduction())
            {
                otelBuilder.UseAzureMonitor();
            }

            builder.Services.AddSerilog((sp, loggerConfiguration) =>
                {
                    loggerConfiguration
                        .ReadFrom.Configuration(builder.Configuration)
                        .ReadFrom.Services(sp)
                        .Enrich.FromLogContext()
                        .Enrich.WithMachineName()
                        .Enrich.WithEnvironmentName();
                    if (!IsNullOrWhiteSpace(applicationName))
                    {
                        loggerConfiguration
                            .Enrich.WithProperty(nameof(IHostEnvironment.ApplicationName), applicationName);
                    }

                    if (builder.Environment.IsProduction())
                    {
                        loggerConfiguration
                            .WriteTo.Elasticsearch(
                                [elasticsearchNode],
                                elasticsearchSinkOptions =>
                                {
                                    elasticsearchSinkOptions.DataStream = new DataStreamName("logs", "dotnet", nameof(Products));
                                    elasticsearchSinkOptions.BootstrapMethod = BootstrapMethod.Failure;
                                },
                                transportConfiguration =>
                                {
                                    var header = new BasicAuthentication(result[0].Value.Value, result[1].Value.Value);
                                    transportConfiguration.Authentication(header);
                                });
                    }
                });
            return builder;
        }

        public IHostApplicationBuilder AddAuth()
        {
            var authority = builder.Configuration.GetValue<Uri?>("OidcAuthority") ?? throw new InvalidOperationException("Invalid 'OidcAuthority'.");
            builder.Services
                .AddAuthentication()
                .AddJwtBearer(jwtBearerOptions =>
                {
                    jwtBearerOptions.Authority = authority.ToString();
                    jwtBearerOptions.TokenValidationParameters.ValidateAudience = false;
                    jwtBearerOptions.MapInboundClaims = false;
                }).Services
                .AddAuthorizationBuilder()
                .AddPolicy(nameof(Products), policy =>
                {
                    policy.RequireAuthenticatedUser();
                    policy.RequireClaim("scope", "products");
                });
            return builder;
        }

        public IHostApplicationBuilder AddDataProtection(TokenCredential tokenCredential)
        {
            var blobUrl = builder.Configuration.GetValue<Uri?>("BlobUri") ?? throw new InvalidOperationException("Invalid 'BlobUri'.");
            var dataProtectionKeyIdentifier = builder.Configuration.GetValue<Uri?>("DataProtectionKeyIdentifier") ?? throw new InvalidOperationException("Invalid 'DataProtectionKeyIdentifier'.");
            builder.Services
                .AddDataProtection()
                .SetApplicationName(builder.Environment.ApplicationName)
                .PersistKeysToAzureBlobStorage(blobUrl, tokenCredential)
                .ProtectKeysWithAzureKeyVault(dataProtectionKeyIdentifier, tokenCredential).Services
                .AddAzureClientsCore(true);
            return builder;
        }

        public async Task<IHostApplicationBuilder> AddPersistenceAsync(SecretClient secretClient, CancellationToken cancellationToken = default)
        {
            var applicationName = builder.Configuration["WEBSITE_SITE_NAME"];
            var tasks = new[]
            {
                secretClient.GetSecretAsync("MongoDbUsername", cancellationToken: cancellationToken),
                secretClient.GetSecretAsync("MongoDbPassword", cancellationToken: cancellationToken),
            };
            var result = await Task.WhenAll(tasks);
            var mongoUsername = result[0].Value.Value;
            var mongoPassword = result[1].Value.Value;
            var mongoOptionsSection = builder.Configuration.GetSection(nameof(MongoOptions));
            var mongoOptions = mongoOptionsSection.Get<MongoOptions>() ?? throw new InvalidOperationException("Invalid 'MongoOptions'.");
            mongoOptions.ApplicationName = applicationName ?? builder.Environment.ApplicationName;
            var mongoIdentity = new MongoInternalIdentity(mongoOptions.DatabaseName, mongoUsername);
            var mongoIdentityEvidence = new PasswordEvidence(mongoPassword);
            mongoOptions.Credential = new MongoCredential("SCRAM-SHA-256", mongoIdentity, mongoIdentityEvidence);
            builder.Services.Configure<MongoOptions>(mongoOptionsSection);
            var mongoClient = new MongoClient(mongoOptions);
            builder.Services.AddSingleton<IMongoClient>(mongoClient);
            var database = mongoClient.GetDatabase(mongoOptions.DatabaseName);
            builder.Services.AddSingleton(database);
            return builder;
        }
    }
}
