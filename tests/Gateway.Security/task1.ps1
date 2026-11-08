#Requires -Version 7.0
param([switch]$KeepRunning)
$ErrorActionPreference = 'Stop'
$project = 'swiftride-task1-' + [guid]::NewGuid().ToString('N').Substring(0, 10)
$root = Split-Path (Split-Path $PSScriptRoot)
$baseUrl = 'http://127.0.0.1:15100'
$compose = @('compose', '-p', $project, '--env-file', '.env.example', '-f', 'docker-compose.yml', '-f', 'tests/Gateway.Security/compose.test.yml')
function Compose([string[]]$Arguments) {
    & docker @compose @Arguments
    if ($LASTEXITCODE -ne 0) { throw "Docker command failed: $($Arguments -join ' ')" }
}
function WaitReady {
    $deadline = [DateTime]::UtcNow.AddSeconds(120)
    do {
        try {
            $response = Invoke-WebRequest "$baseUrl/health/ready" -TimeoutSec 5 -SkipHttpErrorCheck
            if ($response.StatusCode -eq 200) { return }
        } catch { }
        Start-Sleep -Seconds 1
    } while ([DateTime]::UtcNow -lt $deadline)
    throw 'Gateway readiness timeout.'
}
function RestartGateway {
    Compose -Arguments @('restart', 'api-gateway')
    WaitReady
}
Push-Location $root
try {
    Compose -Arguments @('up', '--build', '-d')
    WaitReady
    $session = Invoke-RestMethod "$baseUrl/auth/login" -Method Post -ContentType 'application/json' -Body (@{email='driver.demo@example.com'; password='SwiftRide-demo-2026!'} | ConvertTo-Json)
    RestartGateway
    $null = Invoke-RestMethod "$baseUrl/auth/refresh-token" -Method Post -ContentType 'application/json' -Body (@{refreshToken=$session.refreshToken} | ConvertTo-Json)
    $migrationCount = Compose -Arguments @('exec', '-T', 'auth-postgres', 'psql', '-U', 'swiftride', '-d', 'swiftride_auth', '-tAc', 'SELECT count(*) FROM "__EFMigrationsHistory"')
    if (($migrationCount | Out-String).Trim() -ne '1') { throw 'Auth initial migration missing or duplicated.' }
    Compose -Arguments @('stop', 'auth-postgres')
    $response = Invoke-WebRequest "$baseUrl/health/ready" -TimeoutSec 40 -SkipHttpErrorCheck
    if ($response.StatusCode -ne 503) { throw 'Readiness accepted unavailable Auth database.' }
    Compose -Arguments @('start', 'auth-postgres')
    WaitReady
    RestartGateway
    & "$PSScriptRoot/../ApiGateway.Smoke/auth-smoke.ps1" -BaseUrl $baseUrl
    RestartGateway
    for ($attempt = 1; $attempt -le 21; $attempt++) {
        $response = Invoke-WebRequest "$baseUrl/auth/login" -Method Post -ContentType 'application/json' -Body '{}' -SkipHttpErrorCheck -TimeoutSec 15
        $expected = if ($attempt -le 20) { 400 } else { 429 }
        if ($response.StatusCode -ne $expected) { throw "Auth rate limit attempt $attempt expected $expected, got $($response.StatusCode)." }
    }
    RestartGateway
    & "$PSScriptRoot/gateway-security.ps1" -BaseUrl $baseUrl -DirectTripUrl 'http://127.0.0.1:15101'
    Write-Host "PASS Task 1: $project; migrations/restart, refresh durability, database readiness, auth/rate limit, Gateway business/SignalR isolation."
}
finally {
    try {
        if (-not $KeepRunning) { Compose -Arguments @('down') }
        else { Write-Host "Retained test project: $project" }
    } finally { Pop-Location }
}
