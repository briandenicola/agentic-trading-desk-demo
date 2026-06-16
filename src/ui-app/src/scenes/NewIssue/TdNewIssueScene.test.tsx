import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import type { AxiosResponse } from 'axios';
import CssBaseline from '@mui/material/CssBaseline';
import { ThemeProvider } from '@mui/material/styles';
import { MemoryRouter } from 'react-router-dom';
import { apiClient, type TdNewIssueStoryboard } from '../../api/client';
import { theme } from '../../theme/theme';
import TdNewIssueScene from './TdNewIssueScene';
import { clearPersistentState } from '../../hooks/usePersistentState';

const mockStory: TdNewIssueStoryboard = {
  mode: 'DEMO',
  asOf: '2026-05-22',
  title: 'New Issue Radar',
  subtitle: 'Prairie Green Renewables · debt + equity · who do we call first?',
  issuer: {
    name: 'Prairie Green Renewables',
    sector: 'Utilities',
    headline: 'Prairie Green Renewables announces $2.5bn debt and equity issuance',
    summary: 'Concurrent senior note and primary equity offering.',
    announcedAt: '2026-05-21 22:14:00',
    tranches: [
      { securityId: 'SEC-3601', securityName: 'Prairie Green Renewables', assetClass: 'Equity', detail: 'Primary equity', referencePrice: 42.5 },
      { securityId: 'SEC-3602', securityName: 'Prairie Green Renewables 6.00% 2034', assetClass: 'Corporate Bond', detail: 'matures 2034-06-15 · BBB-', referencePrice: 99.5 },
    ],
  },
  steps: [
    {
      id: 'announcement', order: 1, beat: 'New issue announced',
      title: 'Prairie Green Renewables is bringing debt and equity',
      narration: 'Overnight announcement.',
      metrics: [{ label: 'Issue type', value: 'Debt + Equity', tone: 'accent' }],
      evidence: [{ kind: 'news', label: 'Announcement', refId: 'NEWS-1901', date: '2026-05-21 22:14:00', securityId: 'SEC-3601' }],
    },
    {
      id: 'holdings', order: 2, beat: 'Holdings cross-reference',
      title: 'Crestline Capital already owns the equity',
      narration: 'Holds ~$1.0bn.',
      metrics: [{ label: 'Equity position', value: '$998.8mm', tone: 'positive' }],
      evidence: [{ kind: 'holding', label: 'Crestline · equity', refId: 'HLD-7901' }],
    },
    {
      id: 'activity', order: 3, beat: 'Recent flow & conversations',
      title: 'Trading the credit and calling us',
      narration: '5 RFQs.',
      metrics: [{ label: 'Electronic RFQs', value: '5', tone: 'accent' }],
      evidence: [{ kind: 'rfq', label: 'RFQ Buy 35mm', refId: 'RFQ-5905' }],
    },
    {
      id: 'outreach', order: 4, beat: 'Prioritized outreach',
      title: 'Call Crestline Capital now',
      narration: 'First call of the morning.',
      metrics: [{ label: 'Priority', value: 'P1', tone: 'warning' }],
      evidence: [{ kind: 'axe', label: 'Distribution axe', refId: 'INV-4901' }],
    },
  ],
  outreach: {
    clientId: 'CL-2015',
    clientName: 'Crestline Capital',
    clientType: 'Asset Manager',
    headline: 'Call Crestline Capital now — anchor them in the new issue',
    talkingPoints: ['You are long ~$1.0bn of the equity.'],
    tradeIdea: { securityId: 'SEC-3602', securityName: 'Prairie Green Renewables 6.00% 2034', side: 'Buy', level: '~99.65' },
    suggestedAction: 'Call immediately with a priority allocation.',
    draftMessage: 'Hi — Prairie Green is bringing the deal you flagged.',
  },
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
  vi.restoreAllMocks();
});

describe('TdNewIssueScene', () => {
  it('auto-runs the radar and renders the first beat', async () => {
    const postSpy = vi.spyOn(apiClient, 'post').mockResolvedValue({
      data: mockStory,
    } as AxiosResponse<TdNewIssueStoryboard>);

    renderScene();

    await screen.findByTestId('ni-storyboard');
    expect(postSpy).toHaveBeenCalledWith(
      '/agent/td-new-issue',
      { payload: { issuerSecurityId: 'SEC-3601', clientId: 'CL-2015', date: '2026-05-22' } },
      expect.objectContaining({ timeout: expect.any(Number) }),
    );

    expect(screen.getByTestId('ni-step-announcement')).toBeInTheDocument();
    expect(screen.getByText('Step 1 of 4')).toBeInTheDocument();
    // Outreach card only reveals on the final beat.
    expect(screen.queryByTestId('ni-outreach')).not.toBeInTheDocument();
  });

  it('walks to the final beat and reveals the outreach recommendation', async () => {
    vi.spyOn(apiClient, 'post').mockResolvedValue({ data: mockStory } as AxiosResponse<TdNewIssueStoryboard>);

    renderScene();
    await screen.findByTestId('ni-storyboard');

    // Jump straight to the outreach beat via the progress rail.
    fireEvent.click(screen.getByText('Prioritized outreach'));

    expect(screen.getByTestId('ni-step-outreach')).toBeInTheDocument();
    expect(screen.getByTestId('ni-outreach')).toBeInTheDocument();
    expect(screen.getAllByText(/Crestline Capital/).length).toBeGreaterThan(0);
  });

  it('highlights the lead-left bookrunner role and renders the lead-left board + upload', async () => {
    const leadLeftStory: TdNewIssueStoryboard = {
      ...mockStory,
      issuer: {
        ...mockStory.issuer,
        leadLeft: true,
        syndicateRole: 'Lead-Left Bookrunner',
        bookStatus: 'Books open',
        pricingDate: '2026-05-26',
        ourAllocationControlPct: 0.45,
        coManagers: ['Summit Securities'],
        tranches: mockStory.issuer.tranches.map((t) => ({ ...t, leadLeft: true })),
      },
      leadLeftBoard: [
        {
          issuer: 'Prairie Green Renewables',
          role: 'Lead-Left Bookrunner',
          leadLeft: true,
          bookStatus: 'Books open',
          pricingDate: '2026-05-26',
          source: 'seed',
        },
      ],
      outreach: {
        ...mockStory.outreach,
        tradeIdea: { ...mockStory.outreach.tradeIdea!, leadLeft: true },
      },
    };

    vi.spyOn(apiClient, 'post').mockResolvedValue({ data: leadLeftStory } as AxiosResponse<TdNewIssueStoryboard>);

    renderScene();
    await screen.findByTestId('ni-storyboard');

    // The issuer header announces that we run the books.
    expect(screen.getByText(/We run the books on the left/)).toBeInTheDocument();

    // The lead-left board lists the deal and offers the spreadsheet upload control.
    const board = screen.getByTestId('ni-lead-left-board');
    expect(board).toBeInTheDocument();
    expect(screen.getByTestId('ni-upload-input')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Upload deals/i })).toBeInTheDocument();

    // The lead-left allocation badge surfaces on the outreach trade idea.
    fireEvent.click(screen.getByText('Prioritized outreach'));
    expect(screen.getByText(/LEAD-LEFT ALLOCATION/)).toBeInTheDocument();
  });
});
