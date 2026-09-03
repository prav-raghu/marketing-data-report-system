using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using DotNetMonoRepoTemplate.Database;
using DotNetMonoRepoTemplate.Export;

namespace CustomerApi.Endpoints;

public static class ExportEndpoints
{
    private static readonly string[] ExportHeaders = { "id", "email", "name", "createdAt" };

    public static void MapExportEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/users/export", async (AppDbContext db, string? format) =>
        {
            var exportFormat = format == "excel" ? ExportFormat.Excel : ExportFormat.Csv;

            var users = await db.Users
                .Select(u => new { u.Id, u.Email, u.Username, u.CreatedAt })
                .Take(1000)
                .ToListAsync();

            var data = users
                .Select(u => (IReadOnlyDictionary<string, object?>)new Dictionary<string, object?>
                {
                    ["id"] = u.Id,
                    ["email"] = u.Email,
                    ["name"] = u.Username,
                    ["createdAt"] = u.CreatedAt.ToString("O"),
                })
                .ToList();

            var buffer = await ExportService.ExportToBufferAsync(data, new ExportServiceOptions
            {
                Format = exportFormat,
                CsvOptions = new CsvExportOptions { Headers = ExportHeaders, Bom = true },
                ExcelOptions = new ExcelExportOptions { SheetName = "Users", StyleHeader = true },
            });

            return Results.File(buffer, ExportService.GetContentType(exportFormat), $"users{ExportService.GetFileExtension(exportFormat)}");
        });

        app.MapGet("/api/v1/users/export/stream", async (HttpContext context, AppDbContext db, string? format) =>
        {
            var exportFormat = format == "excel" ? ExportFormat.Excel : ExportFormat.Csv;

            context.Response.ContentType = ExportService.GetContentType(exportFormat);
            context.Response.Headers.ContentDisposition =
                $"attachment; filename=\"users-export{ExportService.GetFileExtension(exportFormat)}\"";

            var dataSource = FetchUsersStreamAsync(db, context.RequestAborted);

            var buffer = await ExportService.StreamExportAsync(dataSource, new ExportServiceOptions
            {
                Format = exportFormat,
                CsvOptions = new CsvExportOptions { Headers = ExportHeaders, Bom = true },
                ExcelOptions = new ExcelExportOptions
                {
                    SheetName = "Users",
                    Headers = ExportHeaders,
                    FreezeHeader = true,
                    AutoFilter = true,
                    StyleHeader = true,
                },
            });

            await context.Response.Body.WriteAsync(buffer, context.RequestAborted);
        });
    }

    private static async IAsyncEnumerable<IReadOnlyDictionary<string, object?>> FetchUsersStreamAsync(
        AppDbContext db,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        const int batchSize = 100;
        var page = 0;

        while (true)
        {
            var users = await db.Users
                .OrderBy(u => u.Id)
                .Select(u => new { u.Id, u.Email, u.Username, u.CreatedAt })
                .Skip(page * batchSize)
                .Take(batchSize)
                .ToListAsync(cancellationToken);

            if (users.Count == 0)
            {
                yield break;
            }

            foreach (var user in users)
            {
                yield return new Dictionary<string, object?>
                {
                    ["id"] = user.Id,
                    ["email"] = user.Email,
                    ["name"] = user.Username,
                    ["createdAt"] = user.CreatedAt.ToString("O"),
                };
            }

            page++;
        }
    }
}
