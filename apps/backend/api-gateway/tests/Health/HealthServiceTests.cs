using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using ApiGateway.Configuration;
using ApiGateway.Health;
using ApiGateway.Tests.Fixtures;
using FluentAssertions;
using Xunit;

namespace ApiGateway.Tests.Health;

public sealed class HealthServiceTests
{
    private static ApiGatewayOptions BuildOptions() => new()
    {
        NodeEnv = "test",
        Port = 4000,
        CorsOrigin = "https://example.com",
        CustomerApiUrl = "http://customer-api.local",
        AdminApiUrl = "http://admin-api.local",
        SchedulerApiUrl = "http://schedule-api.local",
        RateLimitMax = 200,
        RateLimitTimeWindow = "1 minute",
    };

    [Fact]
    public async Task CheckAllServicesAsync_ReturnsHealthy_WhenAllServicesRespondSuccessfully()
    {
        var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var service = new HealthService(new HttpClient(handler), BuildOptions());

        var result = await service.CheckAllServicesAsync(CancellationToken.None);

        result.Status.Should().Be(HealthStatusValue.Healthy);
        result.Services.Should().HaveCount(2);
        result.Services.Should().OnlyContain(s => s.Status == HealthStatusValue.Healthy);
    }

    [Fact]
    public async Task CheckAllServicesAsync_ReturnsUnhealthy_WhenAllServicesFail()
    {
        var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        var service = new HealthService(new HttpClient(handler), BuildOptions());

        var result = await service.CheckAllServicesAsync(CancellationToken.None);

        result.Status.Should().Be(HealthStatusValue.Unhealthy);
        result.Services.Should().OnlyContain(s => s.Status == HealthStatusValue.Unhealthy);
    }

    [Fact]
    public async Task CheckAllServicesAsync_ReturnsDegraded_WhenOnlyOneServiceFails()
    {
        var handler = new FakeHttpMessageHandler(request =>
            request.RequestUri!.Host == "customer-api.local"
                ? new HttpResponseMessage(HttpStatusCode.OK)
                : new HttpResponseMessage(HttpStatusCode.InternalServerError));
        var service = new HealthService(new HttpClient(handler), BuildOptions());

        var result = await service.CheckAllServicesAsync(CancellationToken.None);

        result.Status.Should().Be(HealthStatusValue.Degraded);
    }

    [Fact]
    public async Task CheckAllServicesAsync_SetsHttpStatusErrorMessage_WhenServiceReturnsNonSuccessStatusCode()
    {
        var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.BadGateway));
        var service = new HealthService(new HttpClient(handler), BuildOptions());

        var result = await service.CheckAllServicesAsync(CancellationToken.None);

        result.Services.Should().OnlyContain(s => s.Error == "HTTP 502");
    }

    [Fact]
    public async Task CheckAllServicesAsync_SetsExceptionMessage_WhenRequestThrows()
    {
        var handler = new FakeHttpMessageHandler(_ => throw new HttpRequestException("connection refused"));
        var service = new HealthService(new HttpClient(handler), BuildOptions());

        var result = await service.CheckAllServicesAsync(CancellationToken.None);

        result.Status.Should().Be(HealthStatusValue.Unhealthy);
        result.Services.Should().OnlyContain(s => s.Error == "connection refused");
    }

    [Fact]
    public async Task CheckServiceByNameAsync_ReturnsNull_WhenServiceNameIsUnknown()
    {
        var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var service = new HealthService(new HttpClient(handler), BuildOptions());

        var result = await service.CheckServiceByNameAsync("unknown-service", CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task CheckServiceByNameAsync_ReturnsHealthResult_WhenServiceNameIsKnown()
    {
        var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var service = new HealthService(new HttpClient(handler), BuildOptions());

        var result = await service.CheckServiceByNameAsync("customer-api", CancellationToken.None);

        result.Should().NotBeNull();
        result!.Name.Should().Be("customer-api");
        result.Url.Should().Be("http://customer-api.local");
        result.Status.Should().Be(HealthStatusValue.Healthy);
    }

    [Fact]
    public void UptimeSeconds_IsNonNegative()
    {
        HealthService.UptimeSeconds.Should().BeGreaterThanOrEqualTo(0);
    }
}
