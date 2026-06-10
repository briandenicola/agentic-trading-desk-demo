import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import CssBaseline from '@mui/material/CssBaseline';
import { ThemeProvider } from '@mui/material/styles';
import { MemoryRouter } from 'react-router-dom';
import { theme } from '../../theme/theme';
import type { ChatReply, ChatTurn } from '../../api/client';

// Stub the network seam: capture the conversation, return a deterministic reply.
const hoisted = vi.hoisted(() => ({
  sendChat: vi.fn<(messages: ChatTurn[], rmId?: string) => Promise<ChatReply>>(),
}));

vi.mock('../../api/client', async (importOriginal) => {
  const actual = await importOriginal<typeof import('../../api/client')>();
  return { ...actual, sendChat: hoisted.sendChat };
});

import ChatScene from './ChatScene';
import { clearPersistentState } from '../../hooks/usePersistentState';

function renderScene() {
  return render(
    <MemoryRouter>
      <ThemeProvider theme={theme}>
        <CssBaseline />
        <ChatScene />
      </ThemeProvider>
    </MemoryRouter>,
  );
}

afterEach(() => {
  cleanup();
  clearPersistentState();
  hoisted.sendChat.mockReset();
});

describe('ChatScene', () => {
  it('renders the greeting and default suggestion chips', () => {
    renderScene();
    expect(screen.getByRole('heading', { name: /AI Chat/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Who should I call today\?/i })).toBeInTheDocument();
  });

  it('sends a typed message and renders the assistant reply with its mode', async () => {
    hoisted.sendChat.mockResolvedValue({
      mode: 'DEMO',
      message: 'Your top call today is **Prairie Grain Cooperative**.',
      suggestions: ['Tell me more'],
    });
    renderScene();

    const input = screen.getByPlaceholderText(/Ask about your book/i);
    fireEvent.change(input, { target: { value: 'who should I call?' } });
    fireEvent.click(screen.getByRole('button', { name: /send message/i }));

    await waitFor(() => expect(hoisted.sendChat).toHaveBeenCalledTimes(1));
    expect(hoisted.sendChat).toHaveBeenCalledWith(
      expect.arrayContaining([expect.objectContaining({ role: 'user', content: 'who should I call?' })]),
      'RM-104',
    );
    expect(await screen.findByText(/Prairie Grain Cooperative/i)).toBeInTheDocument();
    // Mode chip and reply-driven suggestion both appear.
    expect(screen.getByText('DEMO')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Tell me more/i })).toBeInTheDocument();
  });

  it('sends the suggestion text when a suggestion chip is clicked', async () => {
    hoisted.sendChat.mockResolvedValue({ mode: 'LIVE', message: 'Markets are calm.' });
    renderScene();

    fireEvent.click(screen.getByRole('button', { name: /What's happening in the market\?/i }));

    await waitFor(() => expect(hoisted.sendChat).toHaveBeenCalledTimes(1));
    expect(hoisted.sendChat).toHaveBeenCalledWith(
      expect.arrayContaining([
        expect.objectContaining({ role: 'user', content: "What's happening in the market?" }),
      ]),
      'RM-104',
    );
    expect(await screen.findByText(/Markets are calm\./i)).toBeInTheDocument();
  });
});
