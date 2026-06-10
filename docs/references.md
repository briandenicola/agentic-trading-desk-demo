# References — WF-Garage

Curated links for the **Morning Planning & Prioritized Outreach** demo. Internal paths are relative to
the repository root.

## Internal documents

| Document | Path | Purpose |
|---|---|---|
| Project README | [`README.md`](../README.md) | Overview, scenes, quickstart, deploy. |
| Product Requirements | [`docs/prd.md`](prd.md) | Problem, personas, scope, success criteria. |
| Architecture | [`docs/architecture.md`](architecture.md) | Agents, orchestration, traceability, event fan-out, AI Chat. |
| Getting Started | [`docs/getting-started.md`](getting-started.md) | Local DEMO/LIVE setup, tests, deploy, troubleshooting. |
| Constitution | [`.specify/memory/constitution.md`](../.specify/memory/constitution.md) | Binding principles & quality gates (§0 document hierarchy). |
| ADR-0001 | [`docs/adr/0001-adopt-framework.md`](adr/0001-adopt-framework.md) | Adopt Spec Kit + Squad framework. |
| ADR-0002 | [`docs/adr/0002-csharp-foundry-aca-stack.md`](adr/0002-csharp-foundry-aca-stack.md) | C#/.NET 10 + Foundry + Container Apps stack decision. |
| Active spec (001) | [`specs/001-morning-planning-outreach/`](../specs/001-morning-planning-outreach/) | Morning brief feature spec, plan, tasks. |
| Active spec (002) | [`specs/002-reactive-event-cockpit/`](../specs/002-reactive-event-cockpit/) | Reactive multi-event cockpit + News Desk + SSE. |
| Backlog | [`specs/_backlog/`](../specs/_backlog/) | Future features (connectors, auth, observability, MCP, etc.). |
| Squad artifacts | [`.squad/`](../.squad/) | In-repo AI team charters, decisions, logs. |

## Contracts & configuration

| Item | Path | Notes |
|---|---|---|
| Tool contract | [`openapi/tools.yaml`](../openapi/tools.yaml) | OpenAPI **v0.3.0**; the mock-api HTTP seam; importable into Foundry or exposed via MCP. |
| Scene schemas | [`specs/001-morning-planning-outreach/contracts/`](../specs/001-morning-planning-outreach/contracts/) | `MorningBrief` / agent API JSON schemas. |
| Environment template | [`.env.example`](../.env.example) | All configuration variables (no secrets). |
| Agent prompts | [`src/orchestration-api/Prompts/`](../src/orchestration-api/Prompts/) | `rm-daily-briefing`, `morning-brief`, `event-specialist`, `markets-assistant`, `briefing-synthesizer`. |
| Infrastructure | [`infra/`](../infra/) | Terraform for ACA, ACR, Key Vault, identity, App Insights, Foundry. |
| Task workflows | [`tasks/`](../tasks/) | `Taskfile.local.yml`, `Taskfile.build.yml`, `Taskfile.cloud.yml`. |

## Reference architecture

- **online-banking-demo** — the framework/conventions/IaC this project mirrors (Container Apps instead
  of AKS): <https://github.com/briandenicola/online-banking-demo>

## External documentation

| Topic | Link |
|---|---|
| Microsoft Agent Framework | <https://learn.microsoft.com/agent-framework/> |
| Azure AI Foundry | <https://learn.microsoft.com/azure/ai-foundry/> |
| Azure Container Apps | <https://learn.microsoft.com/azure/container-apps/> |
| Azure Key Vault | <https://learn.microsoft.com/azure/key-vault/> |
| Azure Container Registry | <https://learn.microsoft.com/azure/container-registry/> |
| .NET 10 | <https://learn.microsoft.com/dotnet/> |
| React 19 | <https://react.dev/> |
| MUI v9 | <https://mui.com/material-ui/> |
| Vite | <https://vite.dev/> |
| Vitest | <https://vitest.dev/> |
| OpenTelemetry .NET | <https://opentelemetry.io/docs/languages/net/> |
| Serilog | <https://serilog.net/> |
| Terraform `azurerm` | <https://registry.terraform.io/providers/hashicorp/azurerm/latest/docs> |
| Terraform `azapi` | <https://registry.terraform.io/providers/Azure/azapi/latest/docs> |
| Task (go-task) | <https://taskfile.dev/> |
| Gitleaks | <https://github.com/gitleaks/gitleaks> |
| Spec Kit | <https://github.com/github/spec-kit> |
| Model Context Protocol (MCP) | <https://modelcontextprotocol.io/> |
