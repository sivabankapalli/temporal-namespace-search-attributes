using System.Globalization;
using System.Text.RegularExpressions;

namespace Showcase.Core;

public record AttributeDefinition(string Name, string Type, string DotNetType, string Question, string Guidance);
public record PaymentCase(string RefId, string CorrelationId, string[] Tags, string Description,
    bool RequiresReview, double Score, DateTimeOffset BusinessEventTime, int FailuresBeforeSuccess = 0,
    bool HoldForRelease = false, bool BusinessReject = false);
public record DemoManifest(string Run, DateTimeOffset CreatedAt, PaymentCase[] Payments);
public record Scenario(string Id, string Title, string Audience, string Query, string Lesson,
    string[] ExpectedRefs, bool Baseline = true);

public static partial class DemoData
{
    // Fixed GUID fixtures keep the recording repeatable. In an application, create
    // Guid.NewGuid() once at the initiating client and propagate it to related work.
    public static readonly Guid BatchCorrelationId = Guid.Parse("84f8c6a1-4db8-4d5b-9c11-2a6e9f0d7b32");

    public static readonly AttributeDefinition[] Attributes =
    [
        new("CorrelationID", "Keyword", "string", "Which payments belong to the same batch?", "GUID formatted as a lowercase D string (with hyphens), indexed as Keyword. Reuse it across related executions; not an authorization boundary."),
        new("RefID", "Keyword", "string", "Where is this exact payment?", "Preserve formatting and leading zeros; IDs are not numbers."),
        new("ProcessingTags", "KeywordList", "IReadOnlyCollection<string>", "Which cases are priority or awaiting review?", "Membership search; this demo also encodes business stage as one tag."),
        new("ProcessingDescription", "Text", "string", "Which cases mention a word?", "Tokenized word search. Never use Text for exact reference matching."),
        new("RequiresReview", "Bool", "bool", "Which cases need human attention?", "A business flag; not the built-in workflow execution status."),
        new("ProcessingAttempt", "Int", "long", "Which cases needed repeated processing?", "Explicit business attempts in this demo, not the SDK's automatic activity attempt counter."),
        new("ProcessingScore", "Double", "double", "Which cases exceed a review score?", "Synthetic triage score, not a real risk model. Not a financial ledger amount."),
        new("BusinessEventTime", "Datetime", "DateTimeOffset", "Which business events happened recently?", "UTC business event time can differ from built-in StartTime.")
    ];

    [GeneratedRegex("^[a-z0-9][a-z0-9-]{0,31}$")]
    private static partial Regex RunPattern();

    public static string Prefix(string run)
    {
        if (!RunPattern().IsMatch(run))
            throw new ArgumentException("DEMO_RUN must contain 1-32 lowercase letters, digits, or hyphens, starting with a letter or digit.");
        return $"sa-showcase-{run}-";
    }

    public static string Scope(string run, string? query) =>
        $"WorkflowId STARTS_WITH '{Prefix(run)}'" +
        (string.IsNullOrWhiteSpace(query) ? "" : $" AND ({query})");

    public static DemoManifest Create(string run, DateTimeOffset anchor)
    {
        _ = Prefix(run);
        return new(run, anchor, [
            new("PAY-1001", BatchCorrelationId.ToString("D"), ["payments", "priority"], "Priority payment awaiting approval ref PAY-1001", true, .92, anchor.AddMinutes(-10)),
            new("PAY-10010", Guid.Parse("6a02a1b9-2df0-54fd-a1da-82525e442277").ToString("D"), ["payments", "standard"], "Standard payment queued ref PAY-10010", false, .12, anchor.AddMinutes(-20)),
            new("PAY-1002", BatchCorrelationId.ToString("D"), ["payments", "priority"], "Priority payment awaiting approval ref PAY-1002", true, .85, anchor.AddMinutes(-30)),
            new("PAY-1003", BatchCorrelationId.ToString("D"), ["payments", "recovery"], "Payment with simulated transient issue ref PAY-1003", false, .25, anchor.AddMinutes(-40), FailuresBeforeSuccess: 2),
            new("PAY-1004", Guid.Parse("c1704b51-44e1-5743-a99e-f1cf5f1acac8").ToString("D"), ["payments", "standard"], "Standard payment queued ref PAY-1004", false, .15, anchor.AddHours(-2)),
            new("PAY-1005", Guid.Parse("43b12c9d-94c1-5baa-b6fc-efd3d85604f9").ToString("D"), ["payments", "policy"], "Payment rejected by synthetic policy ref PAY-1005", false, .99, anchor.AddHours(-3), BusinessReject: true),
            new("PAY-1006", Guid.Parse("af6e6a9e-65bb-5140-817f-c17f7a9a7285").ToString("D"), ["payments", "scheduled"], "Payment waiting for release ref PAY-1006", false, .44, anchor.AddHours(-4), HoldForRelease: true),
            new("PAY-1007", Guid.Parse("b4bed15d-f6e5-5b22-bc96-63ce6357607a").ToString("D"), ["payments", "standard"], "Standard payment queued ref PAY-1007", false, .05, anchor.AddHours(-6)),
            new("PAY-1008", Guid.Parse("bab5f8ac-0af5-5a33-8d52-5039dbe2535e").ToString("D"), ["payments", "recovery"], "Payment with simulated persistent issue ref PAY-1008", false, .70, anchor.AddHours(-7), FailuresBeforeSuccess: 99),
            new("PAY-1009", Guid.Parse("52581697-2304-5e84-9872-d9e9a3772746").ToString("D"), ["payments", "priority", "international"], "Older payment awaiting approval ref PAY-1009", true, .91, anchor.AddHours(-26))
        ]);
    }

    public static Scenario[] Scenarios(DemoManifest manifest)
    {
        var all = manifest.Payments.Select(p => p.RefId).ToArray();
        // Read the stored value, so old manifests retain their original correlation
        // scene. New runs use canonical GUID strings without rewriting old workflows.
        var batchCorrelation = manifest.Payments.Single(p => p.RefId == "PAY-1001").CorrelationId;
        var cutoff = manifest.CreatedAt.AddHours(-24).ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'", CultureInfo.InvariantCulture);
        return [
            new("all", "The operational picture", "Everyone", "", "Ten real Temporal executions, synthetic payment data. Wait for activity processing before comparing baseline counts.", all),
            new("exact", "Find one exact payment", "Customer support", "RefID = 'PAY-1001'", "Keyword equality finds PAY-1001 without also finding PAY-10010.", ["PAY-1001"]),
            new("correlation", "Follow the batch", "Support / integration", $"CorrelationID = '{batchCorrelation}'", "Related workflows share one correlation ID. Fresh runs use a GUID encoded as Keyword; it does not replace WorkflowId.", ["PAY-1001", "PAY-1002", "PAY-1003"]),
            new("prefix", "Find a reference family", "Operations", "RefID STARTS_WITH 'PAY-1001'", "Prefix matching deliberately returns PAY-1001 and PAY-10010.", ["PAY-1001", "PAY-10010"]),
            new("review", "Build the review queue", "Operations", "RequiresReview = true AND ExecutionStatus = 'Running'", "A Bool plus built-in execution status creates an operational queue. Approve PAY-1001 to make it leave this result.", ["PAY-1001", "PAY-1002", "PAY-1009"]),
            new("tags", "Find priority work", "Operations", "ProcessingTags = 'priority'", "KeywordList equality tests membership; it does not compare the whole list.", ["PAY-1001", "PAY-1002", "PAY-1009"]),
            new("score", "Apply a score threshold", "Risk / analysts", "ProcessingScore >= 0.8", "Numeric comparison over a synthetic score. A matching case might already be business-rejected.", ["PAY-1001", "PAY-1002", "PAY-1005", "PAY-1009"]),
            new("attempts", "Find repeated processing", "SRE / engineering", "ProcessingAttempt >= 3", "One case recovered and one failed. Repeated business attempts do not necessarily mean workflow failure.", ["PAY-1003", "PAY-1008"]),
            new("time", "Find recent business events", "Analysts", $"BusinessEventTime >= '{cutoff}'", "The cutoff is frozen when this run's manifest is created for repeatable recording. PAY-1009 is an older business event started today.", all.Where(r => r != "PAY-1009").ToArray()),
            new("text", "The Text matching surprise", "Everyone", "ProcessingDescription = 'PAY-1001'", "Text tokenization can match shared words such as PAY. Compare with the exact RefID search. Confirm actual behavior on your namespace during rehearsal.", all),
            new("combined", "Find today's urgent review work", "Team leads", $"RequiresReview = true AND ProcessingTags = 'priority' AND ProcessingScore >= 0.8 AND BusinessEventTime >= '{cutoff}' AND ExecutionStatus = 'Running'", "Combine four custom types and built-in status to answer one business question.", ["PAY-1001", "PAY-1002"]),
            new("rejected", "A business rejection can be Completed", "Product / engineering", "ProcessingTags = 'rejected' AND ExecutionStatus = 'Completed'", "The workflow successfully reached a business decision; the payment was not settled.", ["PAY-1005"]),
            new("failed", "Find a technical failure", "SRE / engineering", "ExecutionStatus = 'Failed'", "PAY-1008 exhausts the demo's three explicit attempts and fails with an ApplicationFailureException.", ["PAY-1008"]),
            new("unset", "Show an absent attribute", "SDK developers", "ProcessingDescription IS NULL", "Initially empty. Clear the description on PAY-1006 to demonstrate ValueUnset. Input/history are not erased by unsetting indexed metadata.", [], false)
        ];
    }
}
