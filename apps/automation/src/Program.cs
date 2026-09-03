using DotNetMonoRepoTemplate.Logging;
using DotNetMonoRepoTemplate.Metrics;
using DotNetMonoRepoTemplate.Observability;
using Elsa.Extensions;
using Elsa.Persistence.EFCore.Extensions;
using Elsa.Persistence.EFCore.Modules.Management;
using Elsa.Persistence.EFCore.Modules.Runtime;
using Serilog;
using WorkflowApi.Configuration;

using var sentry = SentryBootstrapper.Init();

var builder = WebApplication.CreateBuilder(args);

var workflowApiOptions = WorkflowApiOptionsFactory.Load(builder.Configuration);
builder.Services.AddSingleton(workflowApiOptions);
builder.WebHost.UseUrls($"http://0.0.0.0:{workflowApiOptions.Port}");

Log.Logger = SerilogBootstrapper.CreateBaseConfiguration("automation").CreateLogger();
builder.Host.UseSerilog();

builder.Services.AddDotNetMonoRepoTemplateTelemetry("automation");
builder.Services.AddDotNetMonoRepoTemplateMetrics("automation_");

builder.Services.AddElsa(elsa =>
{
    elsa.UseWorkflowManagement(management =>
        management.UseEntityFrameworkCore(ef => ef.UsePostgreSql(workflowApiOptions.DatabaseUrl)));

    elsa.UseWorkflowRuntime(runtime =>
        runtime.UseEntityFrameworkCore(ef => ef.UsePostgreSql(workflowApiOptions.DatabaseUrl)));

    elsa.UseWorkflowsApi();
    elsa.UseHttp();
    elsa.UseScheduling();
});

builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
    .WithOrigins(workflowApiOptions.CorsOrigin)
    .AllowAnyHeader()
    .AllowAnyMethod()));

var app = builder.Build();

app.UseCors();
app.UseDotNetMonoRepoTemplateMetrics();
app.UseRouting();
app.UseWorkflowsApi();
app.UseWorkflows();

app.MapDotNetMonoRepoTemplateMetrics();
app.MapGet("/api/v1/ping", () => Results.Ok(new { status = "pong" })).AllowAnonymous();

var logger = new Logger("WorkflowApi");
logger.Info("Automation (Elsa Workflows) API started", new Dictionary<string, object?> { ["port"] = workflowApiOptions.Port });

app.Run();
