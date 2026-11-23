namespace ONA.Insights.Application.Ports;

public sealed record InsightRequest(
    string NodeName,
    double Betweenness,
    int InDegree,
    int OutDegree,
    double AverageBetweenness,
    double AverageDegree,
    int NodeCount);

/// <summary>Turns metrics into advice text. Rule-based today; an LLM adapter can replace it.</summary>
public interface IInsightGeneratorPort
{
    Task<string> GenerateAsync(InsightRequest request, CancellationToken cancellationToken = default);
}
