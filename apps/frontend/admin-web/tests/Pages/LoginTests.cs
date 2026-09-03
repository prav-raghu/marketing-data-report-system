using System.Text.Json;
using AdminWeb.Auth;
using AdminWeb.Models;
using AdminWeb.Pages;
using AdminWeb.Services;
using AdminWeb.Validators;
using Bunit;
using FluentAssertions;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AdminWeb.Tests.Pages;

public sealed class LoginTests : BunitContext
{
    public LoginTests()
    {
        Services.AddSingleton(new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        Services.AddSingleton<AuthTokenStore>();
        Services.AddScoped<AuthService>();
        Services.AddScoped<IValidator<LoginRequest>, LoginRequestValidator>();
        Services.AddHttpClient("AdminApi", client => client.BaseAddress = new Uri("http://localhost:4001"));
    }

    [Fact]
    public async Task Submit_ShowsInlineErrors_WhenFormIsEmpty()
    {
        var component = Render<Login>();

        var form = component.Find("form");
        await form.SubmitAsync();

        component.Markup.Should().Contain("must not be empty");
    }

    [Fact]
    public void Login_RendersEmailAndPasswordFields()
    {
        var component = Render<Login>();

        component.Find("#email").Should().NotBeNull();
        component.Find("#password").Should().NotBeNull();
    }
}
