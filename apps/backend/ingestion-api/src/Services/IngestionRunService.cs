using DotNetMonoRepoTemplate.Database;
using DotNetMonoRepoTemplate.Database.Entities;
using DotNetMonoRepoTemplate.Ingestion.Connectors;
using DotNetMonoRepoTemplate.Logging;
using DotNetMonoRepoTemplate.Types;
using IngestionApi.Configuration;
using IngestionApi.Dtos;
using Microsoft.EntityFrameworkCore;

namespace IngestionApi.Services;

public sealed class IngestionRunService : IIngestionRunService
{
    private readonly AppDbContext _db;
    private readonly TimeProvider _timeProvider;
    private readonly TimeZoneInfo _reportingTimeZone;
    private readonly Logger _logger = new("IngestionRunService");

    public IngestionRunService(AppDbContext db, IngestionApiOptions options, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(options);

        _db = db;
        _timeProvider = timeProvider;
        _reportingTimeZone = TimeZoneInfo.FindSystemTimeZoneById(options.ReportingTimezone);
    }

    public async Task<IngestionRunDto> StartAsync(StartRunRequestDto request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var connector = await _db.SourceConnectors
            .FirstOrDefaultAsync(c => c.Id == request.SourceConnectorId && c.IsActive, cancellationToken)
            .ConfigureAwait(false);

        if (connector is null)
        {
            throw new SourceConnectorNotFoundException(request.SourceConnectorId);
        }

        var alreadyInFlight = await _db.IngestionRuns
            .AnyAsync(
                r => r.SourceConnectorId == connector.Id
                    && (r.Status == IngestionRunStatus.Pending || r.Status == IngestionRunStatus.Running),
                cancellationToken)
            .ConfigureAwait(false);

        if (alreadyInFlight)
        {
            throw new IngestionRunConflictException(connector.Id);
        }

        var window = ResolveWindow(request, connector);
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        var run = new IngestionRun
        {
            SourceConnectorId = connector.Id,
            Status = IngestionRunStatus.Running,
            Trigger = request.Trigger,
            WindowStart = window.StartDate,
            WindowEnd = window.EndDate,
            StartedAt = now,
            TriggeredBy = request.TriggeredBy,
        };

        _db.IngestionRuns.Add(run);

        try
        {
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException exception)
        {
            _logger.Warn(
                "Concurrent run start rejected by the database",
                new Dictionary<string, object?>
                {
                    ["sourceConnectorId"] = connector.Id,
                    ["reason"] = exception.GetBaseException().Message,
                });
            throw new IngestionRunConflictException(connector.Id);
        }

        _logger.Info(
            "Ingestion run started",
            new Dictionary<string, object?>
            {
                ["runId"] = run.Id,
                ["sourceConnectorId"] = connector.Id,
                ["windowStart"] = window.StartDate,
                ["windowEnd"] = window.EndDate,
                ["trigger"] = request.Trigger.ToString(),
            });

        return ToDto(run);
    }

    public async Task<IngestionRunDto?> GetAsync(string runId, CancellationToken cancellationToken)
    {
        var run = await _db.IngestionRuns
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == runId, cancellationToken)
            .ConfigureAwait(false);

        return run is null ? null : ToDto(run);
    }

    public async Task<IngestionRunDto?> CompleteAsync(
        string runId,
        CompleteRunRequestDto request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var run = await _db.IngestionRuns
            .FirstOrDefaultAsync(r => r.Id == runId, cancellationToken)
            .ConfigureAwait(false);

        if (run is null)
        {
            return null;
        }

        run.Status = IngestionRunStatus.Succeeded;
        run.CompletedAt = _timeProvider.GetUtcNow().UtcDateTime;
        run.RecordCount = request.RecordCount;
        run.PartCount = request.PartCount;
        run.CompressedBytes = request.CompressedBytes;

        await UpdateConnectorLastRunAsync(run, cancellationToken).ConfigureAwait(false);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.Info(
            "Ingestion run completed",
            new Dictionary<string, object?>
            {
                ["runId"] = run.Id,
                ["recordCount"] = request.RecordCount,
                ["partCount"] = request.PartCount,
            });

        return ToDto(run);
    }

    public async Task<IngestionRunDto?> FailAsync(
        string runId,
        FailRunRequestDto request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var run = await _db.IngestionRuns
            .FirstOrDefaultAsync(r => r.Id == runId, cancellationToken)
            .ConfigureAwait(false);

        if (run is null)
        {
            return null;
        }

        run.Status = IngestionRunStatus.Failed;
        run.CompletedAt = _timeProvider.GetUtcNow().UtcDateTime;
        run.ErrorCode = request.ErrorCode;
        run.ErrorMessage = request.ErrorMessage;

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.Error(
            "Ingestion run failed",
            new Dictionary<string, object?>
            {
                ["runId"] = run.Id,
                ["errorCode"] = request.ErrorCode,
            });

        return ToDto(run);
    }

    private async Task UpdateConnectorLastRunAsync(IngestionRun run, CancellationToken cancellationToken)
    {
        var connector = await _db.SourceConnectors
            .FirstOrDefaultAsync(c => c.Id == run.SourceConnectorId, cancellationToken)
            .ConfigureAwait(false);

        if (connector is not null)
        {
            connector.LastRunAt = run.CompletedAt;
        }
    }

    private ExtractionWindow ResolveWindow(StartRunRequestDto request, SourceConnector connector)
    {
        if (request.WindowStart.HasValue && request.WindowEnd.HasValue)
        {
            return new ExtractionWindow
            {
                StartDate = request.WindowStart.Value,
                EndDate = request.WindowEnd.Value,
            };
        }

        var asOfDate = request.AsOfDate ?? CurrentReportingDate();
        return ExtractionWindow.Restatement(asOfDate, connector.RestatementDays);
    }

    private DateOnly CurrentReportingDate()
    {
        var reportingNow = TimeZoneInfo.ConvertTimeFromUtc(_timeProvider.GetUtcNow().UtcDateTime, _reportingTimeZone);
        return DateOnly.FromDateTime(reportingNow);
    }

    private static IngestionRunDto ToDto(IngestionRun run) => new()
    {
        Id = run.Id,
        SourceConnectorId = run.SourceConnectorId,
        Status = run.Status,
        Trigger = run.Trigger,
        WindowStart = run.WindowStart,
        WindowEnd = run.WindowEnd,
        StartedAt = run.StartedAt,
        CompletedAt = run.CompletedAt,
        RecordCount = run.RecordCount,
        PartCount = run.PartCount,
        CompressedBytes = run.CompressedBytes,
        ErrorCode = run.ErrorCode,
        ErrorMessage = run.ErrorMessage,
    };
}
