[CmdletBinding()]
param(
    [string]$Address,
    [string]$Namespace,
    [string]$DemoRun = 'team-demo-01'
)
$ErrorActionPreference = 'Stop'
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { throw 'Install the .NET 10 SDK first: https://dotnet.microsoft.com/download/dotnet/10.0' }
if (-not $Address) { $Address = Read-Host 'Paste the API-key gRPC endpoint from Temporal Cloud Connect (host:7233, no https://)' }
if (-not $Namespace) { $Namespace = Read-Host 'Enter the full namespace ID (name.account-id)' }
if ($Address -match '://' -or $Address -notmatch ':\d+$') { throw 'Use host:port without a URL scheme or path.' }
if ($DemoRun -cnotmatch '^[a-z0-9][a-z0-9-]{0,31}$') { throw 'DemoRun must contain 1-32 lowercase letters, digits, or hyphens.' }
$temporalNames = @('TEMPORAL_ADDRESS', 'TEMPORAL_NAMESPACE', 'TEMPORAL_API_KEY', 'DEMO_RUN')
$temporalOriginal = @{}
foreach ($temporalName in $temporalNames) { $temporalOriginal[$temporalName] = [Environment]::GetEnvironmentVariable($temporalName, 'Process') }
$temporalSecure = $null
Push-Location -LiteralPath $PSScriptRoot
try {
    $env:TEMPORAL_ADDRESS = $Address
    $env:TEMPORAL_NAMESPACE = $Namespace
    $env:DEMO_RUN = $DemoRun
    $temporalSecure = Read-Host 'Enter your namespace runtime API key (hidden)' -AsSecureString
    $env:TEMPORAL_API_KEY = ([System.Net.NetworkCredential]::new('', $temporalSecure)).Password
    $temporalSecure.Dispose()
    $temporalSecure = $null
    Write-Host "Target namespace: $Namespace"
    Write-Host "Demo run: $DemoRun. Open http://127.0.0.1:5080 after the app starts."
    Write-Host 'The app will not create workflows until you click Start 10 synthetic cases.'
    & dotnet restore '.\src\Showcase\Showcase.csproj'
    if ($LASTEXITCODE -ne 0) { throw 'Package restore failed. Check NuGet connectivity and your approved package feed.' }
    Set-Location -LiteralPath (Join-Path $PSScriptRoot 'src\Showcase')
    & dotnet run --project '.\Showcase.csproj' --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'The app stopped with an error. Review the output above.' }
}
finally {
    if ($null -ne $temporalSecure) { $temporalSecure.Dispose() }
    foreach ($temporalName in $temporalNames) { [Environment]::SetEnvironmentVariable($temporalName, $temporalOriginal[$temporalName], 'Process') }
    Pop-Location
}
