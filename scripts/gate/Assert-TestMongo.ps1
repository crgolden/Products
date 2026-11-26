if ([string]::IsNullOrWhiteSpace($env:MongoServerHost)) {
    $mongo = Get-Service -Name 'MongoDB' -ErrorAction SilentlyContinue
    if (-not $mongo -or $mongo.Status -ne 'Running') {
        Write-Host "the MongoDB service is not running ($($mongo.Status)); start it, or set MongoServerHost and MongoServerPort to name another test server"
        exit 1
    }
    Write-Host 'the MongoDB service is running'
    exit 0
}
if ([string]::IsNullOrWhiteSpace($env:MongoServerPort)) {
    Write-Host 'MongoServerHost is set in the environment without MongoServerPort'
    exit 1
}
$endpoint = "$($env:MongoServerHost):$($env:MongoServerPort)"
$reachable = $false
$deadline = [DateTimeOffset]::UtcNow.AddSeconds(60)
while (-not $reachable -and [DateTimeOffset]::UtcNow -lt $deadline) {
    $client = [Net.Sockets.TcpClient]::new()
    try { $reachable = $client.ConnectAsync($env:MongoServerHost, [int]$env:MongoServerPort).Wait(5000) -and $client.Connected }
    catch { $reachable = $false }
    finally { $client.Dispose() }
    if (-not $reachable) { Start-Sleep -Seconds 5 }
}
if (-not $reachable) {
    Write-Host "nothing accepted a connection on $endpoint (MongoServerHost from the environment) within 60 seconds"
    exit 1
}
Write-Host "MongoServerHost taken from the environment, and $endpoint accepts connections"
exit 0
