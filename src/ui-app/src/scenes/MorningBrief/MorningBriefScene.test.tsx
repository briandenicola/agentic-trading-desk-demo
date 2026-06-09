import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import type { AxiosResponse } from 'axios';
import CssBaseline from '@mui/material/CssBaseline';
import { ThemeProvider } from '@mui/material/styles';
import { MemoryRouter } from 'react-router-dom';
import { apiClient, type MorningBrief } from '../../api/client';
import { theme } from '../../theme/theme';
import MorningBriefScene from './MorningBriefScene';

const mockBrief: MorningBrief = {
  mode: 'DEMO',
  asOf: '2026-06-04T11:30:00Z',
  marketStrip: [
    { label: '10y UST', value: '4.46%', change: '15bp', direction: 'up' },
    { label: 'S&P fut', value: '-1.1%', direction: 'down' },
  ],
  reasoning: [
    { text: 'Pulled overnight macro events + market reaction.', status: 'done' },
    { text: 'Generated personalized talking points per priority client.', status: 'done' },
  ],
  macroNarrative: {
    summary: 'The Fed delivered a surprise 25bp hike and markets moved risk-off.',
    whyItMatters: 'Long-duration holders and unhedged floating exposure need attention.',
    sources: ['Fed statement', 'Overnight rates feed'],
  },
  mostAffectedClients: [
    {
      cid: 'ATLAS',
      name: 'Atlas Pension',
      tier: 'Tier 1',
      exposure: 'Heavy long-duration bonds',
      concern: { label: 'Price drop', kind: 'sell' },
      drivingEvents: [
        {
          eventId: 'evt-20260604-001',
          headline: 'Fed surprise 25bp hike jolts long-duration holders',
          entityRef: 'ATLAS',
          contribution: 30,
          rationale: 'High-severity macro rate event hits long-duration exposure.',
        },
      ],
    },
    {
      cid: 'BROOK',
      name: 'Brookline Bank',
      tier: 'Tier 1',
      exposure: 'Interest-rate swaps book',
      concern: { label: 'Hedge adjust', kind: 'warm' },
    },
  ],
  eventsConsidered: [
    {
      id: 'evt-20260604-001',
      type: 'macro_rate',
      headline: 'Fed surprise 25bp hike jolts long-duration holders',
      summary: 'A fictional surprise hike pushed rates higher overnight.',
      severity: 'high',
      scope: 'overnight',
      origin: 'seed',
      direction: 'negative',
    },
  ],
  outreach: [
    {
      rank: 1,
      cid: 'ATLAS',
      name: 'Atlas Pension',
      suggestedTopic: 'Discuss hedging; mention new 10Y swap axes available.',
      talkingPoints: [
        'Lead with duration impact from the surprise hike.',
        'Offer a 10Y swap to hedge price risk.',
      ],
      rationale: {
        walletScore: 0.95,
        engagementScore: 0.8,
        eventRelevanceScore: 0.9,
        compositeScore: 0.89,
        explanation: 'Top wallet and event relevance make Atlas the first call.',
      },
    },
    {
      rank: 2,
      cid: 'BROOK',
      name: 'Brookline Bank',
      suggestedTopic: 'Review swap hedges vs higher terminal rate.',
      talkingPoints: ['Walk through swap-book sensitivity to a higher terminal rate.'],
      rationale: {
        walletScore: 0.82,
        engagementScore: 0.76,
        eventRelevanceScore: 0.84,
        compositeScore: 0.81,
        explanation: 'Brookline has active engagement and rate-sensitive swaps.',
      },
    },
  ],
};

function renderScene() {
  render(
    <ThemeProvider theme={theme}>
      <CssBaseline />
      <MemoryRouter>
        <MorningBriefScene />
      </MemoryRouter>
    </ThemeProvider>,
  );
}

async function runBrief() {
  const postSpy = vi.spyOn(apiClient, 'post').mockResolvedValue({
    data: mockBrief,
  } as AxiosResponse<MorningBrief>);

  renderScene();
  fireEvent.click(screen.getByRole('button', { name: /run morning brief/i }));
  await screen.findByTestId('call-plan-status');

  return postSpy;
}

afterEach(() => {
  cleanup();
  vi.restoreAllMocks();
});

describe('MorningBriefScene call plan', () => {
  it('updates the editable plan and marks it edited when a talking point changes and a client is removed', async () => {
    await runBrief();

    const talkingPoint = screen.getByLabelText('Talking point 1 for Atlas Pension');
    fireEvent.change(talkingPoint, { target: { value: 'Ask about their Q3 reallocation.' } });
    expect(talkingPoint).toHaveValue('Ask about their Q3 reallocation.');
    expect(screen.getByText(/2 priority clients/i)).toBeInTheDocument();
    expect(screen.getByTestId('call-plan-status')).toHaveTextContent('edited=true');

    fireEvent.click(screen.getByRole('button', { name: /remove brookline bank from plan/i }));

    expect(screen.queryByTestId('call-plan-item-BROOK')).not.toBeInTheDocument();
    expect(screen.getByText(/1 priority client/i)).toBeInTheDocument();
    expect(screen.getByTestId('call-plan-status')).toHaveTextContent('edited=true');
  });

  it('approves locally without issuing another outbound request', async () => {
    const postSpy = await runBrief();
    expect(postSpy).toHaveBeenCalledTimes(1);

    fireEvent.click(screen.getByRole('button', { name: /approve plan/i }));

    expect(screen.getByTestId('call-plan-status')).toHaveTextContent('approvalState=approved');
    expect(screen.getByTestId('call-plan-status')).toHaveTextContent('sent=false');
    expect(screen.getByText(/Plan approved — ready to dial/i)).toBeInTheDocument();
    expect(postSpy).toHaveBeenCalledTimes(1);
  });

  it('renders events considered and per-client driving events', async () => {
    await runBrief();

    const eventsPanel = screen.getByTestId('events-considered');
    expect(eventsPanel).toHaveTextContent(/Events considered \(1\)/i);
    expect(screen.getByTestId('event-evt-20260604-001')).toHaveTextContent(/Fed surprise 25bp hike/i);

    const drivers = screen.getByTestId('driving-events-ATLAS');
    expect(drivers).toHaveTextContent(/Fed surprise 25bp hike/i);
  });
});
