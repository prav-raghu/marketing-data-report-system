namespace IngestionApi.Services;

public interface IAccountTierService
{
    public Task<int> RecalculateAsync(CancellationToken cancellationToken);
}
