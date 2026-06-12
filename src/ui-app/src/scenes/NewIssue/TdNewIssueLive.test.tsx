import { act, cleanup, render, screen } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import type { AxiosResponse } from 'axios';
import CssBaseline from '@mui/material/CssBaseline';
import { ThemeProvider } from '@mui/material/styles';
import { MemoryRouter } from 'react-router-dom';
import { theme } from '../../theme/theme';
import type {
  LiveSubscriptionHandlers,
  LiveUpdate,
  TdNewIssueStoryboard,
} from '../../api/client';

// Capture the SSE handlers the scene registers so the test can push a live update without a
// real EventSource (jsdom has none). Everything else in the client module stays real.
const hoisted = vi.hoisted(() => ({
  handlers: null as LiveSubscriptionHandlers<TdNewIssueStoryboard> | null,
}));
vi.mock('../../api/client', async (importOriginal) => {
  const actual = await importOriginal<typeof import('../../api/client')>();
  return {
    ...actual,
    subscribeToEvents: (_scene: string, handlers: LiveSubscriptionHandlers<TdNewIssueStoryboard>) => {
      hoisted.handlers = handlers;
      return () => {
        hoisted.handlers = null;
      };
    },
  };
});

import { apiClient } from '../../api/client';
import TdNewIssueScene from './TdNewIssueScene';
import { clearPersistentState } from '../../hooks/usePersistentState';

const baseStory: TdNewIssueStoryboard = {
  mode: 'DEMO',
  asOf: '2026-05-22',
  title: 'New Issue Radar',
  subtitle: 'Prairie Green Renewables · who do we call first?',
  issuer: {
    name: 'Prairie Green Renewables',
    sector: 'Utilities',
    headline: 'Prairie Green Renewables announces debt and equity issuance',
    tranches: [
      { securityId: 'SEC-3601', securityName: 'Prairie Green Renewables', assetClass: 'Equity' },
      { securityId: 'SEC-3602', securityName: 'Prairie Green 6.00% 2034', assetClass: 'Corporate Bond' },
    ],
  },
  steps: [
    {
      id: 'announcement', order: 1, beat: 'New issue announced', title: 'Bringing debt and equity',
      narration: 'Overnight announcement.',
      metrics: [{ label: 'Issue type', value: 'Debt + Equity', tone: 'accent' }],
      evidence: [{ kind: 'news', label: 'Announcement', refId: 'NEWS-1901' }],
    },
    {
      id: 'outreach', order: 2, beat: 'Prioritized outreach', title: 'Call Crestline now',
      narration: 'First call.', metrics: [{ label: 'Priority', value: 'P1', tone: 'warning' }],
    },
  ],
  outreach: {
    clientId: 'CL-2015', clientName: 'Crestline Capital', headline: 'Call Crestline now',
    talkingPoints: ['You are long the equity.'], suggestedAction: 'Call now.',
  },
};

// Folded-in update the SSE hub would push after a matching News Desk inject.
const liveStory: TdNewIssueStoryboard = {
  ...baseStory,
  steps: [
    {
      ...baseStory.steps[0],
      metrics: [{ label: 'Live update', value: 'Ratings agency upgrades Prairie Green', tone: 'warning', live: true }, ...baseStory.steps[0].metrics!],
      evidence: [{ kind: 'news', label: 'Ratings agency upgrades Prairie Green', refId: 'EVT-9', live: true }, ...baseStory.steps[0].evidence!],
    },
    baseStory.steps[1],
  ],
  liveEvents: [
    {
      id: 'EVT-9', type: 'issuer_credit', headline: 'Ratings agency upgrades Prairie Green',
      summary: 'Upgrade to BBB.', severity: 'high',
      affectedEntities: { issuers: ['Prairie Green Renewables'] },
    },
  ],
};

function renderScene() {
  render(
    <ThemeProvider theme={theme}>
      <CssBaseline />
      <MemoryRouter>
        <TdNewIssueScene />
      </MemoryRouter>
    </ThemeProvider>,
  );
}

afterEach(() => {
  cleanup();
  clearPersistentState();
  hoisted.handlers = null;
  vi.restoreAllMocks();
});

describe('TdNewIssueScene live reactivity', () => {
  it('applies an injected event: surfaces the live alert banner and a LIVE evidence row', async () => {
    vi.spyOn(apiClient, 'post').mockResolvedValue({ data: baseStory } as AxiosResponse<TdNewIssueStoryboard>);

    renderScene();
    await screen.findByTestId('ni-storyboard');
    expect(hoisted.handlers).not.toBeNull();

    const update: LiveUpdate<TdNewIssueStoryboard> = {
      sequence: 2,
      scene: 'td-new-issue',
      alert: { priority: 'urgent', headline: 'Ratings agency upgrades Prairie Green', eventIds: ['EVT-9'], noImpact: false },
      briefing: liveStory,
    };

    act(() => {
      hoisted.handlers!.onUpdate(update);
    });

    expect(screen.getByTestId('live-alert-banner')).toHaveTextContent(/Ratings agency upgrades Prairie Green/i);
    expect(screen.getAllByText(/Ratings agency upgrades Prairie Green/i).length).toBeGreaterThan(0);
    expect(screen.getByTestId('ni-step-announcement')).toHaveTextContent('LIVE');
  });
});
