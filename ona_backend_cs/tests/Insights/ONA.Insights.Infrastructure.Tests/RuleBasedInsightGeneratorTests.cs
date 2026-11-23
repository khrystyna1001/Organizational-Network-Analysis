using ONA.Insights.Application.Ports;
using ONA.Insights.Infrastructure.Generation;

namespace ONA.Insights.Infrastructure.Tests;

public sealed class RuleBasedInsightGeneratorTests
{
    private static readonly InsightRequest Request = new("Alice Smith", 0.6, 3, 4, 0.2, 2.5, 20);

    [Fact]
    public async Task Generate_IsDeterministic()
    {
        var generator = new RuleBasedInsightGenerator();

        Assert.Equal(await generator.GenerateAsync(Request), await generator.GenerateAsync(Request));
    }

    [Fact]
    public async Task Generate_MentionsNodeAndAsksForHumanVerification()
    {
        var text = await new RuleBasedInsightGenerator().GenerateAsync(Request);

        Assert.Contains("Alice Smith", text);
        Assert.Contains("3.0x the average", text);
        Assert.Contains("delegated", text);
        Assert.Contains("Administrative load", text);
        Assert.Contains("human must verify", text);
    }

    [Fact]
    public async Task Generate_WithZeroAverage_DoesNotDivideByZero()
    {
        var text = await new RuleBasedInsightGenerator().GenerateAsync(Request with { AverageBetweenness = 0 });

        Assert.Contains("far above the average", text);
    }

    [Fact]
    public async Task Generate_OnALargeGraph_ShowsTinyBetweennessInScientificNotationNotZero()
    {
        // Betweenness is normalised by (n-1)(n-2): with ~6000 people a real bottleneck scores ~1e-8.
        var text = await new RuleBasedInsightGenerator().GenerateAsync(
            Request with { Betweenness = 8.5e-8, AverageBetweenness = 1e-8, NodeCount = 5945 });

        Assert.Contains("betweenness 8.5e-8", text);
        Assert.Contains("8.5x the average", text);
    }
}
