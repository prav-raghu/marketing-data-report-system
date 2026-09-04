using IngestionApi.Dtos;

namespace IngestionApi.Services;

public interface IIngestionRunService
{
    public Task<IngestionRunDto> StartAsync(StartRunRequestDto request, CancellationToken cancellationToken);

    public Task<IngestionRunDto?> GetAsync(string runId, CancellationToken cancellationToken);

    public Task<IngestionRunDto?> CompleteAsync(string runId, CompleteRunRequestDto request, CancellationToken cancellationToken);

    public Task<IngestionRunDto?> FailAsync(string runId, FailRunRequestDto request, CancellationToken cancellationToken);
}
