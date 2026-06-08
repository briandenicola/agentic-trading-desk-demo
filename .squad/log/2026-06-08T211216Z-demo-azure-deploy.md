# Squad Log — DEMO-mode Azure deploy + morning-brief validation

- **When:** 2026-06-08T21:12:16Z
- **Requested by:** Brian Denicola
- **Agent:** Basher (Infra/CI-CD)
- **Goal:** Deploy app to Azure in DEMO mode (skip Foundry), region swedencentral, validate public morning-brief endpoint.

## Outcome: ✅ SUCCESS

Public endpoint serves a schema-valid DEMO morning brief.

## What was done
1. **Foundry toggle** — added `enable_foundry` (bool, default true) to `infra/variables.tf`; gated all Foundry/azapi resources with `count = var.enable_foundry ? 1 : 0` across `ai.tf`, `ai-connections.tf`, `identity.tf`; fixed cross-refs to `[0]`. `terraform fmt` + `validate` pass.
2. **tfvars** — `infra/terraform.tfvars`: location=swedencentral, enable_foundry=false, demo_mode=true, project_prefix=wfgarage, environment=demo. (gitignored)
3. **Provision** — `terraform init/plan/apply` with local state. Plan = 19 resources, ZERO Foundry/AI/CognitiveServices. Hit + fixed KV name length (locals.tf, 25→24 chars). Deleted one orphaned failed `mock-api` app to unblock recreation.
4. **Images** — built mock-api, orchestration-api, ui-app, agent-provisioner via `az acr build` (server-side, no local docker). Tag `demo-9712e11`. Resolved OneDrive placeholder issue (staged clean build context). Worked around Dockerfile uid collision + ui-app nginx SNI/Host bugs in the staged context.
5. **Roll** — `az containerapp update` rolled all 3 apps to demo tags; ui-app rebuilt twice for nginx fixes (final tag demo-9712e11-3).
6. **Validate** — see results below.

## Resource group
- RG: `wfgarage-demo-rg`
- ACR: `wfgaragedemo59f7ac59` (wfgaragedemo59f7ac59.azurecr.io)
- Public URL: https://ui-app.salmonrock-8782a063.swedencentral.azurecontainerapps.io

## Validation
- `GET /` → 200, text/html
- `POST /api/agent/morning-brief` (empty body → defaults to fed_surprise_hike) → 200
- mode=DEMO; top-level keys match schema required set: mode, asOf, marketStrip, reasoning, macroNarrative, mostAffectedClients, outreach
- Outreach ranking: #1 ATLAS (1.00), #2 BROOK (0.83), #3 CEDAR (0.48)

## Teardown (human runs when done)
`az group delete --name wfgarage-demo-rg --yes --no-wait`

## Follow-ups for human (committed source, not changed)
- Fix .NET Dockerfiles adduser uid collision (uid 1654).
- Add `proxy_ssl_server_name on;` and change `Host $host` → `Host $proxy_host` in src/ui-app/nginx.conf.
- Investigate POST morning-brief HTTP 400 when a JSON body is supplied (request binding).
