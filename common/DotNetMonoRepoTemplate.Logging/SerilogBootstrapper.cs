using Serilog;
using Serilog.Events;
using Serilog.Formatting.Json;

namespace DotNetMonoRepoTemplate.Logging;

public static class SerilogBootstrapper
{
    public static LoggerConfiguration CreateBaseConfiguration(string serviceName)
    {
        var configuration = new LoggerConfiguration()
            .Enrich.FromLogContext()
            .Enrich.WithProperty("service", serviceName)
            .MinimumLevel.Is(ResolveMinimumLevel())
            .WriteTo.Console(new JsonFormatter());

        var logsEndpoint = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_LOGS_ENDPOINT");
        if (!string.IsNullOrWhiteSpace(logsEndpoint))
        {
            configuration = configuration.WriteTo.OpenTelemetry(options => options.Endpoint = logsEndpoint);
        }

        return configuration;
    }

    private static LogEventLevel ResolveMinimumLevel()
    {
        var configuredLevel = Environment.GetEnvironmentVariable("LOG_LEVEL");
        if (string.IsNullOrWhiteSpace(configuredLevel))
        {
            var isProduction = string.Equals(
                Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT"),
                "Production",
                StringComparison.OrdinalIgnoreCase);
            configuredLevel = isProduction ? "info" : "debug";
        }

        return configuredLevel.ToLowerInvariant() switch
        {
            "trace" => LogEventLevel.Verbose,
            "debug" => LogEventLevel.Debug,
            "info" => LogEventLevel.Information,
            "warn" => LogEventLevel.Warning,
            "error" => LogEventLevel.Error,
            "fatal" => LogEventLevel.Fatal,
            _ => LogEventLevel.Information,
        };
    }
}
