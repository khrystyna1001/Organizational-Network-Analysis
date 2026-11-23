namespace ONA.Insights.Presentation.Contracts;

public sealed record GenerateRecommendationsRequest(string? AnalysisRunId);

public sealed record RespondToRecommendationRequest(string? Response, string? Comment);
