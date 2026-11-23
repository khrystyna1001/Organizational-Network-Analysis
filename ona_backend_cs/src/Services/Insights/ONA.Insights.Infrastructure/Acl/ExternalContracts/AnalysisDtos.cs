// Foreign Analysis contract. Never leaves Infrastructure/Acl.
#pragma warning disable IDE1006, CA1707
namespace ONA.Insights.Infrastructure.Acl.ExternalContracts;

internal sealed class ANALYSIS_NODE_METRIC_DTO
{
    public string? NodeId { get; set; }
    public string? NodeName { get; set; }
    public int InDegree { get; set; }
    public int OutDegree { get; set; }
    public double Betweenness { get; set; }
    public bool IsBottleneck { get; set; }
}

internal sealed class ANALYSIS_RUN_DTO
{
    public string? Id { get; set; }
    public string? BoardId { get; set; }
    public string? GraphVersionId { get; set; }
    public string? Status { get; set; }
    public string? ErrorMessage { get; set; }
    public List<ANALYSIS_NODE_METRIC_DTO>? NodeMetrics { get; set; }
    public List<string>? Bottlenecks { get; set; }
}
