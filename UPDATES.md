# Search Attribute type explorer UI

The main UI now focuses on Search Attributes. Seven type buttons select eight
examples with purpose, namespace name/type, .NET declaration/assignment, illustrative
value, editable query, fresh baseline and actual returned values. Keyword includes
both GUID correlation and exact reference. Text uses the stable Payment-word filter.

Recording view hides the introduction while retaining namespace/run context.
Inspect focuses on actual metadata, with Signal actions for the Bool change and Text
unset. Extra processing scenarios are collapsed under More filters and transitions.

Replace the complete source, keep src/Showcase/data, and restart. Use a fresh run,
for example .\start-demo.ps1 -DemoRun types-ui-01, for unchanged baseline counts.
The API config field uiVersion must be type-explorer-v1. Hard-refresh with Ctrl+F5.
See docs/UI-WALKTHROUGH.md and the updated docs/VIDEO-SCRIPT.md.

The results always come from the live SDK endpoint. Empty/error responses never
fall back to illustrative rows. Rapid selection cancels an older pending search.
Thirty-six core checks pass; desktop UI checks used an isolated HTTP fixture server
outside the package. Full SDK/Cloud integration still requires the connected laptop.

# GUID correlation values

New datasets use GUID values for CorrelationID, stored as canonical lowercase
D-format Keyword strings. The namespace attribute remains Keyword. The shared GUID
for PAY-1001, PAY-1002 and PAY-1003 is 84f8c6a1-4db8-4d5b-9c11-2a6e9f0d7b32.
The other seven cases have separate GUIDs. See docs/GUID-CORRELATION.md for code.

Replace the source, keep existing data, and start a fresh run to create GUID cases:

```powershell
.\start-demo.ps1 -DemoRun guid-demo-01
```

Click Start 10 synthetic cases and wait for processing/indexing. Follow the batch
finds the three shared-GUID payments. Existing rehearsal-01 workflows are not
rewritten; their manifest still supplies their original correlation query.
The /api/config response now includes correlationIdFormat.

# Cloud namespace schema inspection correction

The previous build used OperatorService.ListSearchAttributes on the namespace's
workflow endpoint. Your API key successfully started all ten workflows while that
inspection returned PermissionDenied. OperatorService APIs are not generally
compatible with Temporal Cloud. Granting Account Owner is not the fix for that route.

This build reads actual Cloud namespace definitions using:

```text
GET https://saas-api.tmprl.cloud/cloud/namespaces/search-attributes-demo.ravxd
temporal-cloud-api-version: v0.26.0
Authorization: Bearer <key supplied by the launcher>
```

It validates the returned namespace, reads namespace.spec.searchAttributes, and
compares every expected name/type. It does not create or modify definitions.

Stop the app, replace the extracted source code, keep src/Showcase/data, and launch:

```powershell
.\start-demo.ps1 -DemoRun rehearsal-01
```

Hard-refresh the browser with Ctrl+F5. In a second terminal:

```powershell
Invoke-RestMethod 'http://127.0.0.1:5080/api/config' |
    Select-Object namespace, run, schemaCheckVersion

$schema = Invoke-RestMethod 'http://127.0.0.1:5080/api/schema'
$schema | Select-Object verified, definitionsRead, source, notice
$schema.attributes | Format-Table name, expected, actual, matches -AutoSize
```

The version must be cloud-ops-schema-v4. All eight matching definitions produce
verified=true and definitionsRead=true. A missing or different type shows a mismatch.
HTTP 401/403 returns definitionsRead=false; have the credential/role owner check
GetNamespace read access specifically. Network errors need approved HTTPS access
to saas-api.tmprl.cloud:443. Do not replace a working workflow key with a broad admin
key just to make the inspector green. Cloud UI can also show the actual definitions.

Thirty-three offline checks passed, including GUID formatting/grouping, compiling
the new HTTP reader and testing
synthetic HTTP responses. C# and JavaScript syntax checks passed. The full SDK app
cannot be built here because NuGet is unreachable; a connected rehearsal is still
needed to confirm the Cloud request. See VALIDATION.md for the detailed limits.

No reseeding is needed to inspect definitions. Existing workflow IDs are skipped
if you deliberately click Start 10 synthetic cases again for the same run.
