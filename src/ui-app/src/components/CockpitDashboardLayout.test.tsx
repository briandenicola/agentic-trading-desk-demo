import { cleanup, render, screen } from '@testing-library/react';
import { afterEach, describe, expect, it } from 'vitest';
import CssBaseline from '@mui/material/CssBaseline';
import { ThemeProvider } from '@mui/material/styles';
import { theme } from '../theme/theme';
import CockpitDashboardLayout from './CockpitDashboardLayout';
import type { LiveAlert, MorningBrief } from '../api/client';

function mockBrief(topClientName: string): MorningBrief {
  return {
    mode: 'DEMO',
    asOf: '2026-06-04',
    marketStrip: [{ label: 'UST 10Y', value: '4.21%', change: '+3bp', direction: 'up' }],
    reasoning: [{ text: 'Scanned overnight tape.', status: 'done' }],
    macroNarrative: { summary: 'Rates higher.', whyItMatters: 'Munis cheapen.', sources: ['Bloomberg'] },
    mostAffectedClients: [
      {
        cid: 'C-1',
        name: topClientName,
        tier: 'Tier 1',
        exposure: '$125M',
        concern: { label: 'Sell', kind: 'sell' },
        drivingEvents: [
          { eventId: 'evt-a', headline: 'Rate shock', entityRef: 'C-1', contribution: 30, rationale: 'Hits duration.' },
        ],
      },
    ],
    outreach: [
      {
        rank: 1,
        cid: 'C-1',
        name: topClientName,
        suggestedTopic: 'Duration hedge',
        talkingPoints: ['Curve'],
        rationale: { walletScore: 0.9, engagementScore: 0.8, eventRelevanceScore: 0.95, compositeScore: 0.9, explanation: '' },
      },
    ],
    eventsConsidered: [
      {
        id: 'evt-a',
        type: 'macro_rate',
        headline: 'Fed surprise hike',
        summary: 'Unexpected 25bp move.',
        severity: 'high',
        scope: 'overnight',
        direction: 'negative',
      },
    ],
  };
}

function renderLayout(brief: MorningBrief, liveAlert?: LiveAlert | null) {
  return render(
    <ThemeProvider theme={theme}>
      <CssBaseline />
      <CockpitDashboardLayout brief={brief} liveAlert={liveAlert} />
    </ThemeProvider>,
  );
}

afterEach(cleanup);

describe('CockpitDashboardLayout', () => {
  it('renders all three columns with the morning-call data', () => {
    renderLayout(mockBrief('Acme Capital'));

    expect(screen.getByTestId('cockpit-client-view')).toBeInTheDocument();
    expect(screen.getByTestId('cockpit-ticker-view')).toBeInTheDocument();
    expect(screen.getByTestId('cockpit-overall-view')).toBeInTheDocument();

    expect(screen.getByTestId('cockpit-focus-client')).toHaveTextContent('Acme Capital');
    expect(screen.getByTestId('cockpit-top-priorities')).toHaveTextContent('Duration hedge');
    expect(screen.getByTestId('cockpit-market-news')).toHaveTextContent('Fed surprise hike');
  });

  it('shows the live alert banner and re-ranks when a live update arrives', () => {
    const alert: LiveAlert = {
      priority: 'urgent',
      headline: 'Intraday: grain shock',
      eventIds: ['evt-b'],
      noImpact: false,
    };
    const { rerender } = renderLayout(mockBrief('Acme Capital'), null);
    expect(screen.queryByTestId('cockpit-live-alert')).not.toBeInTheDocument();

    rerender(
      <ThemeProvider theme={theme}>
        <CssBaseline />
        <CockpitDashboardLayout brief={mockBrief('Prairie Grain Co-op')} liveAlert={alert} />
      </ThemeProvider>,
    );

    expect(screen.getByTestId('cockpit-live-alert')).toHaveTextContent('grain shock');
    expect(screen.getByTestId('cockpit-focus-client')).toHaveTextContent('Prairie Grain Co-op');
  });
});
