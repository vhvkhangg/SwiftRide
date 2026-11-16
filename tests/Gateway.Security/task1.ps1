#Requires -Version 7.0
param([switch]$KeepRunning)
$ErrorActionPreference = 'Stop'
$project = 'swiftride-task1-' + [guid]::NewGuid().ToString('N').Substring(0, 10)
$root = Split-Path (Split-Path $PSScriptRoot)
$baseUrl = 'http://127.0.0.1:15100'
$keycloakUrl = 'http://127.0.0.1:18180'
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
    $oidc = "$keycloakUrl/realms/swiftride/protocol/openid-connect"
    $form = @{grant_type='password'; client_id='swiftride-postman'; scope='openid'; username='driver.demo@example.com'; password='SwiftRide-demo-2026!'}
    $session = Invoke-RestMethod "$oidc/token" -Method Post -Body $form -ContentType 'application/x-www-form-urlencoded'
    # Gateway is stateless: a Keycloak token issued before a Gateway restart stays valid after it.
    RestartGateway
    $response = Invoke-WebRequest "$baseUrl/api/trips/active" -Headers @{Authorization = "Bearer $($session.access_token)"} -SkipHttpErrorCheck -TimeoutSec 15
    # 404 = authenticated, driver simply has no active trip; 401 would mean the token was rejected.
    if ($response.StatusCode -notin @(200, 404)) { throw "Token rejected after Gateway restart: $($response.StatusCode)." }
    # Readiness depends on Keycloak discovery; realm, users and sessions survive a Keycloak restart.
    Compose -Arguments @('stop', 'keycloak')
    $response = Invoke-WebRequest "$baseUrl/health/ready" -TimeoutSec 40 -SkipHttpErrorCheck
    if ($response.StatusCode -ne 503) { throw 'Readiness accepted unavailable Keycloak.' }
    Compose -Arguments @('start', 'keycloak')
    WaitReady
    $refreshed = Invoke-RestMethod "$oidc/token" -Method Post -ContentType 'application/x-www-form-urlencoded' -Body @{grant_type='refresh_token'; client_id='swiftride-postman'; refresh_token=$session.refresh_token}
    if (-not $refreshed.access_token) { throw 'Refresh token lost across Keycloak restart.' }
    $null = Invoke-WebRequest "$oidc/logout" -Method Post -ContentType 'application/x-www-form-urlencoded' -Body @{client_id='swiftride-postman'; refresh_token=$refreshed.refresh_token}
    $replay = Invoke-WebRequest "$oidc/token" -Method Post -ContentType 'application/x-www-form-urlencoded' -SkipHttpErrorCheck -Body @{grant_type='refresh_token'; client_id='swiftride-postman'; refresh_token=$refreshed.refresh_token}
    if ($replay.StatusCode -ne 400) { throw 'Refresh token still valid after Keycloak logout.' }
    & "$PSScriptRoot/gateway-security.ps1" -BaseUrl $baseUrl -KeycloakUrl $keycloakUrl -DirectTripUrl 'http://127.0.0.1:15101'
    Write-Host "PASS Task 1: $project; Keycloak tokens across Gateway/Keycloak restart, readiness, logout, Gateway business/SignalR isolation."
}
finally {
    try {
        if (-not $KeepRunning) { Compose -Arguments @('down') }
        else { Write-Host "Retained test project: $project" }
    } finally { Pop-Location }
}
