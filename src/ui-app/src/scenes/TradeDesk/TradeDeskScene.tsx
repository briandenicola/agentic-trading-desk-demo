import {
  Alert,
  Box,
  Button,
  Chip,
  CircularProgress,
  Paper,
  Stack,
  Typography,
} from '@mui/material';
import PhoneInTalkOutlinedIcon from '@mui/icons-material/PhoneInTalkOutlined';
import RefreshRoundedIcon from '@mui/icons-material/RefreshRounded';
import TrendingUpRoundedIcon from '@mui/icons-material/TrendingUpRounded';
import { Link } from 'react-router-dom';
import CommandCenterShell from '../../components/CommandCenterShell';
import LiveAlertBanner from '../../components/LiveAlertBanner';
import SectionTitle from '../../components/SectionTitle';
import { mint } from '../../theme/theme';
import TdCallCard from './TdCallCard';
import { AgentReasoning, AxeBoard, EventsConsidered, MacroThemes, MarketStrip } from './TdPanels';
import { useTdBriefing } from './useTdBriefing';
import { deriveKpis, deriveTicker } from './tdShellData';
import { ChatDockProvider, useChatDock } from '../Workspace/ChatOverlay';
import { tdChatConfig } from './tdChatConfig';

export default function TradeDeskScene() {
  return (
    <ChatDockProvider config={tdChatConfig}>
      <TradeDeskInner />
    </ChatDockProvider>
  );
}

function TradeDeskInner() {
  const { openChat } = useChatDock();
  const { brief, loading, error, reload, liveAlert, dismissAlert } = useTdBriefing('td-desk/brief');

  return (
    <CommandCenterShell
      mode={brief?.mode ?? 'DEMO'}
      asOf={brief?.asOf}
      kpis={brief ? deriveKpis(brief) : []}
      marketChips={brief?.marketStrip ?? []}
      tickerItems={brief ? deriveTicker(brief, liveAlert) : []}
      priorityCount={brief?.priorityCallList.length}
    >
      <Box sx={{ px: { xs: 2, md: 3 }, py: 2.5, maxWidth: 1480, mx: 'auto' }}>
        <LiveAlertBanner alert={liveAlert} onDismiss={dismissAlert} />
        <Box sx={{ mb: 2.5 }}>
          <Typography variant="overline" color="text.secondary">
            Morning · Institutional Sales &amp; Trading
          </Typography>
          <Box sx={{ display: 'flex', alignItems: 'baseline', gap: 2, flexWrap: 'wrap', mt: 0.5 }}>
            <Typography variant="h3">
              {brief ? brief.greeting : 'Trading Desk Morning Plan'}
            </Typography>
            {brief && (
              <Chip
                label={brief.mode}
                size="small"
                color={brief.mode === 'LIVE' ? 'success' : 'default'}
                sx={{ fontWeight: 700 }}
              />
            )}
          </Box>
          {brief ? (
            <Typography variant="body2" color="text.secondary" sx={{ maxWidth: 880, mt: 0.75 }}>
              {brief.salesperson.name}
              {brief.salesperson.desk ? ` · ${brief.salesperson.desk}` : ''}
              {brief.salesperson.coverage ? ` · ${brief.salesperson.coverage}` : ''} — {brief.salesperson.clientCount}{' '}
              clients covered. Ranked by overnight research, open RFQs, client inquiries and our inventory axes.
            </Typography>
          ) : (
            <Typography variant="body2" color="text.secondary" sx={{ maxWidth: 880, mt: 0.75 }}>
              The desk assistant weighs overnight news &amp; research, open RFQs, client inquiries and our
              inventory axes against each client&apos;s book — then builds a ranked, explainable call list.
            </Typography>
          )}
        </Box>

        <Box sx={{ mb: 2.5, display: 'flex', alignItems: 'center', gap: 2, flexWrap: 'wrap' }}>
          <Typography variant="body2" sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
            <span style={{ opacity: 0.5 }}>✦</span>
            <span>Who do I need to call first this morning?</span>
          </Typography>
          <Button
            variant="contained"
            onClick={reload}
            disabled={loading}
            startIcon={loading ? <CircularProgress size={16} /> : <RefreshRoundedIcon />}
          >
            {loading ? 'Running…' : brief ? 'Refresh plan' : 'Run morning plan'}
          </Button>
          <Button component={Link} to="/desk/morning-brief" variant="outlined" startIcon={<TrendingUpRoundedIcon />}>
            Morning brief view
          </Button>
        </Box>

        {error && (
          <Alert severity="error" sx={{ mb: 3 }}>
            {error}
          </Alert>
        )}

        {brief && (
          <Stack spacing={2} data-testid="td-briefing">
            <MarketStrip items={brief.marketStrip} />
            <AgentReasoning steps={brief.reasoning} />

            {/* Primary cockpit: prioritized call list (left) · axes + macro + events (right) */}
            <Box
              sx={{
                display: 'grid',
                gap: 2,
                alignItems: 'start',
                gridTemplateColumns: { xs: '1fr', lg: '1.5fr 1fr' },
              }}
            >
              <Box>
                <SectionTitle
                  icon={<PhoneInTalkOutlinedIcon fontSize="inherit" />}
                  caption="— ranked by research, RFQs, inquiries & inventory axes"
                >
                  Prioritized client call list
                </SectionTitle>
                <Stack spacing={1.5} sx={{ mt: 1.5 }}>
                  {brief.priorityCallList.map((call) => (
                    <TdCallCard key={call.clientId} call={call} onOpenChat={openChat} />
                  ))}
                </Stack>

                <Paper
                  sx={{
                    mt: 2,
                    p: 2.5,
                    display: 'flex',
                    alignItems: 'flex-start',
                    gap: 1.5,
                    backgroundImage: `linear-gradient(135deg, ${mint.blue}24, ${mint.cyan}12)`,
                    borderColor: `${mint.blue}59`,
                  }}
                >
                  <PhoneInTalkOutlinedIcon sx={{ color: mint.blueBright, mt: 0.25 }} />
                  <Box>
                    <Typography variant="overline" sx={{ color: mint.blueBright, display: 'block', lineHeight: 1.6 }}>
                      Suggested first action
                    </Typography>
                    <Typography variant="body2">{brief.suggestedFirstAction}</Typography>
                  </Box>
                </Paper>
              </Box>

              <Stack spacing={2}>
                <AxeBoard axes={brief.inventoryAxes} />
                <MacroThemes themes={brief.macroThemes} />
                {brief.eventsConsidered && <EventsConsidered events={brief.eventsConsidered} />}
              </Stack>
            </Box>

            {brief.notes && brief.notes.length > 0 && (
              <Alert severity="warning" sx={{ fontSize: 13 }}>
                <Typography variant="body2" sx={{ fontWeight: 600, mb: 0.5 }}>
                  Notes:
                </Typography>
                {brief.notes.map((note, idx) => (
                  <Typography key={idx} variant="body2">
                    {note}
                  </Typography>
                ))}
              </Alert>
            )}
          </Stack>
        )}

        {!brief && loading && (
          <Box sx={{ display: 'flex', justifyContent: 'center', py: 8 }}>
            <CircularProgress />
          </Box>
        )}

        <Box sx={{ mt: 4, textAlign: 'center' }}>
          <Typography variant="caption" color="text.secondary">
            Institutional Sales &amp; Trading · fictional data.
          </Typography>
        </Box>
      </Box>
    </CommandCenterShell>
  );
}
