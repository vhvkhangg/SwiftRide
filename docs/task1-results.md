# Task 1 - Gateway and Auth Verification

Verification date: 8 November 2026.

## Delivered

- Gateway validates JWT before Ocelot proxying and strips spoofed `X-User-*` headers.
- Auth uses PostgreSQL EF migration `20261108052137_InitialAuthSchema`; Compose test overlay enables migration instead of `EnsureCreated`.
- `/health` remains liveness; `/health/ready` checks both Auth tables and returns `503` when Auth PostgreSQL is unavailable.
- Refresh token rotation, replay-family revocation, logout, rate limiting and SignalR authentication remain covered.
- Trip enables WebSockets before authentication, so SignalR query-token connections work through Gateway and direct service.

## Full-stack result

Command:

```powershell
pwsh -NoProfile -File tests/Gateway.Security/task1.ps1
```

Result: **PASS**. Isolated Compose project `swiftride-task1-a84c0eaebe`; test containers, network and database volumes were isolated and containers/network were removed after the run.

Coverage: Auth migration history and restart persistence, refresh after Gateway restart, Auth database outage readiness, Auth smoke, 20-request rate limit, Gateway ownership/role checks, direct-service spoof rejection, Trip/Matching/Payment flow, SignalR invitation/tripChanged delivery and recipient isolation.

Additional local checks: Release solution build passed with 0 warnings and 0 errors; Gateway transport/security tests passed 4/4; full non-integration solution tests passed 153/153; system helper tests passed 2/2.

The payment path in Gateway smoke uses the existing simulated ledger and message flow. It does not represent a real PSP or bank integration.
