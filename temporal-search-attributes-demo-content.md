# Recording screenplay: Search Attributes in Temporal Cloud and .NET

**Working title:** Find workflows by meaningful metadata.

**Duration:** about 23 minutes, including clicking, screen changes and short pauses.
The timestamps are pacing cues, not a requirement to stretch the narration.

**Audience:** technical and nontechnical colleagues.

**Recording objective:** explain the concept and all seven purposes before opening
the live screens, then locate namespace definitions, demonstrate
all seven types and trace the values through the .NET SDK into real search results.
The ten sample executions supply test metadata; their payment-processing logic is
outside the narration.

Read the **Speak** paragraphs. Follow **Screen and action** as stage directions.
Count statements assume the fresh baseline has been rehearsed successfully. Do not
read a target count as an observed result if the actual screen disagrees.

## Before recording: prepare without capturing credentials

1. Start the latest sample with a new run ID:

```powershell
.\start-demo.ps1 -DemoRun video-take-01
```

2. Configure your existing namespace and runtime API key through the launcher.
   Keep the key prompt out of the recording.
3. Open http://127.0.0.1:5080. Confirm the namespace is
   `search-attributes-demo.ravxd` and the run is `video-take-01`.
4. Click **Start 10 test executions**, wait for processing/indexing, then click
   **Check fresh-run baseline**. Investigate discrepancies before filming.
5. Click **Recording view**. Leave the automatic five-second refresh unchecked so
   you control when the screen changes. Select the Keyword correlation example.
6. Open separate browser tabs for the Cloud namespace definitions and the namespace's
   Workflows view. Locate the Search Attributes configuration in your account during
   rehearsal; exact tab/menu labels may differ. These UI labels have not been inspected
   on your live Cloud account here.
7. Open the SDK files in your editor and bookmark these locations:

| File in the extracted project | Location to show |
| --- | --- |
| src/Showcase/SearchKeys.cs | Eight declarations and AtStart collection builder |
| src/Showcase/DemoService.cs | SeedAsync: TypedSearchAttributes assignment |
| src/Showcase/DemoService.cs | SearchAsync: ListWorkflowsAsync and CountWorkflowsAsync |
| src/Showcase/PaymentWorkflow.cs | RunAsync: RequiresReview.ValueSet(false) |
| src/Showcase/PaymentWorkflow.cs | ClearDescriptionAsync: Description.ValueUnset() |

Keep unrelated customer information out of the frame. Use readable browser/editor
font sizes. You do not need to show Terraform apply, package restore, authentication
troubleshooting, the schema API implementation or workflow processing internals.

## Scene 1 | 00:00-00:45 | Introduce the outcome

**Screen and action:** show a simple title card: Search Attributes in Temporal Cloud
and .NET. Keep the live Lab, Cloud UI and editor off screen until the overview ends.

**Speak:**

> In this video, we will see how Search Attributes help us find Temporal workflows
> using information that matters to our teams, such as a correlation ID or a reference.
>
> We will locate the definitions in Temporal Cloud, understand the available types,
> and see how .NET code supplies the values we search. For each type, we will look
> at its purpose, a small SDK example, a filter, and the values actually returned.
>
> The executions here contain synthetic test data. Our focus is the searchable
> metadata rather than the sample's business processing.

**Cue:** move to the concept card; keep the live screens off camera for now.

## Scene 2 | 00:45-02:00 | Explain the concept

**Screen and action:** show a concept card with three lines: searchable workflow
metadata; namespace = name + type; execution = actual value. Include WorkflowId,
ExecutionStatus and StartTime as examples of built-in fields. Do not open Cloud yet.

**Speak:**

> When we know a Workflow ID, we can look up that workflow directly. But often a
> support or engineering question starts with a different piece of information.
> We may have a reference, a correlation ID, a flag or a time range.
>
> A Search Attribute is a typed, indexed metadata field attached to a workflow
> execution. It gives us another way to discover matching executions.
>
> Temporal already supplies fields such as WorkflowId, ExecutionStatus and StartTime.
> We add custom fields for requirements specific to our applications.
>
> The key distinction is that the namespace registers a field's name and type,
> while each workflow execution supplies its own value.

**On-screen takeaway:** Namespace definition = name + type. Workflow metadata = value.

## Scene 3 | 02:00-05:00 | Explain all seven types and their purposes before the demo

**Screen and action:** show this overview as a simple slide or static reference card.
Reveal or point to one row at a time. Keep the live application and code out of frame
until you finish this section. This is a presentation cue, not an extra application feature.

| Type | Choose it when the question is about... | Simple example |
| --- | --- | --- |
| Keyword | An exact identifier or category | A correlation GUID or reference ID |
| KeywordList | Membership in several labels | priority within a collection of tags |
| Text | Words in descriptive content | A word in a short processing description |
| Bool | A yes/no fact | RequiresReview is true |
| Int | A whole-number count or range | ProcessingAttempt is at least 3 |
| Double | A decimal measurement or threshold | ProcessingScore is at least 0.8 |
| Datetime | A timestamp or time window | BusinessEventTime is after a cutoff |

**Speak:**

> Before we open the demo, let us understand the types and why we would choose them.
> The type describes the value we can supply and how we can search it. We choose it
> from the question the team needs to answer.
>
> Keyword is for exact identifiers or categories. If we have a correlation ID or a
> reference and want its complete value to match, Keyword is the appropriate choice.
> Our GUID correlation ID is stored as a consistently formatted Keyword string.
>
> KeywordList is for several labels on the same execution. For example, an execution
> might have priority and international labels. We use this type when the question
> is whether a particular label is present in the collection.
>
> Text is for searching words in a short description. It behaves differently from
> Keyword even though both use strings in .NET. Text is useful for descriptive words;
> Keyword is the choice for an exact identifier.
>
> Bool represents a yes-or-no fact. A review-required flag is an example. It helps
> us find executions where that condition is true or false.
>
> Int is for whole numbers, such as an attempt count or number of items. We can
> search for a particular number or compare it with a threshold. In the .NET SDK,
> this Temporal type uses a long value.
>
> Double is for a decimal-valued measurement, such as a score. It lets us ask which
> executions are at or above a chosen threshold.
>
> Datetime is for timestamps and time comparisons. For example, we might want
> executions associated with business events after a particular time. That business
> timestamp can be different from when the workflow itself started.
>
> The practical rule is to start with the search requirement, then choose the type.
> Exact ID: Keyword. Several labels: KeywordList. Words: Text. Yes or no: Bool.
> Whole count: Int. Decimal threshold: Double. Time window: Datetime.
>
> These are the seven types we will now demonstrate. For each one, we will see the
> namespace definition, its .NET declaration and assigned value, and a filter returning
> actual matching executions. Let us start with the namespace in Temporal Cloud.

**Cue:** only now switch to the live Cloud namespace configuration. The first five
minutes have explained the concept and purposes without jumping into the demo.

## Scene 4 | 05:00-06:15 | Show namespace definitions

**Screen and action:** open the real Cloud namespace configuration for
`search-attributes-demo.ravxd`. Show its Search Attributes list. Point to CorrelationID,
RefID, then the remaining fields. Use the Cloud UI itself for this scene; the local
app's Inspect namespace definitions dialog is an optional supporting view.

**Speak:**

> These are the custom attribute definitions registered in our demo namespace.
> Notice the name and type of each field.
>
> CorrelationID and RefID both use Keyword. The other fields cover KeywordList, Text,
> Bool, Int, Double and Datetime. We therefore have eight definitions covering seven types.
>
> This is the shared contract the application uses. The SDK must refer to the same
> registered name and supply the corresponding type.
>
> Registering a definition does not create a workflow or populate a value. It makes
> that field available for executions that choose to use it.

**Cue:** point to the exact capitalization of `CorrelationID`.

## Scene 5 | 06:15-07:15 | Connect definitions to values

**Screen and action:** switch to Cloud Workflows. Apply the recording scope:

```sql
WorkflowId STARTS_WITH 'sa-showcase-video-take-01-'
```

Open the execution ending in PAY-1001 and show its actual Search Attribute values.
Point to CorrelationID, RequiresReview and ProcessingScore.

**Speak:**

> This is one execution using those definitions. CorrelationID now has a particular
> GUID string, RequiresReview has a Boolean value, and ProcessingScore has a number.
>
> We have moved from the namespace's permitted names and types to values belonging
> to this execution.
>
> A field in the workflow input does not become searchable automatically. Our SDK
> explicitly sets the Search Attribute value. Other executions can have different
> values, and some may not set a registered field at all.

**Cue:** return to the local Lab.

## Scene 6 | 07:15-08:15 | Show the common SDK pattern

**Screen and action:** open SearchKeys.cs. Highlight a factory, then the AtStart
builder. Open DemoService.SeedAsync and highlight this assignment:

```csharp
TypedSearchAttributes = SearchKeys.AtStart(payment)
```

**Speak:**

> The .NET example has two parts. First, we declare a typed key using the name
> registered in the namespace. Second, we build a collection containing actual values.
>
> That collection is passed through TypedSearchAttributes when starting a workflow.
> Declaring a SearchAttributeKey in C# does not register the definition in Cloud.
>
> We will now repeat the same pattern for each type. The Lab shows the relevant
> declaration and assignment beside the filter, so we can follow the code and result
> together.

**Cue:** return to the Lab and leave the editor ready for later upsert scenes.

## Scene 7 | 08:15-09:45 | Keyword: GUID correlation and exact reference

**Screen and action:** select **Keyword** at the top, or **Keyword / CorrelationID**
on the left. Point to the key factory and canonical GUID example. The preset runs
automatically; **Run live search** repeats it.

```csharp
SearchAttributeKey.CreateKeyword("CorrelationID")
```

```sql
CorrelationID = '84f8c6a1-4db8-4d5b-9c11-2a6e9f0d7b32'
```

**Speak:**

> Keyword is the right choice for an exact structured identifier. Here we use it
> for CorrelationID.
>
> Our application can use a Guid in .NET, but Temporal indexes its formatted string
> as a Keyword. We consistently use the lowercase format with hyphens.
>
> This fresh run returns three executions. Look at the value column: all three
> contain the same correlation ID. The shared ID connects related executions; it
> is not a uniqueness constraint or a replacement for WorkflowId.

**Screen and action:** select **Keyword / RefID** on the left. Show:

```sql
RefID = 'PAY-1001'
```

**Speak:**

> The second Keyword example searches an exact reference. This returns PAY-1001.
> PAY-10010 is a different value, so it is excluded.
>
> For our organisation, the useful agreement is who creates the correlation ID,
> how it is formatted, and how related services carry the same value forward.

**Rehearsal target:** GUID query = 3; exact reference query = 1.

## Scene 8 | 09:45-10:45 | KeywordList: membership

**Screen and action:** select **KeywordList**. Point to the collection type in the
SDK declaration and the list of tags in a returned row.

```csharp
SearchAttributeKey.CreateKeywordList("ProcessingTags")
```

```sql
ProcessingTags = 'priority'
```

**Speak:**

> KeywordList lets one execution carry several labels. In the SDK, the value is a
> collection of strings.
>
> This filter asks which executions include priority in their labels. It returns
> three in the fresh run.
>
> Notice that each returned value contains more than one label. The equality filter
> checks membership; it does not require the whole list to equal one string.
>
> This is useful for categories, capabilities or routing labels when several can
> apply to one execution.

**Rehearsal target:** 3; show priority within the actual returned collections.

## Scene 9 | 10:45-11:45 | Text: word matching

**Screen and action:** select **Text**. Point to CreateText and the current descriptions.

```csharp
SearchAttributeKey.CreateText("ProcessingDescription")
```

```sql
ProcessingDescription = 'Payment'
```

**Speak:**

> Text is useful when the requirement is to find words in descriptive metadata.
> It takes a string in .NET, just as Keyword does, but the matching behavior differs.
>
> Our current descriptions contain the word Payment, and this fresh run returns
> ten matching executions.
>
> Although the filter uses an equals sign, this is analyzed word matching, not an
> exact comparison of the entire description. That is why we use Keyword for an
> exact reference or GUID.
>
> We are searching the current indexed value. Workflow code can replace a description
> after the execution starts, so it may differ from its original input wording.

**Rehearsal target:** 10 before any description is unset.

## Scene 10 | 11:45-12:45 | Bool: a yes/no fact

**Screen and action:** select **Bool**. Point to the .NET bool declaration and true
values in the result column. Do not change a value yet.

```csharp
SearchAttributeKey.CreateBool("RequiresReview")
```

```sql
RequiresReview = true
```

**Speak:**

> Bool represents a yes-or-no fact supplied by the application. Our example asks
> which executions are marked as requiring review.
>
> This fresh run returns three, and the actual values in the table are true.
>
> We supply a Boolean value, rather than the string true. This custom flag is also
> separate from Temporal's built-in ExecutionStatus.
>
> We will change one flag later and watch the search results change. For now, we
> will keep the baseline intact while demonstrating the remaining types.

**Rehearsal target:** 3.

## Scene 11 | 12:45-13:45 | Int: counts and ranges

**Screen and action:** select **Int**. Point to CreateLong, the initial 0L assignment,
and the returned values equal to 3.

```csharp
SearchAttributeKey.CreateLong("ProcessingAttempt")
```

```sql
ProcessingAttempt >= 3
```

**Speak:**

> Int is appropriate for a whole-number count or range comparison. Temporal's Int
> uses a C# long in this SDK.
>
> The sample initially sets zero and later writes its processing-attempt count.
> Our filter selects values of at least three and returns two executions.
>
> The application defines what this counter means. In this sample it counts explicit
> processing attempts; it is not automatically populated from Temporal's activity
> retry attempt number.
>
> The useful point is that these are numeric comparisons over numeric values.

**Rehearsal target:** 2, PAY-1003 and PAY-1008.

## Scene 12 | 13:45-14:45 | Double: decimal thresholds

**Screen and action:** select **Double**. Show the declaration, 0.92 example and actual
scores in the result column.

```csharp
SearchAttributeKey.CreateDouble("ProcessingScore")
```

```sql
ProcessingScore >= 0.8
```

**Speak:**

> Double is useful for decimal-valued measurements and threshold searches. This
> example uses a synthetic score.
>
> The filter asks for scores at or above zero point eight. We get four executions,
> and we can inspect each returned score to understand why it matched.
>
> The number is supplied as a double, not a quoted string. We would choose this type
> for an appropriate measurement or score, while keeping the authoritative business
> record in its proper system of record.

**Rehearsal target:** 4, PAY-1001, PAY-1002, PAY-1005 and PAY-1009.

## Scene 13 | 14:45-16:00 | Datetime: business time

**Screen and action:** select **Datetime**. Show CreateDateTimeOffset, the actual UTC
cutoff in the filter and returned timestamps. Use the preset; do not type an arbitrary date.

```csharp
SearchAttributeKey.CreateDateTimeOffset("BusinessEventTime")
```

**Speak:**

> Datetime is the type to use for time comparisons. This field represents a business
> event timestamp and maps to DateTimeOffset in the .NET example.
>
> The filter compares that value with the UTC cutoff shown here. Nine executions
> match, while one represents an older business event and falls outside the window.
>
> A workflow can start today while describing an event that happened earlier. Our
> custom BusinessEventTime therefore answers a different question from Temporal's
> built-in StartTime.
>
> For a repeatable recording, this sample freezes the cutoff when the run's data is
> created. It is not a continuously moving last-twenty-four-hours filter.

**Rehearsal target:** 9; PAY-1009 is excluded.

## Scene 14 | 16:00-17:30 | Repeat one filter in Cloud and trace the SDK call

**Screen and action:** select the Keyword correlation example again. After the query
finishes, click **Copy exact query for Cloud UI**. Paste it into the Cloud namespace's
workflow filter and run it. Show the three actual matching executions.

**Speak:**

> We can run this same filter directly in Temporal Cloud. The copied version also
> includes our recording run's WorkflowId prefix, so older rehearsals do not change
> the result set.
>
> We are now viewing the same correlation through Cloud's workflow search. This
> connects the SDK example with the namespace and execution metadata we saw earlier.

**Screen and action:** show DemoService.SearchAsync in the editor. Highlight
ListWorkflowsAsync and CountWorkflowsAsync without walking through the entire service.

**Speak:**

> Under the Lab, the client calls ListWorkflowsAsync with the filter. The returned
> executions populate the table. This is a server-side Visibility request.
>
> The Count API is a separate read, which is why its approximate value is labeled
> separately. The fresh-baseline check compares the returned references, rather than
> assuming the expected count proves the result is correct.

**Cue:** return to the Lab.

## Scene 15 | 17:30-19:00 | Combine requirements before changing values

**Screen and action:** select Bool. Replace its editable filter with:

```sql
RequiresReview = true
AND ProcessingTags = 'priority'
AND ProcessingScore >= 0.8
AND ExecutionStatus = 'Running'
```

Click **Run live search**. The local app still adds the recording prefix automatically.

**Speak:**

> Once each field has a clear meaning, we can combine them to answer a more specific
> question. Here we ask for a true flag, a priority label, a score threshold and a
> running execution.
>
> Three executions match in our fresh run. We have combined Bool, KeywordList and
> Double with a built-in Temporal field.
>
> Notice that this is now an edited filter. The UI pauses its preset baseline
> comparison instead of presenting the original expected count as a result for our
> custom query.
>
> We choose fields and types around the questions our teams actually need to ask.

**Rehearsal target:** 3. This query deliberately has no business-time cutoff. Adding
the Datetime preset's cutoff would exclude PAY-1009 and produce 2 before mutations.

## Scene 16 | 19:00-20:30 | Update a Bool value and watch the results change

**Screen and action:** select Bool again to restore `RequiresReview = true`. Show the
three matches. In the editor, highlight the existing workflow statement:

```csharp
Workflow.UpsertTypedSearchAttributes(
    SearchKeys.RequiresReview.ValueSet(false));
```

**Speak:**

> Search Attribute values can change while a workflow runs. Here the workflow
> upserts RequiresReview to false. The namespace definition remains Bool; we are
> changing one execution's value.

**Screen and action:** return to the Lab. Inspect PAY-1001. Click
**Set RequiresReview to false**, confirm the test action, then close the dialog and
run the Bool search again after the worker handles the signal and indexing catches up.

**Speak:**

> This action sends a Signal to the workflow. The workflow handles it and executes
> the upsert; the button does not directly edit the search index.
>
> The true-value search now returns two executions instead of three. The changed
> execution no longer satisfies that condition.
>
> The fresh-baseline comparison now differs because we deliberately changed a value.
> That is an expected transition. Visibility updates asynchronously, so a successful
> signal response does not guarantee the search index has already caught up.

**Cue:** if processing/indexing is still in progress, pause and refresh. Show the
observed two-row result rather than narrating it before it appears.

## Scene 17 | 20:30-21:30 | Unset a value without removing its definition

**Screen and action:** select Text, replace the filter and run:

```sql
ProcessingDescription IS NULL
```

Show the initial zero rows. Open **More filters and transitions**, select
**The operational picture**, inspect PAY-1006 and choose **Unset ProcessingDescription**.
Close the dialog. Select Text again, restore the IS NULL filter, and run it after processing/indexing.

**Speak:**

> An absent value is different from a value set to false, zero or an empty string.
> Initially, no execution in this run is missing its description.
>
> We will now unset the description on one test execution. The workflow uses ValueUnset
> to remove that execution's indexed value.
>
> The missing-description search now returns one execution. If we inspect it, the
> description value is absent. The Text definition still exists in the namespace.
>
> Unsetting indexed metadata does not erase the original workflow input or past history.

**Optional editor insert:** highlight only this statement in ClearDescriptionAsync:

```csharp
Workflow.UpsertTypedSearchAttributes(SearchKeys.Description.ValueUnset());
```

**Rehearsal target:** IS NULL changes from 0 to 1. The regular Text word search now
has 9 matches, so do not rerun baseline checks and describe that deliberate difference as a failure.

## Scene 18 | 21:30-22:15 | Apply the pattern to the organisation

**Screen and action:** show the Cloud definition list again, or the Lab's seven type
choices with CorrelationID selected.

**Speak:**

> For our own SDK requirements, we should start with the question we need to answer.
> For exact CorrelationID and RefID lookups, Keyword is the natural fit.
>
> We need to agree what each ID represents, who creates it, the GUID format and how
> it is propagated. We also need consistent attribute names and ownership of the
> namespace definitions.
>
> Search Attributes are readable for indexing and are not encrypted through a custom
> Payload Codec. We should use approved metadata and avoid secrets or sensitive content.

**Cue:** keep this practical. No RBAC setup or infrastructure deployment walkthrough is needed.

## Scene 19 | 22:15-23:00 | Close with the decision rule

**Screen and action:** leave the seven type buttons visible. Point across them slowly.

**Speak:**

> Use Keyword for exact identifiers, KeywordList for membership, Text for words,
> Bool for yes-or-no facts, Int for whole counts, Double for decimal thresholds,
> and Datetime for time comparisons.
>
> The namespace defines the field. The SDK supplies a typed value. Workflow code can
> update or unset that value, and Visibility lets us find matching executions.
>
> We have followed that pattern from Cloud definitions, through .NET code, to real
> search results. We can now apply the same approach to our CorrelationID and RefID
> requirements.

**End frame:** hold the screen for three seconds, then stop recording.

## Presenter quick-reference card

| Demonstration | Initial result | What to point to |
| --- | --- | --- |
| CorrelationID Keyword | 3 | Same GUID on three executions |
| RefID Keyword | 1 | PAY-1001 matches; PAY-10010 does not |
| KeywordList | 3 | Actual list contains priority |
| Text | 10 | Current description contains Payment |
| Bool | 3 | Actual Boolean true |
| Int | 2 | Actual attempt value 3 |
| Double | 4 | Actual score at/above 0.8 |
| Datetime | 9 | Business timestamps after the frozen cutoff |
| Combined filter in Scene 15 | 3 | No Datetime cutoff in that query |
| Bool update | 3 to 2 | True-value query after one upsert |
| Text unset | 0 to 1 | IS NULL after one ValueUnset |

## Short recovery lines for recording

**Index has not caught up:**

> The request was accepted. I will refresh after the workflow handles it and the
> Visibility index catches up.

**Unexpected count:**

> I will check the selected run, filter and current metadata before interpreting this result.

Stop or edit the take if the explanation no longer matches the screen. A real
mismatch should not be narrated as the expected result.

**Optional schema inspector unavailable:**

> This optional API inspection is unavailable for this connection. We can inspect
> the actual definitions in the Cloud namespace configuration.

Prepare the Cloud view beforehand; do not spend the main recording troubleshooting
an inspection RPC or changing permissions.

## Technical references for rehearsal

The screenplay uses the current sample's UI labels and source code. Count statements
are fixture-derived targets for a fresh run, not a claim of live verification here.

- Search Attributes: https://docs.temporal.io/search-attribute
- Filter semantics: https://docs.temporal.io/list-filter
- .NET set, update, unset and list: https://docs.temporal.io/develop/dotnet/platform/observability
- Typed key factories: https://dotnet.temporal.io/api/Temporalio.Common.SearchAttributeKey.html
- ValueSet / ValueUnset: https://dotnet.temporal.io/api/Temporalio.Common.SearchAttributeKey-1.html
- Visibility consistency: https://docs.temporal.io/visibility
