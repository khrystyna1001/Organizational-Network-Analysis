namespace ONA.Analysis.Infrastructure.Acl.ExternalContracts;

// Foreign model of the Graph snapshot endpoint (snake_case JSON). Never leaves Acl/.

internal sealed class GRAPH_NODE_DTO
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public string? Title { get; set; }
    public string? Department { get; set; }
}

internal sealed class GRAPH_CONNECTION_DTO
{
    public string? Id { get; set; }
    public string? SourceNodeId { get; set; }
    public string? TargetNodeId { get; set; }
    public double Weight { get; set; }
}

internal sealed class GRAPH_SNAPSHOT_DTO
{
    public string? BoardId { get; set; }
    public string? GraphVersionId { get; set; }
    public string? Lifecycle { get; set; }
    public List<GRAPH_NODE_DTO>? Nodes { get; set; }
    public List<GRAPH_CONNECTION_DTO>? Connections { get; set; }
}

internal sealed class GRAPH_PROBLEM_DTO
{
    public string? Code { get; set; }
}
