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
