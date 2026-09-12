# Known issues

- Docker service healthchecks require `curl` in the runtime image. Workers do not expose HTTP probes.
- Kafka Testcontainers tests skip automatically when Docker cannot start a container.
- The optional ML.NET model is trained on synthetic data and is disabled unless `Risk:UseMlNet=true`. Confidence is intentionally below the combination threshold in the default trainer so it cannot silently dominate decisions.
- Blazor login is cookie-based; APIs are JWT-based. The dashboard reads PostgreSQL directly rather than proxying every query through the APIs.
- `dotnet format --verify-no-changes` is advisory in CI until a full formatter pass is enforced.
- Transitive NuGet advisories may still appear for Microsoft.OpenApi and MessagePack versions pulled by ASP.NET OpenAPI / SignalR. They are not used as public attack surface in this local demo.
- System.Security.Cryptography.Xml can appear as a transitive advisory from JWT tooling; it is not part of the scoring path.
