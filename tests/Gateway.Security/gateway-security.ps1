param(
    [string]$BaseUrl = "http://localhost:5100",
    [string]$SeedPassword = "SwiftRide-demo-2026!",
    [string]$DirectTripUrl = "",
    [ValidateRange(5, 120)][int]$WaitSeconds = 45
)
$ErrorActionPreference = "Stop"
. "$PSScriptRoot/signalr-helpers.ps1"
$checks = 0
function Call([string]$Method, [string]$Path, $Body, [string]$Token, [int[]]$Expected) {
    $parameters = @{ Uri = "$BaseUrl$Path"; Method = $Method; SkipHttpErrorCheck = $true; TimeoutSec = 15 }
    if ($null -ne $Body) { $parameters.Body = $Body | ConvertTo-Json -Depth 10; $parameters.ContentType = "application/json" }
    $parameters.Headers = @{"X-User-Roles" = "Admin"; "X-User-Id" = [guid]::NewGuid().ToString()}
    if ($Token) { $parameters.Headers.Authorization = "Bearer $Token" }
    $response = Invoke-WebRequest @parameters
    if ([int]$response.StatusCode -notin $Expected) { throw "$Method $Path expected $Expected, got $($response.StatusCode): $($response.Content)" }
    $script:checks++
    if ($response.StatusCode -eq 404) { return $null }
    if ($response.Content) { return $response.Content | ConvertFrom-Json }
}
function Login([string]$Email) {
    (Call POST "/auth/login" @{email=$Email;password=$SeedPassword} "" 200).accessToken
}
function NewTrip([string]$Token) {
    Call POST "/api/trips/" @{
        riderId=[guid]::NewGuid().ToString();pickupAddress="Gateway test pickup";dropoffAddress="Gateway test destination"
        pickupLatitude=10.77;pickupLongitude=106.69;distance=2;estimatedTime=5;rideType="standard"
    } $Token 201
}
function WaitInvitation([string]$TripId, [string]$Token) {
    $deadline = [DateTime]::UtcNow.AddSeconds($WaitSeconds)
    do {
        $invitations = @(Call GET "/api/trips/driver/invitations" $null $Token 200)
        if ($invitations.id -contains $TripId) { return }
        Start-Sleep -Milliseconds 250
    } while ([DateTime]::UtcNow -lt $deadline)
    throw "Invitation timeout for $TripId. Check matching consumer, candidate data and error queues."
}
$null = Call GET "/auth/me" $null "" 401
$tag = [guid]::NewGuid().ToString("N")
$email = "security-$tag@example.com"
$otherEmail = "other-$tag@example.com"
$null = Call POST "/auth/register" @{email=$email;password=$SeedPassword;role="Admin"} "" 201
$null = Call POST "/auth/register" @{email=$otherEmail;password=$SeedPassword} "" 201
$rider = Login $email
$other = Login $otherEmail
$driver = Login "driver.demo@example.com"
$driverOther = Login "driver.other@example.com"
$me = Call GET "/auth/me" $null $rider 200
if ($me.roles -notcontains "Rider" -or $me.roles -contains "Admin") { throw "Public role escalation." }
$driverMe = Call GET "/auth/me" $null $driver 200
$null = Call GET "/api/trips/driver/invitations" $null "" 401
$null = Call GET "/api/trips/driver/invitations" $null "invalid-token" 401
$null = Call GET "/api/trips/driver/invitations" $null $rider 403
$null = Call POST "/api/hubs/trips/negotiate?negotiateVersion=1" $null "" 401
$null = Call POST "/api/hubs/trips/negotiate?negotiateVersion=1" $null $rider 200
$null = Call POST "/api/trips/" @{} "" 401
$null = Call POST "/api/trips/" @{} $driver 403
$null = Call POST "/api/drivers/location" @{latitude=10.77;longitude=106.69;isAvailable=$true} $rider 403
$null = Call POST "/api/drivers/location" @{latitude=10.77;longitude=106.69;isAvailable=$false} $driverOther 200
$riderSocket = $null
$driverSocket = $null
$otherSocket = $null
try {
    $riderSocket = Open-HubSocket $BaseUrl $rider $true
    $driverSocket = Open-HubSocket $BaseUrl $driver
    $otherSocket = Open-HubSocket $BaseUrl $other $true
    $location = Call POST "/api/drivers/location" @{driverId="spoofed";latitude=10.77;longitude=106.69;isAvailable=$true} $driver 200
    if ($location.driverId -ne $driverMe.id) { throw "DriverId trusted from body." }
    $null = Call POST "/api/matching/quote" @{distance=2;estimatedTime=5} $rider 200
    $trip = NewTrip $rider
    if ($trip.riderId -ne $me.id) { throw "RiderId trusted from body." }
    $id = $trip.id
    $null = Call GET "/api/trips/$id" $null $rider 200
    $null = Call GET "/api/trips/$id" $null $other 403
    $null = Call POST "/api/trips/$id/cancel" @{} $other 403
    $null = Call POST "/api/trips/$id/confirm-price" @{} $other 403
    $null = Call POST "/api/trips/$id/confirm-price" @{} $driver 403
    $null = Call POST "/api/trips/$id/driver-accept" @{} $driver 409
    $null = Call POST "/api/matching/match" @{tripId=$id;pickupLatitude=10.77;pickupLongitude=106.69;radiusKm=5} $other 403
    $null = Call POST "/api/trips/$id/confirm-price" @{} $rider 202
    WaitInvitation $id $driver
    $invitationEvent = Wait-HubEvent $driverSocket 'driverInvitation' $id $WaitSeconds
    if ($invitationEvent.fareEstimate -ne $trip.fareEstimate) { throw "SignalR invitation price mismatch." }
    $others = @(Call GET "/api/trips/driver/invitations" $null $driverOther 200)
    if ($others.id -contains $id) { throw "Invitation leaked to another driver." }
    $null = Call POST "/api/trips/$id/driver-accept" @{} $driverOther 403
    $null = Call POST "/api/trips/$id/driver-accept" @{driverId=[guid]::NewGuid().ToString()} $driver 204
    $accepted = Call GET "/api/trips/$id" $null $driver 200
    if ($accepted.driverId -ne $driverMe.id) { throw "Accept used body DriverId." }
    $null = Wait-HubEvent $riderSocket 'tripChanged' $id
    $null = Wait-HubEvent $driverSocket 'tripChanged' $id
    Assert-NoHubInvocation $otherSocket
    $null = Call POST "/api/trips/$id/driver-accept" @{} $driver 409
    $null = Call POST "/api/trips/$id/enroute" @{} $driverOther 403
    $null = Call POST "/api/trips/$id/enroute" @{} $rider 403
    $null = Call POST "/api/trips/$id/enroute" @{} $driver 204
    $null = Call POST "/api/trips/$id/pickup" @{} $driver 204
    foreach ($action in @("pay","fail","payment-pending")) {
        $null = Call POST "/api/trips/$id/$action" @{} $rider 401
    }
    $null = Call GET "/internal/trips/$id" $null $rider 404
    $null = Call POST "/api/trips/$id/dropoff" @{} $driver 202
    $deadline = [DateTime]::UtcNow.AddSeconds($WaitSeconds)
    do {
        $paid = Call GET "/api/trips/$id" $null $rider 200
        if ($paid.status -eq 7 -or $paid.status -eq "Paid") { break }
        Start-Sleep -Milliseconds 250
    } while ([DateTime]::UtcNow -lt $deadline)
    if ($paid.status -ne 7 -and $paid.status -ne "Paid") { throw "Trip did not become Paid; check payment/result consumers and error queues." }
    $payment = Call GET "/api/payments/trip/$id" $null $rider 200
    if ($payment.status -ne "Succeeded" -or $payment.amount -ne $trip.fareEstimate) { throw "Payment does not match trip quote." }
    $null = Call GET "/api/payments/$($payment.id)" $null $other 403
    $null = Call GET "/api/payments/$($payment.id)" $null $rider 200
    $null = Call GET "/api/payments/trip/$id" $null $other 403
    $null = Call POST "/api/payments/$($payment.id)/refund" @{} $rider 403
    $cancelTrip = NewTrip $rider
    $null = Call POST "/api/trips/$($cancelTrip.id)/cancel" @{} $rider 204
    if ($DirectTripUrl) {
        $response = Invoke-WebRequest "$DirectTripUrl/trips/$id" -Headers @{"X-User-Id"=$me.id;"X-User-Roles"="Rider"} -SkipHttpErrorCheck -TimeoutSec 15
        if ($response.StatusCode -ne 401) { throw "Direct spoofed headers accepted by service." }
        $checks++
    }
}
finally {
    foreach ($socket in @($riderSocket,$driverSocket,$otherSocket)) {
        if ($null -ne $socket) { $socket.Dispose() }
    }
    foreach ($token in @($driver,$driverOther)) {
        try { $null = Call POST "/api/drivers/location" @{latitude=10.77;longitude=106.69;isAvailable=$false} $token 200 }
        catch { Write-Warning "Could not set demo driver offline; check test environment." }
    }
}
Write-Host "PASS: $checks HTTP checks plus real SignalR invitation/trip events and recipient isolation. Payment completed through messages, without POST /payments."
