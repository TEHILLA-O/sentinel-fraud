# 007. Explainable risk decisions

## Context

Investigators must answer why a transaction was blocked after configuration has changed.

## Decision

Store the transaction snapshot, profile snapshot, triggered rules, scores, ruleset version and correlation ID immutably.

## Consequences

Later ruleset edits never rewrite history.
