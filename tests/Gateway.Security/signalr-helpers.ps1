function Read-HubFrames($Socket, [int]$TimeoutSeconds = 15, [switch]$AllowTimeout) {
    $timeout = [Threading.CancellationTokenSource]::new([TimeSpan]::FromSeconds($TimeoutSeconds))
    $stream = [IO.MemoryStream]::new()
    try {
        $buffer = [byte[]]::new(8192)
        do {
            $received = $Socket.ReceiveAsync([ArraySegment[byte]]::new($buffer), $timeout.Token).GetAwaiter().GetResult()
            if ($received.MessageType -ne [Net.WebSockets.WebSocketMessageType]::Text) { throw "Hub closed unexpectedly." }
            $stream.Write($buffer, 0, $received.Count)
        } while (!$received.EndOfMessage)
        [Text.Encoding]::UTF8.GetString($stream.ToArray()).Split([char]30, [StringSplitOptions]::RemoveEmptyEntries) |
            ForEach-Object { $_ | ConvertFrom-Json }
    }
    catch {
        if ($AllowTimeout -and $timeout.IsCancellationRequested) { return @{type=-1} }
        throw
    }
    finally { $stream.Dispose(); $timeout.Dispose() }
}

function Open-HubSocket([string]$BaseUrl, [string]$Token, [bool]$QueryToken = $false) {
    $socket = [Net.WebSockets.ClientWebSocket]::new()
    $timeout = [Threading.CancellationTokenSource]::new([TimeSpan]::FromSeconds(15))
    $uri = [UriBuilder]::new("$($BaseUrl.TrimEnd('/'))/api/hubs/trips")
    $uri.Scheme = if ($uri.Scheme -eq 'https') { 'wss' } else { 'ws' }
    if ($QueryToken) { $uri.Query = 'access_token=' + [Uri]::EscapeDataString($Token) }
    else { $socket.Options.SetRequestHeader('Authorization', "Bearer $Token") }
    try {
        $null = $socket.ConnectAsync($uri.Uri, $timeout.Token).GetAwaiter().GetResult()
        $handshake = [Text.Encoding]::UTF8.GetBytes('{"protocol":"json","version":1}' + [char]30)
        $null = $socket.SendAsync([ArraySegment[byte]]::new($handshake), [Net.WebSockets.WebSocketMessageType]::Text, $true, $timeout.Token).GetAwaiter().GetResult()
        $reply = @(Read-HubFrames $socket)
        if ($reply.Count -ne 1 -or $reply[0].error -or $reply[0].type) { throw "Invalid SignalR handshake." }
        return $socket
    }
    catch { $socket.Dispose(); throw "SignalR connection/handshake failed; token URL omitted." }
    finally { $timeout.Dispose() }
}

function Wait-HubEvent($Socket, [string]$Event, [string]$TripId, [int]$TimeoutSeconds = 15) {
    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    do {
        foreach ($frame in @(Read-HubFrames $Socket ([Math]::Max(1, [int]($deadline - [DateTime]::UtcNow).TotalSeconds)))) {
            if ($frame.type -eq 7) { throw "SignalR server closed connection." }
            if ($frame.type -eq 1 -and $frame.target -eq $Event) {
                $payload = $frame.arguments[0]
                if ($payload.tripId -eq $TripId -or $payload.id -eq $TripId) { return $payload }
            }
        }
    } while ([DateTime]::UtcNow -lt $deadline)
    throw "SignalR event timeout: $Event for $TripId."
}

function Assert-NoHubInvocation($Socket) {
    if ($Socket.State -ne [Net.WebSockets.WebSocketState]::Open) { throw "Unrelated user's socket is not open." }
    do {
            foreach ($frame in @(Read-HubFrames $Socket 2 -AllowTimeout)) {
                if ($frame.type -eq -1) { return }
                if ($frame.type -eq 1) { throw "SignalR event leaked to unrelated user." }
                if ($frame.type -eq 7) { throw "Unrelated user's socket closed unexpectedly." }
            }
    } while ($true)
}
