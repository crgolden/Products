namespace Products.Tests.Unit.Models;

using System.Text.Json;
using Products.Models;

[Trait("Category", "Unit")]
public class AddToInventoryRequestTests
{
    [Fact]
    public void ManualUrl_DeserializesFromAJsonString_SoTheRequestContractIsUnchanged()
    {
        // Arrange
        var manualUrl = Generated.NewManualUrl();
        var json = JsonSerializer.Serialize(
            new { manualUrl = manualUrl.AbsoluteUri },
            JsonSerializerOptions.Web);

        // Act
        var request = JsonSerializer.Deserialize<AddToInventoryRequest>(json, JsonSerializerOptions.Web);

        // Assert
        Assert.NotNull(request);
        Assert.Equal(manualUrl, request.ManualUrl);
    }

    [Fact]
    public void ManualUrl_SerializesBackToTheSameJsonString()
    {
        // Arrange
        var manualUrl = Generated.NewManualUrl();
        var request = new AddToInventoryRequest { ManualUrl = manualUrl };
        var json = JsonSerializer.Serialize(request, JsonSerializerOptions.Web);

        // Act
        var roundTripped = JsonSerializer.Deserialize<AddToInventoryRequest>(json, JsonSerializerOptions.Web);

        // Assert
        Assert.NotNull(roundTripped);
        Assert.Equal(manualUrl, roundTripped.ManualUrl);
    }

    [Fact]
    public void ManualUrl_IsNullWhenTheJsonOmitsIt()
    {
        // Arrange
        var json = JsonSerializer.Serialize(
            new { name = Generated.NewProductName() },
            JsonSerializerOptions.Web);

        // Act
        var request = JsonSerializer.Deserialize<AddToInventoryRequest>(json, JsonSerializerOptions.Web);

        // Assert
        Assert.NotNull(request);
        Assert.Null(request.ManualUrl);
    }
}
