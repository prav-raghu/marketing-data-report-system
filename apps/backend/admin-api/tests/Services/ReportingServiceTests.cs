using AdminApi.Services;
using AdminApi.Tests.Fixtures;
using FluentAssertions;
using DotNetMonoRepoTemplate.Types;
using Xunit;

namespace AdminApi.Tests.Services;

public sealed class ReportingServiceTests
{
    [Fact]
    public async Task GenerateReportAsync_ReturnsCompletedWithZeroRecords_WhenNoUsersMatchUserActivityFilter()
    {
        await using var db = TestDbContextFactory.Create();
        var service = new ReportingService(db);

        var result = await service.GenerateReportAsync(new ReportRequest { Type = ReportType.UserActivity, Format = ReportFormat.Csv });

        result.Status.Should().Be(ReportStatus.Completed);
        result.RecordCount.Should().Be(0);
        result.Url.Should().BeNull();
    }

    [Fact]
    public async Task GenerateReportAsync_ReturnsCompletedWithCsvUrl_ForSystemMetrics()
    {
        await using var db = TestDbContextFactory.Create();
        var service = new ReportingService(db);

        var result = await service.GenerateReportAsync(new ReportRequest { Type = ReportType.SystemMetrics, Format = ReportFormat.Csv });

        result.Status.Should().Be(ReportStatus.Completed);
        result.RecordCount.Should().Be(4);
        result.Url.Should().StartWith("data:text/csv");
    }

    [Fact]
    public async Task GenerateReportAsync_ReturnsCompletedWithExcelUrl_ForSystemMetrics()
    {
        await using var db = TestDbContextFactory.Create();
        var service = new ReportingService(db);

        var result = await service.GenerateReportAsync(new ReportRequest { Type = ReportType.SystemMetrics, Format = ReportFormat.Excel });

        result.Status.Should().Be(ReportStatus.Completed);
        result.Url.Should().StartWith("data:application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
    }

    [Fact]
    public async Task GenerateReportAsync_ReturnsFailed_WhenReportTypeIsUnsupported()
    {
        await using var db = TestDbContextFactory.Create();
        var service = new ReportingService(db);

        var result = await service.GenerateReportAsync(new ReportRequest { Type = "NOT_A_REAL_TYPE", Format = ReportFormat.Csv });

        result.Status.Should().Be(ReportStatus.Failed);
        result.Error.Should().NotBeNullOrEmpty();
        result.RecordCount.Should().BeNull();
    }

    [Fact]
    public async Task GetUserActivityReportAsync_FiltersByDateRange()
    {
        await using var db = TestDbContextFactory.Create();
        var role = await RoleBuilder.CreateAsync(db);
        var status = await UserStatusBuilder.CreateAsync(db);
        var withinRange = await UserBuilder.CreateAsync(db, u =>
        {
            u.RoleId = role.Id;
            u.UserStatusId = status.Id;
            u.CreatedAt = DateTime.UtcNow.AddDays(-5);
        });
        await UserBuilder.CreateAsync(db, u =>
        {
            u.RoleId = role.Id;
            u.UserStatusId = status.Id;
            u.CreatedAt = DateTime.UtcNow.AddDays(-90);
        });
        var service = new ReportingService(db);

        var report = await service.GetUserActivityReportAsync(new ReportFilter { StartDate = DateTime.UtcNow.AddDays(-30), EndDate = DateTime.UtcNow });

        report.Should().ContainSingle();
        report[0]["userId"].Should().Be(withinRange.Id);
    }

    [Fact]
    public async Task GetUserActivityReportAsync_FiltersByUserId()
    {
        await using var db = TestDbContextFactory.Create();
        var role = await RoleBuilder.CreateAsync(db);
        var status = await UserStatusBuilder.CreateAsync(db);
        var target = await UserBuilder.CreateAsync(db, u => { u.RoleId = role.Id; u.UserStatusId = status.Id; });
        await UserBuilder.CreateAsync(db, u => { u.RoleId = role.Id; u.UserStatusId = status.Id; });
        var service = new ReportingService(db);

        var report = await service.GetUserActivityReportAsync(new ReportFilter { UserId = target.Id });

        report.Should().ContainSingle(r => (string?)r["userId"] == target.Id);
    }

    [Fact]
    public async Task GetWebhookDeliveryReportAsync_ReturnsDeliveries_WithinDateRange()
    {
        await using var db = TestDbContextFactory.Create();
        var subscription = await WebhookSubscriptionBuilder.CreateAsync(db);
        var recent = await WebhookDeliveryBuilder.CreateAsync(db, subscription.Id, d =>
        {
            d.Status = "delivered";
            d.CreatedAt = DateTime.UtcNow.AddDays(-1);
        });
        await WebhookDeliveryBuilder.CreateAsync(db, subscription.Id, d =>
        {
            d.Status = "delivered";
            d.CreatedAt = DateTime.UtcNow.AddDays(-30);
        });
        var service = new ReportingService(db);

        var report = await service.GetWebhookDeliveryReportAsync(new ReportFilter { StartDate = DateTime.UtcNow.AddDays(-7), EndDate = DateTime.UtcNow });

        report.Should().ContainSingle(r => (string?)r["deliveryId"] == recent.Id);
    }

    [Fact]
    public async Task GetWebhookDeliveryReportAsync_FiltersByStatus()
    {
        await using var db = TestDbContextFactory.Create();
        var subscription = await WebhookSubscriptionBuilder.CreateAsync(db);
        var failed = await WebhookDeliveryBuilder.CreateAsync(db, subscription.Id, d => d.Status = "failed");
        await WebhookDeliveryBuilder.CreateAsync(db, subscription.Id, d => d.Status = "delivered");
        var service = new ReportingService(db);

        var report = await service.GetWebhookDeliveryReportAsync(new ReportFilter { Status = "failed" });

        report.Should().ContainSingle(r => (string?)r["deliveryId"] == failed.Id);
    }

    [Fact]
    public async Task GetSystemMetricsReportAsync_ReturnsFourMetrics_WithCounts()
    {
        await using var db = TestDbContextFactory.Create();
        var role = await RoleBuilder.CreateAsync(db);
        var status = await UserStatusBuilder.CreateAsync(db);
        await UserBuilder.CreateAsync(db, u => { u.RoleId = role.Id; u.UserStatusId = status.Id; u.IsActive = true; });
        var service = new ReportingService(db);

        var report = await service.GetSystemMetricsReportAsync();

        report.Should().HaveCount(4);
        report.Should().Contain(m => (string?)m["metric"] == "Total Users" && (int)(m["value"] ?? 0) == 1);
    }
}
