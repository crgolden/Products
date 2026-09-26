namespace Products.Tests.Unit.Controllers.ODataPipeline;

public sealed class PipelineRow
{
    public Guid Id { get; set; }

    public required string Name { get; set; }

    public required PipelineRowDetail Detail { get; set; }
}
