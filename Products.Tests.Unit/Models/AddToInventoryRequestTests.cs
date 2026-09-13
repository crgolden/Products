namespace Products.Tests.Unit.Models;

using System.Text.Json;
using Products.Models;
using Products.Tests.Unit.TestSupport;

public class AddToInventoryRequestTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void ManualUrl_DeserializesFromAJsonString_SoTheRequestContractIsUnchanged()
    {
        var manualUrl = TestValues.NewManualUrl();
        var json = JsonSerializer.Serialize(
            new { manualUrl = manualUrl.AbsoluteUri },
            JsonSerializerOptions.Web);

        var request = JsonSerializer.Deserialize<AddToInventoryRequest>(json, JsonSerializerOptions.Web);

        Assert.NotNull(request);
        Assert.Equal(manualUrl, request.ManualUrl);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ManualUrl_SerializesBackToTheSameJsonString()
    {
        var manualUrl = TestValues.NewManualUrl();
        var request = new AddToInventoryRequest { ManualUrl = manualUrl };

        var json = JsonSerializer.Serialize(request, JsonSerializerOptions.Web);
        var roundTripped = JsonSerializer.Deserialize<AddToInventoryRequest>(json, JsonSerializerOptions.Web);

        Assert.NotNull(roundTripped);
        Assert.Equal(manualUrl, roundTripped.ManualUrl);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ManualUrl_IsNullWhenTheJsonOmitsIt()
    {
        var json = JsonSerializer.Serialize(
            new { name = TestValues.NewProductName() },
            JsonSerializerOptions.Web);

        var request = JsonSerializer.Deserialize<AddToInventoryRequest>(json, JsonSerializerOptions.Web);

        Assert.NotNull(request);
        Assert.Null(request.ManualUrl);
    }
}
