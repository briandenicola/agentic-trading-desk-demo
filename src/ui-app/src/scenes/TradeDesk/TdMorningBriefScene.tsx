import { Alert, Box, Chip, CircularProgress, Container, Paper, Stack, Typography } from '@mui/material';
import PhoneInTalkOutlinedIcon from '@mui/icons-material/PhoneInTalkOutlined';
import TdNav from '../../components/TdNav';
import SectionTitle from '../../components/SectionTitle';
import { mint } from '../../theme/theme';
import TdCallCard from './TdCallCard';
import { AgentReasoning, AxeBoard, EventsConsidered, MacroThemes, MarketStrip } from './TdPanels';
import { useTdBriefing } from './useTdBriefing';

/**
 * Two-column morning-brief view of the same TdBriefing: LEFT = macro/market context,
 * agent reasoning, axe board and events considered; RIGHT = the prioritized outreach
 * plan. Shares the persisted brief with the trading desk via the same store key.
 */
export default function TdMorningBriefScene() {
  const { brief, loading, error } = useTdBriefing('td-desk/brief');

  return (
    <Box sx={{ minHeight: '100vh', bgcolor: 'background.default' }}>
      <TdNav />

      <Container maxWidth="xl" sx={{ py: 4 }}>
        <Box sx={{ mb: 3 }}>
          <Typography variant="overline" color="text.secondary" sx={{ fontSize: 12 }}>
            Morning brief · Institutional Sales &amp; Trading
          </Typography>
          <Box sx={{ display: 'flex', alignItems: 'baseline', gap: 2, flexWrap: 'wrap', mt: 1 }}>
            <Typography variant="h3" sx={{ fontWeight: 500 }}>
              {brief ? brief.greeting : 'Trading Morning Brief'}
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
          {brief && (
            <Typography variant="body1" color="text.secondary" sx={{ maxWidth: 880, mt: 1 }}>
              {brief.salesperson.name}
              {brief.salesperson.coverage ? ` · ${brief.salesperson.coverage}` : ''} — macro context and the
              morning outreach plan, side by side.
            </Typography>
          )}
        </Box>

        {error && (
          <Alert severity="error" sx={{ mb: 3 }}>
            {error}
          </Alert>
        )}

        {!brief && loading && (
          <Box sx={{ display: 'flex', justifyContent: 'center', py: 8 }}>
            <CircularProgress />
          </Box>
        )}

        {brief && (
          <Stack spacing={2} data-testid="td-morning-brief">
            <MarketStrip items={brief.marketStrip} />
            <Box
              sx={{
                display: 'grid',
                gap: 2,
                alignItems: 'start',
                gridTemplateColumns: { xs: '1fr', md: '1fr 1fr' },
              }}
            >
              {/* LEFT — macro / market context */}
              <Stack spacing={2}>
                <AgentReasoning steps={brief.reasoning} />
                <MacroThemes themes={brief.macroThemes} />
                <AxeBoard axes={brief.inventoryAxes} limit={8} />
                {brief.eventsConsidered && <EventsConsidered events={brief.eventsConsidered} limit={8} />}
              </Stack>

              {/* RIGHT — prioritized outreach plan */}
              <Box>
                <SectionTitle
                  icon={<PhoneInTalkOutlinedIcon fontSize="inherit" />}
                  caption="— who to call first this morning"
                >
                  Your outreach plan
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
            </Box>

            {brief.notes && brief.notes.length > 0 && (
              <Alert severity="warning" sx={{ fontSize: 13 }}>
                {brief.notes.map((note, idx) => (
                  <Typography key={idx} variant="body2">
                    {note}
                  </Typography>
                ))}
              </Alert>
            )}
          </Stack>
        )}

        <Box sx={{ mt: 4, textAlign: 'center' }}>
          <Typography variant="caption" color="text.secondary">
            Trading Morning Brief · fictional data.
          </Typography>
        </Box>
      </Container>
    </Box>
  );
}
