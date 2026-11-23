using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
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

builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
    policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Resolved lazily so test hosts can override configuration.
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            RoleClaimType = "role",
            NameClaimType = "sub"
        };
    });
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IConfiguration>((options, configuration) =>
    {
        var key = configuration["Jwt:Key"];
        if (string.IsNullOrWhiteSpace(key) || key.Length < 32)
        {
            throw new InvalidOperationException("Jwt:Key must be set and at least 32 characters long.");
        }

        options.TokenValidationParameters.ValidIssuer = configuration["Jwt:Issuer"] ?? "ona-identity";
        options.TokenValidationParameters.ValidAudience = configuration["Jwt:Audience"] ?? "ona";
        options.TokenValidationParameters.IssuerSigningKey =
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
    });
builder.Services.AddAuthorization();

var app = builder.Build();

await app.Services.InitializeGraphDatabaseAsync();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseCors();
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

public partial class Program
{
}
