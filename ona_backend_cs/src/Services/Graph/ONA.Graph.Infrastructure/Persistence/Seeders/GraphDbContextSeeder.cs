using Microsoft.EntityFrameworkCore;
using ONA.Graph.Domain.Aggregates;

namespace ONA.Graph.Infrastructure.Persistence.Seeders;

public sealed class GraphDbContextSeeder(GraphDbContext context)
{
    public async Task SeedAsync()
    {
        if (context.Database.IsRelational())
        {
            await context.Database.MigrateAsync();
        }
        else
        {
            await context.Database.EnsureCreatedAsync();
        }

        if (await context.Boards.AnyAsync())
        {
            return;
        }

        var starterBoard = new Board(
            Guid.NewGuid().ToString(), 
            "Starter Board", 
            "A complete default board for evaluation.");

        var edgeCaseBoard = new Board(
            Guid.NewGuid().ToString(), 
            "Edge Case Board", 
            "Board intended for boundary testing.");

        context.Boards.AddRange(starterBoard, edgeCaseBoard);
        await context.SaveChangesAsync();
    }
}