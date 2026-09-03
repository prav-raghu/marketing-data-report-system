using ApiGateway.Configuration;
using FluentAssertions;
using Xunit;

namespace ApiGateway.Tests.Configuration;

public sealed class ApiGatewayOptionsValidatorTests
{
    private readonly ApiGatewayOptionsValidator _validator = new();

    private static ApiGatewayOptions BuildValidOptions() => new()
    {
        NodeEnv = "production",
        Port = 4000,
        CorsOrigin = "https://example.com",
        CustomerApiUrl = "http://customer-api:4002",
        AdminApiUrl = "http://admin-api:4001",
        SchedulerApiUrl = "http://schedule-api:4003",
        RateLimitMax = 200,
        RateLimitTimeWindow = "1 minute",
    };

    [Fact]
    public void Validate_Succeeds_WhenAllFieldsAreValid()
    {
        var result = _validator.Validate(BuildValidOptions());

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("development")]
    [InlineData("test")]
    [InlineData("production")]
    public void Validate_Succeeds_ForEachAllowedNodeEnv(string nodeEnv)
    {
        var options = BuildValidOptions() with { NodeEnv = nodeEnv };

        var result = _validator.Validate(options);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_Fails_WhenNodeEnvIsNotAnAllowedValue()
    {
        var options = BuildValidOptions() with { NodeEnv = "staging" };

        var result = _validator.Validate(options);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(ApiGatewayOptions.NodeEnv));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_Fails_WhenPortIsNotGreaterThanZero(int port)
    {
        var options = BuildValidOptions() with { Port = port };

        var result = _validator.Validate(options);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(ApiGatewayOptions.Port));
    }

    [Fact]
    public void Validate_Fails_WhenCorsOriginIsEmpty()
    {
        var options = BuildValidOptions() with { CorsOrigin = string.Empty };

        var result = _validator.Validate(options);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(ApiGatewayOptions.CorsOrigin));
    }

    [Fact]
    public void Validate_Fails_WhenCustomerApiUrlIsEmpty()
    {
        var options = BuildValidOptions() with { CustomerApiUrl = string.Empty };

        var result = _validator.Validate(options);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(ApiGatewayOptions.CustomerApiUrl));
    }

    [Fact]
    public void Validate_Fails_WhenAdminApiUrlIsEmpty()
    {
        var options = BuildValidOptions() with { AdminApiUrl = string.Empty };

        var result = _validator.Validate(options);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(ApiGatewayOptions.AdminApiUrl));
    }

    [Fact]
    public void Validate_Fails_WhenSchedulerApiUrlIsEmpty()
    {
        var options = BuildValidOptions() with { SchedulerApiUrl = string.Empty };

        var result = _validator.Validate(options);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(ApiGatewayOptions.SchedulerApiUrl));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Validate_Fails_WhenRateLimitMaxIsNotGreaterThanZero(int rateLimitMax)
    {
        var options = BuildValidOptions() with { RateLimitMax = rateLimitMax };

        var result = _validator.Validate(options);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(ApiGatewayOptions.RateLimitMax));
    }

    [Fact]
    public void Validate_Fails_WhenRateLimitTimeWindowIsEmpty()
    {
        var options = BuildValidOptions() with { RateLimitTimeWindow = string.Empty };

        var result = _validator.Validate(options);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(ApiGatewayOptions.RateLimitTimeWindow));
    }
}
