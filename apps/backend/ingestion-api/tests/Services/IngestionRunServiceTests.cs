using DotNetMonoRepoTemplate.Database;
using DotNetMonoRepoTemplate.Types;
using FluentAssertions;
using IngestionApi.Configuration;
using IngestionApi.Dtos;
using IngestionApi.Services;
using IngestionApi.Tests.Fixtures;
using Xunit;

namespace IngestionApi.Tests.Services;

public sealed class IngestionRunServiceTests
{
    private static readonly DateTimeOffset FixedUtcNow = new(2026, 9, 2, 22, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task StartAsync_DerivesRestatementWindowFromConnector()
    {
        await using var db = TestDbContextFactory.Create();
        var connector = new SourceConnectorBuilder().WithRestatementDays(28).Build();
        db.SourceConnectors.Add(connector);
        await db.SaveChangesAsync();

        var service = CreateService(db);

        var run = await service.StartAsync(
            new StartRunRequestDto { SourceConnectorId = connector.Id },
            CancellationToken.None);

        run.Status.Should().Be(IngestionRunStatus.Running);
        run.WindowEnd.Should().Be(new DateOnly(2026, 9, 3));
        run.WindowStart.Should().Be(new DateOnly(2026, 8, 7));
    }

    [Fact]
    public async Task StartAsync_UsesReportingTimezoneForTheAsOfDate()
    {
        await using var db = TestDbContextFactory.Create();
        var connector = new SourceConnectorBuilder().WithRestatementDays(1).Build();
        db.SourceConnectors.Add(connector);
        await db.SaveChangesAsync();

        var service = CreateService(db);

        var run = await service.StartAsync(
            new StartRunRequestDto { SourceConnectorId = connector.Id },
            CancellationToken.None);

        run.WindowStart.Should().Be(new DateOnly(2026, 9, 3));
        run.WindowEnd.Should().Be(new DateOnly(2026, 9, 3));
    }

    [Fact]
    public async Task StartAsync_WithExplicitWindow_HonoursIt()
    {
        await using var db = TestDbContextFactory.Create();
        var connector = new SourceConnectorBuilder().Build();
        db.SourceConnectors.Add(connector);
        await db.SaveChangesAsync();

        var service = CreateService(db);

        var run = await service.StartAsync(
            new StartRunRequestDto
            {
                SourceConnectorId = connector.Id,
                Trigger = IngestionRunTrigger.Backfill,
                WindowStart = new DateOnly(2025, 1, 1),
                WindowEnd = new DateOnly(2025, 1, 31),
            },
            CancellationToken.None);

        run.Trigger.Should().Be(IngestionRunTrigger.Backfill);
        run.WindowStart.Should().Be(new DateOnly(2025, 1, 1));
        run.WindowEnd.Should().Be(new DateOnly(2025, 1, 31));
    }

    [Fact]
    public async Task StartAsync_WhenARunIsAlreadyInFlight_Throws()
    {
        await using var db = TestDbContextFactory.Create();
        var connector = new SourceConnectorBuilder().Build();
        db.SourceConnectors.Add(connector);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var request = new StartRunRequestDto { SourceConnectorId = connector.Id };
        await service.StartAsync(request, CancellationToken.None);

        var act = async () => await service.StartAsync(request, CancellationToken.None);

        await act.Should().ThrowAsync<IngestionRunConflictException>();
    }

    [Fact]
    public async Task StartAsync_ForAnInactiveConnector_Throws()
    {
        await using var db = TestDbContextFactory.Create();
        var connector = new SourceConnectorBuilder().Inactive().Build();
        db.SourceConnectors.Add(connector);
        await db.SaveChangesAsync();

        var service = CreateService(db);

        var act = async () => await service.StartAsync(
            new StartRunRequestDto { SourceConnectorId = connector.Id },
            CancellationToken.None);

        await act.Should().ThrowAsync<SourceConnectorNotFoundException>();
    }

    [Fact]
    public async Task CompleteAsync_RecordsCountsAndStampsTheConnector()
    {
        await using var db = TestDbContextFactory.Create();
        var connector = new SourceConnectorBuilder().Build();
        db.SourceConnectors.Add(connector);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var run = await service.StartAsync(
            new StartRunRequestDto { SourceConnectorId = connector.Id },
            CancellationToken.None);

        var completed = await service.CompleteAsync(
            run.Id,
            new CompleteRunRequestDto { RecordCount = 4210, PartCount = 2, CompressedBytes = 91_233 },
            CancellationToken.None);

        completed.Should().NotBeNull();
        completed!.Status.Should().Be(IngestionRunStatus.Succeeded);
        completed.RecordCount.Should().Be(4210);
        completed.CompletedAt.Should().Be(FixedUtcNow.UtcDateTime);
        connector.LastRunAt.Should().Be(FixedUtcNow.UtcDateTime);
    }

    [Fact]
    public async Task CompleteAsync_ReleasesTheConnectorForTheNextRun()
    {
        await using var db = TestDbContextFactory.Create();
        var connector = new SourceConnectorBuilder().Build();
        db.SourceConnectors.Add(connector);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var request = new StartRunRequestDto { SourceConnectorId = connector.Id };
        var first = await service.StartAsync(request, CancellationToken.None);
        await service.CompleteAsync(
            first.Id,
            new CompleteRunRequestDto { RecordCount = 1, PartCount = 1, CompressedBytes = 1 },
            CancellationToken.None);

        var second = await service.StartAsync(request, CancellationToken.None);

        second.Id.Should().NotBe(first.Id);
    }

    [Fact]
    public async Task FailAsync_RecordsTheErrorAndReleasesTheConnector()
    {
        await using var db = TestDbContextFactory.Create();
        var connector = new SourceConnectorBuilder().Build();
        db.SourceConnectors.Add(connector);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var run = await service.StartAsync(
            new StartRunRequestDto { SourceConnectorId = connector.Id },
            CancellationToken.None);

        var failed = await service.FailAsync(
            run.Id,
            new FailRunRequestDto { ErrorCode = "credential_expired", ErrorMessage = "Token rejected" },
            CancellationToken.None);

        failed.Should().NotBeNull();
        failed!.Status.Should().Be(IngestionRunStatus.Failed);
        failed.ErrorCode.Should().Be("credential_expired");
    }

    [Fact]
    public async Task GetAsync_ForAnUnknownRun_ReturnsNull()
    {
        await using var db = TestDbContextFactory.Create();
        var service = CreateService(db);

        var run = await service.GetAsync("does-not-exist", CancellationToken.None);

        run.Should().BeNull();
    }

    private static IngestionRunService CreateService(AppDbContext db) =>
        new(db, CreateOptions(), new FixedTimeProvider(FixedUtcNow));

    private static IngestionApiOptions CreateOptions() => new()
    {
        IngestionApiKey = new string('k', 32),
        RedisUrl = "redis://localhost:6379",
        CorsOrigin = "http://localhost:4004",
        RateLimitWindow = "1m",
        RateLimitMax = 100,
        Port = 4007,
        NodeEnv = "development",
        RawZoneConnectionString = "UseDevelopmentStorage=true",
        RawZoneContainer = "raw",
        ReportingTimezone = "Africa/Johannesburg",
        ReportingCurrency = "ZAR",
        MaxConcurrentExtractions = 20,
    };
}
