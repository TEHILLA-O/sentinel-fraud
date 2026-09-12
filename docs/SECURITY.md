# Security

## Roles

| Role | Capabilities |
| --- | --- |
| FraudAnalyst | Review cases, add notes, confirm fraud / false positive |
| SeniorAnalyst | Analyst actions plus escalate |
| RiskManager | Activate new ruleset versions |
| Administrator | System management and all of the above |
| Auditor | Read-only cases, rules and audit log |

Policies live in `SentinelPolicies` and are registered in `AddSentinelJwtAuth`.

## Local credentials

Password for every seeded user: `Sentinel!23`

- analyst@sentinel.local
- senior@sentinel.local
- risk@sentinel.local
- admin@sentinel.local
- auditor@sentinel.local

Rotate the JWT signing key before any shared deployment. Privileged ruleset changes write `audit_events`.
