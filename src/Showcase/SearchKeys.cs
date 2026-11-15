using Temporalio.Common;
using Showcase.Core;

namespace Showcase;

// Keep these names and types identical to the namespace's Terraform definitions.
public static class SearchKeys
{
    // GUID values use canonical Guid.ToString("D"); Temporal Keyword keys accept strings.
    public static readonly SearchAttributeKey<string> CorrelationId = SearchAttributeKey.CreateKeyword("CorrelationID");
    public static readonly SearchAttributeKey<string> RefId = SearchAttributeKey.CreateKeyword("RefID");
    public static readonly SearchAttributeKey<IReadOnlyCollection<string>> Tags = SearchAttributeKey.CreateKeywordList("ProcessingTags");
    public static readonly SearchAttributeKey<string> Description = SearchAttributeKey.CreateText("ProcessingDescription");
    public static readonly SearchAttributeKey<bool> RequiresReview = SearchAttributeKey.CreateBool("RequiresReview");
    public static readonly SearchAttributeKey<long> Attempt = SearchAttributeKey.CreateLong("ProcessingAttempt");
    public static readonly SearchAttributeKey<double> Score = SearchAttributeKey.CreateDouble("ProcessingScore");
    public static readonly SearchAttributeKey<DateTimeOffset> EventTime = SearchAttributeKey.CreateDateTimeOffset("BusinessEventTime");

    public static readonly SearchAttributeKey[] All = [CorrelationId, RefId, Tags, Description, RequiresReview, Attempt, Score, EventTime];

    public static SearchAttributeCollection AtStart(PaymentCase payment) => new SearchAttributeCollection.Builder()
        .Set(CorrelationId, payment.CorrelationId)
        .Set(RefId, payment.RefId)
        .Set(Tags, (IReadOnlyCollection<string>)payment.Tags.Append("received").ToArray())
        .Set(Description, payment.Description)
        .Set(RequiresReview, payment.RequiresReview)
        .Set(Attempt, 0L)
        .Set(Score, payment.Score)
        .Set(EventTime, payment.BusinessEventTime)
        .ToSearchAttributeCollection();
}
