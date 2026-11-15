using System.Net.Http.Headers;
using System.Text.Json;

namespace Showcase.Core;

// Cloud namespace configuration is a control-plane resource. Read it from
// Cloud Ops rather than the namespace's workflow/worker gRPC endpoint.
public static class CloudNamespaceSchema
{
    public const string ApiVersion = "v0.26.0";

    public static async Task<Dictionary<string, string>> ReadAsync(
        HttpClient http, string namespaceId, string apiKey, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(namespaceId) || string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("Configure the namespace ID and API key before inspecting Cloud definitions.");
        using var request = new HttpRequestMessage(HttpMethod.Get,
            "https://saas-api.tmprl.cloud/cloud/namespaces/" + Uri.EscapeDataString(namespaceId));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Headers.Add("temporal-cloud-api-version", ApiVersion);
        using var response = await http.SendAsync(request, token);
        // Do not echo a raw error body or credentials into the local dashboard.
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"Cloud Ops GetNamespace returned HTTP {(int)response.StatusCode} ({response.StatusCode}).",
                null, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(token));
        return Parse(document.RootElement, namespaceId);
    }

    public static Dictionary<string, string> Parse(JsonElement root, string namespaceId)
    {
        if (root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty("namespace", out var ns) || ns.ValueKind != JsonValueKind.Object ||
            !ns.TryGetProperty("namespace", out var id) || id.ValueKind != JsonValueKind.String ||
            id.GetString() != namespaceId ||
            !ns.TryGetProperty("spec", out var spec) || spec.ValueKind != JsonValueKind.Object)
            throw new InvalidOperationException("Cloud Ops response did not contain configuration for the requested full namespace ID.");

        var keys = new Dictionary<string, string>(StringComparer.Ordinal);
        // Protobuf JSON can omit an empty map. That means no definitions were
        // returned, not that the expected local definitions should be substituted.
        if (!spec.TryGetProperty("searchAttributes", out var attributes)) return keys;
        if (attributes.ValueKind != JsonValueKind.Object)
            throw new InvalidOperationException("Cloud Ops searchAttributes was not a name/type map.");
        foreach (var attribute in attributes.EnumerateObject())
        {
            var type = attribute.Value.ValueKind switch
            {
                JsonValueKind.String => attribute.Value.GetString()!,
                JsonValueKind.Number => attribute.Value.GetRawText(),
                _ => throw new InvalidOperationException("Cloud Ops returned an invalid search attribute type.")
            };
            keys.Add(attribute.Name, type switch
            {
                "SEARCH_ATTRIBUTE_TYPE_TEXT" or "1" => "Text",
                "SEARCH_ATTRIBUTE_TYPE_KEYWORD" or "2" => "Keyword",
                "SEARCH_ATTRIBUTE_TYPE_INT" or "3" => "Int",
                "SEARCH_ATTRIBUTE_TYPE_DOUBLE" or "4" => "Double",
                "SEARCH_ATTRIBUTE_TYPE_BOOL" or "5" => "Bool",
                "SEARCH_ATTRIBUTE_TYPE_DATETIME" or "6" => "Datetime",
                "SEARCH_ATTRIBUTE_TYPE_KEYWORD_LIST" or "7" => "KeywordList",
                _ => "Unknown Cloud type: " + type
            });
        }
        return keys;
    }
}
