param(
    [string]$BaseUrl = "http://localhost:5100",
    [string]$SeedPassword = "SwiftRide-demo-2026!",
    [string]$DirectTripUrl = ""
)
$ErrorActionPreference = "Stop"
$checks = 0
function Call([string]$Method, [string]$Path, $Body, [string]$Token, [int]$Expected) {
    $parameters = @{ Uri = "$BaseUrl$Path"; Method = $Method; SkipHttpErrorCheck = $true; TimeoutSec = 15 }
    if ($Body) { $parameters.Body = $Body | ConvertTo-Json -Depth 10; $parameters.ContentType = "application/json" }
    if ($Token) { $parameters.Headers = @{Authorization = "Bearer $Token"; "X-User-Roles" = "Admin"; "X-User-Id" = [guid]::NewGuid().ToString()} }
    $response = Invoke-WebRequest @parameters
    if ([int]$response.StatusCode -ne $Expected) { throw "$Method $Path expected $Expected, got $($response.StatusCode): $($response.Content)" }
    $script:checks++
    if ($response.Content) { return $response.Content | ConvertFrom-Json }
}
function Login([string]$Email) {
    (Call POST "/auth/login" @{email=$Email;password=$SeedPassword} "" 200).accessToken
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

$null = Call POST "/api/trips/" @{pickupAddress="A";dropoffAddress="B";fareEstimate=100} "" 401
$trip = Call POST "/api/trips/" @{riderId=[guid]::NewGuid().ToString();pickupAddress="A";dropoffAddress="B";fareEstimate=100} $rider 201
if ($trip.riderId -ne $me.id) { throw "RiderId trusted from body." }
$id = $trip.id
$null = Call GET "/api/trips/$id" $null $rider 200
$null = Call GET "/api/trips/$id" $null $other 403
$null = Call POST "/api/trips/$id/cancel" @{} $other 403
$null = Call POST "/api/trips/$id/driver-accept" @{} $driver 403
$null = Call POST "/api/drivers/location" @{latitude=10.77;longitude=106.69;isAvailable=$true} $rider 403
$location = Call POST "/api/drivers/location" @{driverId="spoofed";latitude=10.77;longitude=106.69;isAvailable=$true} $driver 200
if ($location.driverId -ne $driverMe.id) { throw "DriverId trusted from body." }
$null = Call POST "/api/matching/quote" @{distance=2;estimatedTime=5;surge=1} $rider 200
$null = Call POST "/api/matching/match" @{tripId=$id;pickupLatitude=10.77;pickupLongitude=106.69;radiusKm=5} $other 403
$matched = Call POST "/api/matching/match" @{tripId=$id;pickupLatitude=10.77;pickupLongitude=106.69;radiusKm=5} $rider 200
if ($matched.drivers.driverId -notcontains $driverMe.id) { throw "Driver not returned by Matching." }
$invitations = Call GET "/api/trips/invitations" $null $driver 200
if ($invitations.tripId -notcontains $id) { throw "Invitation missing." }
$null = Call POST "/api/trips/$id/driver-accept" @{driverId=[guid]::NewGuid().ToString()} $driver 204
$accepted = Call GET "/api/trips/$id" $null $driver 200
if ($accepted.driverId -ne $driverMe.id) { throw "Accept used body DriverId." }
$null = Call POST "/api/trips/$id/enroute" @{} $driverOther 403
$null = Call POST "/api/trips/$id/enroute" @{} $rider 403
$null = Call POST "/api/trips/$id/enroute" @{} $driver 204
$null = Call POST "/api/trips/$id/pickup" @{} $driver 204
$null = Call POST "/api/trips/$id/dropoff" @{} $driver 204
foreach ($action in @("pay","fail","payment-pending")) {
    $null = Call POST "/api/trips/$id/$action" @{} $rider 401
}
$paymentBody = @{tripId=$id;amount=100;idempotencyKey="security-$tag";payerAccount="rider-$($me.id)";payeeAccount="driver-$($driverMe.id)"}
$null = Call POST "/api/payments/" $paymentBody $other 403
$payment = Call POST "/api/payments/" $paymentBody $rider 201
if (!$payment.isTripSynced) { throw "Payment did not sync via internal service authentication." }
$null = Call GET "/api/payments/$($payment.id)" $null $other 403
$null = Call GET "/api/payments/$($payment.id)" $null $rider 200
$null = Call GET "/api/payments/trip/$id" $null $other 403
$null = Call POST "/api/payments/$($payment.id)/refund" @{} $rider 403
$null = Call GET "/internal/trips/$id" $null $rider 404
$cancelTrip = Call POST "/api/trips/" @{pickupAddress="C";dropoffAddress="D";fareEstimate=100} $rider 201
$null = Call POST "/api/trips/$($cancelTrip.id)/cancel" @{} $rider 204
$null = Call POST "/api/trips/" @{pickupAddress="C";dropoffAddress="D";fareEstimate=100} $driver 403

# Two invited drivers race for the same trip: only one can win.
$null = Call POST "/api/drivers/location" @{latitude=10.77;longitude=106.69;isAvailable=$true} $driverOther 200
$raceTrip = Call POST "/api/trips/" @{pickupAddress="C";dropoffAddress="D";fareEstimate=100} $rider 201
$null = Call POST "/api/matching/match" @{tripId=$raceTrip.id;pickupLatitude=10.77;pickupLongitude=106.69;radiusKm=5} $rider 200
$jobs = @($driver,$driverOther) | ForEach-Object {
    Start-Job -ArgumentList "$BaseUrl/api/trips/$($raceTrip.id)/driver-accept", $_ -ScriptBlock {
        param($url,$token)
        [int](Invoke-WebRequest $url -Method Post -Headers @{Authorization="Bearer $token"} -SkipHttpErrorCheck -TimeoutSec 15).StatusCode
    }
}
try {
    $statuses = @($jobs | Wait-Job | Receive-Job -ErrorAction Stop)
    if (@($statuses | Where-Object {$_ -eq 204}).Count -ne 1 -or
        @($statuses | Where-Object {$_ -eq 409}).Count -ne 1) { throw "Concurrent accept must return one 204 and one 409: $statuses" }
    $checks += 2
}
finally { $jobs | Remove-Job -Force }
if ($DirectTripUrl) {
    $response = Invoke-WebRequest "$DirectTripUrl/trips/$id" -Headers @{"X-User-Id"=$me.id;"X-User-Roles"="Rider"} -SkipHttpErrorCheck
    if ($response.StatusCode -ne 401) { throw "Direct spoofed headers accepted by service." }
    $checks++
}
Write-Host "PASS: $checks HTTP checks through Gateway (roles, ownership, invitation, matching, payment sync and spoofing)."
# Unique test accounts/trips/payments remain in the development databases.
