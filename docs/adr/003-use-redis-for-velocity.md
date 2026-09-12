# 003. Use Redis for velocity

## Context

Sliding-window counts cannot be computed from PostgreSQL at ingestion latency.

## Decision

Redis sorted sets store timestamped transaction, spend, merchant, country and failure members with key expiry.

## Consequences

Redis loss resets windows. PostgreSQL remains the system of record for decisions and cases.
