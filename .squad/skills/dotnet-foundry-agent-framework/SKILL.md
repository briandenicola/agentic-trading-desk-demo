# Skill: .NET Agent Framework + Azure AI Foundry wiring

- **Confidence**: low (seeded at team hire 2026-06-08; bump when first applied in code)
- **Domain**: orchestration-api (Livingston), agent-provisioner (Basher), prompts (Rusty)

## When to use
Building or wiring the Morning Brief agent: LIVE tool-calling on Azure AI Foundry via the
Microsoft Agent Framework (.NET), and idempotent agent registration.

## Pattern
- Packages (central-managed in `Directory.Packages.props`, no inline `Version`):
  `Microsoft.Agents.AI`, `Microsoft.Agents.AI.AzureAI`, `Azure.AI.Agents.Persistent`,
  `Azure.AI.Projects`, `Azure.Identity`.
- Auth: `DefaultAzureCredential` — **only acquire it in LIVE**; the DEMO path must run with no
  credential and no network to a model (Principle III, T033).
- `AgentRunner.cs`: chat client from `Microsoft.Agents.AI.AzureAI`; tool-calling loop **capped
  at `MAX_TOOL_HOPS`** (Principle VIII) to stop runaway loops; map model output → `MorningBrief`
  DTO so LIVE and DEMO emit the identical JSON shape.
- Tools (`Agents\Tools\`): thin wrappers over the mock-api endpoints using a typed `HttpClient`;
  return JSON, **never throw** — degrade with a `notes` entry on tool error (FR-011, T014).
- Provisioner (`src\agent-provisioner`): `PersistentAgentsClient` registers/updates the agent
  version idempotently; runs as an ACA job. Mirror the reference repo's `init_agents.py`.

## Guardrails
- Every tool must also exist as an operation in `openapi\tools.yaml` (Principle VI).
- Never read `src\mock-api\Data\` JSON in-process — always go over HTTP (Principle II, FR-002).
- No hardcoded endpoints/keys; everything from env / Key Vault.

## References
- `specs\001-morning-planning-outreach\research.md` (Foundry wiring, R4 RBAC wait)
- `contracts\agent-api.yaml`, `contracts\morning-brief.schema.json`, `openapi\tools.yaml`
- Reference architecture: `briandenicola/online-banking-demo`
