using System.Globalization;
using System.Security.Cryptography;
using AdminApi.Dtos;
using Microsoft.EntityFrameworkCore;
using DotNetMonoRepoTemplate.Database;
using DotNetMonoRepoTemplate.Export;
using DotNetMonoRepoTemplate.Types;

namespace AdminApi.Services;

public sealed class ReportingService
{
    private readonly AppDbContext _db;

    public ReportingService(AppDbContext db) => _db = db;

    public async Task<ReportResult> GenerateReportAsync(ReportRequest request, CancellationToken cancellationToken = default)
    {
        var reportId = GenerateReportId();
        try
        {
            var data = await FetchReportDataAsync(request.Type, request.Filters, cancellationToken);
            if (data.Count == 0)
            {
                return new ReportResult
                {
                    Id = reportId,
                    Type = request.Type,
                    Format = request.Format,
                    Status = ReportStatus.Completed,
                    RecordCount = 0,
                    GeneratedAt = DateTime.UtcNow,
                };
            }

            var format = MapReportFormat(request.Format);
            var buffer = await ExportService.ExportToBufferAsync(
                data,
                new ExportServiceOptions
                {
                    Format = format,
                    CsvOptions = new CsvExportOptions
                    {
                        Bom = true,
                        Headers = request.IncludeHeaders != false ? data[0].Keys.ToList() : null,
                    },
                    ExcelOptions = new ExcelExportOptions
                    {
                        SheetName = GetSheetName(request.Type),
                        FreezeHeader = true,
                        StyleHeader = true,
                        AutoFilter = true,
                    },
                },
                cancellationToken);

            return new ReportResult
            {
                Id = reportId,
                Type = request.Type,
                Format = request.Format,
                Status = ReportStatus.Completed,
                RecordCount = data.Count,
                GeneratedAt = DateTime.UtcNow,
                Url = $"data:{ExportService.GetContentType(format)};base64,{Convert.ToBase64String(buffer)}",
            };
        }
        catch (Exception ex)
        {
            return new ReportResult
            {
                Id = reportId,
                Type = request.Type,
                Format = request.Format,
                Status = ReportStatus.Failed,
                Error = ex.Message,
                GeneratedAt = DateTime.UtcNow,
            };
        }
    }

    public async IAsyncEnumerable<Dictionary<string, object?>> StreamReportDataAsync(
        ReportRequest request,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var data = await FetchReportDataAsync(request.Type, request.Filters, cancellationToken);
        foreach (var record in data)
        {
            yield return record;
        }
    }

    public async Task<List<Dictionary<string, object?>>> GetUserActivityReportAsync(
        ReportFilter? filters, CancellationToken cancellationToken = default)
    {
        var startDate = filters?.StartDate ?? DateTime.UtcNow.AddDays(-30);
        var endDate = filters?.EndDate ?? DateTime.UtcNow;

        var query = _db.Users.Where(u => u.CreatedAt >= startDate && u.CreatedAt <= endDate);
        if (!string.IsNullOrEmpty(filters?.UserId))
        {
            query = query.Where(u => u.Id == filters.UserId);
        }
        if (!string.IsNullOrEmpty(filters?.Status))
        {
            query = query.Where(u => u.UserStatusId == filters.Status);
        }

        var users = await query
            .OrderByDescending(u => u.CreatedAt)
            .Select(u => new { u.Id, u.Email, u.Username, u.UserStatusId, u.CreatedAt, u.UpdatedAt })
            .ToListAsync(cancellationToken);

        return users
            .Select(user => new Dictionary<string, object?>
            {
                ["userId"] = user.Id,
                ["email"] = user.Email,
                ["username"] = user.Username,
                ["status"] = user.UserStatusId,
                ["createdAt"] = user.CreatedAt.ToString("o"),
                ["lastUpdated"] = user.UpdatedAt.ToString("o"),
            })
            .ToList();
    }

    public async Task<List<Dictionary<string, object?>>> GetWebhookDeliveryReportAsync(
        ReportFilter? filters, CancellationToken cancellationToken = default)
    {
        var startDate = filters?.StartDate ?? DateTime.UtcNow.AddDays(-7);
        var endDate = filters?.EndDate ?? DateTime.UtcNow;

        var query = _db.WebhookDeliveries.Where(d => d.CreatedAt >= startDate && d.CreatedAt <= endDate);
        if (!string.IsNullOrEmpty(filters?.Status))
        {
            query = query.Where(d => d.Status == filters.Status);
        }

        var deliveries = await query
            .OrderByDescending(d => d.CreatedAt)
            .Take(10000)
            .Select(d => new
            {
                d.Id,
                d.SubscriptionId,
                d.EventType,
                d.Status,
                d.HttpStatus,
                d.AttemptCount,
                d.CreatedAt,
                d.DeliveredAt,
            })
            .ToListAsync(cancellationToken);

        return deliveries
            .Select(delivery => new Dictionary<string, object?>
            {
                ["deliveryId"] = delivery.Id,
                ["subscriptionId"] = delivery.SubscriptionId,
                ["eventType"] = delivery.EventType,
                ["status"] = delivery.Status,
                ["httpStatus"] = delivery.HttpStatus?.ToString(CultureInfo.InvariantCulture) ?? "N/A",
                ["attempts"] = delivery.AttemptCount,
                ["createdAt"] = delivery.CreatedAt.ToString("o"),
                ["deliveredAt"] = delivery.DeliveredAt?.ToString("o") ?? "N/A",
            })
            .ToList();
    }

    public async Task<List<Dictionary<string, object?>>> GetSystemMetricsReportAsync(
        ReportFilter? filters = null, CancellationToken cancellationToken = default)
    {
        var startDate = filters?.StartDate ?? DateTime.UtcNow.AddDays(-1);
        var endDate = filters?.EndDate ?? DateTime.UtcNow;

        var totalUsers = await _db.Users.CountAsync(cancellationToken);
        var activeUsers = await _db.Users.CountAsync(u => u.IsActive && u.UpdatedAt >= startDate, cancellationToken);
        var webhookSubscriptions = await _db.WebhookSubscriptions.CountAsync(w => w.IsActive, cancellationToken);
        var pendingDeliveries = await _db.WebhookDeliveries.CountAsync(
            d => d.Status == "pending" && d.CreatedAt >= startDate && d.CreatedAt <= endDate, cancellationToken);

        var now = DateTime.UtcNow.ToString("o");
        return new List<Dictionary<string, object?>>
        {
            new() { ["metric"] = "Total Users", ["value"] = totalUsers, ["timestamp"] = now },
            new() { ["metric"] = "Active Users", ["value"] = activeUsers, ["timestamp"] = now },
            new() { ["metric"] = "Webhook Subscriptions", ["value"] = webhookSubscriptions, ["timestamp"] = now },
            new() { ["metric"] = "Pending Webhook Deliveries", ["value"] = pendingDeliveries, ["timestamp"] = now },
        };
    }

    private Task<List<Dictionary<string, object?>>> FetchReportDataAsync(
        string type, ReportFilter? filters, CancellationToken cancellationToken) => type switch
    {
        ReportType.UserActivity => GetUserActivityReportAsync(filters, cancellationToken),
        ReportType.WebhookDelivery => GetWebhookDeliveryReportAsync(filters, cancellationToken),
        ReportType.SystemMetrics => GetSystemMetricsReportAsync(filters, cancellationToken),
        ReportType.AuditLog => Task.FromResult(new List<Dictionary<string, object?>>()),
        _ => throw new InvalidOperationException($"Unsupported report type: {type}"),
    };

    private static ExportFormat MapReportFormat(string format) => format switch
    {
        ReportFormat.Excel => ExportFormat.Excel,
        _ => ExportFormat.Csv,
    };

    private static string GetSheetName(string type) => type switch
    {
        ReportType.UserActivity => "User Activity",
        ReportType.WebhookDelivery => "Webhook Deliveries",
        ReportType.SystemMetrics => "System Metrics",
        ReportType.AuditLog => "Audit Log",
        _ => "Report",
    };

    private static string GenerateReportId() =>
        $"report_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(9))}";
}
