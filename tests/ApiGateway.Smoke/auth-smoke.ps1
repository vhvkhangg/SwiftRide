param([string]$BaseUrl = "http://localhost:5100")
$ErrorActionPreference = "Stop"

function Send-Json([string]$Path, $Body, [int]$ExpectedStatus) {
    $response = Invoke-WebRequest -Uri "$BaseUrl$Path" -Method Post -ContentType "application/json" -Body ($Body | ConvertTo-Json) -SkipHttpErrorCheck -TimeoutSec 15
    if ([int]$response.StatusCode -ne $ExpectedStatus) {
        throw "$Path expected $ExpectedStatus but received $($response.StatusCode): $($response.Content)"
    }
    if ($ExpectedStatus -eq 200 -and $Path -in @('/auth/login','/auth/refresh-token') -and
        "$($response.Headers['Cache-Control'])" -notmatch 'no-store') { throw "Token response must disable caching." }
    if ($response.Content) { return $response.Content | ConvertFrom-Json }
}
$credentials = @{ email = "smoke-$([Guid]::NewGuid().ToString('N'))@example.com"; password = "Smoke-test-password-2026!"; role = 'Admin' }
$registered = Send-Json "/auth/register" $credentials 201
$null = Send-Json "/auth/register" $credentials 409
$null = Send-Json "/auth/login" @{ email = $credentials.email; password = "Wrong-password-2026!" } 401
$session = Send-Json "/auth/login" $credentials 200
$me = Invoke-RestMethod "$BaseUrl/auth/me" -Headers @{Authorization="Bearer $($session.accessToken)"; 'X-User-Roles'='Admin'} -TimeoutSec 15
if ($me.id -ne $registered.id -or $me.email -ne $credentials.email -or
    @($me.roles).Count -ne 1 -or $me.roles[0] -ne 'Rider') { throw '/auth/me identity or registration role mismatch.' }
foreach ($token in @('', 'invalid-token')) {
    $headers = @{'X-User-Id'=$registered.id; 'X-User-Roles'='Admin'}
    if ($token) { $headers.Authorization="Bearer $token" }
    $response = Invoke-WebRequest "$BaseUrl/auth/me" -Headers $headers -SkipHttpErrorCheck -TimeoutSec 15
    if ($response.StatusCode -ne 401) { throw '/auth/me accepted missing/invalid token.' }
}
$rotated = Send-Json "/auth/refresh-token" @{ refreshToken = $session.refreshToken } 200
if ($session.refreshToken -eq $rotated.refreshToken) { throw "Rotation returned the same secret." }
$null = Send-Json "/auth/refresh-token" @{ refreshToken = $session.refreshToken } 401
# Replaying the parent must revoke the child, not just reject the parent.
$null = Send-Json "/auth/refresh-token" @{ refreshToken = $rotated.refreshToken } 401
$session = Send-Json "/auth/login" $credentials 200
$null = Send-Json "/auth/revoke" @{ refreshToken = $session.refreshToken } 204
$null = Send-Json "/auth/revoke" @{ refreshToken = $session.refreshToken } 204
$null = Send-Json "/auth/refresh-token" @{ refreshToken = $session.refreshToken } 401
$response = Invoke-WebRequest "$BaseUrl/api/orders/me" -Headers @{ "X-User-Id" = [Guid]::NewGuid().ToString(); "X-User-Roles" = "Admin" } -SkipHttpErrorCheck
if ([int]$response.StatusCode -ne 401) { throw "Spoofed headers bypassed authentication." }
$session = Send-Json "/auth/login" $credentials 200
$body = @{ refreshToken = $session.refreshToken } | ConvertTo-Json
$jobs = 1..2 | ForEach-Object {
    Start-Job -ArgumentList "$BaseUrl/auth/refresh-token", $body -ScriptBlock {
        param($url, $payload)
        $response = Invoke-WebRequest -Uri $url -Method Post -ContentType "application/json" -Body $payload -SkipHttpErrorCheck -TimeoutSec 15
        @{ Status = [int]$response.StatusCode; Body = $response.Content }
    }
}
try {
    $results = @($jobs | Wait-Job | Receive-Job -ErrorAction Stop)
    if (@($results | Where-Object Status -eq 200).Count -ne 1 -or
        @($results | Where-Object Status -eq 401).Count -ne 1) {
        throw "Concurrent rotation must have exactly one 200 and one 401; got: $($results.Status -join ','). If 429, wait one minute before retrying."
    }
    $child = ($results | Where-Object Status -eq 200).Body | ConvertFrom-Json
    $null = Send-Json "/auth/refresh-token" @{ refreshToken = $child.refreshToken } 401
}
finally { $jobs | Remove-Job -Force }
Write-Host "PASS: register/role isolation, /auth/me, token cache headers, duplicate email, invalid login, rotation (including concurrent requests), replay/family revocation, logout and protected route."
# A unique test account remains in the development database.
