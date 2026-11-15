namespace Showcase.Core;

public record TypeExample(string Id, string Type, string Attribute, string Title,
    string Purpose, string Declaration, string Assignment, string ExampleValue,
    string Query, string[] ExpectedRefs, string Caution, string Source);

public static class TypeExamples
{
    public static TypeExample[] Create(DemoManifest manifest)
    {
        var scenarios = DemoData.Scenarios(manifest).ToDictionary(s => s.Id);
        var first = manifest.Payments.Single(p => p.RefId == "PAY-1001");
        var all = manifest.Payments.Select(p => p.RefId).ToArray();
        return
        [
            new("guid", "Keyword", "CorrelationID", "Find related executions by correlation ID",
                "Use Keyword for an exact structured identifier. Related executions can share the same GUID string.",
                "SearchAttributeKey<string> CorrelationId = SearchAttributeKey.CreateKeyword(\"CorrelationID\");",
                ".Set(CorrelationId, payment.CorrelationId)\n// New runs format the GUID with correlationId.ToString(\"D\").",
                first.CorrelationId, scenarios["correlation"].Query, scenarios["correlation"].ExpectedRefs,
                "GUID is an application type; Temporal indexes its canonical string as Keyword. Existing runs keep their saved values.",
                "SearchKeys.cs: CorrelationId and AtStart; Core/DemoData.cs: BatchCorrelationId"),
            new("reference", "Keyword", "RefID", "Find one exact reference",
                "Use Keyword when the complete reference must match. PAY-1001 and PAY-10010 are different values.",
                "SearchAttributeKey<string> RefId = SearchAttributeKey.CreateKeyword(\"RefID\");",
                ".Set(RefId, payment.RefId)", first.RefId, scenarios["exact"].Query, scenarios["exact"].ExpectedRefs,
                "Use STARTS_WITH deliberately for a prefix search. Keyword equality does not mean substring matching.",
                "SearchKeys.cs: RefId and AtStart"),
            new("tags", "KeywordList", "ProcessingTags", "Find executions with a particular label",
                "Use a collection of strings when an execution can belong to several categories. Search for membership in that collection.",
                "SearchAttributeKey<IReadOnlyCollection<string>> Tags = SearchAttributeKey.CreateKeywordList(\"ProcessingTags\");",
                ".Set(Tags, (IReadOnlyCollection<string>)payment.Tags.Append(\"received\").ToArray())",
                "[\"payments\", \"priority\", \"received\"]", scenarios["tags"].Query, scenarios["tags"].ExpectedRefs,
                "The equals filter tests membership. Workflow upserts can replace the list; returned rows show the current list, not this initial example.",
                "SearchKeys.cs: Tags and AtStart; PaymentWorkflow.cs: SetStage"),
            new("text", "Text", "ProcessingDescription", "Find a word in descriptive metadata",
                "Use Text for analyzed word matching. Both Text and Keyword take .NET strings, but their matching behavior differs.",
                "SearchAttributeKey<string> Description = SearchAttributeKey.CreateText(\"ProcessingDescription\");",
                ".Set(Description, payment.Description)\n// The workflow later upserts: $\"Payment {stage} ref {payment.RefId}\".",
                "Payment awaiting-review ref PAY-1001", "ProcessingDescription = 'Payment'", all,
                "Text equality is word matching, not equality of the whole string. Use Keyword for exact references or GUIDs. Clearing a value changes this baseline.",
                "SearchKeys.cs: Description and AtStart; PaymentWorkflow.cs: SetStage"),
            new("review", "Bool", "RequiresReview", "Find executions with a true flag",
                "Use Bool for a yes/no fact supplied by your application. A custom flag is separate from Temporal's execution status.",
                "SearchAttributeKey<bool> RequiresReview = SearchAttributeKey.CreateBool(\"RequiresReview\");",
                ".Set(RequiresReview, payment.RequiresReview)", "true", "RequiresReview = true", scenarios["review"].ExpectedRefs,
                "Write a Boolean, not the string \"true\". An absent value is different from false. Use Inspect to trigger a false-value update on a waiting case.",
                "SearchKeys.cs: RequiresReview and AtStart; PaymentWorkflow.cs: RunAsync"),
            new("attempts", "Int", "ProcessingAttempt", "Find whole-number values above a threshold",
                "Use Int for counts and numeric ranges. The .NET SDK uses long for this Temporal type.",
                "SearchAttributeKey<long> Attempt = SearchAttributeKey.CreateLong(\"ProcessingAttempt\");",
                ".Set(Attempt, 0L)\n// Workflow update: SearchKeys.Attempt.ValueSet(attempt)",
                "0 at start; 3 after repeated processing", scenarios["attempts"].Query, scenarios["attempts"].ExpectedRefs,
                "This sample explicitly writes business processing attempts. It does not automatically mirror the SDK's activity retry attempt number.",
                "SearchKeys.cs: Attempt and AtStart; PaymentWorkflow.cs: RunAsync"),
            new("score", "Double", "ProcessingScore", "Find decimal values above a threshold",
                "Use Double for decimal measurements and threshold comparisons, such as this synthetic score.",
                "SearchAttributeKey<double> Score = SearchAttributeKey.CreateDouble(\"ProcessingScore\");",
                ".Set(Score, payment.Score)", "0.92", scenarios["score"].Query, scenarios["score"].ExpectedRefs,
                "The score is synthetic. Floating-point indexed values are not an authoritative financial ledger.",
                "SearchKeys.cs: Score and AtStart"),
            new("time", "Datetime", "BusinessEventTime", "Find values within a business time window",
                "Use Datetime for time comparisons. A business event timestamp can differ from the built-in workflow StartTime.",
                "SearchAttributeKey<DateTimeOffset> EventTime = SearchAttributeKey.CreateDateTimeOffset(\"BusinessEventTime\");",
                ".Set(EventTime, payment.BusinessEventTime)", first.BusinessEventTime.ToString("O"),
                scenarios["time"].Query, scenarios["time"].ExpectedRefs,
                "The UTC cutoff is frozen when this run's manifest is created. It is not a moving last-24-hours query. Returned timestamps are shown in UTC.",
                "SearchKeys.cs: EventTime and AtStart; Core/DemoData.cs: Scenarios")
        ];
    }
}
