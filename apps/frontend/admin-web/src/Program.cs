using System.Net;
using System.Text.Json;
using AdminWeb;
using AdminWeb.Auth;
using AdminWeb.Models;
using AdminWeb.Services;
using AdminWeb.Validators;
using FluentValidation;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Polly;
using Polly.Extensions.Http;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var apiBaseUrl = builder.Configuration["ApiBaseUrl"] ?? "http://localhost:4001";

builder.Services.AddSingleton(new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

builder.Services.AddSingleton<AuthTokenStore>();
builder.Services.AddScoped<AuthorizationMessageHandler>();
builder.Services.AddScoped<AuthenticationStateProvider, AdminAuthStateProvider>();
builder.Services.AddAuthorizationCore();

builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<CounterState>();
builder.Services.AddScoped<IValidator<LoginRequest>, LoginRequestValidator>();
builder.Services.AddScoped<IValidator<Verify2FARequest>, Verify2FARequestValidator>();
builder.Services.AddScoped<IValidator<Disable2FARequest>, Disable2FARequestValidator>();

builder.Services
    .AddHttpClient("AdminApi", client => client.BaseAddress = new Uri(apiBaseUrl))
    .AddHttpMessageHandler<AuthorizationMessageHandler>()
    .AddPolicyHandler(GetRetryPolicy());

builder.Services
    .AddHttpClient("JsonPlaceholder", client => client.BaseAddress = new Uri("https://jsonplaceholder.typicode.com/"))
    .AddPolicyHandler(GetRetryPolicy());

await builder.Build().RunAsync();

static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy() =>
    HttpPolicyExtensions
        .HandleTransientHttpError()
        .OrResult(response => response.StatusCode == HttpStatusCode.RequestTimeout || response.StatusCode == HttpStatusCode.TooManyRequests)
        .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt - 1)));
