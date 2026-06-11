import { cleanup, render, screen } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import type { AxiosResponse } from 'axios';
import CssBaseline from '@mui/material/CssBaseline';
import { ThemeProvider } from '@mui/material/styles';
import { MemoryRouter } from 'react-router-dom';
import { apiClient, type RmBriefing } from '../../api/client';
import { theme } from '../../theme/theme';
import RmBriefingScene from './RmBriefingScene';
import { clearPersistentState } from '../../hooks/usePersistentState';

const mockBrief: RmBriefing = {
  mode: 'DEMO',
  asOf: '2026-05-14',
  greeting: 'Good morning, Marcus',
  rm: { rmId: 'RM-104', name: 'Marcus Johnson', title: 'Relationship Manager', territory: 'Great Lakes & Plains' },
  portfolio: { customerCount: 14, totalExposureMm: 819, totalDepositsMm: 194.3 },
  kpis: {
    yesterdayTouchpoints: 6,
    openPipelineCount: 9,
    openPipelineAmountMm: 47.5,
    closingWithin14Days: 2,
    activeComplaints: 3,
  },
  reasoning: [
    { text: 'Scored the book on complaints, follow-ups, closing and stuck deals.', status: 'done' },
    { text: 'Ranked the priority call list.', status: 'done' },
  ],
  priorityCallList: [
    {
      rank: 1,
      priority: 1,
      customerId: 'CB-10036',
      customerName: 'Prairie Grain Cooperative',
      industrySector: 'Agriculture',
      hqCity: 'Fargo',
      state: 'ND',
      annualRevenueMm: 120,
      riskRating: '3',
      score: 150,
      tags: [
        { label: 'Escalated complaint', kind: 'escalated' },
        { label: 'Closing ≤14d', kind: 'closing' },
      ],
      reasons: ['Two open complaints, one escalated.', 'Opportunity closing in 9 days.'],
      suggestedAction: 'Call the CFO to resolve the escalated complaint and confirm the closing terms.',
      drivingEvents: [
        {
          eventId: 'evt-20260514-002',
          headline: 'Bumper-harvest forecast sinks grain prices to a 3-year low',
          entityRef: 'CB-10036',
          contribution: 30,
          rationale: 'High-severity sector event hits the Agriculture sector: downside risk to manage (+30 priority).',
        },
      ],
    },
    {
      rank: 2,
      priority: 1,
      customerId: 'CB-10012',
      customerName: 'Lakeside Manufacturing',
      industrySector: 'Manufacturing',
      hqCity: 'Milwaukee',
      state: 'WI',
      annualRevenueMm: 85,
      riskRating: '2',
      score: 90,
      tags: [{ label: 'Follow-up overdue', kind: 'followup' }],
      reasons: ['Follow-up overdue by 3 days.'],
      suggestedAction: 'Return the treasury-services follow-up call.',
    },
  ],
  complaintsSnapshot: [
    {
      complaintId: 'C-501',
      customerName: 'Prairie Grain Cooperative',
      category: 'Service',
      severity: 'High',
      status: 'Escalated',
      dateFiled: '2026-05-10',
    },
  ],
  pipelineClosing: [
    {
      opportunityId: 'OPP-20056',
      customerName: 'Prairie Grain Cooperative',
      productType: 'Term Loan',
      stage: 'Negotiation',
      amountMm: 10,
      expectedCloseDate: '2026-05-23',
    },
  ],
  macroSnapshot: [
    { headline: 'Ag commodity prices firm', detail: 'Supports grain-sector borrowers in your book.' },
  ],
  suggestedFirstAction: 'Start with Prairie Grain Cooperative — escalated complaint plus a deal closing this week.',
  eventsConsidered: [
    {
      id: 'evt-20260514-002',
      type: 'sector',
      headline: 'Bumper-harvest forecast sinks grain prices to a 3-year low',
      summary: 'A fictional record harvest forecast pressured grain prices overnight.',
      severity: 'high',
      scope: 'overnight',
      origin: 'seed',
      direction: 'negative',
    },
  ],
};

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

async function runBriefing() {
  const postSpy = vi.spyOn(apiClient, 'post').mockResolvedValue({
    data: mockBrief,
  } as AxiosResponse<RmBriefing>);

  renderScene();
  await screen.findByTestId('rm-briefing');

  return postSpy;
}

afterEach(() => {
  cleanup();
  clearPersistentState();
  vi.restoreAllMocks();
});

describe('RmBriefingScene', () => {
  it('renders the greeting, KPIs and ranked priority call list after running', async () => {
    const postSpy = await runBriefing();

    expect(postSpy).toHaveBeenCalledTimes(1);
    expect(postSpy).toHaveBeenCalledWith(
      '/agent/rm-briefing',
      { payload: { rmId: 'RM-104', date: '2026-05-14' } },
      expect.objectContaining({ timeout: expect.any(Number) }),
    );

    expect(screen.getByRole('heading', { name: /good morning, marcus/i })).toBeInTheDocument();
    expect(screen.getAllByText(/14 customers/i).length).toBeGreaterThan(0);

    const firstCall = screen.getByTestId('priority-call-CB-10036');
    expect(firstCall).toHaveTextContent('1. Prairie Grain Cooperative');
    expect(firstCall).toHaveTextContent('Escalated complaint');
    expect(firstCall).toHaveTextContent('Suggested action:');

    expect(screen.getByTestId('priority-call-CB-10012')).toHaveTextContent('2. Lakeside Manufacturing');
  });

  it('shows the active complaints, pipeline and suggested first action', async () => {
    await runBriefing();

    expect(screen.getByRole('heading', { name: /active complaints/i })).toBeInTheDocument();
    expect(screen.getAllByText(/Prairie Grain Cooperative/i).length).toBeGreaterThan(1);
    expect(screen.getByText(/Term Loan/i)).toBeInTheDocument();
    expect(screen.getByText(/Suggested first action/i)).toBeInTheDocument();
    expect(
      screen.getByText(/Start with Prairie Grain Cooperative/i),
    ).toBeInTheDocument();
  });

  it('renders the events-considered list and per-call driving events', async () => {
    await runBriefing();

    const eventsPanel = screen.getByTestId('events-considered');
    expect(eventsPanel).toHaveTextContent(/Events considered \(1\)/i);
    expect(screen.getByTestId('event-evt-20260514-002')).toHaveTextContent(/Bumper-harvest forecast/i);

    const drivers = screen.getByTestId('driving-events-CB-10036');
    expect(drivers).toHaveTextContent(/DRIVING EVENTS/i);
    expect(drivers).toHaveTextContent(/downside risk to manage/i);
  });
});
