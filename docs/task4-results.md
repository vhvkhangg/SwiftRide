# Task 4 - Payment Verification

## Delivered

- Dropoff is the only business initiator for `ProcessPaymentV1`; Trip sends one canonical command with a trip-derived idempotency key.
- Payment validates command correlation, currency, amount precision, canonical rider/driver accounts and authoritative Trip snapshot before writing.
- Payment success writes exactly one payment plus debit/credit ledger entries; duplicate commands replay the persisted result without new ledger rows.
- Payment declines publish `PAYMENT_DECLINED` only with a persisted failure reason. Technical faults remain `PaymentPending` and can be repaired and replayed.
- Trip validates payment result identity, amount, currency and timestamps before moving to `Paid` or `Failed`; late results remain idempotent.
- Refund requires a valid original two-entry ledger and uses optimistic concurrency to prevent a second refund.

## Verification

| Check | Result |
|---|---|
| Release solution build | PASS; 0 warnings, 0 errors |
| Full non-integration solution tests | PASS; 174 passed, 0 failed |
| Payment/Trip focused tests | PASS; Payment 46 and Trip 26 passed |
| Payment PostgreSQL integration | PASS; 12 Payment, 9 Trip and 5 Matching integration tests |
| Full messaging/system suite | PASS; 24/24 checks via `python tests/System/messaging.py --dotnet dotnet --output artifacts/task4/system-final` |

## Limits

- Payment is a deterministic ledger simulation, not a real bank or PSP charge.
- No card token, bank transfer, webhook or external settlement reconciliation exists in this task.
- Technical retry requires fixing the recorded infrastructure cause before replaying the failed message.
