using System.Net;
using Showcase;
using Showcase.Core;
using Temporalio.Exceptions;

var contentRoot = Directory.GetCurrentDirectory();
if (Directory.Exists(Path.Combine(contentRoot, "src", "Showcase", "wwwroot")))
    contentRoot = Path.Combine(contentRoot, "src", "Showcase");
var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = args, ContentRootPath = contentRoot });
// Local demonstration only. Do not expose this app as a shared/production portal.
builder.WebHost.UseUrls("http://127.0.0.1:5080");
builder.Services.AddSingleton<DemoSettings>();
builder.Services.AddSingleton<DemoService>();
builder.Services.AddHostedService<DemoWorker>();
var app = builder.Build();

app.Use(async (context, next) =>
{
    if (context.Connection.RemoteIpAddress is { } ip && !IPAddress.IsLoopback(ip))
    {
        context.Response.StatusCode = 403;
        return;
    }
    if (context.Request.Method == "POST" && context.Request.Headers["X-Showcase-Action"] != "1")
    {
        context.Response.StatusCode = 403;
        return;
    }
    try { await next(); }
    catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested) { }
    catch (Exception ex)
    {
        var key = app.Services.GetRequiredService<DemoSettings>().ApiKey;
        var message = ex.Message;
        if (key.Length > 0) message = message.Replace(key, "[redacted]", StringComparison.Ordinal);
        var grpcCode = (ex as RpcException)?.Code.ToString();
        context.Response.StatusCode = grpcCode switch
        {
            "Unauthenticated" => 401,
            "PermissionDenied" => 403,
            _ => 400
        };
        var hint = grpcCode is "Unauthenticated" or "PermissionDenied"
            ? "Check the denied operation. Successful List/Count proves read access only; starting workflows and processing worker tasks also need namespace Write/equivalent access. Schema inspection is optional."
            : null;
        await context.Response.WriteAsJsonAsync(new { error = message, grpcCode, hint });
    }
});
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapGet("/api/config", (DemoSettings settings, DemoService service) => new
{
    settings.Host, settings.Namespace, settings.Run, settings.TaskQueue, settings.Configured,
    service.WorkerRunning, service.WorkerMessage,
    schemaCheckVersion = "cloud-ops-schema-v4",
    correlationIdFormat = "Guid D string (lowercase, hyphens); new runs",
    uiVersion = "type-explorer-v1",
    manifestCreatedAt = service.Manifest.CreatedAt,
    mode = "Live Temporal SDK; synthetic payment simulation"
});
app.MapGet("/api/catalog", (DemoService service) => new
{
    attributes = DemoData.Attributes,
    scenarios = DemoData.Scenarios(service.Manifest),
    typeExamples = TypeExamples.Create(service.Manifest)
});
app.MapGet("/api/schema", async (DemoService service, CancellationToken token) => await service.SchemaAsync(token));
app.MapPost("/api/seed", async (DemoService service, CancellationToken token) => await service.SeedAsync(token));
app.MapGet("/api/search", async (string? query, DemoService service, CancellationToken token) => await service.SearchAsync(query, token));
app.MapGet("/api/details", async (string id, DemoService service, CancellationToken token) => await service.DetailsAsync(id, token));
app.MapPost("/api/action", async (ActionRequest request, DemoService service, CancellationToken token) => await service.ActionAsync(request.Id, request.Action, token));
app.MapGet("/api/verify", async (DemoService service, CancellationToken token) => await service.VerifyAsync(token));
app.Run();

record ActionRequest(string Id, string Action);
