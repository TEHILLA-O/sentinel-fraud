# Failure modes, fixes, and results

Honest engineering notes for this project. Nothing here is invented for polish.

## What can go wrong

- **Kafka at-least-once double processing.** Impact: duplicate decisions/cases. Mitigation: inbox on `EventId`, unique `TransactionId` on decisions/cases; replay skips scoring (`docs/IDEMPOTENCY.md`).
- **Velocity or Redis gaps producing wrong scores.** Impact: false BLOCK/APPROVE. Mitigation: Redis sorted-set windows with expiry; rules independently tested; ruleset version persisted with each decision.
- **Optional ML model dominating deterministic rules.** Impact: opaque scores. Mitigation: `NoOpRiskModel` default; ML.NET optional, confidence kept below combination threshold in default trainer (`docs/KNOWN_ISSUES.md`).
- **Docker/Testcontainers unavailable in CI.** Impact: skipped integration coverage. Mitigation: Kafka tests skip when Docker cannot start; document curl healthcheck requirement.

## What went wrong

**No recorded production incident in this repo yet.** Known issues file lists demo limitations (Blazor cookie vs API JWT, advisory `dotnet format`, transitive NuGet advisories not on the scoring path).

## How it was resolved

- Idempotency and outbox semantics documented and implemented for consumer crashes mid-write.
- Thirteen rules + decision policy separation; SignalR case console for REVIEW/BLOCK.
- Performance tests measure engine-only latency; docs forbid quoting unmeasured E2E throughput.

## Results

- Engine-only: `Sentinel.PerformanceTests` recorded **P99 under 250 ms** for 250 evaluations of the documented example transaction (workstation measurement in `docs/PERFORMANCE.md`).
- Successful demo: Compose/Aspire up, generator `--mode mixed`, analyst login, inspect BLOCK example rules on account `A-91828`.
