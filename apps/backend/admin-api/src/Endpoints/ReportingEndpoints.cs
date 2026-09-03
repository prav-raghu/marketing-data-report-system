using System.Text.Json;
using AdminApi.Auth;
using AdminApi.Dtos;
using AdminApi.Services;
using DotNetMonoRepoTemplate.Types;

namespace AdminApi.Endpoints;

public static class ReportingEndpoints
{
    public static void MapReportingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/reports");

        group.MapPost("/generate", async (GenerateReportDto body, ReportingService reportingService) =>
        {
            var request = new ReportRequest
            {
                Type = body.Type,
                Format = body.Format,
                Filters = body.Filters is null
                    ? null
                    : new ReportFilter
                    {
                        StartDate = body.Filters.StartDate is { } start ? DateTime.Parse(start).ToUniversalTime() : null,
                        EndDate = body.Filters.EndDate is { } end ? DateTime.Parse(end).ToUniversalTime() : null,
                        UserId = body.Filters.UserId,
                        Status = body.Filters.Status,
                    },
                IncludeHeaders = body.IncludeHeaders ?? true,
            };
            var result = await reportingService.GenerateReportAsync(request);
            return Results.Json(new { isSuccessful = result.Status == ReportStatus.Completed, data = result });
        }).WithMetadata(new RequirePermissionsAttribute(PermissionName.ReportExport));

        group.MapGet("/stream", async (
            HttpContext context,
            ReportingService reportingService,
            string type,
            string? format,
            string? startDate,
            string? endDate) =>
        {
            var resolvedFormat = format ?? "csv";
            var request = new ReportRequest
            {
                Type = type,
                Format = resolvedFormat.ToUpperInvariant(),
                Filters = new ReportFilter
                {
                    StartDate = startDate is not null ? DateTime.Parse(startDate).ToUniversalTime() : null,
                    EndDate = endDate is not null ? DateTime.Parse(endDate).ToUniversalTime() : null,
                },
                IncludeHeaders = true,
            };

            var contentType = resolvedFormat == "excel"
                ? "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
                : "text/csv";
            var extension = resolvedFormat == "excel" ? ".xlsx" : ".csv";

            context.Response.ContentType = contentType;
            context.Response.Headers.ContentDisposition =
                $"attachment; filename=\"report-{type}-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}{extension}\"";

            await foreach (var chunk in reportingService.StreamReportDataAsync(request))
            {
                var line = JsonSerializer.Serialize(chunk) + "\n";
                await context.Response.WriteAsync(line);
            }
        }).WithMetadata(new RequirePermissionsAttribute(PermissionName.ReportExport));

        group.MapGet("/user-activity", async (
            ReportingService reportingService,
            string? startDate,
            string? endDate,
            string? userId,
            string? status) =>
        {
            var filters = new ReportFilter
            {
                StartDate = startDate is not null ? DateTime.Parse(startDate).ToUniversalTime() : null,
                EndDate = endDate is not null ? DateTime.Parse(endDate).ToUniversalTime() : null,
                UserId = userId,
                Status = status,
            };
            var data = await reportingService.GetUserActivityReportAsync(filters);
            return Results.Ok(new { isSuccessful = true, data = new { recordCount = data.Count, records = data } });
        }).WithMetadata(new RequirePermissionsAttribute(PermissionName.ReportView));

        group.MapGet("/webhook-delivery", async (
            ReportingService reportingService,
            string? startDate,
            string? endDate,
            string? status) =>
        {
            var filters = new ReportFilter
            {
                StartDate = startDate is not null ? DateTime.Parse(startDate).ToUniversalTime() : null,
                EndDate = endDate is not null ? DateTime.Parse(endDate).ToUniversalTime() : null,
                Status = status,
            };
            var data = await reportingService.GetWebhookDeliveryReportAsync(filters);
            return Results.Ok(new { isSuccessful = true, data = new { recordCount = data.Count, records = data } });
        }).WithMetadata(new RequirePermissionsAttribute(PermissionName.ReportView));

        group.MapGet("/system-metrics", async (ReportingService reportingService) =>
        {
            var data = await reportingService.GetSystemMetricsReportAsync();
            return Results.Ok(new { isSuccessful = true, data = new { recordCount = data.Count, metrics = data } });
        }).WithMetadata(new RequirePermissionsAttribute(PermissionName.ReportView));
    }
}
