# Validation status

This package contains source code, not a prebuilt/published application.

## Checks performed while preparing the package

- Thirty-six offline core checks passed: dataset, exact/prefix distinction, correlation grouping,
  review/score/date boundaries, combined search expectations, safe run identifier,
  restart manifest serialization, catalog consistency, Cloud namespace JSON types,
  namespace mismatches, missing/malformed maps, HTTP error handling, canonical GUID
  correlation values, backward-compatible manifest queries and type example metadata.
- The new dependency-free Cloud HTTP reader compiled in the check project. Synthetic
  HTTP handlers verify the endpoint/auth/version headers, parsing, and 401/403/500
  propagation without exposing credentials or raw error bodies. These are fixtures,
  not captured live responses or proof of Cloud access.
- C# syntax parsing across application and check-project source files.
- JavaScript syntax and PowerShell script parsing.
- Project XML and ZIP contents checked.
- Browser checks passed against an isolated synthetic HTTP fixture server: eight
  examples across seven types displayed their returned values, custom false-value
  results rendered, rapid selection did not display an older response, zero results
  stayed empty, and a simulated PermissionDenied cleared the prior rows.
- The desktop layout and compact Recording view were visually reviewed. These UI
  checks used fixtures derived from the core dataset, not live Temporal Cloud data.
  No mobile layout verification is claimed.
- API usage checked against official .NET SDK API references.

These checks do not replace compiling against the real Temporal SDK or integration
testing on Temporal Cloud.

## Blocked in the preparation environment

`dotnet restore` for the app failed with NU1301 because this environment could not
resolve `api.nuget.org`. The Temporalio dependency could not be downloaded. Therefore
the complete application has **not been compiled against Temporalio 1.22.0 here**.
No Temporal credentials were supplied, no Cloud workflows were started, and no live
Visibility result or approval path has been claimed as tested. The application is
not yet cleared for recording until the connected checklist below passes.

## Required on your laptop before recording

1. `dotnet restore src/Showcase/Showcase.csproj` succeeds with your approved feed.
2. `dotnet build src/Showcase/Showcase.csproj --no-restore` succeeds.
3. Launch using the correct full namespace ID, API-key endpoint, and runtime key.
4. Confirm all eight definitions in the dedicated namespace in Cloud UI. Optional
   SDK inspection may be unavailable; an unverified result is not a schema match.
5. Seed ten cases and let the worker process activities and Visibility index them.
6. Baseline verifier passes all thirteen initial scenes. Review actual mismatches
   if any; the Text scene is intentionally sensitive to real server matching semantics.
7. Rehearse approval: human review count 3 -> 2 after handling and indexing.
8. Rehearse unset on PAY-1006: missing description search 0 -> 1.
9. Confirm business rejection is Completed and persistent technical failure is Failed.
10. Confirm UI detail/history navigation works on your Cloud account's layout.
11. Start a fresh DemoRun for the recording so mutations do not alter baseline counts.

Record the SDK version, namespace, run ID, and rehearsal date with your internal
review evidence. Do not include API keys in the recording or evidence files.
