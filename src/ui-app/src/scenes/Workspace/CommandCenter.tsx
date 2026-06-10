import { useRef, useState } from 'react';
import { Box, CircularProgress, IconButton, Stack, Typography } from '@mui/material';
import AddRoundedIcon from '@mui/icons-material/AddRounded';
import GridViewRoundedIcon from '@mui/icons-material/GridViewRounded';
import DashboardCustomizeRoundedIcon from '@mui/icons-material/DashboardCustomizeRounded';
import NotificationsNoneRoundedIcon from '@mui/icons-material/NotificationsNoneRounded';
import SettingsRoundedIcon from '@mui/icons-material/SettingsRounded';
import AutoAwesomeRoundedIcon from '@mui/icons-material/AutoAwesomeRounded';
import AttachFileRoundedIcon from '@mui/icons-material/AttachFileRounded';
import SendRoundedIcon from '@mui/icons-material/SendRounded';
import { sendChat, type ChatTurn } from '../../api/client';
import { usePersistentState } from '../../hooks/usePersistentState';
import { mint } from '../../theme/theme';
import { workspace } from './workspaceData';
import {
  AxesOfferingsPanel,
  ClientOverviewPanel,
  PerformancePanel,
  PortfolioExposurePanel,
  RiskAnalyticsPanel,
  TopHoldingsPanel,
  WorkspaceInsightsPanel,
} from './WorkspacePanels';

interface Msg {
  id: number;
  role: 'user' | 'assistant';
  content: string;
}

const RM_CONTEXT = 'RM-104';

function ToolbarButton({ children, label }: { children: React.ReactNode; label: string }) {
  return (
    <IconButton size="small" aria-label={label} sx={{ color: mint.textDim, '&:hover': { color: mint.text } }}>
      {children}
    </IconButton>
  );
}

function Bubble({ msg }: { msg: Msg }) {
  const isUser = msg.role === 'user';
  return (
    <Box sx={{ display: 'flex', justifyContent: isUser ? 'flex-end' : 'flex-start', gap: 1.25 }}>
      {!isUser && (
        <Box
          sx={{
            width: 30,
            height: 30,
            borderRadius: 1.5,
            flexShrink: 0,
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            background: `linear-gradient(135deg, ${mint.violet}, ${mint.violetBright})`,
          }}
        >
          <AutoAwesomeRoundedIcon sx={{ fontSize: 16, color: '#fff' }} />
        </Box>
      )}
      <Box
        sx={{
          maxWidth: '76%',
          px: 1.75,
          py: 1.1,
          borderRadius: 2.5,
          fontSize: 13,
          lineHeight: 1.5,
          color: isUser ? '#fff' : mint.text,
          border: `1px solid ${isUser ? 'transparent' : mint.border}`,
          background: isUser ? `linear-gradient(135deg, ${mint.violet}, #5b8cff)` : mint.paperHi,
        }}
      >
        <Typography sx={{ fontSize: 13, lineHeight: 1.5, whiteSpace: 'pre-wrap' }}>{msg.content}</Typography>
        <Typography sx={{ fontSize: 9, color: isUser ? 'rgba(255,255,255,0.7)' : mint.textDim, mt: 0.5, textAlign: 'right' }}>
          {workspace.conversation.time}
        </Typography>
      </Box>
    </Box>
  );
}

/**
 * Center command workspace: a toolbar, the AI command/conversation thread wired to
 * the real /api/chat endpoint, and the configurable panel grid (front-end first).
 */
export default function CommandCenter() {
  const [messages, setMessages] = usePersistentState<Msg[]>('workspace/chat', [
    { id: 1, role: 'user', content: workspace.conversation.prompt },
    { id: 2, role: 'assistant', content: workspace.conversation.reply },
  ]);
  const [input, setInput] = useState('');
  const [loading, setLoading] = useState(false);
  const idRef = useRef<number | null>(null);
  if (idRef.current === null) {
    idRef.current = messages.reduce((m, x) => Math.max(m, x.id), 0) + 1;
  }
  const nextId = () => {
    const id = idRef.current ?? 1;
    idRef.current = id + 1;
    return id;
  };

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
    <Stack spacing={2}>
      {/* Toolbar + command thread */}
      <Box sx={{ p: 2, borderRadius: 3, border: `1px solid ${mint.border}`, background: mint.paper }}>
        <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'flex-end', gap: 0.5, mb: 1.5 }}>
          <Box
            sx={{
              display: 'flex',
              alignItems: 'center',
              gap: 0.5,
              px: 1.25,
              py: 0.5,
              mr: 'auto',
              borderRadius: 2,
              border: `1px solid ${mint.border}`,
              color: mint.text,
              fontSize: 12,
              fontWeight: 600,
              cursor: 'pointer',
            }}
          >
            <AddRoundedIcon sx={{ fontSize: 16 }} /> Add Panel
          </Box>
          <ToolbarButton label="Grid layout">
            <GridViewRoundedIcon fontSize="small" />
          </ToolbarButton>
          <ToolbarButton label="Customize layout">
            <DashboardCustomizeRoundedIcon fontSize="small" />
          </ToolbarButton>
          <ToolbarButton label="Notifications">
            <NotificationsNoneRoundedIcon fontSize="small" />
          </ToolbarButton>
          <ToolbarButton label="Settings">
            <SettingsRoundedIcon fontSize="small" />
          </ToolbarButton>
        </Box>

        <Stack spacing={1.25} sx={{ mb: 1.5 }}>
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

        <Box
          sx={{
            display: 'flex',
            alignItems: 'center',
            gap: 1,
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
          <IconButton size="small" aria-label="Attach" sx={{ color: mint.textDim }}>
            <AttachFileRoundedIcon fontSize="small" />
          </IconButton>
          <IconButton
            size="small"
            aria-label="Send message"
            onClick={() => void send()}
            disabled={loading || input.trim().length === 0}
            sx={{ color: '#fff', background: mint.violet, '&:hover': { background: mint.violetBright }, '&.Mui-disabled': { color: mint.textDim, background: mint.borderSoft } }}
          >
            <SendRoundedIcon fontSize="small" />
          </IconButton>
        </Box>
      </Box>

      {/* Panel grid */}
      <Box sx={{ display: 'grid', gap: 1.75, gridTemplateColumns: { xs: '1fr', md: 'repeat(3, 1fr)' } }}>
        <ClientOverviewPanel />
        <PortfolioExposurePanel />
        <PerformancePanel />
      </Box>
      <Box sx={{ display: 'grid', gap: 1.75, gridTemplateColumns: { xs: '1fr', sm: 'repeat(2, 1fr)', xl: 'repeat(4, 1fr)' } }}>
        <TopHoldingsPanel />
        <RiskAnalyticsPanel />
        <WorkspaceInsightsPanel />
        <AxesOfferingsPanel />
      </Box>
    </Stack>
  );
}
