# 002. Use Kafka

## Context

Fraud scoring must survive producer spikes and allow independent consumers (scoring, profiles, cases, live UI).

## Decision

Apache Kafka with Confluent.Kafka is the system bus. Topics are versioned. Keys are `AccountId`.

## Consequences

Ordering is per account, not global. Consumers must be idempotent.
