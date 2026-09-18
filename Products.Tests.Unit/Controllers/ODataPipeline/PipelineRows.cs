namespace Products.Tests.Unit.Controllers.ODataPipeline;

using Products.Tests.Unit.TestSupport;

internal static class PipelineRows
{
    internal static readonly int RowsBeforeTheFailure = Random.Shared.Next(1, 5);

    internal static readonly PipelineRow[] Rows = Enumerable
        .Range(0, RowsBeforeTheFailure)
        .Select(_ => new PipelineRow
        {
            Id = Guid.NewGuid(),
            Name = TestValues.NewProductName(),
            Detail = new PipelineRowDetail { Id = Guid.NewGuid(), Label = TestValues.NewProductName() },
        })
        .ToArray();

    internal static IQueryable<PipelineRow> FailingAfterTheRows() => YieldThenFail().AsQueryable();

    private static IEnumerable<PipelineRow> YieldThenFail()
    {
        foreach (var row in Rows)
        {
            yield return row;
        }

        throw new InvalidOperationException("the provider failed after the last row it had");
    }
}
