namespace Products.Tests.Unit.Controllers.ODataPipeline;

using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.OData;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.OData.ModelBuilder;
using Products.OpenApi;
using Products.Tests.Unit.TestSupport;

[Trait("Category", "Unit")]
public sealed class MaterializeODataListPipelineTests : IAsyncLifetime
{
    private static readonly string RoutePrefix = Generated.NewRoutePrefix();

    private static readonly string DetailEntitySetName = Generated.NewEntitySetName();

    private readonly PipelineRows _rows = new();

    private WebApplication? _app;

    private HttpClient? _client;

    public async ValueTask InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton(_rows);
        builder.Services.AddControllers().AddOData(options =>
        {
            var modelBuilder = new ODataConventionModelBuilder();
            modelBuilder.EntitySet<PipelineRow>(FailingMaterializedRowsController.EntitySetName);
            modelBuilder.EntitySet<PipelineRow>(FailingStreamedRowsController.EntitySetName);
            modelBuilder.EntitySet<PipelineRow>(MaterializedRowsController.EntitySetName);
            modelBuilder.EntitySet<PipelineRow>(StreamedRowsController.EntitySetName);
            modelBuilder.EntitySet<PipelineRowDetail>(DetailEntitySetName);
            options.Select();
            options.Expand();
            options.OrderBy();
            options.Filter();
            options.Count();
            options.AddRouteComponents(RoutePrefix, modelBuilder.GetEdmModel());
        });

        _app = builder.Build();
        _app.UseExceptionHandler(errors => errors.Run(async context =>
        {
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            var failure = context.Features.Get<IExceptionHandlerFeature>();
            if (failure is not null)
            {
                await context.Response.WriteAsync(failure.Error.ToString(), context.RequestAborted);
            }
        }));
        _app.MapControllers();
        await _app.StartAsync(TestContext.Current.CancellationToken);
        _client = _app.GetTestClient();
    }

    public async ValueTask DisposeAsync()
    {
        _client?.Dispose();
        if (_app is not null)
        {
            await _app.DisposeAsync();
        }
    }

    [Fact]
    public async Task AProviderFailureMidEnumeration_ReachesTheClientAsAFailure_NotATruncated200()
    {
        // Arrange
        var url = ListUrl(FailingMaterializedRowsController.EntitySetName);

        // Act
        var response = await GetWithoutBufferingTheBodyAsync(url);

        // Assert
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    [Fact]
    public async Task WithoutMaterialization_TheSameFailure_IsA200WhoseBodyNeverCloses()
    {
        // Arrange
        var url = ListUrl(FailingStreamedRowsController.EntitySetName);

        // Act
        var response = await GetWithoutBufferingTheBodyAsync(url);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(
            await BodyIsCompleteJsonAsync(response),
            "this is the control: the streamed action must reproduce the truncated 200 the filter exists to close");
    }

    [Fact]
    public async Task ASelectedPayload_IsIdenticalWhetherOrNotTheListWasMaterialized()
    {
        // Arrange
        var query = $"{ODataQueryParameterTransformer.SelectQueryOption}={nameof(PipelineRow.Name)}";

        // Act
        var materialized = await BodyWithoutContextAsync(ListUrl(MaterializedRowsController.EntitySetName, query));
        var streamed = await BodyWithoutContextAsync(ListUrl(StreamedRowsController.EntitySetName, query));

        // Assert
        Assert.Equal(streamed, materialized);
        Assert.All(_rows.Rows, row => Assert.Contains(row.Name, materialized, StringComparison.Ordinal));
    }

    [Fact]
    public async Task AnExpandedPayload_IsIdenticalWhetherOrNotTheListWasMaterialized()
    {
        // Arrange
        var query = $"{ODataQueryParameterTransformer.ExpandQueryOption}={nameof(PipelineRow.Detail)}";

        // Act
        var materialized = await BodyWithoutContextAsync(ListUrl(MaterializedRowsController.EntitySetName, query));
        var streamed = await BodyWithoutContextAsync(ListUrl(StreamedRowsController.EntitySetName, query));

        // Assert
        Assert.Equal(streamed, materialized);
        Assert.All(_rows.Rows, row => Assert.Contains(row.Id.ToString(), materialized, StringComparison.Ordinal));
        Assert.All(_rows.Rows, row => Assert.Contains(row.Detail.Id.ToString(), materialized, StringComparison.Ordinal));
        Assert.All(_rows.Rows, row => Assert.Contains(row.Detail.Label, materialized, StringComparison.Ordinal));
    }

    private static string ListUrl(string entitySetName, string? query = null) =>
        query is null ? $"/{RoutePrefix}/{entitySetName}" : $"/{RoutePrefix}/{entitySetName}?{query}";

    private static async Task<bool> BodyIsCompleteJsonAsync(HttpResponseMessage response)
    {
        try
        {
            var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            using var document = JsonDocument.Parse(body);
            return true;
        }
        catch (Exception exception) when (exception is JsonException or IOException or HttpRequestException)
        {
            return false;
        }
    }

    private HttpClient Client() => _client ?? throw new InvalidOperationException("The test host has not started.");

    private Task<HttpResponseMessage> GetWithoutBufferingTheBodyAsync(string url) =>
        Client().GetAsync(url, HttpCompletionOption.ResponseHeadersRead, TestContext.Current.CancellationToken);

    private async Task<string> BodyWithoutContextAsync(string url)
    {
        var response = await GetWithoutBufferingTheBodyAsync(url);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"{url} answered {(int)response.StatusCode}: {body}");
        var node = JsonNode.Parse(body) ?? throw new InvalidOperationException("The list body was not JSON.");
        node.AsObject().Remove(ODataProtocolConstants.ContextAnnotation);
        return node.ToJsonString();
    }
}
