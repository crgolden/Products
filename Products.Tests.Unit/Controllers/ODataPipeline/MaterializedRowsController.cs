namespace Products.Tests.Unit.Controllers.ODataPipeline;

using Microsoft.AspNetCore.OData.Query;
using Microsoft.AspNetCore.OData.Routing.Controllers;
using Products.Controllers;

public sealed class MaterializedRowsController : ODataController
{
    internal const string EntitySetName = "MaterializedRows";

    [EnableQuery]
    [MaterializeODataList]
    public IQueryable<PipelineRow> Get() => PipelineRows.FailingAfterTheRows();
}
