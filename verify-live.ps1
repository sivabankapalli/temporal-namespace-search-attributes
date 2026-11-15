[CmdletBinding()]
param([string]$BaseUrl = 'http://127.0.0.1:5080')
$ErrorActionPreference = 'Stop'
Write-Host 'Run before approving, releasing, rejecting, or clearing any demo case.'
$result = Invoke-RestMethod -Uri "$BaseUrl/api/verify" -TimeoutSec 180
$result.results | Select-Object title, passed, @{Name='Expected'; Expression={ $_.expected -join ', ' }}, @{Name='Actual'; Expression={ $_.actual -join ', ' }} | Format-Table -Wrap
if (-not $result.allPassed) { throw 'A baseline check did not pass. Wait for workflows/indexing and retry. Read the mismatches; do not record them as a pass.' }
Write-Host 'All baseline Visibility scenarios passed against this connected namespace.'
