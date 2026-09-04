using DotNetMonoRepoTemplate.Database.Entities;
using DotNetMonoRepoTemplate.Types;

namespace IngestionApi.Tests.Fixtures;

public sealed class SourceConnectorBuilder
{
    private string _sourceSystemId = "src-tiktok";
    private string _accountId = "act-1";
    private int _restatementDays = 28;
    private AccountTier _tier = AccountTier.Tier1;
    private decimal _trailingSpend = 100_000m;
    private bool _isActive = true;

    public SourceConnectorBuilder WithAccountId(string accountId)
    {
        _accountId = accountId;
        return this;
    }

    public SourceConnectorBuilder WithRestatementDays(int restatementDays)
    {
        _restatementDays = restatementDays;
        return this;
    }

    public SourceConnectorBuilder WithTier(AccountTier tier)
    {
        _tier = tier;
        return this;
    }

    public SourceConnectorBuilder WithTrailingSpend(decimal trailingSpend)
    {
        _trailingSpend = trailingSpend;
        return this;
    }

    public SourceConnectorBuilder Inactive()
    {
        _isActive = false;
        return this;
    }

    public SourceConnector Build() => new()
    {
        SourceSystemId = _sourceSystemId,
        SourceEntity = "ad_insights_daily",
        ContractVersion = "2026-06",
        AccountId = _accountId,
        CronSchedule = "0 2 * * *",
        Tier = _tier,
        RestatementDays = _restatementDays,
        TrailingNinetyDaySpendZar = _trailingSpend,
        IsActive = _isActive,
    };
}
