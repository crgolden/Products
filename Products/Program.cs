#pragma warning disable SA1200
using System.Diagnostics;
using System.Security.Claims;
using Azure.Identity;
using Azure.Monitor.OpenTelemetry.AspNetCore;
using Azure.Security.KeyVault.Secrets;
using Elastic.Ingest.Elasticsearch;
using Elastic.Ingest.Elasticsearch.DataStreams;
using Elastic.Serilog.Sinks;
using Elastic.Transport;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.OData;
using Microsoft.Extensions.Azure;
using Microsoft.OData.ModelBuilder;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;
using MongoDB.Driver.Core.Extensions.DiagnosticSources;
using OpenTelemetry.Instrumentation.AspNetCore;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Products.Authorization;
using Products.Extensions;
using Products.Models;
using Products.OpenApi;
using Serilog;
#pragma warning restore SA1200

Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);
    var mongoClientSettings = new MongoClientSettings();
    string mongoDatabaseName = builder.Configuration.GetRequired<string>("MongoDatabaseName"),
        mongoServerHost = builder.Configuration.GetRequired<string>("MongoServerHost");
    var mongoServerPort = builder.Configuration.GetRequired<int>("MongoServerPort");
    var mongoUseTls = builder.Configuration.GetRequired<bool>("MongoUseTls");
    var oidcAuthority = builder.Configuration.GetRequired<Uri>("OidcAuthority");
    string mongoDbUsername, mongoDbPassword;
    if (builder.Environment.IsProduction())
    {
        var defaultAzureCredentialOptionsSection = builder.Configuration.GetRequiredSection(nameof(DefaultAzureCredentialOptions));
        var defaultAzureCredentialOptions = defaultAzureCredentialOptionsSection.Get<DefaultAzureCredentialOptions>() ?? throw new InvalidOperationException($"Invalid '{nameof(DefaultAzureCredentialOptions)}' section.");
        var tokenCredential = new DefaultAzureCredential(defaultAzureCredentialOptions);
        Uri blobUri = builder.Configuration.GetRequired<Uri>("BlobUri"),
            dataProtectionKeyIdentifier = builder.Configuration.GetRequired<Uri>("DataProtectionKeyIdentifier"),
            elasticsearchNode = builder.Configuration.GetRequired<Uri>("ElasticsearchNode"),
            keyVaultUrl = builder.Configuration.GetRequired<Uri>("KeyVaultUri");
        var applicationName = builder.Configuration.GetRequired<string>("WEBSITE_SITE_NAME");
        mongoClientSettings.ApplicationName = applicationName;
        mongoClientSettings.ClusterConfigurator = cb => cb.Subscribe(new DiagnosticsActivityEventSubscriber());
        var secretClient = new SecretClient(keyVaultUrl, tokenCredential);
        var secrets = secretClient.GetProductsSecrets();
        mongoDbUsername = secrets.MongoDbUsername.Value;
        mongoDbPassword = secrets.MongoDbPassword.Value;
        builder.Services.Configure<AspNetCoreTraceInstrumentationOptions>(options =>
            options.Filter = context => !context.Request.Path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase));
        builder.Logging.AddOpenTelemetry(openTelemetryLoggerOptions =>
        {
            openTelemetryLoggerOptions.IncludeFormattedMessage = true;
            openTelemetryLoggerOptions.IncludeScopes = true;
        });
        builder.Services
            .AddSerilog((serviceProvider, loggerConfiguration) => loggerConfiguration
                .ReadFrom.Configuration(builder.Configuration)
                .ReadFrom.Services(serviceProvider)
                .Enrich.WithProperty(nameof(IHostEnvironment.ApplicationName), applicationName)
                .WriteTo.Elasticsearch(
                    [elasticsearchNode],
                    elasticsearchSinkOptions =>
                    {
                        elasticsearchSinkOptions.DataStream = new DataStreamName("logs", "dotnet", nameof(Products));
                        elasticsearchSinkOptions.BootstrapMethod = BootstrapMethod.Failure;
                        elasticsearchSinkOptions.TextFormatting.MapCustom = (ecsDocument, _) =>
                        {
                            ecsDocument.Service ??= new Elastic.CommonSchema.Service();
                            ecsDocument.Service.Name = applicationName;
                            return ecsDocument;
                        };
                    },
                    transportConfiguration =>
                    {
                        var header = new BasicAuthentication(secrets.ElasticsearchUsername.Value, secrets.ElasticsearchPassword.Value);
                        transportConfiguration.Authentication(header);
                    }))
            .AddOpenTelemetry()
            .ConfigureResource(resourceBuilder => resourceBuilder
                .AddService(applicationName, null, typeof(Program).Assembly.GetName().Version?.ToString() ?? "0.0.0")
                .AddAttributes(new Dictionary<string, object>
                {
                    ["deployment.environment"] = builder.Environment.EnvironmentName.ToLowerInvariant()
                }))
            .WithMetrics(meterProviderBuilder => meterProviderBuilder
                .AddRuntimeInstrumentation()
                .AddView(instrument =>
                    instrument.Meter.Name == "System.Net.Http" ? MetricStreamConfiguration.Drop : null)
                .AddOtlpExporter(o => o.Endpoint = new Uri(builder.Configuration.GetRequired<string>("AlloyEndpoint"))))
            .WithTracing(tracerProviderBuilder => tracerProviderBuilder
                .SetSampler(new AlwaysOnSampler())
                .AddSource(nameof(Products))
                .AddSource("MongoDB.Driver.Core.Extensions.DiagnosticSources")
                .AddOtlpExporter(o => o.Endpoint = new Uri(builder.Configuration.GetRequired<string>("AlloyEndpoint"))))
            .UseAzureMonitor().Services
            .AddDataProtection()
            .SetApplicationName(applicationName)
            .PersistKeysToAzureBlobStorage(blobUri, tokenCredential)
            .ProtectKeysWithAzureKeyVault(dataProtectionKeyIdentifier, tokenCredential).Services
            .AddAzureClientsCore(true);
    }
    else
    {
        if (builder.Environment.IsDevelopment())
        {
            builder.Configuration.AddUserSecrets("efff68f7-73ce-43f6-9083-6659719fc179");
        }

        var secrets = builder.Configuration.GetProductsSecrets();
        mongoDbUsername = secrets.MongoDbUsername;
        mongoDbPassword = secrets.MongoDbPassword;
        builder.Services
            .AddSerilog((serviceProvider, loggerConfiguration) => loggerConfiguration
                .ReadFrom.Configuration(builder.Configuration)
                .ReadFrom.Services(serviceProvider))
            .AddDataProtection()
            .UseEphemeralDataProtectionProvider();
    }

    builder.Services.AddOpenApi(openApiOptions =>
    {
        openApiOptions.AddDocumentTransformer<ODataQueryParameterTransformer>();
    });

    var mongoInternalIdentity = new MongoInternalIdentity(mongoDatabaseName, mongoDbUsername);
    var passwordEvidence = new PasswordEvidence(mongoDbPassword);
    mongoClientSettings.Credential = new MongoCredential("SCRAM-SHA-256", mongoInternalIdentity, passwordEvidence);
    mongoClientSettings.Server = new MongoServerAddress(mongoServerHost, mongoServerPort);
    mongoClientSettings.UseTls = mongoUseTls;
    var mongoClient = new MongoClient(mongoClientSettings);
    builder.Services.AddSingleton<IMongoClient>(mongoClient);
    var mongoDatabase = mongoClient.GetDatabase(mongoDatabaseName);
    builder.Services.AddSingleton(mongoDatabase);
    BsonClassMap.TryRegisterClassMap<Product>(bsonClassMap =>
    {
        bsonClassMap.AutoMap();
        bsonClassMap.MapIdMember(p => p.Id).SetSerializer(new GuidSerializer(BsonType.String));
        bsonClassMap.MapMember(p => p.OwnerId).SetSerializer(new NullableSerializer<Guid>(new GuidSerializer(BsonType.String)));
    });
    var mongoCollection = mongoDatabase.GetCollection<Product>("Products");
    var indexModels = new[]
    {
        new CreateIndexModel<Product>(Builders<Product>.IndexKeys.Ascending(p => p.Name)),
        new CreateIndexModel<Product>(Builders<Product>.IndexKeys.Descending(p => p.CreatedAt)),
        new CreateIndexModel<Product>(Builders<Product>.IndexKeys.Ascending(p => p.OwnerId)),
    };
    await mongoCollection.Indexes.CreateManyAsync(indexModels);
    builder.Services.AddControllers().AddOData(oDataOptions =>
    {
        var modelBuilder = new ODataConventionModelBuilder();
        modelBuilder.EntitySet<Product>("Products");
        var model = modelBuilder.GetEdmModel();
        oDataOptions.Select();
        oDataOptions.Filter();
        oDataOptions.OrderBy();
        oDataOptions.Expand();
        oDataOptions.Count();
        oDataOptions.SetMaxTop(100);
        oDataOptions.AddRouteComponents("odata", model);
    }).Services
        .AddEndpointsApiExplorer()
        .AddHealthChecks().Services
        .AddAuthentication()
        .AddJwtBearer(jwtBearerOptions =>
        {
            jwtBearerOptions.Authority = oidcAuthority.ToString();
            jwtBearerOptions.TokenValidationParameters.ValidateAudience = false;
            jwtBearerOptions.MapInboundClaims = false;
        }).Services
        .AddAuthorizationBuilder()
        .AddPolicy(nameof(Products), policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.RequireClaim("scope", "products");
        }).Services
        .AddSingleton<IAuthorizationHandler, ProductAuthorizationHandler>()
        .Configure<ForwardedHeadersOptions>(forwardedHeadersOptions =>
        {
            forwardedHeadersOptions.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            forwardedHeadersOptions.KnownIPNetworks.Clear();
            forwardedHeadersOptions.KnownProxies.Clear();
        });

    var app = builder.Build();
    app.UseForwardedHeaders();
    app.UseSerilogRequestLogging(options =>
    {
        options.EnrichDiagnosticContext = (diagnosticContext, _) =>
        {
            if (Activity.Current is null)
            {
                return;
            }

            diagnosticContext.Set(nameof(Activity.TraceId), Activity.Current.TraceId.ToString());
            diagnosticContext.Set(nameof(Activity.SpanId), Activity.Current.SpanId.ToString());
        };
    });
    if (app.Environment.IsDevelopment())
    {
        app.UseDeveloperExceptionPage();
        app.UseODataRouteDebug();
    }
    else
    {
        app.UseHsts();
    }

    app.UseHttpsRedirection();
    app.UseAuthentication();
    app.UseAuthorization();
    app.Use((ctx, next) =>
    {
        if (ctx.User.Identity?.IsAuthenticated != true)
        {
            return next(ctx);
        }

        using (Serilog.Context.LogContext.PushProperty("UserId", ctx.User.FindFirstValue("sub")))
        using (Serilog.Context.LogContext.PushProperty("UserEmail", ctx.User.FindFirstValue("email")))
        {
            return next(ctx);
        }
    });
    app.MapOpenApi();
    app.MapHealthChecks("/health").DisableHttpMetrics();
    app.MapControllers();
    await app.RunAsync();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    await Log.CloseAndFlushAsync();
}
