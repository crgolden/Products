namespace Products.Tests.Unit.Controllers.ODataPipeline;

public sealed class PipelineRows
{
    public PipelineRows()
    {
        var rowsBeforeTheFailure = Random.Shared.Next(1, 5);
        Rows = Enumerable
            .Range(0, rowsBeforeTheFailure)
            .Select(_ => RowWithDetail())
            .ToArray();
    }

    internal IReadOnlyList<PipelineRow> Rows { get; }

    internal IQueryable<PipelineRow> AllRows() => Rows.AsQueryable();

    internal IQueryable<PipelineRow> FailingAfterTheRows() => YieldThenFail().AsQueryable();

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

    private IEnumerable<PipelineRow> YieldThenFail()
    {
        foreach (var row in Rows)
        {
            yield return row;
        }

        throw new InvalidOperationException("the provider failed after the last row it had");
    }
}
