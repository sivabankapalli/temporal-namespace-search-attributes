# Temporal Search Attributes Lab: Cloud UI and .NET examples

Choose one of seven Search Attribute types to see its purpose, registered name/type,
.NET key declaration, value assignment, example filter and actual returned values.
There are eight examples: GUID correlation and exact reference both use Keyword.

The app uses real Temporal SDK List and Count calls. Illustrative values and fresh
baseline counts are clearly separate from the returned workflows. The sample's
synthetic processing supplies metadata; the walkthrough focuses on Search Attributes.

## What is included

| File / folder | Purpose |
| --- | --- |
| `start-demo.ps1` | Hidden API key prompt, restore, and app launch |
| `src/Showcase` | .NET app, worker, workflow, activity, and dashboard |
| `src/Showcase/SearchKeys.cs` | Eight strongly typed search attribute keys |
| `src/Showcase/Core/DemoData.cs` | Ten test executions and baseline data |
| `src/Showcase/Core/TypeExamples.cs` | Eight type examples with SDK excerpts, live filters and fresh-baseline reference sets |
| `docs/UI-WALKTHROUGH.md` | Type-by-type UI recording order and expected results |
| `docs/VIDEO-SCRIPT.md` | Narration, screen actions, expected outcomes, and recording timing |
| `docs/SCENARIOS.md` | Data dictionary and initial result matrix |
| `docs/TECHNICAL-GUIDE.md` | SDK flow, design decisions, operational limitations, and troubleshooting |
| `docs/ORGANISATION-ADOPTION.md` | Schema decisions, customer SDK integration, and review checklist |
| `docs/SOURCES.md` | Official documentation and SDK references |
| `verify-live.ps1` | Compare real Visibility results with the initial dataset |
| `tests/CoreChecks` | Dependency-free checks for the demo data and query catalog |
| `VALIDATION.md` | What was checked and what needs a connected rehearsal |

## Before you run

1. Install the **.NET 10 SDK**. The sample pins **Temporalio 1.22.0**.
2. Use the dedicated demo namespace you provisioned, under project
   `3010f5ebea7f44b89360a2151c926d35`. The application connects to the namespace, not
   to the project ID. The full namespace ID includes the account suffix.
3. Confirm the eight definitions below already exist in that namespace. The app
   can inspect their schema; it does not create definitions or change Terraform state.
4. Copy the **API-key client gRPC endpoint** from that namespace's **Connect** panel.
   Use the exact `host:port`; do not infer an Azure hostname or use the Cloud Ops API endpoint.
5. Use a runtime API key belonging to a user/service account with namespace **Write**
   access (or equivalent permissions to start workflows, poll/complete worker tasks,
   signal/query, and read Visibility). Terraform's namespace administration key is a
   separate concern; the runtime does not need namespace creation permissions.
6. Ensure NuGet access and network access to the namespace endpoint on its gRPC port.
   Private endpoint namespaces require appropriate VPN/network access and DNS.

| Namespace attribute | Temporal type |
| --- | --- |
| CorrelationID | Keyword |
| RefID | Keyword |
| ProcessingTags | KeywordList |
| ProcessingDescription | Text |
| RequiresReview | Bool |
| ProcessingAttempt | Int |
| ProcessingScore | Double |
| BusinessEventTime | Datetime |

Names, capitalization, and types must match exactly. Use `keyword_list` in the
Terraform provider and `CreateKeywordList` in the .NET SDK.

## Launch on Windows

Extract the ZIP. Open PowerShell in the `temporal-search-showcase` folder:

```powershell
.\start-demo.ps1 -DemoRun rehearsal-01
```

Enter the endpoint, full namespace ID, and API key at the prompts. The API key is
hidden, held in the process environment, and restored/removed when the launcher exits.
Do not enter it in the browser or commit it to a config file.

If PowerShell reports that local scripts are disabled, and your device policy permits:

```powershell
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass
.\start-demo.ps1 -DemoRun rehearsal-01
```

This changes only that PowerShell session. Organisation Group Policy takes precedence.
`config.example.ps1.txt` contains manual commands if you cannot run the launcher.

Open **http://127.0.0.1:5080**. The app also starts a worker for the selected run.

1. Check the eight definitions in Temporal Cloud UI. **Inspect schema (optional)**
   reads actual `namespace.spec.searchAttributes` through Cloud Ops `GetNamespace`
   over HTTPS, using the same API key. Your laptop must reach `saas-api.tmprl.cloud:443`.
   HTTP 401/403 reports **not verified**; matching definitions report **verified**;
   readable but missing or different types report **Namespace definitions differ**.
   This does not block starting cases. A successful workflow read alone does not prove
   write permission; Temporal validates permissions and attribute names/types on each start.
2. Click **Start 10 test executions** and confirm the namespace shown.
3. Allow activity processing to settle (usually around 10-20 seconds for this tiny
   dataset), then run **Check fresh-run baseline**. Visibility indexing delay is not
   guaranteed. If a result is still catching up, wait and retry; there is no fixed SLA here.
4. Select a type at the top. The main panel explains the key/value assignment and
   runs its preset query against Temporal. The value column shows the selected
   attribute from each returned execution, including Text, lists and timestamps.
5. After starting the cases, click **Recording view** for a compact video layout.
   Copy the exact scoped query to repeat the same request in Cloud UI.
6. Follow `docs/VIDEO-SCRIPT.md` and `docs/UI-WALKTHROUGH.md`. Demonstrate the fresh
   baseline before changing or unsetting values through Inspect.

The seed button is idempotent for the same run while the IDs remain known to Temporal:
existing executions are skipped, including closed ones, using RejectDuplicate. It
does not reset prior approvals. Workflow ID reuse protection is subject to server
retention; this is not a permanent external idempotency store.

## Record a fresh take

Fresh datasets now use GUID correlation IDs stored as Keyword strings, formatted
with `Guid.ToString("D")`. PAY-1001, PAY-1002 and PAY-1003 share
`84f8c6a1-4db8-4d5b-9c11-2a6e9f0d7b32`. See `docs/GUID-CORRELATION.md`
for the .NET example. No Terraform change is needed. To demonstrate the new values,
use a fresh run such as `guid-demo-01`; existing workflows keep their original IDs.

Stop with Ctrl+C and start with a new run ID:

```powershell
.\start-demo.ps1 -DemoRun recording-01
```

Each run has its own workflow ID prefix and task queue. The same run resumes using
its persisted manifest and task queue after a worker restart. A fresh run creates
another ten executions; it does not delete the previous take. Only run one recording
at a time unless you intentionally want multiple workers.

The manifest under `src/Showcase/data/<run>.json` stores synthetic inputs and the
frozen business-time cutoff. Keep it for repeatable results within that run. It
contains no credentials. The process can only read detail/send actions to workflow
IDs beginning with its run prefix, but filters/prefixes are not security boundaries.
Actual access remains governed by your Temporal namespace permissions.

## Three commands for developers

```powershell
dotnet restore .\src\Showcase\Showcase.csproj
dotnet build .\src\Showcase\Showcase.csproj --no-restore
dotnet run --project .\tests\CoreChecks\CoreChecks.csproj
```

After the app is running and cases have settled, before mutations:

```powershell
.\verify-live.ps1
```

Live verification is intentionally independent of the offline checks. It compares
the actual returned reference sets, not the approximate Count API result.

## How the demo works

The client supplies all eight typed values at workflow start. The workflow updates
review status, stage tags, and attempt count through `UpsertTypedSearchAttributes`.
The dashboard searches through `ListWorkflowsAsync`, and also calls `CountWorkflowsAsync`.
Inspect uses Describe plus a Workflow Query; its data can be ahead of Visibility.
An approval is a Signal to the workflow, not a direct index edit.

There are no mocked search responses, fallback fake data, external UI libraries,
CDN assets, or automatically started payment cases. The dashboard binds to loopback
only. It is a demonstration application, not a production multi-user portal.

## Ending the session

Use Ctrl+C. Stopping the worker does not stop durable workflow executions. Four cases
initially wait for human actions; all started runs have a two-hour RunTimeout to avoid
indefinite demo waits. Resume the same run and worker to finish them, or manage the
dedicated demo executions in Temporal UI. Closed records remain for the namespace's
retention period. Do not delete state or shared resources as a substitute for cleanup.

Before recording: use synthetic data only, hide API keys, terminal history containing
other commands, browser account details, and unrelated workflows. Search attributes
are readable indexed metadata and are not encrypted by a custom Payload Codec.
