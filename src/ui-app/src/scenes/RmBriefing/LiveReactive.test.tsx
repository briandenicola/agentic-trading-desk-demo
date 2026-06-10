import { act, cleanup, render, screen } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import type { AxiosResponse } from 'axios';
import CssBaseline from '@mui/material/CssBaseline';
import { ThemeProvider } from '@mui/material/styles';
import { MemoryRouter } from 'react-router-dom';
import { theme } from '../../theme/theme';
import LiveAlertBanner from '../../components/LiveAlertBanner';
import type { LiveAlert, LiveSubscriptionHandlers, LiveUpdate, RmBriefing } from '../../api/client';

// Capture the SSE handlers the scene registers so the test can push a live update without a
// real EventSource (jsdom has none). Everything else in the client module stays real.
const hoisted = vi.hoisted(() => ({ handlers: null as LiveSubscriptionHandlers<RmBriefing> | null }));
vi.mock('../../api/client', async (importOriginal) => {
  const actual = await importOriginal<typeof import('../../api/client')>();
  return {
    ...actual,
    subscribeToEvents: (_scene: string, handlers: LiveSubscriptionHandlers<RmBriefing>) => {
      hoisted.handlers = handlers;
      return () => {
        hoisted.handlers = null;
      };
    },
  };
});

import { apiClient } from '../../api/client';
import RmBriefingScene from './RmBriefingScene';
import { clearPersistentState } from '../../hooks/usePersistentState';

function baseBrief(topCustomerId: string, topName: string): RmBriefing {
  return {
    mode: 'DEMO',
    asOf: '2026-05-14',
    greeting: 'Good morning, Marcus',
    rm: { rmId: 'RM-104', name: 'Marcus Johnson' },
    portfolio: { customerCount: 14, totalExposureMm: 819, totalDepositsMm: 194.3 },
    kpis: {
      yesterdayTouchpoints: 6,
      openPipelineCount: 9,
      openPipelineAmountMm: 47.5,
      closingWithin14Days: 2,
      activeComplaints: 3,
    },
    reasoning: [{ text: 'Scored the book.', status: 'done' }],
    priorityCallList: [
      {
        rank: 1,
        priority: 1,
        customerId: topCustomerId,
        customerName: topName,
        score: 150,
        tags: [{ label: 'Escalated complaint', kind: 'escalated' }],
        reasons: ['Top signal.'],
        suggestedAction: 'Call first.',
      },
    ],
    complaintsSnapshot: [],
    pipelineClosing: [],
    macroSnapshot: [],
    suggestedFirstAction: `Start with ${topName}.`,
  };
}

function renderScene() {
  render(
    <ThemeProvider theme={theme}>
      <CssBaseline />
      <MemoryRouter>
        <RmBriefingScene />
      </MemoryRouter>
    </ThemeProvider>,
  );
}

afterEach(() => {
  cleanup();
  clearPersistentState();
  vi.restoreAllMocks();
  hoisted.handlers = null;
});

describe('LiveAlertBanner', () => {
  function renderBanner(alert: LiveAlert | null) {
    render(
      <ThemeProvider theme={theme}>
        <CssBaseline />
        <LiveAlertBanner alert={alert} />
      </ThemeProvider>,
    );
  }

  it('renders an urgent alert with its headline and priority', () => {
    renderBanner({
      priority: 'urgent',
      headline: 'Surprise rate move hits the book',
      eventIds: ['evt-1'],
      noImpact: false,
    });

    expect(screen.getByTestId('live-alert-banner')).toHaveTextContent('Surprise rate move hits the book');
    expect(screen.getByTestId('live-alert-priority')).toHaveTextContent('URGENT');
    expect(screen.getByText(/1 new event/i)).toBeInTheDocument();
  });

  it('renders a no-impact update as a low-key note', () => {
    renderBanner({
      priority: 'info',
      headline: 'New event — no portfolio impact: minor wire story',
      eventIds: ['evt-9'],
      noImpact: true,
    });

    expect(screen.getByTestId('live-alert-priority')).toHaveTextContent('NO IMPACT');
  });

  it('stays hidden when there is no alert', () => {
    renderBanner(null);
    expect(screen.queryByTestId('live-alert-banner')).not.toBeInTheDocument();
  });
});

describe('RmBriefingScene live re-rank', () => {
  it('applies an SSE briefing-update: shows the alert and re-ranks the call list in place', async () => {
    vi.spyOn(apiClient, 'post').mockResolvedValue({
      data: baseBrief('CB-10012', 'Lakeside Manufacturing'),
    } as AxiosResponse<RmBriefing>);

    renderScene();
    await screen.findByTestId('rm-briefing');

    expect(screen.getByTestId('priority-call-CB-10012')).toBeInTheDocument();
    expect(screen.queryByTestId('live-alert-banner')).not.toBeInTheDocument();
    expect(hoisted.handlers).not.toBeNull();

    const update: LiveUpdate<RmBriefing> = {
      sequence: 1,
      scene: 'rm-briefing',
      alert: {
        priority: 'urgent',
        headline: 'Grain shock vaults Prairie Grain to the top',
        eventIds: ['evt-intraday-1'],
        noImpact: false,
      },
      briefing: baseBrief('CB-10036', 'Prairie Grain Cooperative'),
    };

    act(() => {
      hoisted.handlers!.onUpdate(update);
    });

    expect(screen.getByTestId('live-alert-banner')).toHaveTextContent(/Grain shock vaults Prairie Grain/i);
    expect(screen.getByTestId('priority-call-CB-10036')).toBeInTheDocument();
    expect(screen.queryByTestId('priority-call-CB-10012')).not.toBeInTheDocument();
  });
});
