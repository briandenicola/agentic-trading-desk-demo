import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useRef,
  useState,
  type ReactNode,
} from 'react';
import { Box, Chip, CircularProgress, IconButton, Stack, Typography } from '@mui/material';
import AutoAwesomeRoundedIcon from '@mui/icons-material/AutoAwesomeRounded';
import ChatBubbleRoundedIcon from '@mui/icons-material/ChatBubbleRounded';
import CloseRoundedIcon from '@mui/icons-material/CloseRounded';
import SendRoundedIcon from '@mui/icons-material/SendRounded';
import { sendChat, type ChatTurn } from '../../api/client';
import { usePersistentState } from '../../hooks/usePersistentState';
import { mint } from '../../theme/theme';
import MarkdownMessage from '../../components/MarkdownMessage';

// ---------------------------------------------------------------------------
// Floating chat dock. A persistent "Open Chat" launcher in the bottom-right that
// expands into an overlay panel wired to the real /api/chat assistant. Any panel
// can pop it open (optionally seeded with context) via useChatDock().openChat().
// ---------------------------------------------------------------------------

const RM_CONTEXT = 'RM-104';

interface ChatDockValue {
  open: boolean;
  openChat: (seed?: string) => void;
  closeChat: () => void;
}

const ChatDockContext = createContext<ChatDockValue | null>(null);

interface Msg {
  id: number;
  role: 'user' | 'assistant';
  content: string;
}

export function ChatDockProvider({ children }: { children: ReactNode }) {
  const [open, setOpen] = useState(false);
  const [seed, setSeed] = useState<string | null>(null);

  const openChat = useCallback((s?: string) => {
    if (s) setSeed(s);
    setOpen(true);
  }, []);
  const closeChat = useCallback(() => setOpen(false), []);

  return (
    <ChatDockContext.Provider value={{ open, openChat, closeChat }}>
      {children}
      <ChatOverlay open={open} seed={seed} onSeedConsumed={() => setSeed(null)} onClose={closeChat} onOpen={() => setOpen(true)} />
    </ChatDockContext.Provider>
  );
}

export function useChatDock(): ChatDockValue {
  const ctx = useContext(ChatDockContext);
  if (!ctx) throw new Error('useChatDock must be used within a ChatDockProvider');
  return ctx;
}

function Bubble({ msg }: { msg: Msg }) {
  const isUser = msg.role === 'user';
  return (
    <Box sx={{ display: 'flex', justifyContent: isUser ? 'flex-end' : 'flex-start', gap: 1 }}>
      {!isUser && (
        <Box
          sx={{
            width: 26,
            height: 26,
            borderRadius: 1.25,
            flexShrink: 0,
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            background: `linear-gradient(135deg, ${mint.violet}, ${mint.violetBright})`,
          }}
        >
          <AutoAwesomeRoundedIcon sx={{ fontSize: 14, color: '#fff' }} />
        </Box>
      )}
      <Box
        sx={{
          maxWidth: '82%',
          px: 1.5,
          py: 1,
          borderRadius: 2,
          color: isUser ? '#fff' : mint.text,
          border: `1px solid ${isUser ? 'transparent' : mint.border}`,
          background: isUser ? `linear-gradient(135deg, ${mint.violet}, #5b8cff)` : mint.paperHi,
        }}
      >
        {isUser ? (
          <Typography sx={{ fontSize: 13, lineHeight: 1.5, whiteSpace: 'pre-wrap' }}>{msg.content}</Typography>
        ) : (
          <MarkdownMessage content={msg.content} fontSize={13} />
        )}
      </Box>
    </Box>
  );
}

interface ChatOverlayProps {
  open: boolean;
  seed: string | null;
  onSeedConsumed: () => void;
  onClose: () => void;
  onOpen: () => void;
}

function ChatOverlay({ open, seed, onSeedConsumed, onClose, onOpen }: ChatOverlayProps) {
  const [messages, setMessages] = usePersistentState<Msg[]>('workspace/chat', []);
  const [input, setInput] = useState('');
  const [loading, setLoading] = useState(false);
  const [mode, setMode] = usePersistentState<'DEMO' | 'LIVE' | null>('workspace/chat-mode', null);
  const scrollRef = useRef<HTMLDivElement | null>(null);
  const idRef = useRef<number | null>(null);
  if (idRef.current === null) {
    idRef.current = messages.reduce((m, x) => Math.max(m, x.id), 0) + 1;
  }
  const nextId = () => {
    const id = idRef.current ?? 1;
    idRef.current = id + 1;
    return id;
  };

  // Pre-fill the composer when a panel opens the dock with context.
  useEffect(() => {
    if (open && seed) {
      setInput(seed);
      onSeedConsumed();
    }
  }, [open, seed, onSeedConsumed]);

  useEffect(() => {
    if (open && scrollRef.current) {
      scrollRef.current.scrollTop = scrollRef.current.scrollHeight;
    }
  }, [open, messages, loading]);

  const send = async () => {
    const text = input.trim();
    if (!text || loading) return;
    const convo: Msg[] = [...messages, { id: nextId(), role: 'user', content: text }];
    setMessages(convo);
    setInput('');
    setLoading(true);
    try {
      const turns: ChatTurn[] = convo.map(({ role, content }) => ({ role, content }));
      const reply = await sendChat(turns, RM_CONTEXT);
      setMode(reply.mode);
      setMessages((m) => [...m, { id: nextId(), role: 'assistant', content: reply.message }]);
    } catch {
      setMessages((m) => [
        ...m,
        { id: nextId(), role: 'assistant', content: 'Sorry — I could not reach the assistant right now.' },
      ]);
    } finally {
      setLoading(false);
    }
  };

  return (
    <>
      {/* Floating launcher */}
      {!open && (
        <Box
          onClick={onOpen}
          role="button"
          aria-label="Open Chat"
          sx={{
            position: 'fixed',
            bottom: 24,
            right: 24,
            zIndex: 1300,
            display: 'flex',
            alignItems: 'center',
            gap: 1,
            px: 2,
            py: 1.25,
            borderRadius: 99,
            cursor: 'pointer',
            color: '#fff',
            fontWeight: 700,
            fontSize: 14,
            background: `linear-gradient(135deg, ${mint.violet}, ${mint.magenta})`,
            boxShadow: `0 8px 28px ${mint.violet}66`,
            '&:hover': { filter: 'brightness(1.08)' },
          }}
        >
          <ChatBubbleRoundedIcon sx={{ fontSize: 18 }} />
          Open Chat
        </Box>
      )}

      {/* Overlay panel */}
      {open && (
        <Box
          sx={{
            position: 'fixed',
            bottom: { xs: 0, sm: 24 },
            right: { xs: 0, sm: 24 },
            width: { xs: '100%', sm: 400 },
            height: { xs: '80vh', sm: 560 },
            maxHeight: '88vh',
            zIndex: 1300,
            display: 'flex',
            flexDirection: 'column',
            borderRadius: { xs: '16px 16px 0 0', sm: 3 },
            border: `1px solid ${mint.border}`,
            background: mint.paper,
            boxShadow: `0 18px 50px rgba(0,0,0,0.55)`,
            overflow: 'hidden',
          }}
        >
          {/* Header */}
          <Box
            sx={{
              display: 'flex',
              alignItems: 'center',
              gap: 1.25,
              px: 2,
              py: 1.5,
              borderBottom: `1px solid ${mint.border}`,
              background: `linear-gradient(135deg, ${mint.violet}1f, transparent 70%)`,
            }}
          >
            <Box
              sx={{
                width: 30,
                height: 30,
                borderRadius: 1.5,
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
                background: `linear-gradient(135deg, ${mint.violet}, ${mint.violetBright})`,
              }}
            >
              <AutoAwesomeRoundedIcon sx={{ fontSize: 17, color: '#fff' }} />
            </Box>
            <Box sx={{ flex: 1, minWidth: 0 }}>
              <Typography sx={{ fontSize: 13, fontWeight: 800, color: mint.text, lineHeight: 1.2 }}>
                Markets-Intelligence Assistant
              </Typography>
              <Typography sx={{ fontSize: 10.5, color: mint.textDim }}>Grounded in your book & live feed</Typography>
            </Box>
            {mode && (
              <Chip
                label={mode}
                size="small"
                sx={{
                  height: 19,
                  fontSize: 9,
                  fontWeight: 800,
                  color: mode === 'LIVE' ? '#04101c' : mint.textDim,
                  bgcolor: mode === 'LIVE' ? mint.green : mint.borderSoft,
                }}
              />
            )}
            <IconButton size="small" aria-label="Close chat" onClick={onClose} sx={{ color: mint.textDim }}>
              <CloseRoundedIcon fontSize="small" />
            </IconButton>
          </Box>

          {/* Thread */}
          <Box ref={scrollRef} sx={{ flex: 1, overflowY: 'auto', p: 2 }}>
            {messages.length === 0 && (
              <Typography sx={{ fontSize: 13, color: mint.textDim, textAlign: 'center', mt: 4, px: 2 }}>
                Ask about who to call first, a specific customer, complaints or your pipeline.
              </Typography>
            )}
            <Stack spacing={1.25}>
              {messages.map((m) => (
                <Bubble key={m.id} msg={m} />
              ))}
              {loading && (
                <Stack direction="row" spacing={1} sx={{ alignItems: 'center', color: mint.textDim, pl: 0.5 }}>
                  <CircularProgress size={13} thickness={6} />
                  <Typography sx={{ fontSize: 12 }}>Assistant is thinking…</Typography>
                </Stack>
              )}
            </Stack>
          </Box>

          {/* Composer */}
          <Box
            sx={{
              display: 'flex',
              alignItems: 'center',
              gap: 1,
              m: 1.5,
              px: 1.5,
              py: 0.5,
              borderRadius: 2.5,
              border: `1px solid ${mint.border}`,
              background: mint.bgAlt,
            }}
          >
            <Box
              component="input"
              value={input}
              autoFocus
              onChange={(e: React.ChangeEvent<HTMLInputElement>) => setInput(e.target.value)}
              onKeyDown={(e: React.KeyboardEvent) => {
                if (e.key === 'Enter') {
                  e.preventDefault();
                  void send();
                }
              }}
              placeholder="Ask anything or give a command..."
              sx={{
                flex: 1,
                background: 'transparent',
                border: 'none',
                outline: 'none',
                color: mint.text,
                fontSize: 13,
                py: 1,
                fontFamily: 'inherit',
                '&::placeholder': { color: mint.textDim },
              }}
            />
            <IconButton
              size="small"
              aria-label="Send message"
              onClick={() => void send()}
              disabled={loading || input.trim().length === 0}
              sx={{
                color: '#fff',
                background: mint.violet,
                '&:hover': { background: mint.violetBright },
                '&.Mui-disabled': { color: mint.textDim, background: mint.borderSoft },
              }}
            >
              <SendRoundedIcon fontSize="small" />
            </IconButton>
          </Box>
        </Box>
      )}
    </>
  );
}
