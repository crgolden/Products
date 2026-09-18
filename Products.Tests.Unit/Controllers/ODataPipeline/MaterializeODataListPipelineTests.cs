namespace Products.Tests.Unit.Controllers.ODataPipeline;

using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.OData;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.OData.ModelBuilder;

[Trait("Category", "Unit")]
public sealed class MaterializeODataListPipelineTests : IAsyncLifetime
{
    private const string RoutePrefix = "odata";

    private const string ContextPropertyName = "@odata.context";

    private const string DetailEntitySetName = "PipelineRowDetails";

    private WebApplication? _app;

    private HttpClient? _client;

    public async ValueTask InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseTestServer();
        builder.Services.AddControllers().AddOData(options =>
        {
            var modelBuilder = new ODataConventionModelBuilder();
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
        _app.UseExceptionHandler(errors => errors.Run(context =>
        {
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            return Task.CompletedTask;
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
        var url = ListUrl(MaterializedRowsController.EntitySetName);

        // Act
        var response = await Client().GetAsync(url, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    [Fact]
    public async Task WithoutMaterialization_TheSameFailure_IsA200WhoseBodyNeverCloses()
    {
        // Arrange
        var url = ListUrl(StreamedRowsController.EntitySetName);

        // Act
        var response = await Client().GetAsync(url, TestContext.Current.CancellationToken);

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
        var query = $"$select={nameof(PipelineRow.Name)}&$top={PipelineRows.RowsBeforeTheFailure}";

        // Act
        var materialized = await BodyWithoutContextAsync(ListUrl(MaterializedRowsController.EntitySetName, query));
        var streamed = await BodyWithoutContextAsync(ListUrl(StreamedRowsController.EntitySetName, query));

        // Assert
        Assert.Equal(streamed, materialized);
    }

    [Fact]
    public async Task AnExpandedPayload_IsIdenticalWhetherOrNotTheListWasMaterialized()
    {
        // Arrange
        var query = $"$expand={nameof(PipelineRow.Detail)}&$top={PipelineRows.RowsBeforeTheFailure}";

        // Act
        var materialized = await BodyWithoutContextAsync(ListUrl(MaterializedRowsController.EntitySetName, query));
        var streamed = await BodyWithoutContextAsync(ListUrl(StreamedRowsController.EntitySetName, query));

        // Assert
        Assert.Equal(streamed, materialized);
        Assert.Contains(nameof(PipelineRow.Detail), materialized, StringComparison.Ordinal);
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

    private async Task<string> BodyWithoutContextAsync(string url)
    {
        var response = await Client().GetAsync(url, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var node = JsonNode.Parse(body) ?? throw new InvalidOperationException("The list body was not JSON.");
        node.AsObject().Remove(ContextPropertyName);
        return node.ToJsonString();
    }
}
