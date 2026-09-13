# Contributing

Thanks for helping with Sentinel. Keep scoring explainable and consumers idempotent.

## Prerequisites

- .NET SDK 10 (see `global.json`, rollForward latestFeature)
- Docker for Testcontainers-based integration tests and Compose
- Optional: .NET Aspire workload for `Sentinel.AppHost`

## Setup

```bash
dotnet restore
dotnet build
```

## Test

```bash
dotnet test
```

Rule and domain tests should run without Docker. Integration tests use Testcontainers for PostgreSQL, Redis, and Kafka when Docker is available.

## Run locally (Aspire)

```bash
dotnet run --project src/Sentinel.AppHost
```

Aspire starts PostgreSQL, Redis, Kafka, and Sentinel processes.

## Run with Docker Compose

```bash
docker compose up --build
```

Surfaces (see README for ports): dashboard, ingestion API, cases API, admin API, Kafka UI.

Demo traffic:

```bash
dotnet run --project tools/Sentinel.TransactionGenerator -- --mode mixed --rate 20 --endpoint http://localhost:8081/api/v1/transactions
```

## Guidelines

- Add or change rules as independently tested `IRiskRule` implementations.
- Do not invent unverified throughput claims; use `docs/PERFORMANCE.md` and performance tests.
- Preserve ruleset versioning on stored decisions.
- Keep commit messages short and human.
