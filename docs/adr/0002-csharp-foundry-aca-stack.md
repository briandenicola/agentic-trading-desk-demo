# ADR-0002: C#/.NET + Microsoft Agent Framework on Azure AI Foundry & Container Apps

**Status**: Accepted
**Date**: 2026-06
**Author**: Brian Denicola (with Copilot)
**Amends**: `.specify/memory/constitution.md` (Principle V; §17) — constitution bumped to **v0.2.0**

## Context

The project constitution v0.1.0 mandates a **Python/FastAPI** stack (Principle V), `pytest`/`ruff`
quality gates (§17), and a three-layer architecture rooted at `api/`/`frontend/` (Principle II). The
repository to date is a storyboard mockup (`mockup/`) plus the `openapi/tools.yaml` tool contract and
the Spec Kit / Squad scaffold — **no application code exists yet**.

For the first interactive build — **Demo 1: Morning Planning & Prioritized Outreach**
(`specs/001-morning-planning-outreach/`) — the stakeholder requires:
- Agents built with the **Microsoft Agent Framework in C#/.NET**, **hosted in Azure AI Foundry**.
- A **React** UI and a **C# mock API**, both running in **Azure Container Apps**.
- The framework, coding conventions, and design of
  [`briandenicola/online-banking-demo`](https://github.com/briandenicola/online-banking-demo)
  (which targets **.NET 10**, central NuGet package management, Serilog + OpenTelemetry, Terraform
  with `azurerm`+`azapi`, and a React 19 + MUI v9 UI) — **Container Apps instead of AKS**.

This directly conflicts with Principle V (Python/FastAPI) and the §17 tooling. Per constitution §22,
a stack change of this magnitude requires an amendment recorded as an ADR with a semver bump.

## Decision

Adopt a **C#/.NET 10 + Microsoft Agent Framework + Azure AI Foundry + Azure Container Apps** stack as
the project's primary technology, replacing the Python/FastAPI mandate. The three-layer architecture
(Principle II) and LIVE/DEMO parity (Principle III), API-first tool contract (Principle VI), secrets
hygiene (Principle IV/IX), and testing pyramid (Principle VII) are **retained**, re-expressed for the
new stack.

### Reasons
1. **Stakeholder mandate** — "Agent Framework and C#", agents in Foundry, UI/API in Container Apps.
2. **Reference-architecture parity** — mirrors `online-banking-demo` so patterns, conventions, and
   IaC are reusable and reviewable against a known-good implementation.
3. **Foundry-native agents** — Microsoft Agent Framework (.NET) + `Azure.AI.Agents.Persistent` give
   managed, versioned, monitored agents, satisfying the existing `007-foundry-migration` backlog goal.
4. **Container Apps fit** — managed HTTPS ingress, scale-to-zero, and direct Key Vault secret
   references suit a demo without AKS/Istio operational weight.

### What changes in the constitution (v0.1.0 → v0.2.0)
- **Principle V** "Python & FastAPI Standards" → **".NET & C# Standards"**: C#/.NET 10 (`net10.0`,
  `global.json` pin), nullable + implicit usings, **central package management**
  (`Directory.Packages.props`), `dotnet format` for style, Serilog + OpenTelemetry via a shared
  `src/shared/Observability` library, multi-stage **Alpine** Docker images running **non-root**,
  `/healthz` + `/readyz` probes. Python remains acceptable only for auxiliary scripts.
- **Principle II** layer paths: Experience = `src/ui-app` (React), Agents = `src/orchestration-api`
  (+ `agent-provisioner`), Mock Data = `src/mock-api`. The HTTP data seam is the mock API surface
  defined by `openapi/tools.yaml` (unchanged contract).
- **Principle VII / §17 / §21 Quality Gate**: `pytest`→`dotnet test` (xunit) + Vitest/RTL;
  `ruff`→`dotnet format` + ESLint/Prettier; add `terraform fmt/validate`; keep `gitleaks`.
- **Principle X** extension recipes re-expressed for C# (new scene = prompt + registry + DEMO composer
  + UI; new data source = JSON fixture + mock-API endpoint + tool wrapper + `openapi/tools.yaml`).

## Alternatives Considered

| Option | Pros | Cons |
|--------|------|------|
| **Keep Python/FastAPI (status quo)** | No amendment; constitution unchanged | Fails the explicit "Agent Framework and C#" + Foundry requirement; diverges from the reference repo |
| **C#/.NET + Foundry + Container Apps (chosen)** | Meets mandate; reference parity; Foundry-native, managed agents | Requires constitution amendment; new conventions to establish |
| **C#/.NET on AKS (exact reference)** | Closest to reference | User chose Container Apps; AKS/Istio is heavier than a demo needs |
| **Polyglot (Python agents + C# services)** | Reuses reference Python agent code | Splits the stack, contradicts "C#" requirement, two toolchains |

## Consequences

- **Positive**: Aligns with stakeholder intent and a proven reference architecture; unblocks the
  Demo 1 plan's Constitution Check; enables Foundry-managed/versioned agents.
- **Negative**: The Python-oriented constitution text, `requirements.txt`, and Python-centric
  copilot-instructions must be updated; new .NET/React/Terraform conventions must be documented.
- **Operational**: Implementation lives under `src/` (`orchestration-api`, `mock-api`,
  `agent-provisioner`, `ui-app`, `shared/Observability`), `infra/` (Terraform, ACA + Foundry),
  `tasks/` (Taskfiles), and `.github/workflows/` (CI/CD). The constitution header is bumped to
  **v0.2.0** with a §23 revision-history row referencing this ADR.
