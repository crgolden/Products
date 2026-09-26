namespace Products.Tests.Unit.Controllers.ODataPipeline;

using Microsoft.AspNetCore.OData.Query;
using Microsoft.AspNetCore.OData.Routing.Controllers;
using Products.Tests.Unit.TestSupport;

public sealed class FailingStreamedRowsController : ODataController
{
    internal static readonly string EntitySetName = ConventionalEntitySets.For<FailingStreamedRowsController>();

    [EnableQuery]
    public IQueryable<PipelineRow> Get() => PipelineRows.FailingAfterTheRows();
}
