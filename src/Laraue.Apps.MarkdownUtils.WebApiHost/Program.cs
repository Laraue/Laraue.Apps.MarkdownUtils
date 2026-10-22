using Laraue.Apps.MarkdownUtils.Services;
using Laraue.Core.Exceptions;
using Microsoft.Extensions.Options;
using OpenTelemetry.Metrics;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.AddJsonConsole();

builder.Services.AddOpenApi();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddControllers();
builder.Services.AddHealthChecks();

builder.Services.AddOptions<OpenAiClientOptions>();
builder.Services.AddOptions<MarkdownTranslatorOptions>();

builder.Services.Configure<OpenAiClientOptions>(builder.Configuration.GetSection("OpenAiClientOptions"));
builder.Services.Configure<MarkdownTranslatorOptions>(builder.Configuration.GetSection("MarkdownTranslatorOptions"));

builder.Services.AddSingleton<ExceptionHandleMiddleware>();
builder.Services.AddSingleton<IMarkdownTranspilerService, MarkdownTranspilerService>();
builder.Services.AddSingleton<IMarkdownTranslatorService, MarkdownTranslatorService>();
builder.Services.AddSingleton<MarkdownTranslatorMetrics>();
builder.Services.AddSingleton<Laraue.Interpreter.Markdown.IMarkdownTranspiler, Laraue.Interpreter.Markdown.MarkdownTranspiler>();

builder.Services.AddHttpClient<IOpenAiClient, OpenAiClient>((services, client) =>
{
    var options = services.GetRequiredService<IOptions<OpenAiClientOptions>>();
    client.BaseAddress = new Uri(options.Value.BaseUrl);
    client.DefaultRequestHeaders.Add("Authorization", $"Bearer {options.Value.Token}");
});

builder.Services
    .AddOpenTelemetry()
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddRuntimeInstrumentation()
        .AddMeter(MarkdownTranslatorMetrics.MeterName)
        .AddPrometheusExporter());

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options =>
    {
        options
            .WithTitle("Markdown Utils API")
            .WithTheme(ScalarTheme.Purple)
            .WithDefaultHttpClient(ScalarTarget.JavaScript, ScalarClient.Axios);
    });
}

var origins = builder
    .Configuration
    .GetRequiredSection("Cors:Hosts")
    .Get<string[]>() ?? throw new InvalidOperationException();

app.UseCors(corsPolicyBuilder =>
    corsPolicyBuilder.WithOrigins(origins)
        .AllowCredentials()
        .AllowAnyMethod()
        .AllowAnyHeader());

app.UseMiddleware<ExceptionHandleMiddleware>();
app.MapHealthChecks("/_health");
app.MapPrometheusScrapingEndpoint("/_metrics");
app.MapControllers();
app.Run();