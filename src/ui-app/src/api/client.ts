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

export interface MorningBrief {
  mode: 'DEMO' | 'LIVE';
  asOf: string;
  marketStrip: MarketStripItem[];
  reasoning: ReasoningStep[];
  macroNarrative: MacroNarrative;
  mostAffectedClients: AffectedClient[];
  outreach: OutreachItem[];
  notes?: string[];
}

// Implemented end-to-end in Phase 3 (T021). Defined here so the scaffold compiles.
export async function runMorningBrief(req: MorningBriefRequest = {}): Promise<MorningBrief> {
  const { data } = await apiClient.post<MorningBrief>('/agent/morning-brief', req);
  return data;
}
