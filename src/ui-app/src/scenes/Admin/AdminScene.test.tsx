import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import CssBaseline from '@mui/material/CssBaseline';
import { ThemeProvider } from '@mui/material/styles';
import { MemoryRouter } from 'react-router-dom';
import { theme } from '../../theme/theme';
import type { AdminNewsSubmission, MarketEvent } from '../../api/client';

// Stub the network seam: capture ingest calls, return a deterministic event list.
const hoisted = vi.hoisted(() => ({
  ingestNews: vi.fn<(s: AdminNewsSubmission) => Promise<MarketEvent>>(),
  listEvents: vi.fn<() => Promise<MarketEvent[]>>(),
}));

vi.mock('../../api/client', async (importOriginal) => {
  const actual = await importOriginal<typeof import('../../api/client')>();
  return {
    ...actual,
    ingestNews: hoisted.ingestNews,
    listEvents: hoisted.listEvents,
  };
});

import AdminScene from './AdminScene';

function renderScene() {
  return render(
    <MemoryRouter>
      <ThemeProvider theme={theme}>
        <CssBaseline />
        <AdminScene />
      </ThemeProvider>
    </MemoryRouter>,
  );
}

afterEach(() => {
  cleanup();
  hoisted.ingestNews.mockReset();
  hoisted.listEvents.mockReset();
});

describe('AdminScene', () => {
  it('rejects an incomplete submission and ingests nothing', async () => {
    hoisted.listEvents.mockResolvedValue([]);
    renderScene();
    await waitFor(() => expect(hoisted.listEvents).toHaveBeenCalled());

    // Submit with empty headline/summary/entities.
    fireEvent.click(screen.getByRole('button', { name: /inject news item/i }));

    expect(await screen.findByTestId('news-form-error')).toHaveTextContent(/headline is required/i);
    expect(hoisted.ingestNews).not.toHaveBeenCalled();
  });

  it('ingests a valid submission exactly once and refreshes the feed', async () => {
    hoisted.listEvents.mockResolvedValue([]);
    hoisted.ingestNews.mockResolvedValue({
      id: 'evt-20260514-a001',
      type: 'sector',
      headline: 'Grain prices spike',
      summary: 'Drought lifts agricultural commodity prices.',
      severity: 'high',
      scope: 'intraday',
      origin: 'admin',
    });
    renderScene();
    await waitFor(() => expect(hoisted.listEvents).toHaveBeenCalledTimes(1));

    fireEvent.change(screen.getByLabelText(/headline/i), {
      target: { value: 'Grain prices spike' },
    });
    fireEvent.change(screen.getByLabelText(/summary/i), {
      target: { value: 'Drought lifts agricultural commodity prices.' },
    });
    fireEvent.change(screen.getByLabelText(/customer ids/i), {
      target: { value: 'CB-10036' },
    });

    fireEvent.click(screen.getByRole('button', { name: /inject news item/i }));

    await waitFor(() => expect(hoisted.ingestNews).toHaveBeenCalledTimes(1));
    expect(hoisted.ingestNews).toHaveBeenCalledWith(
      expect.objectContaining({
        headline: 'Grain prices spike',
        affectedEntities: expect.objectContaining({ customerIds: ['CB-10036'] }),
      }),
    );
    // Feed refreshed after the successful injection.
    await waitFor(() => expect(hoisted.listEvents).toHaveBeenCalledTimes(2));
    expect(await screen.findByTestId('admin-notice')).toHaveTextContent(/evt-20260514-a001/);
  });
});
