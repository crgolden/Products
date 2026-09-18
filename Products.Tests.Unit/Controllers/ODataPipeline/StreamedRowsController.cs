namespace Products.Tests.Unit.Controllers.ODataPipeline;

using Microsoft.AspNetCore.OData.Query;
using Microsoft.AspNetCore.OData.Routing.Controllers;

public sealed class StreamedRowsController : ODataController
{
    internal const string EntitySetName = "StreamedRows";

    [EnableQuery]
    public IQueryable<PipelineRow> Get() => PipelineRows.FailingAfterTheRows();
}
