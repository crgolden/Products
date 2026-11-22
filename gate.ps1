param([string]$Goal, [string]$Only)

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
Register-GateStep @('Local MongoDB service', 'Restore local tools', 'Begin Sonar analysis', 'Build with dotnet',
    'jb inspectcode', 'Run unit tests with coverage', 'Run integration tests with coverage', 'End Sonar analysis',
    'Fail on open Sonar issues')
Register-StepInput 'Local MongoDB service' @('*')
Register-StepInput 'Restore local tools' @('dotnet-tools.json')
Register-StepInput 'Begin Sonar analysis' @('*')
Register-StepInput 'Build with dotnet' @('*')
Register-StepInput 'jb inspectcode' @('*')
Register-StepInput 'Run unit tests with coverage' @('*', '!Products.Tests.Integration/*')
Register-StepInput 'Run integration tests with coverage' @('*', '!Products.Tests.Unit/*Tests.cs')
Register-StepInput 'End Sonar analysis' @('*')
Register-StepInput 'Fail on open Sonar issues' @('*')
$repo = $PSScriptRoot
$sarif = (Join-Path $gateOutput 'products-inspect.sarif')
$unitTrx = Join-Path $repo 'Products.Tests.Unit\bin\Release\net11.0\TestResults\unit-tests.trx'
$integrationTrx = Join-Path $repo 'Products.Tests.Integration\bin\Release\net11.0\TestResults\integration-tests.trx'
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
$probeProjects = @('Products.Tests.Unit', 'Products.Tests.Integration')
if ($Only) {
    $probeFile, $probeTest = $Only -split '::', 2
    $probeFile = "$probeFile".Trim().Replace('\', '/')
    $probeTest = "$probeTest".Trim()
    $probeProject = $probeFile.Split('/')[0]
    if (-not $probeTest -or $probeTest.Contains(',') -or $probeProject -notin $probeProjects -or
            -not (Test-Path -LiteralPath (Join-Path $repo $probeFile) -PathType Leaf)) {
        Write-Host "PROBE: FAILED (a probe runs exactly one test, named by the file it lives in: -Only '<a test file under $($probeProjects -join ' | ')>::<test method>')"
        exit 1
    }
    $probeClass = [IO.Path]::GetFileNameWithoutExtension($probeFile)
    if ($probeProject -eq $probeProjects[1]) {
        $env:ASPNETCORE_ENVIRONMENT = 'Development'
        $env:MongoDatabaseName ??= 'crgoldenTest'
    }
    $probeResults = Join-Path $gateOutput 'probe-results'
    $probeTrx = Join-Path $probeResults 'probe.trx'
    if (Test-Path -LiteralPath $probeTrx) { Remove-Item -LiteralPath $probeTrx -Force }
    Write-Host "PROBE: running one test, '$probeTest' of $probeClass in $probeProject; no gate record or verdict is written"
    $global:LASTEXITCODE = $null
    dotnet test --project $probeProject --configuration Release -- --filter-query "/*/*/$probeClass/$probeTest" --report-xunit-trx --report-xunit-trx-filename probe.trx --results-directory=$probeResults
    $probeExit = $global:LASTEXITCODE
    if (-not (Test-Path -LiteralPath $probeTrx)) { Write-Host "PROBE: FAILED (no TRX, exit $probeExit)"; exit 1 }
    [xml]$probeDoc = Get-Content -LiteralPath $probeTrx -Raw
    $counters = $probeDoc.TestRun.ResultSummary.Counters
    if ($probeExit -ne 0 -or [int]$counters.executed -ne 1 -or [int]$counters.failed -ne 0) {
        Write-Host "PROBE: FAILED (a probe runs one test, and '$Only' executed $($counters.executed), failed $($counters.failed), exit $probeExit)"
        exit 1
    }
    Write-Host "PROBE: PASSED for the one test named, against the last build's binaries; no verdict was saved"
    exit 0
}
exit (Invoke-Gate {
Initialize-GateState 'Products' $repo
Get-OwedStep | Register-OwedStep
Invoke-GateLibraryStep

$mongoStep = 'Local MongoDB service'
Invoke-GateStep -Name $mongoStep -Always -Body {
    if (-not [string]::IsNullOrWhiteSpace($env:MongoServerHost)) {
        if ([string]::IsNullOrWhiteSpace($env:MongoServerPort)) { Stop-Gate $mongoStep 'MongoServerHost is set in the environment without MongoServerPort' }
        $mongoEndpoint = "$($env:MongoServerHost):$($env:MongoServerPort)"
        $mongoReachable = $false
        $mongoDeadline = [DateTimeOffset]::UtcNow.AddSeconds(60)
        while (-not $mongoReachable -and [DateTimeOffset]::UtcNow -lt $mongoDeadline) {
            $mongoProbe = [Net.Sockets.TcpClient]::new()
            try { $mongoReachable = $mongoProbe.ConnectAsync($env:MongoServerHost, [int]$env:MongoServerPort).Wait(5000) -and $mongoProbe.Connected } catch { $mongoReachable = $false } finally { $mongoProbe.Dispose() }
            if (-not $mongoReachable) { Start-Sleep -Seconds 5 }
        }
        if (-not $mongoReachable) { Stop-Gate $mongoStep "nothing accepted a connection on $mongoEndpoint (MongoServerHost from the environment) within 60 seconds" }
        Write-Row $mongoStep 'PASS' "MongoServerHost taken from the environment, and $mongoEndpoint accepts connections"
    }
    else {
        $mongo = Get-Service -Name 'MongoDB' -ErrorAction SilentlyContinue
        if (-not $mongo -or $mongo.Status -ne 'Running') { Stop-Gate $mongoStep "not running ($($mongo.Status)); start it, or set MongoServerHost and MongoServerPort to name another test server" }
        Write-Row $mongoStep 'PASS' 'Running'
    }
}

$restore = 'Restore local tools (dotnet tool restore)'
Invoke-GateStep -Name $restore -Body {
    $global:LASTEXITCODE = $null
    dotnet tool restore
    Assert-StepExit $restore
}

$sonarCarried = Test-StepCarried $sonarIssues
if ($sonarCarried) {
    $null = Test-StepCarried $beginSonar
    $null = Test-StepCarried $build
    $null = Test-StepCarried $endSonar
}
else {
    $sonarStartedAt = [DateTimeOffset]::UtcNow
    Invoke-GateStep -Name $beginSonar -Always -Body {
        $env:JAVA_HOME = Get-SonarScannerJavaHome $beginSonar
        $global:LASTEXITCODE = $null
        dotnet-sonarscanner begin /k:"crgolden_Products" /o:"crgolden" /d:sonar.host.url="https://sonarcloud.io" /d:sonar.cs.opencover.reportsPaths="coverage.opencover.xml,coverage-integration.opencover.xml" /d:sonar.exclusions="**/bin/**,**/obj/**" /d:sonar.coverage.exclusions="**/Program.cs,**/gate.ps1" /d:sonar.qualitygate.wait=true /d:sonar.scanner.skipJreProvisioning=true /d:sonar.scm.use.blame.algorithm=GIT_NATIVE_BLAME /d:sonar.branch.name="$sonarBranch"
        Assert-StepExit $beginSonar
    }

    Invoke-GateStep -Name $build -Always -Body {
        $global:LASTEXITCODE = $null
        dotnet build --no-incremental --configuration Release /p:RestoreLockedMode=true -warnaserror
        Assert-StepExit $build
    }
}

Invoke-GateStep -Name 'jb inspectcode' -Needs @($build, $restore) -Body {
    if (Test-Path $sarif) { Remove-Item $sarif -Force }
    dotnet jb inspectcode "$repo\Products.slnx" --no-build -e=WARNING --caches-home="$(New-InspectCodeCache $gateOutput)" --output="$sarif"
    Test-Sarif $sarif
}

Invoke-GateStep -Name $unit -Needs @($build, $restore) -Body {
    if (Test-Path $unitTrx) { Remove-Item $unitTrx -Force }
    $global:LASTEXITCODE = $null
    dotnet coverlet Products.Tests.Unit\bin\Release\net11.0 `
        --target "dotnet" `
        --targetargs "test --project Products.Tests.Unit --no-build --configuration Release -- --filter-trait Category=Unit --report-xunit-trx --report-xunit-trx-filename unit-tests.trx --results-directory=Products.Tests.Unit/bin/Release/net11.0/TestResults" `
        --format opencover --output "coverage.opencover.xml" `
        --skipautoprops --exclude-by-attribute GeneratedCodeAttribute --exclude-by-file "**/obj/**" `
        --exclude-by-file "**/Program.cs" --does-not-return-attribute DoesNotReturnAttribute --include "[Products]*"
    Test-Trx $unit $unitTrx $global:LASTEXITCODE -floor 1
}

Invoke-GateStep -Name $integration -Needs @($build, $restore, $mongoStep) -Body {
    if (Test-Path $integrationTrx) { Remove-Item $integrationTrx -Force }
    $env:ASPNETCORE_ENVIRONMENT = 'Development'
    $env:MongoDatabaseName ??= 'crgoldenTest'
    $global:LASTEXITCODE = $null
    dotnet coverlet Products.Tests.Integration\bin\Release\net11.0 `
        --target "dotnet" `
        --targetargs "test --project Products.Tests.Integration --no-build --configuration Release -- --filter-trait Category=Integration --report-xunit-trx --report-xunit-trx-filename integration-tests.trx --results-directory=Products.Tests.Integration/bin/Release/net11.0/TestResults" `
        --format opencover --output "coverage-integration.opencover.xml" `
        --skipautoprops --exclude-by-attribute GeneratedCodeAttribute --exclude-by-file "**/obj/**" `
        --exclude-by-file "**/Program.cs" --does-not-return-attribute DoesNotReturnAttribute --include "[Products]*"
    Test-Trx $integration $integrationTrx $global:LASTEXITCODE -floor 1
}

if (-not $sonarCarried) {
    Invoke-GateStep -Name $endSonar -Needs @($beginSonar, $build, $unit, $integration) -Always -Body {
        $global:LASTEXITCODE = $null
        dotnet-sonarscanner end
        Assert-StepExit $endSonar
    }
    Invoke-GateStep -Name $sonarIssues -Needs @($endSonar) -Always -Body {
        Assert-NoOpenSonarIssue $sonarIssues 'crgolden_Products' $sonarBranch $sonarStartedAt
    }
}

Write-Row 'Upload test results / dotnet publish / Upload artifact / deploy' 'NOT RUN' 'delivery steps, not checks'
Complete-Gate
})
