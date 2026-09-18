namespace Products.Tests.Unit.Controllers.ODataPipeline;

public sealed class PipelineRow
{
    public Guid Id { get; set; }

    public string? Name { get; set; }

    public PipelineRowDetail? Detail { get; set; }
}
