namespace Products.Tests.Unit.Models;

using Microsoft.AspNetCore.OData.Query;
using Microsoft.OData.Edm;
using Microsoft.OData.ModelBuilder;
using Products.Models;

public class CatalogProductTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void CatalogProduct_HasNoOwnerIdProperty_SoTheAnonymousSurfaceCannotLeakOwnership()
    {
        // Act
        var ownerProperty = typeof(CatalogProduct).GetProperty(nameof(InventoryItem.OwnerId));

        // Assert
        Assert.Null(ownerProperty);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void CatalogProduct_DeclaresNoOwnerIdOnTheEdm_SoItCannotBeSelected()
    {
        // Arrange
        var entity = BuildCatalogProductEntity();

        // Act
        var ownerProperty = entity.FindProperty(nameof(InventoryItem.OwnerId));

        // Assert
        Assert.Null(ownerProperty);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void CatalogProduct_MatchKey_IsNotDeclaredOnTheEdm_SoAClientCannotSelectOrPatchIt()
    {
        // Arrange
        var entity = BuildCatalogProductEntity();

        // Act
        var matchKeyProperty = entity.FindProperty(nameof(CatalogProduct.MatchKey));

        // Assert
        Assert.Null(matchKeyProperty);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void CatalogProduct_ManualUrl_IsDeclaredAsAnEdmString_SoAClientCanSendAUrl()
    {
        // Arrange
        var entity = BuildCatalogProductEntity();
        var manualUrl = Assert.IsType<IEdmStructuralProperty>(
            entity.FindProperty(nameof(CatalogProduct.ManualUrl)),
            exactMatch: false);

        // Act
        var kind = manualUrl.Type.Definition.AsElementType() is IEdmPrimitiveType primitive
            ? primitive.PrimitiveKind
            : EdmPrimitiveTypeKind.None;

        // Assert
        Assert.Equal(EdmPrimitiveTypeKind.String, kind);
    }

    private static IEdmEntityType BuildCatalogProductEntity()
    {
        var modelBuilder = new ODataConventionModelBuilder();
        modelBuilder.EntitySet<CatalogProduct>(CatalogProduct.EntitySetName);
        modelBuilder.EntityType<CatalogProduct>().Ignore(c => c.MatchKey);

        var model = modelBuilder.GetEdmModel();
        return Assert.IsType<IEdmEntityType>(
            model.FindDeclaredType(typeof(CatalogProduct).FullName),
            exactMatch: false);
    }
}