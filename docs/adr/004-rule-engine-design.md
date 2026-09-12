# 004. Rule engine design

## Context

Fraud reasons must be independently testable and explainable.

## Decision

Each rule implements `IRiskRule`. Aggregation and decision mapping live outside the rules.

## Consequences

Rules can be enabled, scored and versioned without rewriting the engine.
