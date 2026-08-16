using GameCollector.Api.Authentication;
using GameCollector.Api.Configuration;
using GameCollector.Api.Middleware;
using GameCollector.Infrastructure;
using GameCollector.Application;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddApplication();
builder.Services.AddApiProblemDetails();
builder.Services.AddApiHardening(builder.Configuration);
builder.Services.AddHsts(options =>
{
    options.MaxAge = TimeSpan.FromDays(365);
    options.IncludeSubDomains = true;
});
builder.Services.AddKeycloakAuthentication(builder.Configuration);
builder.Services.AddCurrentUser();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddOpenApi("v1");

var app = builder.Build();

await app.Services.InitializeDatabaseAsync();

if (!app.Environment.IsDevelopment()) app.UseHsts();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseMiddleware<RequestSizeLimitMiddleware>();
app.UseMiddleware<RequestLoggingMiddleware>();
app.UseExceptionHandler();
app.UseStatusCodePages();

app.UseRequestTimeouts();
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();

app.MapControllers();
app.MapOpenApi("/openapi/{documentName}.json");
app.MapHealthChecks("/health/live", new()
{
    Predicate = _ => false
}).DisableRateLimiting();
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("ready")
}).DisableRateLimiting();

app.Run();

public partial class Program;
