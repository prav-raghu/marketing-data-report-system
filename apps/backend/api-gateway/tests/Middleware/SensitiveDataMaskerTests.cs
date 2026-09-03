using System.Text.Json.Nodes;
using ApiGateway.Middleware;
using FluentAssertions;
using Xunit;

namespace ApiGateway.Tests.Middleware;

public sealed class SensitiveDataMaskerTests
{
    [Fact]
    public void Mask_ReturnsNull_WhenNodeIsNull()
    {
        var result = SensitiveDataMasker.Mask(null);

        result.Should().BeNull();
    }

    [Theory]
    [InlineData("password")]
    [InlineData("Password")]
    [InlineData("currentPassword")]
    [InlineData("newPassword")]
    [InlineData("confirmPassword")]
    [InlineData("token")]
    [InlineData("accessToken")]
    [InlineData("refreshToken")]
    [InlineData("secret")]
    [InlineData("apiKey")]
    [InlineData("api_key")]
    [InlineData("clientSecret")]
    [InlineData("privateKey")]
    [InlineData("creditCard")]
    [InlineData("cardNumber")]
    [InlineData("cvv")]
    [InlineData("cvc")]
    [InlineData("ssn")]
    [InlineData("nationalId")]
    [InlineData("pin")]
    [InlineData("otp")]
    [InlineData("twoFactorCode")]
    [InlineData("authorization")]
    [InlineData("cookie")]
    public void Mask_RedactsSensitiveKeys_RegardlessOfCasing(string key)
    {
        var node = new JsonObject { [key] = "super-secret-value" };

        var masked = SensitiveDataMasker.Mask(node) as JsonObject;

        masked![key]!.GetValue<string>().Should().Be("[REDACTED]");
    }

    [Fact]
    public void Mask_PreservesNonSensitiveKeys()
    {
        var node = new JsonObject { ["username"] = "jane-doe", ["email"] = "jane@test.com" };

        var masked = SensitiveDataMasker.Mask(node) as JsonObject;

        masked!["username"]!.GetValue<string>().Should().Be("jane-doe");
        masked["email"]!.GetValue<string>().Should().Be("jane@test.com");
    }

    [Fact]
    public void Mask_RecursesIntoNestedObjects()
    {
        var node = new JsonObject
        {
            ["user"] = new JsonObject
            {
                ["email"] = "jane@test.com",
                ["password"] = "hunter2",
            },
        };

        var masked = SensitiveDataMasker.Mask(node) as JsonObject;
        var user = masked!["user"] as JsonObject;

        user!["email"]!.GetValue<string>().Should().Be("jane@test.com");
        user["password"]!.GetValue<string>().Should().Be("[REDACTED]");
    }

    [Fact]
    public void Mask_RecursesIntoArraysOfObjects()
    {
        var node = new JsonArray
        {
            new JsonObject { ["token"] = "abc123" },
            new JsonObject { ["token"] = "def456" },
        };

        var masked = SensitiveDataMasker.Mask(node) as JsonArray;

        masked![0]!["token"]!.GetValue<string>().Should().Be("[REDACTED]");
        masked[1]!["token"]!.GetValue<string>().Should().Be("[REDACTED]");
    }

    [Fact]
    public void Mask_DoesNotMutateOriginalNode()
    {
        var node = new JsonObject { ["password"] = "hunter2" };

        SensitiveDataMasker.Mask(node);

        node["password"]!.GetValue<string>().Should().Be("hunter2");
    }

    [Fact]
    public void TryParseAndMask_ReturnsMaskedNode_WhenJsonIsValid()
    {
        var json = """{"email":"jane@test.com","password":"hunter2"}""";

        var result = SensitiveDataMasker.TryParseAndMask(json) as JsonObject;

        result!["email"]!.GetValue<string>().Should().Be("jane@test.com");
        result["password"]!.GetValue<string>().Should().Be("[REDACTED]");
    }

    [Fact]
    public void TryParseAndMask_ReturnsNonJsonPayloadMarker_WhenJsonIsInvalid()
    {
        var result = SensitiveDataMasker.TryParseAndMask("not-valid-json{{{");

        result!.GetValue<string>().Should().Be("[non-JSON payload]");
    }
}
