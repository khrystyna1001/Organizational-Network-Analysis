using System.Text.Json;
using ONA.Graph.Infrastructure;
using ONA.Graph.Presentation.Middleware;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers().AddJsonOptions(options =>
    options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower);
builder.Services.AddOpenApi(options =>
    options.AddDocumentTransformer((document, _, _) =>
    {
        document.Info.Title = "ONA Graph API";
        document.Info.Version = "v1";
        document.Info.Description =
            "Import organizational CSV data and manage network boards, nodes, and weighted connections.";
        return Task.CompletedTask;
    }));
builder.Services.AddInfrastructure(provider =>
    provider.GetRequiredService<IConfiguration>().GetConnectionString("Graph")
    ?? throw new InvalidOperationException(
        "Connection string 'Graph' is not configured. Set ConnectionStrings__Graph or use User Secrets."));

var app = builder.Build();

await app.Services.InitializeGraphDatabaseAsync();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();

public partial class Program
{
}
