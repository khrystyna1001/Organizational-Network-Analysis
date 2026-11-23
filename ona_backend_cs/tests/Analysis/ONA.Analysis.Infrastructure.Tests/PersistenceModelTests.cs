using Microsoft.EntityFrameworkCore;
using ONA.Analysis.Domain.Aggregates;
using ONA.Analysis.Infrastructure.Persistence;

namespace ONA.Analysis.Infrastructure.Tests;

public sealed class PersistenceModelTests
{
    [Fact]
    public void Model_MapsAnalysisRunToSnakeCaseColumns()
    {
        var options = new DbContextOptionsBuilder<AnalysisDbContext>()
            .UseMySQL("Server=localhost;Database=unused;User Id=x;Password=x")
            .Options;
        using var context = new AnalysisDbContext(options);

        var entity = context.Model.FindEntityType(typeof(AnalysisRun))!;
        var columns = entity.GetProperties().Select(p => p.GetColumnName()).ToList();

        Assert.Equal("analysis_runs", entity.GetTableName());
        Assert.Contains("graph_version_id", columns);
        Assert.Contains("node_metrics", columns);
        Assert.Contains("metrics", columns);
    }
}
