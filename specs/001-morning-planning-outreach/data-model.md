# Phase 1 Data Model: Morning Planning & Prioritized Outreach

**Feature**: `001-morning-planning-outreach` · **Date**: 2026-06-08

These are the **logical entities** for the morning-brief scene. They drive (a) the `MorningBrief`
response DTO returned by `POST /api/agent/morning-brief` (see `contracts/morning-brief.schema.json`)
and (b) the fictional JSON served by the mock API behind `openapi/tools.yaml`. All values are
fictional.

## Source-of-record entities (served by the mock API)

### Client  (`/mock/tableau/clients`, `/mock/tableau/clients/{cid}`)
| Field | Type | Notes |
|-------|------|-------|
| id (cid) | string | e.g., `ATLAS`, `BROOK`, `CEDAR` |
| name | string | e.g., "Atlas Pension" |
| tier | enum | `Tier 1` \| `Tier 2` \| `Tier 3` |
| revenueYtd | number | fictional revenue |
| shareOfWallet | number | 0–1 |
| rankings | object | desk/firm rank metrics |

### Engagement  (`/mock/dynamics/clients/{cid}/engagement`)
| Field | Type | Notes |
|-------|------|-------|
| cid | string | FK → Client |
| coverage | string | covering banker |
| last30d / last60d / last90d / last180d | object | touch counts / recency footprint |

### Axis / IOI  (`/mock/trading/axes`)
| Field | Type | Notes |
|-------|------|-------|
| id | string | axis id |
| instrument | string | e.g., "10Y swap", a cusip |
| side | enum | `bid` \| `offer` |
| size | number | notional |
| relevanceTags | string[] | e.g., `["duration","hedge"]` |

### Holding  (`/mock/trading/holdings?cusip|state|sector`)
| Field | Type | Notes |
|-------|------|-------|
| cid | string | FK → Client |
| cusip | string | instrument |
| sector / state | string | classification |
| exposureType | enum | `long-duration` \| `swap-book` \| `floating-rate` \| ... |

### MarketData  (`/mock/marketdata`, `/mock/marketdata/relval/{event_id}`)
| Field | Type | Notes |
|-------|------|-------|
| asOf | string (ISO) | timestamp for the market strip |
| rates | object | e.g., 10y/2y UST levels + change (bp) |
| equities | object | e.g., S&P futures % |
| credit | object | IG/HY spread change |
| tone | string | e.g., "Risk-off" |

### NewsEvent  (`/mock/news/{event_id}`)
| Field | Type | Notes |
|-------|------|-------|
| eventId | string | e.g., `fed_surprise_hike` |
| headline | string | fictional |
| entities / sectors / states | string[] | resolved impact dimensions |

### NewIssue  (`/mock/calendar/newissues`)
| Field | Type | Notes |
|-------|------|-------|
| id / issuer / sector / state | string | fictional deal |
| size / maturity / px talk | various | calendar detail |

### Coalition  (`/mock/coalition/{cid}`, `/mock/coalition/sector/{sector}`)
| Field | Type | Notes |
|-------|------|-------|
| cid or sector | string | scope |
| rank | number | competitive rank |
| capture / miss | number | wallet captured vs missed |

## Scene/response entities (returned by the orchestration API)

### MorningBrief  *(root response)*
| Field | Type | Notes |
|-------|------|-------|
| mode | enum | `DEMO` \| `LIVE` (informational; shape identical) |
| asOf | string (ISO) | from MarketData.asOf |
| marketStrip | MarketStripItem[] | the top ticker strip |
| reasoning | ReasoningStep[] | the agent's shown steps |
| macroNarrative | MacroNarrative | event analysis text + sources |
| mostAffectedClients | AffectedClient[] | flagged by rate sensitivity |
| outreach | OutreachItem[] | ranked call list (ordered) |
| notes | string[] | optional degradation/empty-state notes |

### MarketStripItem
`label` (string), `value` (string), `change` (string, optional), `direction` (`up`\|`down`\|`flat`).

### ReasoningStep
`text` (string), `status` (`done`\|`pending`).

### MacroNarrative
`summary` (string), `whyItMatters` (string), `sources` (string[]).

### AffectedClient
`cid` (string), `name` (string), `tier` (string), `exposure` (string),
`concern` (object: `label` string, `kind` enum `sell`\|`warm`\|`info`).

### OutreachItem
| Field | Type | Notes |
|-------|------|-------|
| rank | integer | 1-based ordering |
| cid | string | FK → Client |
| name | string | display name |
| suggestedTopic | string | one-line topic |
| talkingPoints | string[] | personalized points (event + axes/holdings) |
| rationale | RankingRationale | why this rank |

### RankingRationale
`walletScore` (number 0–1), `engagementScore` (number 0–1), `eventRelevanceScore` (number 0–1),
`compositeScore` (number 0–1), `explanation` (string). The composite is a documented weighted blend
(default weights 0.4 wallet / 0.3 engagement / 0.3 event relevance — tunable, documented in code).

### CallPlan  *(UI-side, derived from `outreach`; demo-only, not persisted server-side)*
`items` (ordered subset/edit of OutreachItem with an editable `note`), `approvalState`
(`draft`\|`edited`\|`approved`), `sent` (always `false` — no outbound action).

## Relationships

```
NewsEvent ──resolves──▶ entities/sectors/states ──▶ Holding ──FK──▶ Client
MarketData ──context──▶ MacroNarrative
Client ──1:1──▶ Engagement, Coalition ; Client ──1:n──▶ Holding
OutreachItem ──FK──▶ Client ; OutreachItem.rationale uses Client(wallet)+Engagement+event relevance
MorningBrief = MacroNarrative + AffectedClient[] + OutreachItem[]
CallPlan (UI) derived from MorningBrief.outreach
```

## Validation rules
- `outreach` MUST be sorted by `rank` ascending; `rank` unique and contiguous from 1.
- Every `OutreachItem.cid` and `AffectedClient.cid` MUST exist in the mock client set.
- `compositeScore` MUST equal the documented weighted blend of its component scores (±epsilon).
- DEMO mode MUST produce identical output for identical inputs (deterministic ordering, fixed text).
- `marketStrip[*].direction` ∈ {`up`,`down`,`flat`}; `concern.kind` ∈ {`sell`,`warm`,`info`}.
- Empty-state: if no client is materially affected, `mostAffectedClients`=[] and `outreach`=[] with a
  `notes` entry explaining why (no error).
