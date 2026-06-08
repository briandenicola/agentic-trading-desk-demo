# History — Basher (Platform / Infra Engineer)

## Core Context (seeded 2026-06-08)
- **Project**: Client CV — Muni Sales Agentic Demo. Data fictional. Requested by Brian Denicola.
- **Stack**: Terraform (`azurerm ~>4`, `azapi ~>2`, `random ~>3`), Azure Container Apps,
  Azure AI Foundry, ACR, Key Vault, multi-stage non-root Alpine Docker, GitHub Actions OIDC.
- **You own**: `infra\` (flat `.tf`), `tasks\`, root `Taskfile.yaml`, `docker-compose.yml`,
  `.env.example`, all `Dockerfile`s, `.github\workflows\`, `src\agent-provisioner\`.
- **Reference**: `briandenicola/online-banking-demo` (ACA instead of AKS).

## Learnings
- 2026-06-08: Hired. Foundry account/project/capability-host only exist via the **azapi**
  provider, not azurerm. `ai.tf` must precede `containerapps.tf`.
- research R4: a ~90s RBAC-propagation wait/dependency is required before the capability host.
- agent-provisioner is a C# console job using `PersistentAgentsClient`, registered idempotently;
  it deploys as an ACA job (mirrors `init_agents.py` from the reference repo).
