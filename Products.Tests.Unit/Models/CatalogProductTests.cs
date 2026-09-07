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
        var ownerProperty = typeof(CatalogProduct).GetProperty("OwnerId");

        Assert.Null(ownerProperty);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void CatalogProduct_DeclaresNoOwnerIdOnTheEdm_SoItCannotBeSelected()
    {
        var entity = BuildCatalogProductEntity();

        Assert.Null(entity.FindProperty("OwnerId"));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void CatalogProduct_MatchKey_IsNotDeclaredOnTheEdm_SoAClientCannotSelectOrPatchIt()
    {
        var entity = BuildCatalogProductEntity();

        Assert.Null(entity.FindProperty(nameof(CatalogProduct.MatchKey)));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void CatalogProduct_ManualUrl_IsDeclaredAsAnEdmString_SoAClientCanSendAUrl()
    {
        var entity = BuildCatalogProductEntity();
        var manualUrl = Assert.IsType<IEdmStructuralProperty>(
            entity.FindProperty(nameof(CatalogProduct.ManualUrl)),
            exactMatch: false);

        var kind = manualUrl.Type.Definition.AsElementType() is IEdmPrimitiveType primitive
            ? primitive.PrimitiveKind
            : EdmPrimitiveTypeKind.None;

        Assert.Equal(EdmPrimitiveTypeKind.String, kind);
    }

    private static IEdmEntityType BuildCatalogProductEntity()
    {
        var modelBuilder = new ODataConventionModelBuilder();
        modelBuilder.EntitySet<CatalogProduct>("CatalogProducts");
        modelBuilder.EntityType<CatalogProduct>().Ignore(c => c.MatchKey);

        var model = modelBuilder.GetEdmModel();
        return Assert.IsType<IEdmEntityType>(
            model.FindDeclaredType($"{typeof(CatalogProduct).Namespace}.{nameof(CatalogProduct)}"),
            exactMatch: false);
    }
}