# Feature Specification: Morning Planning & Prioritized Outreach (Interactive Demo)

**Feature Branch**: `001-morning-planning-outreach`
**Created**: 2026-06-08
**Status**: Draft
**Input**: User description: "Interactive demo of Morning Planning and Prioritized Outreach using
Microsoft Agent Framework agents in Azure AI Foundry, with a React UI and C# mock API on Azure
Container Apps, following the framework, coding, and design of
https://github.com/briandenicola/online-banking-demo (Container Apps instead of AKS)."

## Overview

Turn the static **Demo 1 — Morning Pre-Market Planning** storyboard
(`mockup/demos/01-morning-prep.html`) into a working interactive demo. A Municipal Sales VP asks
*"What do I need to know this morning?"* and an AI agent flow produces a market narrative, flags the
clients most affected by overnight events, ranks an outreach list, drafts personalized talking
points, and presents an **editable, human-in-the-loop** call plan. All data is **fictional**.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Run the morning brief (Priority: P1)

As a Municipal Sales VP, when I arrive pre-market I ask "What do I need to know this morning?" and
the assistant returns a single brief: a macro narrative of overnight events, the clients most
affected (with the reason), and a ranked outreach list with suggested topics — so I can start
dialing with context instead of assembling it manually.

**Why this priority**: This is the core demo. Without the end-to-end brief there is no product to
show. It is the MVP slice that delivers the headline value (assembled situational awareness).

**Independent Test**: Trigger the morning brief from the UI (or `POST /api/agent/morning-brief`) and
verify the response contains a macro narrative, a most-affected-clients list, and a ranked outreach
list — rendered in the cockpit matching the mockup.

**Acceptance Scenarios**:

1. **Given** the demo is loaded, **When** the VP clicks "Run morning brief", **Then** the cockpit
   shows agent reasoning steps, a macro event narrative, a most-affected-clients table, and a ranked
   outbound-priority table — sourced only through the mock system-of-record APIs.
2. **Given** an overnight macro event (e.g., a surprise Fed move), **When** the brief runs, **Then**
   the narrative summarizes the rate/equity/credit reaction and explains *why it matters*.
3. **Given** client portfolios with differing rate sensitivity, **When** the brief runs, **Then**
   clients are flagged by exposure (e.g., long-duration, swap book, floating-rate) with a concern tag.

### User Story 2 - Prioritized outreach ranking with talking points (Priority: P1)

As a VP, I need the outreach list ranked by a blend of wallet, recent engagement, and today's event
relevance, each with a concrete suggested topic and personalized talking points, so the highest-value
calls surface first.

**Why this priority**: "Prioritized Outreach" is explicitly named in the request and is the
decision-support payoff of the brief. P1 alongside US1.

**Independent Test**: Inspect the ranked outreach output and confirm each entry has a rank, client,
suggested topic, and talking points, and that ordering reflects the documented ranking factors.

**Acceptance Scenarios**:

1. **Given** the brief has run, **When** the outreach list renders, **Then** each priority client
   shows a rank (#), a suggested topic, and talking points tied to today's event and that client's
   relevant axes/holdings.
2. **Given** two clients with similar exposure, **When** ranked, **Then** the one with higher wallet
   and recent engagement ranks higher, and the rationale is inspectable.

### User Story 3 - Human-in-the-loop plan editing (Priority: P2)

As a VP, I can edit the generated call plan — reorder, remove a client, or add a personal note —
and approve it before any action is taken; nothing is sent automatically.

**Why this priority**: Reinforces trust and the "assistant, not autopilot" framing. Valuable but the
brief is demonstrable without it, so P2.

**Independent Test**: Edit a plan entry and remove a client in the UI, then approve; confirm the
approved plan reflects the edits and no outbound action is triggered.

**Acceptance Scenarios**:

1. **Given** a generated plan, **When** the VP edits a talking point or removes a client, **Then** the
   plan updates in place and shows an "edited" state.
2. **Given** an edited plan, **When** the VP clicks Approve, **Then** the plan is marked approved and
   the demo confirms no message was sent automatically.

### User Story 4 - Deterministic demo mode for on-stage reliability (Priority: P2)

As a presenter, I can run the demo in a deterministic mode that produces the same brief every time
without depending on a live model call, so an on-stage run never fails on a flaky LLM call.

**Why this priority**: Live LLM calls are the #1 on-stage failure point. A deterministic mode makes
the demo safe to present; LIVE (Foundry) mode is for real reasoning.

**Independent Test**: With demo mode enabled and no model credentials, trigger the brief and confirm a
complete, repeatable response with the same JSON shape as LIVE mode.

**Acceptance Scenarios**:

1. **Given** DEMO mode is enabled, **When** the brief runs with no Foundry credentials, **Then** a
   complete, deterministic brief is returned.
2. **Given** the same inputs, **When** the brief runs twice in DEMO mode, **Then** the output is
   identical.
3. **Given** LIVE and DEMO modes, **When** each returns a brief, **Then** the JSON shape is identical
   so the frontend is mode-blind.

### Edge Cases

- **No qualifying clients**: If no client portfolio is materially affected, the brief states that and
  still returns a (possibly empty) ranked list rather than failing.
- **Tool/data source unavailable**: If a mock API call fails, the agent surfaces a structured error
  and degrades gracefully (partial brief with a note), never an HTML error page.
- **Model/tool loop runaway (LIVE)**: Tool-calling is capped (max hops) to prevent runaway loops.
- **Unknown event id**: A market event the agent cannot resolve yields a clear "could not resolve
  event" message, not a crash.
- **Stale market strip**: The market data strip shows the as-of timestamp of the fictional data.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST expose a single agent entry point that produces a "morning brief" for a
  given trading day / market event, returning macro narrative, most-affected clients, and a ranked
  outreach list in one response.
- **FR-002**: Agents MUST obtain all data exclusively through the mock system-of-record APIs defined
  in `openapi/tools.yaml` (client value, engagement, axes, holdings, new issues, market data,
  relative value, news, coalition) — never by reading fixtures in-process.
- **FR-003**: The morning brief MUST include a macro event analysis narrative summarizing the
  overnight rate/equity/credit reaction and an explanation of why it matters.
- **FR-004**: The system MUST identify the clients most affected by the event, each with an exposure
  description and a concern tag, based on portfolio rate sensitivity.
- **FR-005**: The system MUST rank outreach by a documented blend of share-of-wallet, recent
  engagement, and event relevance, and expose the ranking rationale.
- **FR-006**: Each ranked outreach entry MUST include a suggested topic and personalized talking
  points tied to the event and the client's relevant axes/holdings.
- **FR-007**: The UI MUST present an editable call plan supporting reorder, remove-client, edit-note,
  and explicit approval, and MUST NOT trigger any real outbound action (demo only).
- **FR-008**: The system MUST support a deterministic DEMO mode that runs offline with no model
  credentials and is the default for on-stage use.
- **FR-009**: The system MUST support a LIVE mode in which agents run in Azure AI Foundry via the
  Microsoft Agent Framework, performing tool-calling against the mock APIs.
- **FR-010**: LIVE and DEMO modes MUST return the same JSON shape per scene so the frontend is
  mode-blind.
- **FR-011**: The system MUST surface API/tool errors as structured JSON (not HTML error pages) and
  cap tool-calling hops in LIVE mode.
- **FR-012**: The agent tool contract MUST remain `openapi/tools.yaml`; any new tool MUST be added to
  both the tool implementation and the OpenAPI spec, and be importable into Azure AI Foundry.
- **FR-013**: All clients, holdings, revenue, rankings, axes, and headlines MUST be fictional; no real
  market-data vendor may be wired in.
- **FR-014**: The system MUST NOT hardcode secrets; Azure/Foundry configuration MUST come from
  environment/Key Vault, and containers MUST run as non-root.
- **FR-015**: The frontend MUST render the Demo 1 cockpit (market strip, agent reasoning, macro
  narrative, most-affected clients, ranked outreach, editable plan) consistent with the mockup.

### Key Entities *(include if feature involves data)*

- **Market Event**: An overnight macro event (e.g., surprise Fed move) with a reaction across rates,
  equities, and credit; resolves into affected entities/sectors/states.
- **Client**: A fictional municipal-sales client with tier, share-of-wallet, revenue/rankings,
  engagement footprint, holdings, and coalition/competitive benchmarking.
- **Portfolio Exposure**: A client's sensitivity to the event (e.g., long-duration bonds, swap book,
  floating-rate notes) with a concern tag.
- **Axis / IOI**: A live trading axis the desk can offer (e.g., a 10Y swap) relevant to a talking point.
- **Outreach Item**: A ranked call recommendation = client + rank + suggested topic + talking points +
  ranking rationale.
- **Call Plan**: The ordered, editable set of outreach items for the day, with approval state.
- **Morning Brief**: The composite response = macro narrative + most-affected clients + ranked
  outreach (+ reasoning trace).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A presenter can run the full morning brief from the UI and see narrative, most-affected
  clients, and ranked outreach in under 10 seconds in DEMO mode.
- **SC-002**: DEMO mode produces byte-identical brief output across repeated runs with no model
  credentials configured.
- **SC-003**: LIVE and DEMO responses validate against the same response schema (100% shape parity)
  for the morning-brief scene.
- **SC-004**: A user can edit and approve the call plan (reorder, remove, add note) entirely in the UI
  with zero outbound actions triggered.
- **SC-005**: The deployed demo is reachable on a public HTTPS endpoint, with the UI and mock API
  running in Azure Container Apps and agents hosted in Azure AI Foundry.
- **SC-006**: All data shown is fictional, and a secret scan of the repository is clean.
- **SC-007**: 100% of agent data access flows through the `openapi/tools.yaml` mock APIs (no
  in-process fixture reads).

## Assumptions

- **Scope is Demo 1 only** — Morning Planning + Prioritized Outreach as a single agent flow. Demos
  2–5 are out of scope for this feature.
- The implementation stack is fixed by stakeholder decision: **.NET 10 / C# with Microsoft Agent
  Framework**, agents hosted in **Azure AI Foundry**, a **React** UI, a **C# mock API**, all on
  **Azure Container Apps**, provisioned with **Terraform** and deployed via **GitHub Actions → ACR →
  Container Apps**, with **Entra ID** auth and **Key Vault** secrets — modeled on
  `briandenicola/online-banking-demo` (Container Apps instead of AKS).
- The existing `openapi/tools.yaml` is the authoritative tool contract; the mock API implements those
  operations and the fictional JSON data is authored as part of this feature (no `api/` or mock data
  exists yet in the repo).
- The static mockup `mockup/demos/01-morning-prep.html` is the visual/UX reference for the React UI.
- The project constitution (currently Python/FastAPI) will be amended to the C#/.NET stack via an ADR
  before implementation lands (per constitution §22).
- "Human-in-the-loop" actions are demo-only; no integration with real CRM/dialer/email systems.
- A single representative trading day / market event scenario is sufficient to demonstrate the flow.
