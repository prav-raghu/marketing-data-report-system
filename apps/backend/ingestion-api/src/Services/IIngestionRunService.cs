using IngestionApi.Dtos;

namespace IngestionApi.Services;

public interface IIngestionRunService
{
    Task<IngestionRunDto> StartAsync(StartRunRequestDto request, CancellationToken cancellationToken);

    Task<IngestionRunDto?> GetAsync(string runId, CancellationToken cancellationToken);

    Task<IngestionRunDto?> CompleteAsync(string runId, CompleteRunRequestDto request, CancellationToken cancellationToken);

    Task<IngestionRunDto?> FailAsync(string runId, FailRunRequestDto request, CancellationToken cancellationToken);
}
