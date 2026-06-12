import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import type { AxiosResponse } from 'axios';
import CssBaseline from '@mui/material/CssBaseline';
import { ThemeProvider } from '@mui/material/styles';
import { MemoryRouter } from 'react-router-dom';
import { apiClient, type TdBriefing } from '../../api/client';
import { theme } from '../../theme/theme';
import TradeDeskScene from './TradeDeskScene';
import { clearPersistentState } from '../../hooks/usePersistentState';

const mockBrief: TdBriefing = {
  mode: 'DEMO',
  asOf: '2026-05-22',
  greeting: 'Good morning, Theo',
  salesperson: {
    salespersonId: 'Theo Wexler',
    name: 'Theo Wexler',
    desk: 'Institutional Sales',
    coverage: 'Hedge-fund book',
    clientCount: 4,
  },
  marketStrip: [{ label: 'QTZ', value: '162.55', change: 'AI Capex Cycle', direction: 'up' }],
  macroThemes: [{ theme: 'AI / Datacenter Boom', detail: 'Hyperscaler capex guidance lifted overnight.' }],
  reasoning: [
    { text: 'Pulled Theo\u2019s book and 90 days of activity.', status: 'done' },
    { text: 'Ranked the client call list.', status: 'done' },
  ],
  priorityCallList: [
    {
      rank: 1,
      priority: 1,
      clientId: 'CL-2006',
      clientName: 'Tradewinds Partners',
      clientType: 'Hedge Fund',
      region: 'US',
      preferredAssetClass: 'Equity',
      score: 364,
      rationale: {
        newsRelevance: 100,
        openRfqWeight: 52,
        inquiryWeight: 44,
        inventoryAxeMatch: 60,
        urgency: 22,
        compositeScore: 100,
        explanation: 'Deep AI-basket exposure plus an open RFQ.',
      },
      whyNow: [
        { kind: 'news', label: 'Positive news on Nimbus Cloud: AI-capex supercycle', refId: 'NEWS-1002' },
        { kind: 'axe', label: 'We are axed to buy Nimbus Cloud (desk short 10mm)', securityId: 'SEC-3002' },
      ],
      talkingPoints: ['Lead with the tape: AI-capex supercycle. Frame the read-through for their book.'],
      tradeIdeas: [
        { securityId: 'SEC-3002', securityName: 'Nimbus Cloud', side: 'Buy', level: '214.0 / 214.4' },
      ],
      personalNote: 'Wants colour on the AI basket.',
      suggestedAction: 'Call Tradewinds on the overnight tape and bring an actionable idea.',
      drivingEvents: [
        {
          eventId: 'evt-capex',
          headline: 'AI-capex supercycle',
          entityRef: 'CL-2006',
          contribution: 45,
          rationale: 'High-severity event hits 4 names in this client\u2019s book.',
        },
      ],
    },
    {
      rank: 2,
      priority: 2,
      clientId: 'CL-2001',
      clientName: 'Hyperion Capital',
      clientType: 'Hedge Fund',
      score: 322,
      rationale: {
        newsRelevance: 100,
        openRfqWeight: 20,
        inquiryWeight: 36,
        inventoryAxeMatch: 60,
        urgency: 22,
        compositeScore: 100,
        explanation: 'AI-basket holder.',
      },
      whyNow: [{ kind: 'research', label: 'Upgrade on Quartzite', refId: 'RES-2002' }],
      talkingPoints: ['Walk through desk research on Quartzite.'],
      tradeIdeas: [],
      suggestedAction: 'Call Hyperion with the research read-through.',
    },
  ],
  inventoryAxes: [
    {
      securityId: 'SEC-3002',
      securityName: 'Nimbus Cloud Holdings',
      assetClass: 'Equity',
      sector: 'Technology',
      inventorySize: -10000,
      axeSide: 'buy',
      bidPrice: 214.0,
      offerPrice: 214.4,
      desk: 'Equities',
      matchedClients: ['Hyperion Capital', 'Tradewinds Partners'],
    },
  ],
  suggestedFirstAction: 'Start with Tradewinds Partners — AI-capex print plus our short axe.',
  eventsConsidered: [
    {
      id: 'NEWS-1002',
      kind: 'news',
      headline: 'AI-capex supercycle',
      summary: 'Hyperscalers lifted capex guidance.',
      sector: 'Technology',
      sentiment: 'Positive',
    },
  ],
};

function renderScene() {
  render(
    <ThemeProvider theme={theme}>
      <CssBaseline />
      <MemoryRouter>
        <TradeDeskScene />
      </MemoryRouter>
    </ThemeProvider>,
  );
}

afterEach(() => {
  cleanup();
  clearPersistentState();
  vi.restoreAllMocks();
});

describe('TradeDeskScene', () => {
  it('auto-runs the briefing and renders the prioritized client call list', async () => {
    const postSpy = vi.spyOn(apiClient, 'post').mockResolvedValue({
      data: mockBrief,
    } as AxiosResponse<TdBriefing>);

    renderScene();

    await screen.findByTestId('td-briefing');
    expect(postSpy).toHaveBeenCalledWith(
      '/agent/td-briefing',
      { payload: { salespersonId: 'Theo Wexler', date: '2026-05-22' } },
      expect.objectContaining({ timeout: expect.any(Number) }),
    );

    expect(screen.getByText('Good morning, Theo')).toBeInTheDocument();
    expect(screen.getByTestId('td-call-CL-2006')).toBeInTheDocument();
    expect(screen.getByTestId('td-call-CL-2001')).toBeInTheDocument();
    expect(screen.getAllByText(/Tradewinds Partners/).length).toBeGreaterThan(0);
    expect(screen.getByTestId('td-axe-board')).toBeInTheDocument();
  });

  it('surfaces the live driving-events callout on a re-ranked call', async () => {
    vi.spyOn(apiClient, 'post').mockResolvedValue({ data: mockBrief } as AxiosResponse<TdBriefing>);

    renderScene();

    await screen.findByTestId('td-briefing');
    expect(screen.getByTestId('td-driving-events-CL-2006')).toBeInTheDocument();
    expect(screen.getByText(/RE-RANKED BY LIVE EVENTS/)).toBeInTheDocument();
  });

  it('does not show the degraded banner for a normal briefing', async () => {
    vi.spyOn(apiClient, 'post').mockResolvedValue({ data: mockBrief } as AxiosResponse<TdBriefing>);

    renderScene();

    await screen.findByTestId('td-briefing');
    expect(screen.queryByTestId('td-degraded-banner')).not.toBeInTheDocument();
  });

  it('shows an honest degraded banner and DEGRADED chip when the LIVE agent fell back', async () => {
    const degradedBrief: TdBriefing = {
      ...mockBrief,
      mode: 'LIVE',
      degraded: true,
      degradedReason: 'LIVE agent run failed: Foundry endpoint unreachable.',
    };
    vi.spyOn(apiClient, 'post').mockResolvedValue({ data: degradedBrief } as AxiosResponse<TdBriefing>);

    renderScene();

    await screen.findByTestId('td-briefing');
    expect(screen.getByTestId('td-degraded-banner')).toBeInTheDocument();
    expect(screen.getByText(/LIVE agent unavailable/)).toBeInTheDocument();
    expect(screen.getByText(/Foundry endpoint unreachable/)).toBeInTheDocument();
    expect(screen.getByText('LIVE · DEGRADED')).toBeInTheDocument();
  });

  it('opens a trading-grounded chat seeded from a call card and posts the salesperson context', async () => {
    const postSpy = vi.spyOn(apiClient, 'post').mockImplementation((url: string) => {
      if (url === '/chat') {
        return Promise.resolve({
          data: { mode: 'DEMO', message: 'Tradewinds holds the AI basket and has an open RFQ.' },
        } as AxiosResponse);
      }
      return Promise.resolve({ data: mockBrief } as AxiosResponse<TdBriefing>);
    });

    renderScene();
    await screen.findByTestId('td-briefing');

    // The call card exposes an "Open Chat" affordance grounded in the trading book.
    fireEvent.click(screen.getByRole('button', { name: /Open chat about Tradewinds Partners/i }));

    // The composer is pre-seeded with that client's context.
    const input = (await screen.findByPlaceholderText(/Ask about a client/i)) as HTMLInputElement;
    expect(input.value).toContain('Tradewinds Partners (CL-2006)');

    fireEvent.click(screen.getByRole('button', { name: /send message/i }));

    await waitFor(() =>
      expect(postSpy).toHaveBeenCalledWith(
        '/chat',
        expect.objectContaining({ salespersonId: 'Theo Wexler' }),
        expect.objectContaining({ timeout: expect.any(Number) }),
      ),
    );
    expect(await screen.findByText(/Tradewinds holds the AI basket/)).toBeInTheDocument();
  });
});
