using Showcase.Core;
using Temporalio.Activities;
using Temporalio.Common;
using Temporalio.Exceptions;
using Temporalio.Workflows;

namespace Showcase;

public record JourneyEvent(DateTime At, string Stage, string Explanation);
public record PaymentSnapshot(string RefId, string Stage, long Attempts, bool RequiresReview, JourneyEvent[] Journey);

[Workflow]
public class PaymentWorkflow
{
    private PaymentCase? payment;
    private string stage = "received";
    private string? decision;
    private bool released;
    private bool descriptionCleared;
    private long attempt;
    private readonly List<JourneyEvent> journey = [];

    [WorkflowRun]
    public async Task<string> RunAsync(PaymentCase input)
    {
        payment = input;
        SetStage("received", "The workflow has started. All eight attributes were supplied by the client.");
        if (input.BusinessReject)
        {
            SetStage("rejected", "Synthetic business policy rejected the request. The workflow completes normally.");
            return "Business rejected; no money moved.";
        }

        if (input.RequiresReview)
        {
            SetStage("awaiting-review", "Waiting durably for a human decision.");
            await Workflow.WaitConditionAsync(() => decision != null);
            Workflow.UpsertTypedSearchAttributes(SearchKeys.RequiresReview.ValueSet(false));
            if (decision == "reject")
            {
                SetStage("rejected", "The reviewer declined the synthetic payment.");
                return "Reviewer rejected; no money moved.";
            }
            SetStage("approved", "The review flag is now false; the Visibility index will catch up asynchronously.");
        }

        if (input.HoldForRelease)
        {
            SetStage("awaiting-release", "Waiting for the presenter to release this synthetic payment.");
            await Workflow.WaitConditionAsync(() => released);
        }

        // Explicit business attempts are intentional for this teaching example.
        // Activity automatic retries are disabled so the two concepts are not conflated.
        for (attempt = 1; attempt <= 3; attempt++)
        {
            Workflow.UpsertTypedSearchAttributes(SearchKeys.Attempt.ValueSet(attempt));
            SetStage("processing", $"Explicit business processing attempt {attempt}.");
            try
            {
                await Workflow.ExecuteActivityAsync(
                    () => PaymentActivities.SimulateSettlementAsync(input, attempt),
                    new ActivityOptions
                    {
                        StartToCloseTimeout = TimeSpan.FromSeconds(15),
                        RetryPolicy = new RetryPolicy { MaximumAttempts = 1 }
                    });
                SetStage("settled", "Simulation completed. No external payment system was contacted.");
                return "Simulated settlement successful; no money moved.";
            }
            catch (ActivityFailureException)
            {
                if (attempt == 3)
                {
                    SetStage("technical-failure", "All three explicit attempts failed; the workflow will fail.");
                    throw new ApplicationFailureException("Synthetic settlement unavailable after three attempts.",
                        errorType: "DemoSettlementUnavailable", nonRetryable: true);
                }
                SetStage("retry-wait", "A durable three-second timer separates explicit processing attempts.");
                await Workflow.DelayAsync(TimeSpan.FromSeconds(3));
            }
        }
        throw new ApplicationFailureException("Unexpected demo state.", nonRetryable: true);
    }

    [WorkflowSignal]
    public Task DecideAsync(string value)
    {
        if (value is "approve" or "reject") decision ??= value;
        return Task.CompletedTask;
    }

    [WorkflowSignal]
    public Task ReleaseAsync()
    {
        released = true;
        return Task.CompletedTask;
    }

    [WorkflowSignal]
    public Task ClearDescriptionAsync()
    {
        descriptionCleared = true;
        Workflow.UpsertTypedSearchAttributes(SearchKeys.Description.ValueUnset());
        journey.Add(new(Workflow.UtcNow, stage, "ProcessingDescription was unset; workflow input and history remain."));
        return Task.CompletedTask;
    }

    [WorkflowQuery]
    public PaymentSnapshot GetSnapshot() => new(payment?.RefId ?? "starting", stage, attempt,
        payment?.RequiresReview == true && decision == null, journey.ToArray());

    private void SetStage(string value, string explanation)
    {
        stage = value;
        journey.Add(new(Workflow.UtcNow, stage, explanation));
        Workflow.UpsertTypedSearchAttributes(
            SearchKeys.Tags.ValueSet(payment!.Tags.Append(stage).ToArray()));
        if (!descriptionCleared)
            Workflow.UpsertTypedSearchAttributes(SearchKeys.Description.ValueSet($"Payment {stage} ref {payment.RefId}"));
    }
}

public static class PaymentActivities
{
    [Activity]
    public static async Task SimulateSettlementAsync(PaymentCase payment, long businessAttempt)
    {
        await Task.Delay(700, ActivityExecutionContext.Current.CancellationToken);
        if (businessAttempt <= payment.FailuresBeforeSuccess)
            throw new ApplicationFailureException("Intentional synthetic provider failure.", errorType: "DemoProviderIssue");
        // Real external payment calls would belong in activities, with idempotency controls.
    }
}
