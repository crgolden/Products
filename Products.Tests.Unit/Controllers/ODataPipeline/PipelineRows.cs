namespace Products.Tests.Unit.Controllers.ODataPipeline;

internal static class PipelineRows
{
    internal static readonly int RowsBeforeTheFailure = Random.Shared.Next(1, 5);

    internal static readonly PipelineRow[] Rows = Enumerable
        .Range(0, RowsBeforeTheFailure)
        .Select(_ => RowWithDetail())
        .ToArray();

    internal static IQueryable<PipelineRow> AllRows() => Rows.AsQueryable();

    internal static IQueryable<PipelineRow> FailingAfterTheRows() => YieldThenFail().AsQueryable();

    private static PipelineRow RowWithDetail()
    {
        var rowId = Guid.NewGuid();
        var rowName = Generated.NewProductName();
        var detailId = Guid.NewGuid();
        var detailLabel = Generated.NewProductName();
        return new PipelineRow
        {
            Id = rowId,
            Name = rowName,
            Detail = new PipelineRowDetail { Id = detailId, Label = detailLabel },
        };
    }

    private static IEnumerable<PipelineRow> YieldThenFail()
    {
        foreach (var row in Rows)
        {
            yield return row;
        }

        throw new InvalidOperationException("the provider failed after the last row it had");
    }
}
