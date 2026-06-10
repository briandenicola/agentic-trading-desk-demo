import { useEffect, useRef, useState } from 'react';
import {
  Alert,
  Box,
  Button,
  Chip,
  CircularProgress,
  Container,
  Paper,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableRow,
  Typography,
} from '@mui/material';
import {
  runMorningBrief,
  subscribeToEvents,
  type AffectedClient,
  type LiveAlert,
  type MorningBrief,
} from '../../api/client';
import CallPlan from './CallPlan';
import MarketStrip from './MarketStrip';
import { usePersistentState, loadPersistentOnce } from '../../hooks/usePersistentState';
import CockpitNav from '../../components/CockpitNav';
import SectionTitle from '../../components/SectionTitle';
import AiInsightPanel from '../../components/AiInsightPanel';
import LiveAlertBanner from '../../components/LiveAlertBanner';
import { mint } from '../../theme/theme';
import NewspaperOutlinedIcon from '@mui/icons-material/NewspaperOutlined';
import BoltOutlinedIcon from '@mui/icons-material/BoltOutlined';
import GpsFixedOutlinedIcon from '@mui/icons-material/GpsFixedOutlined';
import PlayArrowRoundedIcon from '@mui/icons-material/PlayArrowRounded';

export default function MorningBriefScene() {
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [brief, setBrief] = usePersistentState<MorningBrief | null>('morning-brief/brief', null);
  const [liveAlert, setLiveAlert] = useState<LiveAlert | null>(null);

  const handleRunBrief = async () => {
    setLoading(true);
    setError(null);
    try {
      const result = await loadPersistentOnce('morning-brief/brief', () =>
        runMorningBrief({
          payload: { eventId: 'fed_surprise_hike', date: '2026-06-04' },
        }),
      );
      setBrief(result);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to run morning brief');
    } finally {
      setLoading(false);
    }
  };

  // Auto-run on first visit so the page renders real data without a manual click.
  // The brief is persisted, so navigating back won't re-trigger the agent.
  const didAutoRun = useRef(false);
  useEffect(() => {
    if (didAutoRun.current || brief !== null || loading) return;
    didAutoRun.current = true;
    void handleRunBrief();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  // Reactive live push (002 US2): hold an SSE subscription while a brief is on screen so intraday
  // events re-rank the plan in place and raise a live alert banner (FR-010/FR-011).
  const hasBrief = brief !== null;
  useEffect(() => {
    if (!hasBrief) return;
    const unsubscribe = subscribeToEvents<MorningBrief>('morning-brief', {
      onUpdate: (update) => {
        setBrief(update.briefing);
        setLiveAlert(update.alert);
      },
    });
    return unsubscribe;
  }, [hasBrief, setBrief]);

  return (
    <Box sx={{ minHeight: '100vh', bgcolor: 'background.default' }}>
      <CockpitNav />
      {brief && <MarketStrip items={brief.marketStrip} />}

      <Container maxWidth="lg" sx={{ py: 4 }}>
        <Box sx={{ mb: 4 }}>
          <Typography variant="overline" color="text.secondary" sx={{ fontSize: '12px' }}>
            Morning · 7:30 AM — Pre-market planning
          </Typography>
          <Typography variant="h3" sx={{ mt: 1, mb: 2, fontWeight: 500 }}>
            "What do I need to know this morning?"
          </Typography>
          <Typography variant="body1" color="text.secondary" sx={{ maxWidth: '800px' }}>
            Overnight, a surprise Fed move drove rate volatility. The assistant turns that into a
            market narrative, flags the clients most affected, and builds a ranked outreach plan —
            all editable before the VP starts dialing.
          </Typography>
        </Box>

        <Box sx={{ mb: 3, display: 'flex', alignItems: 'center', gap: 2 }}>
          <Typography variant="body1" sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
            <span style={{ opacity: 0.5 }}>✦</span>
            <span>What do I need to know this morning?</span>
          </Typography>
          <Button
            variant="contained"
            onClick={handleRunBrief}
            disabled={loading}
            startIcon={loading ? <CircularProgress size={16} /> : <PlayArrowRoundedIcon />}
            sx={{ textTransform: 'none', fontWeight: 600 }}
          >
            {loading ? 'Running...' : 'Run morning brief'}
          </Button>
          {brief && (
            <Chip
              label={brief.mode}
              size="small"
              color={brief.mode === 'LIVE' ? 'success' : 'default'}
              sx={{ fontWeight: 700 }}
            />
          )}
        </Box>

        {error && (
          <Alert severity="error" sx={{ mb: 3 }}>
            {error}
          </Alert>
        )}

        {brief && (
          <Stack spacing={2}>
            <LiveAlertBanner alert={liveAlert} onDismiss={() => setLiveAlert(null)} />

            <AiInsightPanel title="Agent reasoning">
              <Box component="ul" sx={{ listStyle: 'none', p: 0, m: 0 }}>
                {brief.reasoning.map((step, idx) => (
                  <Box
                    component="li"
                    key={idx}
                    sx={{ display: 'flex', gap: 1.5, mb: 1.5, '&:last-child': { mb: 0 } }}
                  >
                    <Typography
                      component="span"
                      sx={{
                        color: step.status === 'done' ? 'success.main' : 'text.disabled',
                        fontSize: '14px',
                        minWidth: '16px',
                      }}
                    >
                      {step.status === 'done' ? '✓' : '◦'}
                    </Typography>
                    <Typography variant="body2" sx={{ flex: 1 }}>
                      {step.text}
                    </Typography>
                  </Box>
                ))}
              </Box>
            </AiInsightPanel>

            {/* Two-column cockpit: macro analysis + details on the left, the outreach plan on the right. */}
            <Box
              sx={{
                display: 'grid',
                gridTemplateColumns: { xs: '1fr', lg: '1fr 1fr' },
                gap: 2,
                alignItems: 'start',
              }}
            >
              {/* LEFT — Macro event analysis & details */}
              <Stack spacing={2}>
                <Paper sx={{ p: 3 }}>
                  <SectionTitle
                    icon={<NewspaperOutlinedIcon fontSize="inherit" />}
                    caption="— a narrative you can share with clients"
                  >
                    Macro event analysis
                  </SectionTitle>
                  <Typography variant="body2" sx={{ mt: 2, mb: 1.5 }}>
                    {brief.macroNarrative.summary}
                  </Typography>
                  <Typography variant="body2" color="text.secondary" sx={{ mb: 2, fontStyle: 'italic' }}>
                    <strong>Why it matters:</strong> {brief.macroNarrative.whyItMatters}
                  </Typography>
                  <Box sx={{ display: 'flex', flexWrap: 'wrap', gap: 1 }}>
                    {brief.macroNarrative.sources.map((src, idx) => (
                      <Chip
                        key={idx}
                        label={src}
                        size="small"
                        variant="outlined"
                        sx={{
                          fontSize: '11px',
                          borderColor: 'rgba(255,255,255,0.2)',
                          bgcolor: 'rgba(255,255,255,0.03)',
                        }}
                      />
                    ))}
                  </Box>
                </Paper>

                {brief.eventsConsidered && brief.eventsConsidered.length > 0 && (
                  <Paper sx={{ p: 3 }} data-testid="events-considered">
                    <SectionTitle
                      icon={<BoltOutlinedIcon fontSize="inherit" />}
                      caption="— overnight & intraday signals weighed into client linkage"
                    >
                      Events considered ({brief.eventsConsidered.length})
                    </SectionTitle>
                    <Stack spacing={1.25} sx={{ mt: 2 }}>
                      {brief.eventsConsidered.map((ev) => (
                        <Box
                          key={ev.id}
                          data-testid={`event-${ev.id}`}
                          sx={{
                            p: 1.5,
                            borderRadius: 2,
                            border: `1px solid ${mint.borderSoft}`,
                            bgcolor: 'rgba(255,255,255,0.02)',
                            borderLeft: `3px solid ${
                              ev.severity === 'high' ? mint.red : ev.severity === 'medium' ? mint.amber : mint.cyan
                            }`,
                          }}
                        >
                          <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, flexWrap: 'wrap', mb: 0.5 }}>
                            <Chip
                              label={ev.severity.toUpperCase()}
                              size="small"
                              color={ev.severity === 'high' ? 'error' : ev.severity === 'medium' ? 'warning' : 'default'}
                              sx={{ fontSize: '10px', fontWeight: 700, height: 20 }}
                            />
                            {ev.scope && (
                              <Chip
                                label={ev.scope}
                                size="small"
                                variant="outlined"
                                sx={{ fontSize: '10px', height: 20, borderColor: mint.border }}
                              />
                            )}
                            <Typography variant="body2" sx={{ fontWeight: 600 }}>
                              {ev.headline}
                            </Typography>
                          </Box>
                          <Typography variant="caption" color="text.secondary">
                            {ev.summary}
                          </Typography>
                        </Box>
                      ))}
                    </Stack>
                  </Paper>
                )}

                <Paper>
                  <Box sx={{ p: 2, borderBottom: `1px solid ${mint.border}` }}>
                    <SectionTitle icon={<GpsFixedOutlinedIcon fontSize="inherit" />}>
                      Most-affected clients
                    </SectionTitle>
                    <Typography variant="caption" color="text.secondary">
                      Flagged by portfolio rate sensitivity
                    </Typography>
                  </Box>
                  <Table size="small">
                    <TableHead>
                      <TableRow>
                        <TableCell>Client</TableCell>
                        <TableCell>Exposure</TableCell>
                        <TableCell>Concern</TableCell>
                      </TableRow>
                    </TableHead>
                    <TableBody>
                      {brief.mostAffectedClients.map((client: AffectedClient) => (
                        <TableRow key={client.cid}>
                          <TableCell>
                            <Typography variant="body2" sx={{ fontWeight: 600 }}>
                              {client.name}
                            </Typography>
                            <Typography variant="caption" color="text.secondary">
                              {client.tier}
                            </Typography>
                            {client.drivingEvents && client.drivingEvents.length > 0 && (
                              <Box data-testid={`driving-events-${client.cid}`} sx={{ mt: 0.5 }}>
                                {client.drivingEvents.map((ev) => (
                                  <Typography
                                    key={ev.eventId}
                                    variant="caption"
                                    sx={{ display: 'block', color: mint.violetBright }}
                                  >
                                    ⚡ {ev.headline}
                                  </Typography>
                                ))}
                              </Box>
                            )}
                          </TableCell>
                          <TableCell>
                            <Typography variant="body2">{client.exposure}</Typography>
                          </TableCell>
                          <TableCell>
                            <Chip
                              label={client.concern.label}
                              size="small"
                              color={
                                client.concern.kind === 'sell'
                                  ? 'error'
                                  : client.concern.kind === 'warm'
                                    ? 'warning'
                                    : 'info'
                              }
                              sx={{ fontSize: '11px' }}
                            />
                          </TableCell>
                        </TableRow>
                      ))}
                    </TableBody>
                  </Table>
                </Paper>
              </Stack>

              {/* RIGHT — Your outreach plan */}
              <CallPlan outreach={brief.outreach} asOf={brief.asOf} />
            </Box>

            {brief.notes && brief.notes.length > 0 && (
              <Alert severity="info" sx={{ fontSize: '13px' }}>
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

        <Box sx={{ mt: 4, textAlign: 'center' }}>
          <Typography variant="caption" color="text.secondary">
            Demo 1 of 5 · fictional data.
          </Typography>
        </Box>
      </Container>
    </Box>
  );
}
