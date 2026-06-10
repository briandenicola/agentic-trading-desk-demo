import { render, screen, act } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import WorkspaceScene from './WorkspaceScene';
import { clearPersistentState } from '../../hooks/usePersistentState';
import type { LiveSubscriptionHandlers, LiveUpdate, RmBriefing } from '../../api/client';

// Capture the SSE handlers the workspace registers so the test can drive updates.
let captured: LiveSubscriptionHandlers<RmBriefing> | null = null;

vi.mock('../../api/client', async (importOriginal) => {
  const actual = await importOriginal<typeof import('../../api/client')>();
  return {
    ...actual,
    subscribeToEvents: (_scene: unknown, handlers: LiveSubscriptionHandlers<RmBriefing>) => {
      captured = handlers;
      return () => {};
    },
  };
});

function update(partial: Partial<LiveUpdate<RmBriefing>['alert']> & { sequence: number; noImpact: boolean }): LiveUpdate<RmBriefing> {
  return {
    sequence: partial.sequence,
    scene: 'rm-briefing',
    alert: {
      priority: partial.priority ?? 'urgent',
      headline: partial.headline ?? 'Headline',
      eventIds: partial.eventIds ?? ['evt-1'],
      noImpact: partial.noImpact,
    },
    briefing: {} as RmBriefing,
  };
}

describe('Workspace intraday highlighting', () => {
  beforeEach(() => {
    captured = null;
    clearPersistentState();
  });

  it('treats the first snapshot as baseline, then highlights a later impactful event', () => {
    render(
      <MemoryRouter>
        <WorkspaceScene />
      </MemoryRouter>,
    );

    expect(captured).not.toBeNull();

    // First snapshot is the current baseline — must NOT surface an alert/toast.
    act(() => captured!.onUpdate(update({ sequence: 1, noImpact: true, headline: 'baseline sync' })));
    expect(screen.queryByText('baseline sync')).not.toBeInTheDocument();

    // A later impactful event lights up the shell (rows + banner + toast).
    act(() =>
      captured!.onUpdate(update({ sequence: 2, noImpact: false, priority: 'urgent', headline: 'CPI prints hotter than expected' })),
    );

    expect(screen.getAllByText('CPI prints hotter than expected').length).toBeGreaterThan(0);
    expect(screen.getByText('LIVE')).toBeInTheDocument();
  });
});
