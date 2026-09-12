# Event flow

1. A producer (dashboard generator or card processor) POSTs `TransactionReceivedV1` to `/api/v1/transactions`.
2. Ingestion publishes the event to `sentinel.transactions.raw.v1` partitioned by `AccountId`.
3. `Sentinel.TransactionProcessor` validates the payload. Permanent failures go to `sentinel.deadletter.v1`.
4. The processor records velocity in Redis, loads the customer profile, evaluates rules, and writes `transactions` + `risk_decisions`.
5. An outbox dispatcher publishes:
   - `sentinel.transactions.validated.v1`
   - `sentinel.risk.decisions.v1`
   - `sentinel.alerts.v1` when the decision is REVIEW or BLOCK
6. `Sentinel.ProfileProcessor` updates rolling behavioural statistics and emits `sentinel.profile.updated.v1`.
7. `Sentinel.DecisionPublisher` opens a fraud case when required and publishes the decision to Redis for SignalR.
8. Analyst feedback emits `sentinel.case.feedback.v1` for later rule or model work. Nothing is retrained automatically.

Retries are bounded and exponential. Poison messages (failed validation/deserialization) are not retried indefinitely.
