# 005. At-least-once delivery

## Context

Kafka consumers can redeliver.

## Decision

Inbox + unique decision/case indexes. No exactly-once claim.

## Consequences

Duplicate delivery is safe. See `docs/IDEMPOTENCY.md`.
