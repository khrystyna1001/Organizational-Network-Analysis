using Microsoft.EntityFrameworkCore;
using ONA.Graph.Infrastructure.Persistence;
using Testcontainers.MySql;

namespace ONA.Graph.Infrastructure.IntegrationTests;

public sealed class MySqlDatabaseFixture : IAsyncLifetime
{
    private readonly MySqlContainer _container = new MySqlBuilder("mysql:8.4")
        .WithDatabase("ona_graph_tests")
        .WithUsername("ona_test")
        .WithPassword("ona_test_password")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public Task InitializeAsync() => _container.StartAsync();

    public async Task DisposeAsync() => await _container.DisposeAsync();

    public async Task ResetDatabaseAsync()
    {
        await using var context = CreateDbContext();
        await context.Database.EnsureDeletedAsync();
        await context.Database.MigrateAsync();
    }

    internal GraphDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<GraphDbContext>()
            .UseMySQL(ConnectionString)
            .Options;
        return new GraphDbContext(options);
    }
}

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class MySqlCollection : ICollectionFixture<MySqlDatabaseFixture>
{
    public const string Name = "MySQL integration tests";
}
