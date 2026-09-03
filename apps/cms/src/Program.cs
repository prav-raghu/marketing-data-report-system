using Cms.Configuration;
using Cms.Models;
using DotNetMonoRepoTemplate.Logging;
using DotNetMonoRepoTemplate.Observability;
using Piranha;
using Piranha.AspNetCore.Identity.PostgreSQL;
using Piranha.AttributeBuilder;
using Piranha.Data.EF.PostgreSql;
using Serilog;

using var sentry = SentryBootstrapper.Init();

var builder = WebApplication.CreateBuilder(args);

var cmsOptions = CmsOptionsFactory.Load(builder.Configuration);
builder.Services.AddSingleton(cmsOptions);
builder.WebHost.UseUrls($"http://0.0.0.0:{cmsOptions.Port}");

Log.Logger = SerilogBootstrapper.CreateBaseConfiguration("cms").CreateLogger();
builder.Host.UseSerilog();

builder.Services.AddDotNetMonoRepoTemplateTelemetry("cms");

builder.Services.AddPiranha(options =>
{
    options.AddRazorPages();
    options.UseCms();
    options.UseManager();
    options.UseTinyMCE();
    options.UseImageSharp();
    options.UseFileStorage();
    options.UseEF<PostgreSqlDb>(db => db.UseNpgsql(cmsOptions.DatabaseUrl));
    options.UseIdentityWithSeed<IdentityPostgreSQLDb>(db => db.UseNpgsql(cmsOptions.DatabaseUrl));
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var api = scope.ServiceProvider.GetRequiredService<IApi>();
    App.Init(api);

    new PageTypeBuilder(api)
        .AddType(typeof(StandardPage))
        .Build()
        .DeleteOrphans();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

app.UsePiranha(options =>
{
    options.UseManager();
    options.UseTinyMCE();
    options.UseIdentity();
});

var logger = new Logger("Cms");
logger.Info("CMS started", new Dictionary<string, object?> { ["port"] = cmsOptions.Port });

app.Run();
