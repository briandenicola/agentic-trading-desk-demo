// ---------------------------------------------------------------------------
// M.INT workspace shell — representative fictional data (front-end first).
// Mirrors assets/Designer Layout.png. All values are fictional; panels that map
// to real backend data (e.g. the command bar → /api/chat) are wired separately.
// ---------------------------------------------------------------------------

import { mint } from '../../theme/theme';

export type AlertPriority = 'HIGH' | 'MEDIUM' | 'LOW' | 'DONE';

export type NavIcon =
  | 'home'
  | 'scenes'
  | 'agents'
  | 'prompts'
  | 'scheduled'
  | 'data'
  | 'watchlists'
  | 'settings';

export interface NavItem {
  label: string;
  icon: NavIcon;
  to?: string;
}

export type QuickActionIcon = 'prompt' | 'panel' | 'playbook' | 'agents';

export interface FeedItem {
  id: string;
  priority: AlertPriority;
  title: string;
  subtitle: string;
  body: string;
  time: string;
  icon: 'chat' | 'market' | 'fed' | 'task' | 'trade';
}

export interface ExposureSegment {
  label: string;
  pct: number;
  color: string;
}

export interface Holding {
  ticker: string;
  name?: string;
  weight: number;
  ytd: number;
}

export interface RiskFactor {
  label: string;
  level: 'High' | 'Moderate' | 'Low';
}

export interface AxesOffering {
  opportunity: string;
  relevance: 'High' | 'Medium' | 'Low';
}

export interface MatchedNews {
  id: string;
  source: 'Bloomberg' | 'WSJ' | 'Reuters' | 'CNBC';
  time: string;
  headline: string;
  tagLabel: string;
  tagKind: 'impacts' | 'holds' | 'macro';
  category: 'Markets' | 'Holdings' | 'Economy' | 'Regulatory';
}

export interface ActivityItem {
  id: string;
  title: string;
  subtitle: string;
  time: string;
  icon: 'chat' | 'view' | 'download';
}

export interface Playbook {
  id: string;
  name: string;
  description: string;
  icon: 'open' | 'client' | 'risk' | 'trade' | 'earnings';
  active?: boolean;
}

export interface UpcomingEvent {
  id: string;
  time: string;
  label: string;
}

export const workspace = {
  user: { name: 'James Carter', role: 'CIO, Apex Capital' },

  tagline: {
    line1: 'AI-DRIVEN MARKETS INTELLIGENCE.',
    line2: 'THE RIGHT CONTEXT. IN REAL TIME.',
    sub: 'M.INT scans the markets, matches what matters to you and your clients, and configures your workspace so you can act with confidence.',
  },

  nav: [
    { label: 'Home', icon: 'home', to: '/' },
    { label: 'Scenes / Playbooks', icon: 'scenes', to: '/cockpit' },
    { label: 'Agents', icon: 'agents' },
    { label: 'Prompts', icon: 'prompts' },
    { label: 'Scheduled Tasks', icon: 'scheduled' },
    { label: 'Data Sources', icon: 'data' },
    { label: 'Watchlists', icon: 'watchlists' },
    { label: 'Settings', icon: 'settings' },
  ] as NavItem[],

  activeScene: { name: 'Client Engagement', startedAt: '9:41 AM' },

  quickActions: [
    { label: 'New Prompt', icon: 'prompt' },
    { label: 'Add Panel', icon: 'panel' },
    { label: 'Create Playbook', icon: 'playbook' },
    { label: 'Manage Agents', icon: 'agents' },
  ] as { label: string; icon: QuickActionIcon }[],

  feed: [
    {
      id: 'f1',
      priority: 'HIGH',
      icon: 'chat',
      title: 'Incoming Client Chat',
      subtitle: 'Acme Capital – John Smith',
      body: 'Question about portfolio performance and volatility.',
      time: '9:41 AM',
    },
    {
      id: 'f2',
      priority: 'MEDIUM',
      icon: 'market',
      title: 'Market Move Detected',
      subtitle: 'Tech Sector Volatility',
      body: 'Semiconductor stocks down 2.3% on export restrictions headlines.',
      time: '9:32 AM',
    },
    {
      id: 'f3',
      priority: 'LOW',
      icon: 'fed',
      title: 'Fed Speakers Today',
      subtitle: 'Multiple Fed speakers on deck',
      body: 'Including Powell at 2:00 PM ET.',
      time: '9:21 AM',
    },
    {
      id: 'f4',
      priority: 'DONE',
      icon: 'task',
      title: 'Scheduled Task Complete',
      subtitle: 'Daily Portfolio Summary',
      body: 'Your daily summary is ready.',
      time: '9:15 AM',
    },
    {
      id: 'f5',
      priority: 'DONE',
      icon: 'trade',
      title: 'Trade Alert',
      subtitle: 'Large Buy in MSFT',
      body: '$2.3M block trade detected.',
      time: '9:05 AM',
    },
  ] as FeedItem[],

  conversation: {
    prompt: "Show me Acme Capital's exposure, recent performance, market risks, and relevant news.",
    reply:
      'Here is the full context for Acme Capital, including portfolio exposure, recent performance, key risks, relevant news, and suggested actions.',
    time: '9:41 AM',
  },

  client: {
    name: 'Acme Capital',
    contact: 'John Smith',
    relationshipSince: 'Jan 2022',
    aum: '$125.4M',
    riskProfile: 'Moderate',
    objective: 'Long-term growth',
  },

  exposure: {
    total: '$125.4M',
    totalLabel: 'Total AUM',
    segments: [
      { label: 'Equities', pct: 52, color: mint.cyan },
      { label: 'Fixed Income', pct: 24, color: mint.violet },
      { label: 'Alternatives', pct: 12, color: mint.violetBright },
      { label: 'Cash', pct: 8, color: mint.green },
      { label: 'Other', pct: 4, color: mint.textDim },
    ] as ExposureSegment[],
  },

  performance: {
    rows: [
      { label: 'YTD', value: '+12.45%' },
      { label: '1Y', value: '+18.32%' },
      { label: 'Since Inception', value: '+45.67%' },
    ],
    // Monthly net return path used to draw the sparkline (Jan→Jun, percent).
    series: [2.1, 4.8, 3.2, 7.6, 9.9, 12.45],
    months: ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun'],
  },

  holdings: [
    { ticker: 'MSFT', name: 'Microsoft', weight: 8.7, ytd: 18.3 },
    { ticker: 'NVDA', name: 'NVIDIA', weight: 6.2, ytd: 142.6 },
    { ticker: 'AAPL', name: 'Apple', weight: 5.1, ytd: 12.1 },
    { ticker: 'IVV', name: 'iShares Core S&P 500 ETF', weight: 4.8, ytd: 10.2 },
    { ticker: 'BND', name: 'Vanguard Total Bond Market', weight: 4.3, ytd: 2.1 },
  ] as Holding[],

  risk: {
    score: 68,
    band: 'Moderate',
    factors: [
      { label: 'Market Volatility', level: 'High' },
      { label: 'Concentration', level: 'Moderate' },
      { label: 'Interest Rate Sensitivity', level: 'High' },
      { label: 'Liquidity', level: 'Low' },
    ] as RiskFactor[],
  },

  insights: [
    'Tech sector volatility increased due to export restrictions.',
    'Your portfolio is well positioned for long-term growth.',
    'Consider reviewing exposure to semiconductors given elevated volatility.',
  ],

  axes: [
    { opportunity: 'Tax-Loss Harvesting', relevance: 'High' },
    { opportunity: 'ESG Screening Update', relevance: 'High' },
    { opportunity: 'Private Credit Solution', relevance: 'Medium' },
    { opportunity: 'Defensive Equity Overlay', relevance: 'Medium' },
    { opportunity: 'Currency Hedging Review', relevance: 'Low' },
  ] as AxesOffering[],

  matchedNews: [
    {
      id: 'n1',
      source: 'Bloomberg',
      time: '2m ago',
      headline: 'Semiconductor Export Curbs Could Impact Tech Earnings, Morgan Stanley Says',
      tagLabel: 'IMPACTS: NVDA, AMD, ASML',
      tagKind: 'impacts',
      category: 'Markets',
    },
    {
      id: 'n2',
      source: 'WSJ',
      time: '15m ago',
      headline: "Fed's Powell: Policy in Good Position, Data Dependent Approach Ahead",
      tagLabel: 'MACRO ECONOMY',
      tagKind: 'macro',
      category: 'Economy',
    },
    {
      id: 'n3',
      source: 'Reuters',
      time: '32m ago',
      headline: 'Microsoft Cloud Growth Accelerates Across Key Industries',
      tagLabel: 'HOLDS: MSFT',
      tagKind: 'holds',
      category: 'Holdings',
    },
  ] as MatchedNews[],

  newsTabs: ['All', 'Markets', 'Holdings', 'Economy', 'Regulatory'] as const,

  activity: [
    { id: 'a1', icon: 'chat', title: 'Incoming Client Chat', subtitle: 'John Smith', time: '9:41 AM' },
    { id: 'a2', icon: 'view', title: 'Portfolio Update Viewed', subtitle: 'Acme Capital Team', time: '9:35 AM' },
    { id: 'a3', icon: 'download', title: 'Report Downloaded', subtitle: 'Q2 Performance Report', time: '9:28 AM' },
  ] as ActivityItem[],

  playbooks: [
    { id: 'p1', name: 'Market Open', icon: 'open', description: 'Daily market open briefing with key signals and levels.' },
    { id: 'p2', name: 'Client Engagement', icon: 'client', description: 'Client 360 view with portfolio, activity and insights.', active: true },
    { id: 'p3', name: 'Risk Monitoring', icon: 'risk', description: 'Monitor risk, exposures and portfolio health.' },
    { id: 'p4', name: 'Trade Execution', icon: 'trade', description: 'Execute trades with real-time market and portfolio context.' },
    { id: 'p5', name: 'Earnings Season', icon: 'earnings', description: 'Track earnings, impacts and portfolio implications.' },
  ] as Playbook[],

  events: [
    { id: 'e1', time: '2:00 PM ET', label: 'Fed Chair Powell Speaks' },
    { id: 'e2', time: '3:30 PM ET', label: 'Economic Data: CPI' },
    { id: 'e3', time: 'Tomorrow 8:30 AM ET', label: 'PPI Report' },
  ] as UpcomingEvent[],

  priorityAlert: {
    title: 'Incoming Client Chat',
    subtitle: 'Acme Capital – John Smith',
    body: 'Client asking about portfolio performance and recent volatility.',
    time: '9:41 AM',
  },

  processSteps: ['Scan', 'Detect', 'Understand', 'Match', 'Configure', 'Act'],
} as const;

export type Workspace = typeof workspace;
