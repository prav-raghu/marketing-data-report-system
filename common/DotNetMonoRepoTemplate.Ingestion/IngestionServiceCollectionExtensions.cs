using DotNetMonoRepoTemplate.Ingestion.Lake;
using DotNetMonoRepoTemplate.Ingestion.Writing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DotNetMonoRepoTemplate.Ingestion;

public static class IngestionServiceCollectionExtensions
{
    public static IServiceCollection AddDotNetMonoRepoTemplateIngestion(
        this IServiceCollection services,
        RawZoneOptions rawZoneOptions,
        EnvelopeWriterOptions? envelopeWriterOptions = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(rawZoneOptions);

        services.AddSingleton(rawZoneOptions);
        services.AddSingleton(envelopeWriterOptions ?? new EnvelopeWriterOptions());
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<IRawZoneWriter, BlobRawZoneWriter>();
        services.AddSingleton<IEnvelopeWriter, EnvelopeWriter>();

        return services;
    }
}
