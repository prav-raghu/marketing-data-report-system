using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace DotNetMonoRepoTemplate.Logging;

public static class TelemetryServiceCollectionExtensions
{
    public static IServiceCollection AddDotNetMonoRepoTemplateTelemetry(
        this IServiceCollection services,
        string serviceName,
        string serviceVersion = "1.0.0")
    {
        var tracesEndpoint = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_TRACES_ENDPOINT");
        if (string.IsNullOrWhiteSpace(tracesEndpoint))
        {
            return services;
        }

        services
            .AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(serviceName, serviceVersion: serviceVersion))
            .WithTracing(tracing => tracing
                .AddAspNetCoreInstrumentation(options =>
                {
                    options.Filter = context =>
                    {
                        var path = context.Request.Path.Value ?? string.Empty;
                        return !path.Contains("/health", StringComparison.OrdinalIgnoreCase)
                            && !path.Contains("/metrics", StringComparison.OrdinalIgnoreCase)
                            && path != "/favicon.ico";
                    };
                })
                .AddHttpClientInstrumentation()
                .AddOtlpExporter(otlp => otlp.Endpoint = new Uri(tracesEndpoint)));

        return services;
    }
}
