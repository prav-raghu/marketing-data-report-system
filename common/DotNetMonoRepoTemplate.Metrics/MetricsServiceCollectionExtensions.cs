using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Prometheus;

namespace DotNetMonoRepoTemplate.Metrics;

public static class MetricsServiceCollectionExtensions
{
    public static IServiceCollection AddDotNetMonoRepoTemplateMetrics(this IServiceCollection services, string prefix = "")
    {
        DotNetStats.Register(Prometheus.Metrics.DefaultRegistry);
        services.AddSingleton(new CustomMetricsFactory(prefix));
        services.AddSingleton(new DatabaseMetrics(prefix));
        services.AddSingleton(new CacheMetrics(prefix));
        return services;
    }

    public static IApplicationBuilder UseDotNetMonoRepoTemplateMetrics(this IApplicationBuilder app) => app.UseHttpMetrics();

    public static void MapDotNetMonoRepoTemplateMetrics(this IEndpointRouteBuilder endpoints, string pattern = "/metrics") =>
        endpoints.MapMetrics(pattern);
}
