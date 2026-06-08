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

### Phase 7 - Deployment & Infrastructure (T035–T042)

**Azure AI Foundry RBAC Propagation**: Always use `time_sleep` resource (90s) after role assignments and before capability host creation. RBAC uses eventual consistency; skipping this causes CapabilityHostOperationFailed 100% of the time.

**azapi Schema Validation**: Use `schema_validation_enabled = false` when working with new/preview Azure APIs where provider schema lags behind actual API. Always verify body properties against Azure REST API docs for the exact api-version.

**Dockerfile Context for Shared Projects**: Use repo root as build context when projects reference `../shared/` dependencies. Root .dockerignore must NOT exclude .csproj files, Directory.*.props, or global.json.

**ACA Placeholder Images**: Container Apps need an image at creation but real images don't exist until `az acr build` runs. Use placeholder + `lifecycle { ignore_changes = [image] }` so CD pipeline owns tags via `az containerapp update`.

**nginx envsubst Templates**: Copy config to `/etc/nginx/templates/*.conf.template` with env vars like `${ORCHESTRATION_API_URL}`. nginx:alpine entrypoint auto-runs envsubst on startup. Only substitutes env vars, not nginx's own `$host`/`$request_uri` variables.

**Terraform fmt Idempotency**: May require 2 passes to stabilize. Run `terraform fmt` before `terraform fmt -check` in CI.

**azurerm v4 Deprecations**: `enable_rbac_authorization` → `rbac_authorization_enabled`. Always check provider CHANGELOG on major version upgrades.

**Foundry API Property Renames**: `friendlyName` → `displayName` between API versions. Always verify property names against versioned Azure REST API docs.

**Model SKU Defaults**: Use GlobalStandard for dev/default (broader regional availability), Standard for production (lower latency, known region). Default to gpt-4o-mini (2024-07-18) with capacity 10 for quota-friendliness.
