namespace Products.Tests.Unit.Controllers.ODataPipeline;

using Microsoft.AspNetCore.OData.Query;
using Microsoft.AspNetCore.OData.Routing.Controllers;
using Products.Tests.Unit.TestSupport;

public sealed class FailingStreamedRowsController : ODataController
{
    internal static readonly string EntitySetName = ConventionalEntitySets.For<FailingStreamedRowsController>();

    private readonly PipelineRows _rows;

    public FailingStreamedRowsController(PipelineRows rows)
    {
        _rows = rows;
    }

    [EnableQuery]
    public IQueryable<PipelineRow> Get() => _rows.FailingAfterTheRows();
}
