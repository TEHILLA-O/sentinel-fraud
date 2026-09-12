# Demo

1. Start infrastructure and services:

```bash
docker compose up --build
```

or:

```bash
dotnet run --project src/Sentinel.AppHost
```

2. Open the dashboard at http://localhost:8080 and sign in as `analyst@sentinel.local` / `Sentinel!23`.

3. Generate traffic:

```bash
dotnet run --project tools/Sentinel.TransactionGenerator -- --mode mixed --rate 20 --endpoint http://localhost:8081/api/v1/transactions
```

4. Watch the live transactions grid. High-value US e-commerce from account `A-91828` should score near the documented BLOCK example.

5. Open a BLOCK/REVIEW case, inspect triggered rules, add a note, and mark fraud or false positive.

## Scenario catalogue

- normal grocery spending
- online purchase
- high-value purchase
- rapid transactions / fraud burst
- foreign transaction
- unknown device
- impossible travel (ATM in New York after a UK physical pattern)
