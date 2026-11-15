using System.Net;
using System.Text.Json;
using Showcase.Core;

internal static class CloudSchemaChecks
{
    private const string NamespaceId = "search-attributes-demo.ravxd";
    // Fixture follows the documented GetNamespace protobuf JSON response. It is
    // synthetic contract evidence, not a captured or claimed live response.
    private const string Fixture = """
        {"namespace":{"namespace":"search-attributes-demo.ravxd","spec":{"searchAttributes":{
          "CorrelationID":"SEARCH_ATTRIBUTE_TYPE_KEYWORD",
          "RefID":"SEARCH_ATTRIBUTE_TYPE_KEYWORD",
          "ProcessingTags":"SEARCH_ATTRIBUTE_TYPE_KEYWORD_LIST",
          "ProcessingDescription":"SEARCH_ATTRIBUTE_TYPE_TEXT",
          "RequiresReview":"SEARCH_ATTRIBUTE_TYPE_BOOL",
          "ProcessingAttempt":"SEARCH_ATTRIBUTE_TYPE_INT",
          "ProcessingScore":"SEARCH_ATTRIBUTE_TYPE_DOUBLE",
          "BusinessEventTime":"SEARCH_ATTRIBUTE_TYPE_DATETIME"
        }}}}
        """;

    public static async Task RunAsync(Action<bool, string> check)
    {
        using var doc = JsonDocument.Parse(Fixture);
        var keys = CloudNamespaceSchema.Parse(doc.RootElement, NamespaceId);
        check(keys.Count == 8 && DemoData.Attributes.All(a => keys.GetValueOrDefault(a.Name) == a.Type),
            "Documented Cloud response maps all eight definitions and seven types");

        bool Rejects(string json, string namespaceId = NamespaceId)
        {
            using var parsed = JsonDocument.Parse(json);
            try { _ = CloudNamespaceSchema.Parse(parsed.RootElement, namespaceId); return false; }
            catch (InvalidOperationException) { return true; }
        }
        check(Rejects(Fixture, "other.account"), "Schema for a different namespace cannot be verified");
        check(Rejects("{}"), "Missing namespace response is not interpreted as verified");
        check(Rejects("""{"namespace":{"namespace":"search-attributes-demo.ravxd","spec":{"searchAttributes":[]}}}"""),
            "Malformed attribute map is rejected");
        using var empty = JsonDocument.Parse("""{"namespace":{"namespace":"search-attributes-demo.ravxd","spec":{}}}""");
        check(CloudNamespaceSchema.Parse(empty.RootElement, NamespaceId).Count == 0,
            "Omitted empty map does not manufacture expected definitions");
        using var unknown = JsonDocument.Parse("""{"namespace":{"namespace":"search-attributes-demo.ravxd","spec":{"searchAttributes":{"RefID":"SEARCH_ATTRIBUTE_TYPE_FUTURE","refid":"SEARCH_ATTRIBUTE_TYPE_TEXT","NumericKeyword":2}}}}""");
        var other = CloudNamespaceSchema.Parse(unknown.RootElement, NamespaceId);
        check(other["RefID"].StartsWith("Unknown Cloud type:") && other["refid"] == "Text" && other["NumericKeyword"] == "Keyword",
            "Unknown types remain mismatches, names preserve case, numeric enums are supported");

        using var success = new HttpClient(new Handler(request =>
        {
            check(request.Method == HttpMethod.Get && request.RequestUri!.AbsoluteUri ==
                "https://saas-api.tmprl.cloud/cloud/namespaces/" + NamespaceId &&
                request.Headers.Authorization?.Scheme == "Bearer" &&
                request.Headers.Authorization.Parameter == "synthetic-test-key" &&
                request.Headers.GetValues("temporal-cloud-api-version").Single() == CloudNamespaceSchema.ApiVersion,
                "Schema request uses documented Cloud endpoint, GET, Bearer header and version");
            return new(HttpStatusCode.OK) { Content = new StringContent(Fixture) };
        }));
        var fetched = await CloudNamespaceSchema.ReadAsync(success, NamespaceId, "synthetic-test-key", CancellationToken.None);
        check(fetched.Count == 8, "HTTP response is parsed as actual namespace definitions");

        foreach (var status in new[] { HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.InternalServerError })
        {
            using var denied = new HttpClient(new Handler(_ => new(status) { Content = new StringContent("synthetic-secret-body") }));
            var rejected = false;
            try { _ = await CloudNamespaceSchema.ReadAsync(denied, NamespaceId, "synthetic-test-key", CancellationToken.None); }
            catch (HttpRequestException ex)
            {
                rejected = ex.StatusCode == status && !ex.Message.Contains("synthetic-secret-body") && !ex.Message.Contains("synthetic-test-key");
            }
            check(rejected, $"HTTP {(int)status} remains a failure without exposing key or raw body");
        }
    }

    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(respond(request));
    }
}
