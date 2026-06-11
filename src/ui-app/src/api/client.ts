import axios from 'axios';

// All cockpit calls go through /api (proxied to the orchestration API by Vite in
// dev and nginx in prod). The frontend is mode-blind: DEMO and LIVE return the
// same MorningBrief shape (constitution Principle III).
export const apiClient = axios.create({
  baseURL: '/api',
  timeout: 30_000,
  headers: { 'Content-Type': 'application/json' },
});

export interface MorningBriefRequest {
  payload?: {
    eventId?: string;
    date?: string;
  };
}

// Scene response type mirrors contracts/morning-brief.schema.json.
export interface MarketStripItem {
  label: string;
  value: string;
  change?: string;
  direction: 'up' | 'down' | 'flat';
}

export interface ReasoningStep {
  text: string;
  status: 'done' | 'pending';
}

export interface MacroNarrative {
  summary: string;
  whyItMatters: string;
  sources: string[];
}

export interface AffectedClient {
  cid: string;
  name: string;
  tier: string;
  exposure: string;
  concern: { label: string; kind: 'sell' | 'warm' | 'info' };
  drivingEvents?: EventLinkage[];
}

export interface RankingRationale {
  walletScore: number;
  engagementScore: number;
  eventRelevanceScore: number;
  compositeScore: number;
  explanation: string;
}

export interface OutreachItem {
  rank: number;
  cid: string;
  name: string;
  suggestedTopic: string;
  talkingPoints: string[];
  rationale: RankingRationale;
}

// Reactive event cockpit (002). MarketEvent + per-item EventLinkage mirror the
// orchestration DTOs; both are additive and present in DEMO and LIVE alike.
export interface AffectedEntities {
  customerIds?: string[];
  tickers?: string[];
  sectors?: string[];
  issuers?: string[];
}

export interface MarketEvent {
  id: string;
  type: 'macro_rate' | 'sector' | 'issuer_credit' | 'client_headline';
  headline: string;
  summary: string;
  source?: string;
  severity: 'low' | 'medium' | 'high';
  publishedAt?: string;
  ingestedAt?: string;
  scope?: 'overnight' | 'intraday';
  origin?: 'seed' | 'admin' | 'feed';
  direction?: 'positive' | 'negative' | 'neutral';
  affectedEntities?: AffectedEntities;
}

export interface EventLinkage {
  eventId: string;
  headline: string;
  entityRef: string;
  contribution: number;
  rationale: string;
}

export interface MorningBrief {
  mode: 'DEMO' | 'LIVE';
  asOf: string;
  marketStrip: MarketStripItem[];
  reasoning: ReasoningStep[];
  macroNarrative: MacroNarrative;
  mostAffectedClients: AffectedClient[];
  outreach: OutreachItem[];
  notes?: string[];
  eventsConsidered?: MarketEvent[];
}

// Implemented end-to-end in Phase 3 (T021). Defined here so the scaffold compiles.
export async function runMorningBrief(req: MorningBriefRequest = {}): Promise<MorningBrief> {
  const { data } = await apiClient.post<MorningBrief>('/agent/morning-brief', req);
  return data;
}

// ---------------------------------------------------------------------------
// RM Daily Briefing — PRIMARY scene (Commercial Banking RM). Mirrors the
// orchestration-api RmBriefing DTO; DEMO and LIVE return the same shape.
// ---------------------------------------------------------------------------

export interface RmBriefingRequest {
  payload?: {
    rmId?: string;
    date?: string;
  };
}

export interface RmIdentity {
  rmId: string;
  name: string;
  title?: string;
  territory?: string;
}

export interface RmPortfolio {
  customerCount: number;
  totalExposureMm: number;
  totalDepositsMm: number;
}

export interface RmKpis {
  yesterdayTouchpoints: number;
  openPipelineCount: number;
  openPipelineAmountMm: number;
  closingWithin14Days: number;
  activeComplaints: number;
}

export type CallTagKind = 'escalated' | 'in-progress' | 'followup' | 'closing' | 'stuck' | 'event';

export interface CallTag {
  label: string;
  kind: CallTagKind;
}

export interface PriorityCall {
  rank: number;
  priority: number; // 1..4 colour band (red→green)
  customerId: string;
  customerName: string;
  industrySector?: string;
  hqCity?: string;
  state?: string;
  annualRevenueMm?: number;
  riskRating?: string;
  score: number;
  tags: CallTag[];
  reasons: string[];
  suggestedAction: string;
  drivingEvents?: EventLinkage[];
}

export interface ComplaintSnapshot {
  complaintId: string;
  customerName: string;
  category?: string;
  severity?: string;
  status: string;
  dateFiled?: string;
}

export interface PipelineClose {
  opportunityId: string;
  customerName: string;
  productType?: string;
  stage?: string;
  amountMm: number;
  expectedCloseDate?: string;
}

export interface MacroBullet {
  headline: string;
  detail: string;
}

export interface RmBriefing {
  mode: 'DEMO' | 'LIVE';
  asOf: string;
  greeting: string;
  rm: RmIdentity;
  portfolio: RmPortfolio;
  kpis: RmKpis;
  reasoning: ReasoningStep[];
  priorityCallList: PriorityCall[];
  complaintsSnapshot: ComplaintSnapshot[];
  pipelineClosing: PipelineClose[];
  macroSnapshot: MacroBullet[];
  suggestedFirstAction: string;
  notes?: string[];
  eventsConsidered?: MarketEvent[];
}

export async function runRmBriefing(req: RmBriefingRequest = {}): Promise<RmBriefing> {
  const { data } = await apiClient.post<RmBriefing>('/agent/rm-briefing', req);
  return data;
}

// ---------------------------------------------------------------------------
// Reactive live event stream (002 US2). The browser's native EventSource holds a
// long-lived SSE connection to /api/agent/{scene}/stream and receives a full
// re-synthesized DTO per update (reaction granularity, R7), so the page reconciles
// from the latest snapshot on reconnect. Mirrors contracts/live-update.schema.json.
// ---------------------------------------------------------------------------

export type LiveScene = 'rm-briefing' | 'morning-brief' | 'td-briefing';

export interface LiveAlert {
  priority: 'info' | 'notice' | 'urgent';
  headline: string;
  eventIds: string[];
  noImpact: boolean;
}

export interface LiveUpdate<TBriefing = RmBriefing | MorningBrief | TdBriefing> {
  sequence: number;
  scene: LiveScene;
  alert: LiveAlert;
  briefing: TBriefing;
}

export interface LiveSubscriptionHandlers<TBriefing> {
  onUpdate: (update: LiveUpdate<TBriefing>) => void;
  onReady?: (info: { sequence: number; scene: LiveScene }) => void;
  onError?: (err: Event) => void;
}

/**
 * Subscribe to the reactive SSE stream for a scene. Returns an unsubscribe function.
 * EventSource auto-reconnects and replays `Last-Event-ID`; the server answers a
 * reconnect with `ready` + a fresh snapshot, so no client-side delta tracking is needed.
 */
export function subscribeToEvents<TBriefing = RmBriefing | MorningBrief | TdBriefing>(
  scene: LiveScene,
  handlers: LiveSubscriptionHandlers<TBriefing>,
  options?: { persona?: string },
): () => void {
  const base = apiClient.defaults.baseURL ?? '/api';
  let query = '';
  if (options?.persona) {
    if (scene === 'rm-briefing') query = `?rmId=${encodeURIComponent(options.persona)}`;
    else if (scene === 'td-briefing') query = `?salespersonId=${encodeURIComponent(options.persona)}`;
  }
  const source = new EventSource(`${base}/agent/${scene}/stream${query}`);

  source.addEventListener('briefing-update', (event) => {
    try {
      const update = JSON.parse((event as MessageEvent).data) as LiveUpdate<TBriefing>;
      handlers.onUpdate(update);
    } catch {
      // Ignore malformed frames; the next full snapshot will reconcile state.
    }
  });

  if (handlers.onReady) {
    source.addEventListener('ready', (event) => {
      try {
        handlers.onReady!(JSON.parse((event as MessageEvent).data));
      } catch {
        /* non-fatal */
      }
    });
  }

  source.onerror = (err) => handlers.onError?.(err);

  return () => source.close();
}

// ---------------------------------------------------------------------------
// Admin news injection (002 US3). Posts through the orchestration proxy to the
// SAME ingestion endpoint a real intraday event uses (FR-016), so an injected item
// flows through the reactive SSE path and open briefings react within ~10s.
// ---------------------------------------------------------------------------

export interface AdminNewsSubmission {
  headline: string;
  summary: string;
  source?: string;
  severity: 'low' | 'medium' | 'high';
  type: 'macro_rate' | 'sector' | 'issuer_credit' | 'client_headline';
  direction?: 'positive' | 'negative' | 'neutral';
  affectedEntities: AffectedEntities;
  sceneTargeting?: LiveScene[];
}

/** Inject an operator-authored news item. Resolves to the stored intraday event. */
export async function ingestNews(submission: AdminNewsSubmission): Promise<MarketEvent> {
  const { data } = await apiClient.post<MarketEvent>('/events', submission);
  return data;
}

/** List the current event store (overnight seeds + injected intraday events). */
export async function listEvents(scope?: 'overnight' | 'intraday'): Promise<MarketEvent[]> {
  const { data } = await apiClient.get<MarketEvent[]>('/events', {
    params: scope ? { scope } : undefined,
  });
  return data;
}

/** A customer the admin can target in the News Desk affected-entities type-ahead. */
export interface CustomerOption {
  customerId: string;
  name: string;
}

/** Customer directory for the admin News Desk type-ahead (id + display name). */
export async function listCustomers(): Promise<CustomerOption[]> {
  const { data } = await apiClient.get<CustomerOption[]>('/customers');
  return data;
}

// ---------------------------------------------------------------------------
// Grounded Markets-Intelligence assistant ("AI Chat"). Stateless: the client replays
// the conversation each turn. DEMO returns a deterministic intent answer; LIVE returns
// a Foundry chat-agent answer grounded in the same systems-of-record (Principle III).
// ---------------------------------------------------------------------------

export interface ChatTurn {
  role: 'user' | 'assistant';
  content: string;
}

export interface ChatReply {
  mode: 'DEMO' | 'LIVE';
  message: string;
  suggestions?: string[];
  toolsUsed?: string[];
}

/** Send the conversation to the assistant and resolve the next assistant reply. */
export async function sendChat(messages: ChatTurn[], rmId?: string): Promise<ChatReply> {
  const { data } = await apiClient.post<ChatReply>('/chat', { messages, rmId });
  return data;
}

// ---------------------------------------------------------------------------
// Institutional Sales & Trading — "Morning Planning & Prioritized Outreach" for a
// coverage salesperson (Theo Wexler). Mirrors the orchestration-api TdBriefing DTO;
// DEMO and LIVE return the same shape (Principle III), grounded in /mock/td/*.
// ---------------------------------------------------------------------------

export interface TdBriefingRequest {
  payload?: {
    salespersonId?: string;
    date?: string;
  };
}

export interface SalespersonIdentity {
  salespersonId: string;
  name: string;
  desk?: string;
  coverage?: string;
  clientCount: number;
}

export interface MacroThemeBullet {
  theme: string;
  detail: string;
}

export interface TdEventConsidered {
  id: string;
  kind: string; // news | research
  headline: string;
  summary?: string;
  sector?: string;
  sentiment?: string; // Positive | Negative | Mixed | Neutral
  relatedSecurityId?: string;
  macroTheme?: string;
  timestamp?: string;
}

export interface InventoryAxe {
  securityId: string;
  securityName: string;
  assetClass?: string;
  sector?: string;
  inventorySize: number; // +long / -short
  axeSide: string; // buy | sell | two-way
  bidPrice?: number;
  offerPrice?: number;
  desk?: string;
  matchedClients: string[];
}

export interface WhyNowDriver {
  kind: string; // news | research | rfq | inquiry | holding | axe | crm
  label: string;
  detail?: string;
  securityId?: string;
  refId?: string;
}

export interface TradeIdea {
  securityId: string;
  securityName: string;
  side: string; // client-side: Buy | Sell
  rationale?: string;
  level?: string;
}

export interface TdCallRationale {
  newsRelevance: number;
  openRfqWeight: number;
  inquiryWeight: number;
  inventoryAxeMatch: number;
  urgency: number;
  compositeScore: number;
  explanation: string;
}

export interface TdPriorityCall {
  rank: number;
  priority: number; // 1..4 colour band (red→green)
  clientId: string;
  clientName: string;
  clientType?: string;
  region?: string;
  preferredAssetClass?: string;
  score: number;
  rationale: TdCallRationale;
  whyNow: WhyNowDriver[];
  talkingPoints: string[];
  tradeIdeas: TradeIdea[];
  personalNote?: string;
  suggestedAction: string;
  drivingEvents?: EventLinkage[];
}

export interface TdBriefing {
  mode: 'DEMO' | 'LIVE';
  asOf: string;
  greeting: string;
  salesperson: SalespersonIdentity;
  marketStrip: MarketStripItem[];
  macroThemes: MacroThemeBullet[];
  reasoning: ReasoningStep[];
  priorityCallList: TdPriorityCall[];
  inventoryAxes: InventoryAxe[];
  suggestedFirstAction: string;
  notes?: string[];
  eventsConsidered?: TdEventConsidered[];
  liveEvents?: MarketEvent[];
}

export async function runTdBriefing(req: TdBriefingRequest = {}): Promise<TdBriefing> {
  const { data } = await apiClient.post<TdBriefing>('/agent/td-briefing', req);
  return data;
}
