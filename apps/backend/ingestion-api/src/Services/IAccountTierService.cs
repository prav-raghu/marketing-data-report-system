namespace IngestionApi.Services;

public interface IAccountTierService
{
    Task<int> RecalculateAsync(CancellationToken cancellationToken);
}
