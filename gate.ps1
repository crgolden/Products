param([string]$Goal, [string[]]$Steps)

$ErrorActionPreference = 'Continue'
$gateCommon = Join-Path $PSScriptRoot '..\Tools\Gates\GateCommon.ps1'
if (-not (Test-Path -LiteralPath $gateCommon)) {
    Write-Host "GATE: FAILED (the Tools repository must be cloned beside this one: $gateCommon)"
    exit 1
}
. $gateCommon
$gateOutput = Join-Path ([IO.Path]::GetTempPath()) "crgolden-gates\$(Split-Path -Leaf $PSScriptRoot)"
New-Item -ItemType Directory -Force -Path $gateOutput | Out-Null
Register-GateSteps @('Local MongoDB service', 'Restore local tools', 'Begin Sonar analysis', 'Build with dotnet',
    'jb inspectcode', 'Run unit tests with coverage', 'Run integration tests with coverage', 'End Sonar analysis',
    'Fail on open Sonar issues')
Register-StepInputs @{
    'Local MongoDB service'               = @('*')
    'Restore local tools'                 = @('dotnet-tools.json')
    'Begin Sonar analysis'                = @('*')
    'Build with dotnet'                   = @('*')
    'jb inspectcode'                      = @('*')
    'Run unit tests with coverage'        = @('*')
    'Run integration tests with coverage' = @('*')
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
Initialize-GateState 'Products' $repo
Assert-RequestedSteps $Steps
Invoke-CatalogSteps

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
    $env:JAVA_HOME = "$env:SystemDrive\sonar-scanner-8.0.1.6346-windows-x64\jre"
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

if (-not (Test-StepCarried $unit)) {
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

if (-not (Test-StepCarried $integration)) {
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
