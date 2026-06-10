import { useEffect, useRef, useState } from 'react';
import {
  Box,
  Chip,
  CircularProgress,
  Container,
  IconButton,
  Paper,
  Stack,
  TextField,
  Typography,
} from '@mui/material';
import SendRoundedIcon from '@mui/icons-material/SendRounded';
import AutoAwesomeOutlinedIcon from '@mui/icons-material/AutoAwesomeOutlined';
import { sendChat, type ChatReply, type ChatTurn } from '../../api/client';
import { usePersistentState } from '../../hooks/usePersistentState';
import CockpitNav from '../../components/CockpitNav';
import MarkdownMessage from '../../components/MarkdownMessage';
import { mint } from '../../theme/theme';

const RM_CONTEXT = 'RM-104';

const GREETING =
  "Hi — I'm your **Markets-Intelligence assistant**, grounded in your book and the live event feed. Ask me about who to call, a specific customer, the market, complaints or your pipeline.";

const DEFAULT_SUGGESTIONS = [
  'Who should I call today?',
  "What's happening in the market?",
  'Tell me about CB-10036',
  'Any active complaints?',
];

interface DisplayMessage extends ChatTurn {
  id: number;
}

export default function ChatScene() {
  const [messages, setMessages] = usePersistentState<DisplayMessage[]>('chat/messages', [
    { id: 0, role: 'assistant', content: GREETING },
  ]);
  const [input, setInput] = useState('');
  const [loading, setLoading] = useState(false);
  const [mode, setMode] = usePersistentState<ChatReply['mode'] | null>('chat/mode', null);
  const [suggestions, setSuggestions] = usePersistentState<string[]>(
    'chat/suggestions',
    DEFAULT_SUGGESTIONS,
  );

  const endRef = useRef<HTMLDivElement | null>(null);
  useEffect(() => {
    endRef.current?.scrollIntoView?.({ behavior: 'smooth' });
  }, [messages, loading]);

  // Seed the id counter past any restored messages so new turns never collide on a remount.
  const idRef = useRef<number | null>(null);
  if (idRef.current === null) {
    idRef.current = messages.reduce((max, m) => Math.max(max, m.id), 0) + 1;
  }
  const nextId = () => {
    const id = idRef.current ?? 1;
    idRef.current = id + 1;
    return id;
  };

  const send = async (text: string) => {
    const trimmed = text.trim();
    if (!trimmed || loading) return;

    const userMsg: DisplayMessage = { id: nextId(), role: 'user', content: trimmed };
    const convo = [...messages, userMsg];
    setMessages(convo);
    setInput('');
    setLoading(true);

    try {
      const turns: ChatTurn[] = convo.map(({ role, content }) => ({ role, content }));
      const reply = await sendChat(turns, RM_CONTEXT);
      setMode(reply.mode);
      setSuggestions(
        reply.suggestions && reply.suggestions.length ? reply.suggestions : DEFAULT_SUGGESTIONS,
      );
      setMessages((m) => [...m, { id: nextId(), role: 'assistant', content: reply.message }]);
    } catch {
      setMessages((m) => [
        ...m,
        {
          id: nextId(),
          role: 'assistant',
          content: 'Sorry — I could not reach the assistant. Please try again.',
        },
      ]);
    } finally {
      setLoading(false);
    }
  };

  return (
    <>
      <CockpitNav />
      <Container maxWidth="md" sx={{ py: 3 }}>
        <Stack direction="row" spacing={1.5} sx={{ mb: 2, alignItems: 'center' }}>
          <AutoAwesomeOutlinedIcon sx={{ color: mint.violetBright }} />
          <Typography variant="h5" sx={{ fontWeight: 700 }}>
            AI Chat
          </Typography>
          <Chip
            size="small"
            label="Markets-Intelligence assistant"
            sx={{ color: mint.textDim, borderColor: mint.border }}
            variant="outlined"
          />
          <Box sx={{ flex: 1 }} />
          {mode && (
            <Chip
              size="small"
              label={mode}
              color={mode === 'LIVE' ? 'secondary' : 'default'}
              variant={mode === 'LIVE' ? 'filled' : 'outlined'}
            />
          )}
        </Stack>

        <Paper sx={{ display: 'flex', flexDirection: 'column', height: '68vh', overflow: 'hidden' }}>
          <Box sx={{ flex: 1, overflowY: 'auto', p: 2.5 }}>
            <Stack spacing={1.5}>
              {messages.map((m) => (
                <Box
                  key={m.id}
                  sx={{
                    alignSelf: m.role === 'user' ? 'flex-end' : 'flex-start',
                    maxWidth: '82%',
                    px: 1.75,
                    py: 1.25,
                    borderRadius: 2.5,
                    border: `1px solid ${m.role === 'user' ? 'transparent' : mint.border}`,
                    background:
                      m.role === 'user'
                        ? `linear-gradient(135deg, ${mint.violet} 0%, #5b8cff 100%)`
                        : mint.paperHi,
                    color: m.role === 'user' ? '#ffffff' : mint.text,
                  }}
                >
                  {m.role === 'user' ? (
                    <Typography variant="body2" sx={{ lineHeight: 1.55, whiteSpace: 'pre-wrap' }}>
                      {m.content}
                    </Typography>
                  ) : (
                    <MarkdownMessage content={m.content} fontSize={14} />
                  )}
                </Box>
              ))}
              {loading && (
                <Stack
                  direction="row"
                  spacing={1}
                  sx={{ color: mint.textDim, pl: 0.5, alignItems: 'center' }}
                >
                  <CircularProgress size={14} thickness={6} />
                  <Typography variant="caption">Assistant is thinking…</Typography>
                </Stack>
              )}
              <div ref={endRef} />
            </Stack>
          </Box>

          <Box sx={{ borderTop: `1px solid ${mint.border}`, p: 1.75 }}>
            <Stack direction="row" spacing={1} sx={{ mb: 1, flexWrap: 'wrap', gap: 0.75 }}>
              {suggestions.map((s) => (
                <Chip
                  key={s}
                  label={s}
                  size="small"
                  onClick={() => send(s)}
                  disabled={loading}
                  variant="outlined"
                  sx={{ borderColor: mint.border, color: mint.textDim, cursor: 'pointer' }}
                />
              ))}
            </Stack>
            <Stack direction="row" spacing={1} sx={{ alignItems: 'flex-end' }}>
              <TextField
                fullWidth
                size="small"
                multiline
                maxRows={4}
                placeholder="Ask about your book, the market, a customer…"
                value={input}
                onChange={(e) => setInput(e.target.value)}
                onKeyDown={(e) => {
                  if (e.key === 'Enter' && !e.shiftKey) {
                    e.preventDefault();
                    send(input);
                  }
                }}
              />
              <IconButton
                color="primary"
                onClick={() => send(input)}
                disabled={loading || input.trim().length === 0}
                aria-label="Send message"
                sx={{ mb: 0.25 }}
              >
                <SendRoundedIcon />
              </IconButton>
            </Stack>
          </Box>
        </Paper>

        <Typography variant="caption" sx={{ color: mint.textDim, display: 'block', mt: 1.5 }}>
          Fictional data · grounded in the mock systems-of-record · DEMO/LIVE mode-blind
        </Typography>
      </Container>
    </>
  );
}
