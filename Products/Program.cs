#pragma warning disable SA1200
using System.Diagnostics;
using System.Security.Claims;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.OData;
using Microsoft.OData.ModelBuilder;
using Products.Extensions;
using Products.Models;
using Products.OpenApi;
using Serilog;
#pragma warning restore SA1200

Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);
    if (builder.Environment.IsDevelopment())
    {
        builder.Configuration.AddUserSecrets("efff68f7-73ce-43f6-9083-6659719fc179");
    }

    var tokenCredential = await builder.Configuration.ToTokenCredentialAsync();
    var secretClient = builder.Configuration.ToSecretClient(tokenCredential);
    builder.Services.AddOpenApi(openApiOptions =>
    {
        openApiOptions.AddDocumentTransformer<ODataQueryParameterTransformer>();
    });
    await builder.AddObservabilityAsync(secretClient);
    await builder.AddPersistenceAsync(secretClient);
    builder.AddDataProtection(tokenCredential);
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
    });
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddHealthChecks();
    builder.AddAuth();
    builder.Services.Configure<ForwardedHeadersOptions>(forwardedHeadersOptions =>
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
            var activity = Activity.Current;
            if (activity is null)
            {
                return;
            }

            diagnosticContext.Set(nameof(Activity.TraceId), activity.TraceId.ToString());
            diagnosticContext.Set(nameof(Activity.SpanId), activity.SpanId.ToString());
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
