param([string]$Goal, [string]$Probe, [string]$Only)

$ErrorActionPreference = 'Continue'
$gateCommon = $env:GATE_LIBRARY
if (-not $gateCommon -or -not (Test-Path -LiteralPath $gateCommon -PathType Leaf)) {
    Write-Host 'GATE: FAILED (GATE_LIBRARY names no gate library script; README.md, section Gate, says how to plug one in or run the steps without one)'
    exit 1
}
. $gateCommon
$gateOutput = if (Get-Command Get-GateOutputDirectory -ErrorAction SilentlyContinue) {
    Get-GateOutputDirectory (Split-Path -Leaf $PSScriptRoot)
} else {
    (New-Item -ItemType Directory -Force -Path (Join-Path ([IO.Path]::GetTempPath()) "claude\crgolden-gates\$(Split-Path -Leaf $PSScriptRoot)")).FullName
}
Register-GateSteps @('Local MongoDB service', 'Restore local tools', 'Begin Sonar analysis', 'Build with dotnet',
    'jb inspectcode', 'Run unit tests with coverage', 'Run integration tests with coverage', 'End Sonar analysis',
    'Fail on open Sonar issues')
Register-StepInputs @{
    'Local MongoDB service'               = @('*')
    'Restore local tools'                 = @('dotnet-tools.json')
    'Begin Sonar analysis'                = @('*')
    'Build with dotnet'                   = @('*')
    'jb inspectcode'                      = @('*')
    'Run unit tests with coverage'        = @('*', '!Products.Tests.Integration/*')
    'Run integration tests with coverage' = @('*', '!Products.Tests.Unit/*Tests.cs')
    'End Sonar analysis'                  = @('*')
    'Fail on open Sonar issues'           = @('*')
}
$repo = $PSScriptRoot
$sarif = (Join-Path $gateOutput 'products-inspect.sarif')
$unitTrx = Join-Path $repo 'Products.Tests.Unit\bin\Release\net10.0\TestResults\unit-tests.trx'
$integrationTrx = Join-Path $repo 'Products.Tests.Integration\bin\Release\net10.0\TestResults\integration-tests.trx'
$sonarBranch = Get-SonarBranchName
$beginSonar = "Begin Sonar analysis (branch $sonarBranch)"
$build = 'Build with dotnet (Release, RestoreLockedMode)'
$endSonar = 'End Sonar analysis (quality gate waited)'
$sonarIssues = 'Fail on open Sonar issues'
$unit = 'Run unit tests with coverage (Category=Unit)'
$integration = 'Run integration tests with coverage (Category=Integration)'
$env:TZ = 'UTC'
if ($env:TZ -ne 'UTC') { Write-Host 'GATE: FAILED (TZ pin)'; exit 1 }
Set-Location $repo
$probeSteps = @('Run unit tests with coverage', 'Run integration tests with coverage')
if ($Only -and -not $Probe) { Write-Host 'PROBE: FAILED (-Only names the one test a probe runs, so it needs -Probe)'; exit 1 }
if ($Probe -and ($Probe.Trim() -cnotin $probeSteps -or [string]::IsNullOrWhiteSpace($Only) -or $Only.Contains(','))) {
    Write-Host "PROBE: FAILED (a probe runs exactly one test: -Probe '<$($probeSteps -join ' | ')>' -Only '<fully qualified test method>')"
    exit 1
}
$gateState = @{ Probe = $Probe }
if ((Get-Command Initialize-GateState).Parameters.ContainsKey('Only')) { $gateState['Only'] = $Only }
Initialize-GateState 'Products' $repo @gateState
Resolve-OwedSteps
Invoke-CatalogSteps

function Invoke-ProbeTest([string]$step, [string]$project) {
    $probeResults = Join-Path $gateOutput 'probe-results'
    $probeTrx = Join-Path $probeResults 'probe.trx'
    if (Test-Path -LiteralPath $probeTrx) { Remove-Item -LiteralPath $probeTrx -Force }
    $global:LASTEXITCODE = $null
    dotnet test --project $project --no-build --configuration Release -- --filter-method $Only --report-xunit-trx --report-xunit-trx-filename probe.trx --results-directory=$probeResults
    $probeExit = $global:LASTEXITCODE
    if (-not (Test-Path -LiteralPath $probeTrx)) { Stop-Gate $step "no TRX (exit $probeExit)" }
    [xml]$probeDoc = Get-Content -LiteralPath $probeTrx -Raw
    $counters = $probeDoc.TestRun.ResultSummary.Counters
    if ($probeExit -ne 0 -or [int]$counters.executed -ne 1 -or [int]$counters.failed -ne 0) {
        Stop-Gate $step "a probe runs one test, and '$Only' executed $($counters.executed), failed $($counters.failed) (exit $probeExit)"
    }
    Write-Row $step 'PASS' "one test, '$Only', passed (the last build's binaries; a probe builds nothing)"
}

if (-not [string]::IsNullOrWhiteSpace($env:MongoServerHost)) {
    if ([string]::IsNullOrWhiteSpace($env:MongoServerPort)) { Stop-Gate 'Local MongoDB service' 'MongoServerHost is set in the environment without MongoServerPort' }
    $mongoEndpoint = "$($env:MongoServerHost):$($env:MongoServerPort)"
    $mongoReachable = $false
    $mongoDeadline = [DateTimeOffset]::UtcNow.AddSeconds(60)
    while (-not $mongoReachable -and [DateTimeOffset]::UtcNow -lt $mongoDeadline) {
        $mongoProbe = [Net.Sockets.TcpClient]::new()
        try { $mongoReachable = $mongoProbe.ConnectAsync($env:MongoServerHost, [int]$env:MongoServerPort).Wait(5000) -and $mongoProbe.Connected } catch { $mongoReachable = $false } finally { $mongoProbe.Dispose() }
        if (-not $mongoReachable) { Start-Sleep -Seconds 5 }
    }
    if (-not $mongoReachable) { Stop-Gate 'Local MongoDB service' "nothing accepted a connection on $mongoEndpoint (MongoServerHost from the environment) within 60 seconds" }
    Write-Row 'Local MongoDB service' 'PASS' "MongoServerHost taken from the environment, and $mongoEndpoint accepts connections"
}
else {
    $mongo = Get-Service -Name 'MongoDB' -ErrorAction SilentlyContinue
    if (-not $mongo -or $mongo.Status -ne 'Running') { Stop-Gate 'Local MongoDB service' "not running ($($mongo.Status)); start it, or set MongoServerHost and MongoServerPort to name another test server" }
    Write-Row 'Local MongoDB service' 'PASS' 'Running'
}

$global:LASTEXITCODE = $null
dotnet tool restore
$null = Test-Exit 'Restore local tools (dotnet tool restore)'

$sonarCarried = Test-StepCarried $sonarIssues
if ($sonarCarried) {
    $null = Test-StepCarried $beginSonar
    $null = Test-StepCarried $build
    $null = Test-StepCarried $endSonar
}
else {
    $sonarStartedAt = [DateTimeOffset]::UtcNow
    $env:JAVA_HOME = Get-SonarScannerJavaHome $beginSonar
    $global:LASTEXITCODE = $null
    dotnet-sonarscanner begin /k:"crgolden_Products" /o:"crgolden" /d:sonar.host.url="https://sonarcloud.io" /d:sonar.cs.opencover.reportsPaths="coverage.opencover.xml,coverage-integration.opencover.xml" /d:sonar.exclusions="**/bin/**,**/obj/**" /d:sonar.coverage.exclusions="**/Program.cs,**/gate.ps1" /d:sonar.qualitygate.wait=true /d:sonar.scanner.skipJreProvisioning=true /d:sonar.branch.name="$sonarBranch"
    $null = Test-Exit $beginSonar

    $global:LASTEXITCODE = $null
    dotnet build --no-incremental --configuration Release /p:RestoreLockedMode=true -warnaserror
    $null = Test-Exit $build
}

if (-not (Test-StepCarried 'jb inspectcode')) {
    if (Test-Path $sarif) { Remove-Item $sarif -Force }
    dotnet jb inspectcode "$repo\Products.slnx" --no-build -e=WARNING --caches-home="$(New-InspectCodeCaches $gateOutput)" --output="$sarif"
    Test-Sarif $sarif
}

if ($Only -and -not (Test-StepCarried $unit)) { Invoke-ProbeTest $unit 'Products.Tests.Unit' }
elseif (-not (Test-StepCarried $unit)) {
    if (Test-Path $unitTrx) { Remove-Item $unitTrx -Force }
    $global:LASTEXITCODE = $null
    dotnet coverlet Products.Tests.Unit\bin\Release\net10.0 `
        --target "dotnet" `
        --targetargs "test --project Products.Tests.Unit --no-build --configuration Release -- --filter-trait Category=Unit --stop-on-fail on --report-xunit-trx --report-xunit-trx-filename unit-tests.trx --results-directory=Products.Tests.Unit/bin/Release/net10.0/TestResults" `
        --format opencover --output "coverage.opencover.xml" `
        --skipautoprops --exclude-by-attribute GeneratedCodeAttribute --exclude-by-file "**/obj/**" `
        --exclude-by-file "**/Program.cs" --does-not-return-attribute DoesNotReturnAttribute --include "[Products]*"
    Test-Trx $unit $unitTrx $global:LASTEXITCODE -floor 1
}

if ($Only -and -not (Test-StepCarried $integration)) {
    $env:ASPNETCORE_ENVIRONMENT = 'Development'
    $env:MongoDatabaseName ??= 'crgoldenTest'
    Invoke-ProbeTest $integration 'Products.Tests.Integration'
}
elseif (-not (Test-StepCarried $integration)) {
    if (Test-Path $integrationTrx) { Remove-Item $integrationTrx -Force }
    $env:ASPNETCORE_ENVIRONMENT = 'Development'
    $env:MongoDatabaseName ??= 'crgoldenTest'
    $global:LASTEXITCODE = $null
    dotnet coverlet Products.Tests.Integration\bin\Release\net10.0 `
        --target "dotnet" `
        --targetargs "test --project Products.Tests.Integration --no-build --configuration Release -- --filter-trait Category=Integration --stop-on-fail on --report-xunit-trx --report-xunit-trx-filename integration-tests.trx --results-directory=Products.Tests.Integration/bin/Release/net10.0/TestResults" `
        --format opencover --output "coverage-integration.opencover.xml" `
        --skipautoprops --exclude-by-attribute GeneratedCodeAttribute --exclude-by-file "**/obj/**" `
        --exclude-by-file "**/Program.cs" --does-not-return-attribute DoesNotReturnAttribute --include "[Products]*"
    Test-Trx $integration $integrationTrx $global:LASTEXITCODE -floor 1
}

if (-not $sonarCarried) {
    $global:LASTEXITCODE = $null
    dotnet-sonarscanner end
    $null = Test-Exit $endSonar
    Test-SonarIssues $sonarIssues 'crgolden_Products' $sonarBranch $sonarStartedAt
}

Write-Row 'Upload test results / dotnet publish / Upload artifact / deploy' 'NOT RUN' 'delivery steps, not checks'
Complete-Gate
