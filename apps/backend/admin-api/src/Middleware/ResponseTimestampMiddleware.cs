using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace AdminApi.Middleware;

public sealed class ResponseTimestampMiddleware
{
    private readonly RequestDelegate _next;

    public ResponseTimestampMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? string.Empty;
        if (context.Request.Method == HttpMethods.Options || path.StartsWith("/docs", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        var originalBody = context.Response.Body;
        using var buffer = new MemoryStream();
        context.Response.Body = buffer;

        await _next(context);

        buffer.Seek(0, SeekOrigin.Begin);
        var payload = await new StreamReader(buffer, Encoding.UTF8).ReadToEndAsync();

        context.Response.Body = originalBody;

        if (string.IsNullOrEmpty(payload) || context.Response.ContentType?.Contains("application/json") != true)
        {
            context.Response.ContentLength = Encoding.UTF8.GetByteCount(payload);
            await context.Response.WriteAsync(payload);
            return;
        }

        try
        {
            var node = JsonNode.Parse(payload)?.AsObject() ?? new JsonObject();
            node["responseDateTime"] = DateTime.UtcNow.ToString("O");
            var rewritten = node.ToJsonString();
            context.Response.ContentLength = Encoding.UTF8.GetByteCount(rewritten);
            await context.Response.WriteAsync(rewritten);
        }
        catch (JsonException)
        {
            context.Response.ContentLength = Encoding.UTF8.GetByteCount(payload);
            await context.Response.WriteAsync(payload);
        }
    }
}
