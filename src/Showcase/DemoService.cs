using System.Text.Json;
using Showcase.Core;
using Temporalio.Api.Enums.V1;
using Temporalio.Client;
using Temporalio.Exceptions;
using Temporalio.Worker;

namespace Showcase;

public sealed class DemoSettings
{
    public string Host { get; } = (Environment.GetEnvironmentVariable("TEMPORAL_ADDRESS") ?? "").Trim();
    public string Namespace { get; } = (Environment.GetEnvironmentVariable("TEMPORAL_NAMESPACE") ?? "").Trim();
    public string ApiKey { get; } = (Environment.GetEnvironmentVariable("TEMPORAL_API_KEY") ?? "").Trim();
    public string Run { get; } = Environment.GetEnvironmentVariable("DEMO_RUN") ?? "team-demo-01";
    public string TaskQueue => "sa-showcase-" + Run;
    public bool Local => Host is "localhost:7233" or "127.0.0.1:7233";
    public bool Configured => Host.Length > 0 && Namespace.Length > 0 && (Local || ApiKey.Length > 0);
    public DemoSettings() => _ = DemoData.Prefix(Run);
}

public record WorkflowRow(string Id, string RunId, string RefId, string CorrelationId, string Status,
    string[] Tags, long Attempt, bool RequiresReview, double Score, DateTimeOffset? BusinessEventTime,
    string? Description, Dictionary<string, object> Attributes, string TemporalUiUrl);
public record SearchResponse(string Query, long ApproximateCount, WorkflowRow[] Rows, int Limit, DateTimeOffset FetchedAt);

public sealed class DemoService(DemoSettings settings, ILoggerFactory loggerFactory, IWebHostEnvironment environment)
{
    private static readonly HttpClient schemaHttp = new(new HttpClientHandler { AllowAutoRedirect = false })
    {
        Timeout = TimeSpan.FromSeconds(15)
    };
    private readonly SemaphoreSlim connectionLock = new(1);
    private readonly SemaphoreSlim seedLock = new(1);
    private readonly object manifestLock = new();
    private TemporalClient? client;
    private DemoManifest? manifest;
    public bool WorkerRunning { get; set; }
    public string WorkerMessage { get; set; } = "Waiting for configuration";
    private static RpcOptions Rpc(CancellationToken token) => new() { CancellationToken = token, Timeout = TimeSpan.FromSeconds(12) };

    public DemoManifest Manifest
    {
        get
        {
            lock (manifestLock)
            {
                if (manifest != null) return manifest;
                var path = Path.Combine(environment.ContentRootPath, "data", settings.Run + ".json");
                if (File.Exists(path))
                    manifest = JsonSerializer.Deserialize<DemoManifest>(File.ReadAllText(path))
                        ?? throw new InvalidOperationException("Invalid demo manifest.");
                else
                {
                    manifest = DemoData.Create(settings.Run, DateTimeOffset.UtcNow);
                    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                    File.WriteAllText(path, JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));
                }
                if (manifest.Run != settings.Run) throw new InvalidOperationException("Manifest run ID does not match configuration.");
                return manifest;
            }
        }
    }

    public async Task<TemporalClient> ClientAsync(CancellationToken token)
    {
        if (!settings.Configured)
            throw new InvalidOperationException("Configure TEMPORAL_ADDRESS, TEMPORAL_NAMESPACE, and TEMPORAL_API_KEY, then restart the app. Use start-demo.ps1.");
        await connectionLock.WaitAsync(token);
        try
        {
            client ??= await TemporalClient.ConnectAsync(new(settings.Host)
            {
                Namespace = settings.Namespace,
                ApiKey = settings.Local && settings.ApiKey.Length == 0 ? null : settings.ApiKey,
                Tls = settings.Local ? null : new(),
                // Namespace routing metadata for runtime RPCs (and local schema RPCs).
                RpcMetadata = new Dictionary<string, string>
                {
                    ["temporal-namespace"] = settings.Namespace
                },
                LoggerFactory = loggerFactory
            }).WaitAsync(TimeSpan.FromSeconds(20), token);
            return client;
        }
        finally { connectionLock.Release(); }
    }

    public async Task<object> SchemaAsync(CancellationToken token)
    {
        try
        {
            var keys = await SchemaKeysAsync(token);
            return new
            {
                verified = SearchKeys.All.All(k => keys.TryGetValue(k.Name, out var type) && type == k.ValueType.ToString()),
                definitionsRead = true,
                source = settings.Local ? "Local server OperatorService.ListSearchAttributes" : "Temporal Cloud Ops GetNamespace: namespace.spec.searchAttributes",
                notice = "Actual namespace definitions were read and compared with the SDK contract. Missing or different types are shown below. Workflow starts still validate every submitted attribute.",
                attributes = SearchKeys.All.Select(k => new
                {
                    name = k.Name,
                    expected = k.ValueType.ToString(),
                    actual = keys.TryGetValue(k.Name, out var type) ? type : "Missing",
                    matches = (bool?)(keys.TryGetValue(k.Name, out var actual) && actual == k.ValueType.ToString())
                }).ToArray()
            };
        }
        catch (Exception ex) when (
            ex is HttpRequestException { StatusCode: System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden } ||
            ex is RpcException rpc && (rpc.Code.ToString() is "PermissionDenied" or "Unimplemented"))
        {
            // This optional inspection is not a runtime workflow operation. Do not
            // assert schema success or manufacture actual definitions when it is denied.
            return new
            {
                verified = false,
                definitionsRead = false,
                source = "Local SDK contract only; namespace definitions NOT read",
                notice = settings.Local
                    ? "The local server rejected schema inspection. Namespace definitions have not been read."
                    : $"{ex.Message} Workflow access and Cloud configuration access are checked separately. Confirm this principal can read GetNamespace for the target namespace; do not grant Account Owner just for this read. You can also check definitions in Cloud UI. Seeding remains independent of inspection.",
                attributes = SearchKeys.All.Select(k => new
                {
                    name = k.Name,
                    expected = k.ValueType.ToString(),
                    actual = "Not read from namespace",
                    matches = (bool?)null
                }).ToArray()
            };
        }
    }

    private async Task<Dictionary<string, string>> SchemaKeysAsync(CancellationToken token)
    {
        if (!settings.Local)
            return await CloudNamespaceSchema.ReadAsync(schemaHttp, settings.Namespace, settings.ApiKey, token);
        var c = await ClientAsync(token);
        // OperatorService is used only for an explicitly configured local server.
        var response = await c.OperatorService.ListSearchAttributesAsync(
            new Temporalio.Api.OperatorService.V1.ListSearchAttributesRequest
            {
                Namespace = settings.Namespace
            },
            Rpc(token));
        return response.CustomAttributes.ToDictionary(p => p.Key, p => p.Value.ToString());
    }

    public async Task<object> SeedAsync(CancellationToken token)
    {
        await seedLock.WaitAsync(token);
        try
        {
            if (!WorkerRunning) throw new InvalidOperationException("The worker is not running. Check configuration, permissions, and connectivity first.");
            var c = await ClientAsync(token);
            // Confirm runtime read access without requiring the optional schema RPC.
            // Temporal enforces namespace Write rights and search attribute validity
            // when each real StartWorkflowExecution request is submitted below.
            await c.CountWorkflowsAsync(DemoData.Scope(settings.Run, null), new() { Rpc = Rpc(token) });
            var started = new List<string>();
            var existing = new List<string>();
            foreach (var payment in Manifest.Payments)
            {
                var id = DemoData.Prefix(settings.Run) + payment.RefId;
                try
                {
                    await c.StartWorkflowAsync((PaymentWorkflow w) => w.RunAsync(payment), new(id, settings.TaskQueue)
                    {
                        TypedSearchAttributes = SearchKeys.AtStart(payment),
                        IdReusePolicy = WorkflowIdReusePolicy.RejectDuplicate,
                        RunTimeout = TimeSpan.FromHours(2),
                        Rpc = Rpc(token)
                    });
                    started.Add(id);
                }
                catch (WorkflowAlreadyStartedException) { existing.Add(id); }
            }
            return new { started, existing, message = "Refresh after indexing catches up. Allow about 10-20 seconds for the baseline business attempts; this is not a Visibility SLA." };
        }
        finally { seedLock.Release(); }
    }

    public async Task<SearchResponse> SearchAsync(string? query, CancellationToken token)
    {
        if (query?.Length > 2000) throw new ArgumentException("Keep demo filters under 2,000 characters.");
        var c = await ClientAsync(token);
        var scoped = DemoData.Scope(settings.Run, query);
        var rows = new List<WorkflowRow>();
        await foreach (var execution in c.ListWorkflowsAsync(scoped, new() { Limit = 100, Rpc = Rpc(token) }))
            rows.Add(Row(execution));
        var count = await c.CountWorkflowsAsync(scoped, new() { Rpc = Rpc(token) });
        return new(scoped, count.Count, rows.OrderBy(r => r.RefId, StringComparer.Ordinal).ToArray(), 100, DateTimeOffset.UtcNow);
    }

    public async Task<object> DetailsAsync(string id, CancellationToken token)
    {
        Guard(id);
        var c = await ClientAsync(token);
        var handle = c.GetWorkflowHandle(id);
        var description = await handle.DescribeAsync(new() { Rpc = Rpc(token) });
        PaymentSnapshot? snapshot = null;
        string? snapshotError = null;
        try
        {
            snapshot = await handle.QueryAsync<PaymentWorkflow, PaymentSnapshot>(w => w.GetSnapshot(), new() { Rpc = Rpc(token) });
        }
        catch (Exception ex) when (ex is RpcException or WorkflowQueryFailedException or WorkflowQueryRejectedException)
        { snapshotError = "The worker could not answer the Workflow Query. Indexed metadata and execution status are shown separately."; }
        return new { execution = Row(description), snapshot, snapshotError, note = "Describe and a Workflow Query are separate reads; Visibility list results may temporarily lag either one." };
    }

    public async Task<object> ActionAsync(string id, string action, CancellationToken token)
    {
        Guard(id);
        var c = await ClientAsync(token);
        var handle = c.GetWorkflowHandle(id);
        var execution = await handle.DescribeAsync(new() { Rpc = Rpc(token) });
        if (execution.Status != WorkflowExecutionStatus.Running) throw new InvalidOperationException("This workflow is already closed.");
        var snapshot = await handle.QueryAsync<PaymentWorkflow, PaymentSnapshot>(w => w.GetSnapshot(), new() { Rpc = Rpc(token) });
        var signalOptions = new WorkflowSignalOptions { Rpc = Rpc(token) };
        switch (action)
        {
            case "approve" or "reject" when snapshot.Stage == "awaiting-review":
                await handle.SignalAsync<PaymentWorkflow>(w => w.DecideAsync(action), signalOptions);
                break;
            case "release" when snapshot.Stage == "awaiting-release":
                await handle.SignalAsync<PaymentWorkflow>(w => w.ReleaseAsync(), signalOptions);
                break;
            case "clear-description":
                await handle.SignalAsync<PaymentWorkflow>(w => w.ClearDescriptionAsync(), signalOptions);
                break;
            default: throw new ArgumentException("That action is not valid for this case's current stage.");
        }
        return new { message = "Signal accepted. The worker still needs to handle it, and Visibility updates are asynchronous. Refresh shortly." };
    }

    public async Task<object> VerifyAsync(CancellationToken token)
    {
        var results = new List<object>();
        var allPassed = true;
        foreach (var scenario in DemoData.Scenarios(Manifest).Where(s => s.Baseline))
        {
            var result = await SearchAsync(scenario.Query, token);
            var actual = result.Rows.Select(r => r.RefId).Order(StringComparer.Ordinal).ToArray();
            var expected = scenario.ExpectedRefs.Order(StringComparer.Ordinal).ToArray();
            var passed = actual.SequenceEqual(expected);
            allPassed &= passed;
            results.Add(new { scenario.Id, scenario.Title, passed, expected, actual, result.Query });
        }
        return new { allPassed, results, note = "Baseline only, before review/release/unset actions. A mismatch is shown, never replaced with expected data. Retry after the worker and index catch up." };
    }

    private void Guard(string id)
    {
        if (!id.StartsWith(DemoData.Prefix(settings.Run), StringComparison.Ordinal))
            throw new ArgumentException("Actions and detail reads are limited to this demo run's workflow IDs.");
    }

    private WorkflowRow Row(WorkflowExecution e)
    {
        var a = e.TypedSearchAttributes;
        a.TryGetValue(SearchKeys.RefId, out var reference);
        a.TryGetValue(SearchKeys.CorrelationId, out var correlation);
        a.TryGetValue(SearchKeys.Tags, out var tags);
        a.TryGetValue(SearchKeys.Description, out var description);
        a.TryGetValue(SearchKeys.RequiresReview, out var review);
        a.TryGetValue(SearchKeys.Attempt, out var attempt);
        a.TryGetValue(SearchKeys.Score, out var score);
        var hasTime = a.TryGetValue(SearchKeys.EventTime, out var time);
        var url = settings.Local ? $"http://localhost:8233/namespaces/{Uri.EscapeDataString(settings.Namespace)}/workflows/{Uri.EscapeDataString(e.Id)}/{e.RunId}/history"
            : $"https://cloud.temporal.io/namespaces/{Uri.EscapeDataString(settings.Namespace)}/workflows/{Uri.EscapeDataString(e.Id)}/{e.RunId}/history";
        return new(e.Id, e.RunId, reference ?? e.Id, correlation ?? "", e.Status.ToString(),
            tags?.ToArray() ?? [], attempt, review, score, hasTime ? time : null, description,
            a.UntypedValues.ToDictionary(p => p.Key.Name, p => p.Value), url);
    }
}

public sealed class DemoWorker(DemoService service, DemoSettings settings, ILogger<DemoWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!settings.Configured) return;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var client = await service.ClientAsync(stoppingToken);
                using var worker = new TemporalWorker(client,
                    new TemporalWorkerOptions(settings.TaskQueue).AddWorkflow<PaymentWorkflow>()
                        .AddActivity(PaymentActivities.SimulateSettlementAsync));
                service.WorkerRunning = true;
                service.WorkerMessage = "Worker started; server polling begins asynchronously";
                await worker.ExecuteAsync(stoppingToken);
                return;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception ex)
            {
                service.WorkerMessage = "Worker could not start or stopped. Check the terminal and connection settings.";
                logger.LogError("Demo worker stopped: {ErrorType}. Check endpoint, credentials, namespace permissions, and network access.", ex.GetType().Name);
            }
            finally { service.WorkerRunning = false; }
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }
}
