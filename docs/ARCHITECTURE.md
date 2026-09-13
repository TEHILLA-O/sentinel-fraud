# Architecture

Sentinel is a real-time transaction monitoring, fraud detection, and risk decision platform. It is a C# / .NET 10 event-driven system: HTTP ingest, Kafka transport, an explainable rules engine with Redis velocity context, PostgreSQL auditability, and a Blazor analyst console.

## Product purpose

Demonstrate production-shaped distributed systems work for fraud operations: idempotent consumers, deterministic scoring a reviewer can step through, case management for REVIEW/BLOCK, and observable services. It is not a black-box model that invents a decision without a ruleset version.

## Main components

| Process | Responsibility |
| --- | --- |
| `Sentinel.Ingestion.Api` | Validate inbound HTTP transactions and publish `sentinel.transactions.raw.v1` |
| `Sentinel.TransactionProcessor` | Consume raw events, enrich, score, persist, outbox publish |
| `Sentinel.ProfileProcessor` | Update behavioural profiles and known devices |
| `Sentinel.DecisionPublisher` | Open REVIEW/BLOCK cases and fan out to Redis/SignalR |
| `Sentinel.Cases.Api` | Analyst case workflow and feedback events |
| `Sentinel.Admin.Api` | Versioned rulesets, analytics, audit |
| `Sentinel.Web` | Blazor analyst dashboard |
| `Sentinel.AppHost` | .NET Aspire host for local multi-process + dependencies |

Supporting libraries live under `src/BuildingBlocks`, `src/RiskEngine`, and related folders. Tests cover domain, rules, architecture, integration (Testcontainers), and performance.

## Data and control flow

```text
Transaction producer
        |
        v
HTTP ingestion -> Kafka raw transactions
        |
        v
Transaction processor
        |-- Redis velocity / profile enrichment
        |-- Risk engine (IRiskRule set, capped 0-100)
        |-- PostgreSQL immutable decision + ruleset version
        |-- Kafka decisions / alerts / dead-letter
        v
Case management (REVIEW / BLOCK)
        |
        v
Blazor dashboard (SignalR updates)
```

Partition key for Kafka topics is `AccountId` so per-account ordering is preserved.

## Topics

| Topic | Purpose |
| --- | --- |
| `sentinel.transactions.raw.v1` | Accepted ingress |
| `sentinel.transactions.validated.v1` | Passed validation |
| `sentinel.risk.decisions.v1` | Explainable scores |
| `sentinel.alerts.v1` | REVIEW / BLOCK |
| `sentinel.deadletter.v1` | Poison / exhausted retries |
| `sentinel.profile.updated.v1` | Async profile updates |
| `sentinel.case.feedback.v1` | Analyst labels |

## Risk engine

Rules implement `IRiskRule` and are independently testable. Decision policy is separate from rule code. Configuration is versioned in PostgreSQL and cached. Historical decisions keep the ruleset they were scored with.

Optional `IRiskModel` (`NoOpRiskModel` by default, `MlNetAnomalyRiskModel` behind a flag) can add a bounded number of points when confidence is high. Rules remain the explainable core.

## Redis and PostgreSQL

Redis sorted sets track transactions, spend, distinct merchants, distinct countries, and failures across 1m / 5m / 1h / 24h windows with key expiry.

PostgreSQL stores transactions, immutable risk decisions, ruleset versions, cases, case history, notes, profiles, known devices, audit events, outbox and inbox rows. Indexes are declared in `SentinelDbContext`.

## Design rules

- Money is `decimal` via `Money`.
- Decisions are append-only and include the ruleset version used at score time.
- Consumers are idempotent through an inbox table plus a unique decision index on `TransactionId`.
- JWT on APIs, cookie auth on the dashboard, ASP.NET authorization policies for analyst / senior / risk manager / admin / auditor roles.

## Repository layout

```
src/Ingestion/            HTTP ingress
src/Workers/              processors / publishers
src/RiskEngine/           rules and scoring
src/CaseManagement/       cases API surface
src/Administration/       admin API surface
src/Web/                  Blazor console
src/Sentinel.AppHost/     Aspire orchestration
src/BuildingBlocks/       shared primitives
tests/                    domain, rules, architecture, integration, performance
tools/Sentinel.TransactionGenerator/   demo traffic
docs/                     architecture, event flow, rules, security, ADRs
```

## Related docs

- [EVENT_FLOW.md](EVENT_FLOW.md)
- [RISK_ENGINE.md](RISK_ENGINE.md)
- [RULES.md](RULES.md)
- [IDEMPOTENCY.md](IDEMPOTENCY.md)
- [SECURITY.md](SECURITY.md)
- [PERFORMANCE.md](PERFORMANCE.md)
- ADRs under `docs/adr/`
