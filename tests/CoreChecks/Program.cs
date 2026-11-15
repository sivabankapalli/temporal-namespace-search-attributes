using System.Text.Json;
using Showcase.Core;

var checks = 0;
void Check(bool condition, string name)
{
    if (!condition) throw new Exception("FAILED: " + name);
    checks++;
    Console.WriteLine("PASS " + name);
}

var anchor = DateTimeOffset.Parse("2026-11-14T10:00:00Z");
var data = DemoData.Create("rehearsal-01", anchor);
var scenarios = DemoData.Scenarios(data);
Check(data.Payments.Length == 10, "Ten synthetic cases");
Check(data.Payments.Select(p => p.RefId).Distinct().Count() == 10, "Unique business references");
Check(DemoData.Attributes.Length == 8 && DemoData.Attributes.Select(a => a.Type).Distinct().Count() == 7, "Eight definitions cover seven types");
Check(data.Payments.Count(p => p.RefId == "PAY-1001") == 1 && data.Payments.Count(p => p.RefId.StartsWith("PAY-1001", StringComparison.Ordinal)) == 2, "Exact and prefix searches have intentionally different audiences");
Check(data.Payments.Count(p => p.CorrelationId == DemoData.BatchCorrelationId.ToString("D")) == 3, "One batch connects three independent executions");
Check(data.Payments.All(p => Guid.TryParse(p.CorrelationId, out var id) && p.CorrelationId == id.ToString("D")), "All correlation values are canonical GUID strings");
Check(data.Payments.Select(p => p.CorrelationId).Distinct().Count() == 8 && scenarios.Single(s => s.Id == "correlation").Query == $"CorrelationID = '{DemoData.BatchCorrelationId:D}'", "GUID grouping and correlation scene agree");
var legacy = data with { Payments = data.Payments.Select(p => p.CorrelationId == DemoData.BatchCorrelationId.ToString("D") ? p with { CorrelationId = "CORR-BATCH-001" } : p).ToArray() };
Check(DemoData.Scenarios(legacy).Single(s => s.Id == "correlation").Query == "CorrelationID = 'CORR-BATCH-001'", "Old persisted manifests keep their existing correlation query");
Check(data.Payments.Count(p => p.RequiresReview) == 3, "Initial human review queue has three cases");
Check(data.Payments.Count(p => p.Score >= .8) == 4, "High-score cases include a business rejection");
Check(data.Payments.Count(p => p.BusinessEventTime >= anchor.AddHours(-24)) == 9, "Business time excludes one old event even when seeded today");
Check(data.Payments.Count(p => p.RequiresReview && p.Score >= .8 && p.BusinessEventTime >= anchor.AddHours(-24) && p.Tags.Contains("priority")) == 2, "Combined urgent queue has two cases");
Check(data.Payments.Count(p => p.FailuresBeforeSuccess == 2) == 1 && data.Payments.Count(p => p.FailuresBeforeSuccess >= 3) == 1, "One recovery and one persistent technical failure are distinct");
Check(scenarios.Count(s => s.Baseline) == 13 && scenarios.Single(s => s.Id == "unset").Baseline == false, "Verification excludes the mutation-dependent unset scene");
Check(scenarios.All(s => s.ExpectedRefs.All(r => data.Payments.Any(p => p.RefId == r))), "Every expected reference exists in the dataset");
Check(DemoData.Scope("rehearsal-01", "RefID = 'PAY-1001'") == "WorkflowId STARTS_WITH 'sa-showcase-rehearsal-01-' AND (RefID = 'PAY-1001')", "Scoped queries keep run isolation and precedence");
foreach (var invalid in new[] { "", "UPPER", "run' OR true", "x/y", new string('a', 33) })
{
    var rejected = false;
    try { _ = DemoData.Prefix(invalid); } catch (ArgumentException) { rejected = true; }
    Check(rejected, "Reject invalid run ID: " + invalid);
}
var restored = JsonSerializer.Deserialize<DemoManifest>(JsonSerializer.Serialize(data))!;
Check(restored.Run == data.Run && restored.CreatedAt == anchor && restored.Payments.Length == 10 && restored.Payments[0].Tags.SequenceEqual(data.Payments[0].Tags), "Manifest survives restart without shifting date cutoff or losing data");
await CloudSchemaChecks.RunAsync(Check);
var examples = TypeExamples.Create(data);
Check(examples.Length == 8 && examples.Select(e => e.Type).Distinct().Count() == 7 && examples.All(e => DemoData.Attributes.Any(a => a.Name == e.Attribute && a.Type == e.Type)), "UI examples cover all seven types with registered names and types");
Check(examples.All(e => e.ExpectedRefs.All(r => data.Payments.Any(p => p.RefId == r))) && examples.Single(e => e.Type == "Datetime").Query == scenarios.Single(s => s.Id == "time").Query, "UI baseline references and frozen date filter match the manifest");
Check(TypeExamples.Create(legacy).Single(e => e.Id == "guid").ExampleValue == "CORR-BATCH-001" && TypeExamples.Create(legacy).Single(e => e.Id == "guid").Query == "CorrelationID = 'CORR-BATCH-001'", "UI uses an existing run's actual correlation value instead of inventing a GUID");
Console.WriteLine($"\n{checks} offline core checks passed. These do not test Temporal SDK compilation or live Cloud behavior.");
