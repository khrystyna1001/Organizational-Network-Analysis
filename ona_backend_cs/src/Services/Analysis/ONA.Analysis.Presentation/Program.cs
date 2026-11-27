using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using ONA.Analysis.Infrastructure;
using ONA.Analysis.Presentation.Hubs;
using ONA.Analysis.Presentation.Middleware;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers().AddJsonOptions(options =>
    options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower);
builder.Services.AddOpenApi(options =>
    options.AddDocumentTransformer((document, _, _) =>
    {
        document.Info.Title = "ONA Analysis API";
        document.Info.Version = "v1";
        document.Info.Description = "Calculate organizational network metrics and detect management bottlenecks.";
        return Task.CompletedTask;
    }));
// Live updates: SignalR (hub /hubs/analysis) fed by MediatR notifications published in the Application layer.
// JSON like the HTTP API (snake_case). The Application assembly is registered by AddInfrastructure.
builder.Services.AddSignalR().AddJsonProtocol(options =>
    options.PayloadSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower);
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<AnalysisHub>());
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
    policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));
// JWT issued by Identity; shared settings Jwt:Key / Jwt:Issuer / Jwt:Audience (read lazily).
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IConfiguration>((options, configuration) =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = configuration["Jwt:Issuer"] ?? "ona-identity",
            ValidateAudience = true,
            ValidAudience = configuration["Jwt:Audience"] ?? "ona",
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
                configuration["Jwt:Key"] ?? throw new InvalidOperationException("Configuration 'Jwt:Key' is not set."))),
            RoleClaimType = "role",
            NameClaimType = "sub"
        };

        // Browsers cannot send an Authorization header on a WebSocket: SignalR passes the token in the query string.
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var token = context.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(token) && context.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                {
                    context.Token = token;
                }

                return Task.CompletedTask;
            }
        };
    });
builder.Services.AddAuthorization();
builder.Services.AddInfrastructure(provider =>
    provider.GetRequiredService<IConfiguration>().GetConnectionString("Analysis")
    ?? throw new InvalidOperationException(
        "Connection string 'Analysis' is not configured. Set ConnectionStrings__Analysis or use User Secrets."),
    builder.Configuration);

var app = builder.Build();

// Test seam: API tests use fake repositories and set Database:InitializeOnStartup=false.
if (app.Configuration.GetValue("Database:InitializeOnStartup", true))
{
    await app.Services.InitializeAnalysisDatabaseAsync();
}

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
app.MapHub<AnalysisHub>(AnalysisHub.Path);

app.Run();

public partial class Program
{
}
