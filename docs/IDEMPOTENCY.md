# Idempotency

Sentinel assumes Kafka at-least-once delivery.

## Keys

- `EventId` uniquely identifies a Kafka payload. The inbox table stores it before side effects complete.
- `TransactionId` uniquely identifies a scored transaction. `risk_decisions` and `fraud_cases` have unique indexes on it.

## Replay behaviour

If the same raw event is consumed twice:

1. The inbox insert is rejected and the consumer commits the offset.
2. If a decision already exists for the `TransactionId`, scoring is skipped.

If a consumer crashes after the decision write but before inbox completion, the next delivery sees the unique decision row and completes without creating a second case or a second history row.

Outbox publishing is also at-least-once. Downstream consumers must treat decision events as idempotent by `TransactionId` / `EventId`.
