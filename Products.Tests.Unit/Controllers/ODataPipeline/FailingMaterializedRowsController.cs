namespace Products.Tests.Unit.Controllers.ODataPipeline;

using Microsoft.AspNetCore.OData.Query;
using Microsoft.AspNetCore.OData.Routing.Controllers;
using Products.Controllers;
using Products.Tests.Unit.TestSupport;

public sealed class FailingMaterializedRowsController : ODataController
{
    internal static readonly string EntitySetName = ConventionalEntitySets.For<FailingMaterializedRowsController>();

    private readonly PipelineRows _rows;

    public FailingMaterializedRowsController(PipelineRows rows)
    {
        _rows = rows;
    }

    [EnableQuery]
    [MaterializeODataList]
    public IQueryable<PipelineRow> Get() => _rows.FailingAfterTheRows();
}
