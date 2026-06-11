import {
  Alert,
  Box,
  Button,
  Chip,
  CircularProgress,
  Container,
  Paper,
  Stack,
  Typography,
} from '@mui/material';
import PhoneInTalkOutlinedIcon from '@mui/icons-material/PhoneInTalkOutlined';
import RefreshRoundedIcon from '@mui/icons-material/RefreshRounded';
import TrendingUpRoundedIcon from '@mui/icons-material/TrendingUpRounded';
import { Link } from 'react-router-dom';
import TdNav from '../../components/TdNav';
import LiveAlertBanner from '../../components/LiveAlertBanner';
import SectionTitle from '../../components/SectionTitle';
import { mint } from '../../theme/theme';
import TdCallCard from './TdCallCard';
import { AgentReasoning, AxeBoard, EventsConsidered, MacroThemes, MarketStrip } from './TdPanels';
import { useTdBriefing } from './useTdBriefing';

export default function TradeDeskScene() {
  const { brief, loading, error, reload, liveAlert, dismissAlert } = useTdBriefing('td-desk/brief');

  return (
    <Box sx={{ minHeight: '100vh', bgcolor: 'background.default' }}>
      <TdNav />

      <Container maxWidth="xl" sx={{ py: 4 }}>
        <LiveAlertBanner alert={liveAlert} onDismiss={dismissAlert} />
        <Box sx={{ mb: 3 }}>
          <Typography variant="overline" color="text.secondary" sx={{ fontSize: 12 }}>
            Morning · Institutional Sales &amp; Trading
          </Typography>
          <Box sx={{ display: 'flex', alignItems: 'baseline', gap: 2, flexWrap: 'wrap', mt: 1 }}>
            <Typography variant="h3" sx={{ fontWeight: 500 }}>
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
            <Typography variant="body1" color="text.secondary" sx={{ maxWidth: 880, mt: 1 }}>
              {brief.salesperson.name}
              {brief.salesperson.desk ? ` · ${brief.salesperson.desk}` : ''}
              {brief.salesperson.coverage ? ` · ${brief.salesperson.coverage}` : ''} — {brief.salesperson.clientCount}{' '}
              clients covered. Ranked by overnight research, open RFQs, client inquiries and our inventory axes.
            </Typography>
          ) : (
            <Typography variant="body1" color="text.secondary" sx={{ maxWidth: 880, mt: 1 }}>
              The desk assistant weighs overnight news &amp; research, open RFQs, client inquiries and our
              inventory axes against each client&apos;s book — then builds a ranked, explainable call list.
            </Typography>
          )}
        </Box>

        <Box sx={{ mb: 3, display: 'flex', alignItems: 'center', gap: 2, flexWrap: 'wrap' }}>
          <Typography variant="body1" sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
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
                    <TdCallCard key={call.clientId} call={call} />
                  ))}
                </Stack>

                <Paper
                  sx={{
                    mt: 2,
                    p: 2.5,
                    display: 'flex',
                    alignItems: 'flex-start',
                    gap: 1.5,
                    backgroundImage: `linear-gradient(135deg, ${mint.violet}24, ${mint.cyan}12)`,
                    borderColor: `${mint.violet}59`,
                  }}
                >
                  <PhoneInTalkOutlinedIcon sx={{ color: mint.violetBright, mt: 0.25 }} />
                  <Box>
                    <Typography variant="overline" sx={{ color: mint.violetBright, display: 'block', lineHeight: 1.6 }}>
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
      </Container>
    </Box>
  );
}
