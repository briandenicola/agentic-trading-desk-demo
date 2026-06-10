import { Fragment, useEffect, useRef, useState } from 'react';
import {
  Alert,
  Box,
  Button,
  Chip,
  CircularProgress,
  Container,
  LinearProgress,
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
  type OutreachItem,
  type RankingRationale,
} from '../../api/client';
import CallPlan from './CallPlan';
import MarketStrip from './MarketStrip';
import { usePersistentState } from '../../hooks/usePersistentState';
import CockpitNav from '../../components/CockpitNav';
import SectionTitle from '../../components/SectionTitle';
import AiInsightPanel from '../../components/AiInsightPanel';
import LiveAlertBanner from '../../components/LiveAlertBanner';
import { mint } from '../../theme/theme';
import NewspaperOutlinedIcon from '@mui/icons-material/NewspaperOutlined';
import GpsFixedOutlinedIcon from '@mui/icons-material/GpsFixedOutlined';
import PhoneInTalkOutlinedIcon from '@mui/icons-material/PhoneInTalkOutlined';
import PlayArrowRoundedIcon from '@mui/icons-material/PlayArrowRounded';

const rationaleScores = (rationale: RankingRationale) => [
  { label: 'Wallet', value: rationale.walletScore },
  { label: 'Engagement', value: rationale.engagementScore },
  { label: 'Event relevance', value: rationale.eventRelevanceScore },
  { label: 'Composite', value: rationale.compositeScore },
];

function formatScore(score: number): string {
  return `${Math.round(score * 100)}%`;
}

export default function MorningBriefScene() {
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [brief, setBrief] = usePersistentState<MorningBrief | null>('morning-brief/brief', null);
  const [expandedRationaleCid, setExpandedRationaleCid] = useState<string | null>(null);
  const [liveAlert, setLiveAlert] = useState<LiveAlert | null>(null);

  const handleRunBrief = async () => {
    setLoading(true);
    setError(null);
    setExpandedRationaleCid(null);
    try {
      const result = await runMorningBrief({
        payload: { eventId: 'fed_surprise_hike', date: '2026-06-04' },
      });
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
  }, [hasBrief]);

  const toggleRationale = (cid: string) => {
    setExpandedRationaleCid((current) => (current === cid ? null : cid));
  };

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
                    sx={{
                      display: 'flex',
                      gap: 1.5,
                      mb: 1.5,
                      '&:last-child': { mb: 0 },
                    }}
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
                  icon={<NewspaperOutlinedIcon fontSize="inherit" />}
                  caption="— overnight & intraday signals weighed into client linkage"
                >
                  Events considered ({brief.eventsConsidered.length})
                </SectionTitle>
                <Stack spacing={1} sx={{ mt: 2 }}>
                  {brief.eventsConsidered.map((ev) => (
                    <Box
                      key={ev.id}
                      data-testid={`event-${ev.id}`}
                      sx={{ display: 'flex', alignItems: 'center', gap: 1, flexWrap: 'wrap' }}
                    >
                      <Chip
                        label={ev.severity.toUpperCase()}
                        size="small"
                        color={ev.severity === 'high' ? 'error' : ev.severity === 'medium' ? 'warning' : 'default'}
                        sx={{ fontSize: '10px', fontWeight: 700, height: 20 }}
                      />
                      <Typography variant="body2">{ev.headline}</Typography>
                    </Box>
                  ))}
                </Stack>
              </Paper>
            )}

            <Box sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr', md: '1fr 1fr' }, gap: 2 }}>
              <Paper sx={{ height: '100%' }}>
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

              <Paper sx={{ height: '100%' }}>
                <Box sx={{ p: 2, borderBottom: `1px solid ${mint.border}` }}>
                  <SectionTitle icon={<PhoneInTalkOutlinedIcon fontSize="inherit" />}>
                    Outbound priority
                  </SectionTitle>
                  <Typography variant="caption" color="text.secondary">
                    Wallet + engagement + today's events
                  </Typography>
                </Box>
                <Table size="small">
                  <TableHead>
                    <TableRow>
                      <TableCell sx={{ width: '40px' }}>#</TableCell>
                      <TableCell>Client</TableCell>
                      <TableCell>Suggested topic + talking points</TableCell>
                      <TableCell>Rationale</TableCell>
                    </TableRow>
                  </TableHead>
                  <TableBody>
                    {brief.outreach.map((item: OutreachItem) => (
                      <Fragment key={item.cid}>
                        <TableRow>
                          <TableCell sx={{ textAlign: 'center', fontWeight: 600 }}>
                            {item.rank}
                          </TableCell>
                          <TableCell>
                            <Typography variant="body2" sx={{ fontWeight: 600 }}>
                              {item.name}
                            </Typography>
                          </TableCell>
                          <TableCell>
                            <Typography variant="body2" color="text.secondary" sx={{ mb: 1 }}>
                              {item.suggestedTopic}
                            </Typography>
                            <Box component="ul" sx={{ pl: 2, my: 0 }}>
                              {item.talkingPoints.map((point, idx) => (
                                <Typography
                                  component="li"
                                  key={idx}
                                  variant="caption"
                                  color="text.secondary"
                                  sx={{ display: 'list-item', mb: 0.5 }}
                                >
                                  {point}
                                </Typography>
                              ))}
                            </Box>
                          </TableCell>
                          <TableCell>
                            <Button
                              type="button"
                              size="small"
                              variant="outlined"
                              onClick={() => toggleRationale(item.cid)}
                              aria-expanded={expandedRationaleCid === item.cid}
                              aria-controls={`rationale-${item.cid}`}
                              sx={{ textTransform: 'none' }}
                            >
                              {expandedRationaleCid === item.cid ? 'Hide rationale' : 'Inspect rationale'}
                            </Button>
                          </TableCell>
                        </TableRow>
                        {expandedRationaleCid === item.cid && (
                          <TableRow>
                            <TableCell colSpan={4} sx={{ py: 0, borderBottomColor: 'rgba(255,255,255,0.08)' }}>
                              <Box
                                id={`rationale-${item.cid}`}
                                sx={{
                                  my: 1,
                                  p: 2,
                                  borderRadius: 2,
                                  border: '1px solid rgba(255,255,255,0.1)',
                                  bgcolor: 'rgba(79,140,255,0.08)',
                                }}
                              >
                                <Typography variant="subtitle2" sx={{ mb: 1 }}>
                                  Ranking rationale for {item.name}
                                </Typography>
                                <Stack spacing={1} sx={{ mb: 1.5 }}>
                                  {rationaleScores(item.rationale).map((score) => (
                                    <Box key={score.label}>
                                      <Box
                                        sx={{
                                          display: 'flex',
                                          justifyContent: 'space-between',
                                          mb: 0.5,
                                        }}
                                      >
                                        <Typography variant="caption" color="text.secondary">
                                          {score.label}
                                        </Typography>
                                        <Typography variant="caption" sx={{ fontWeight: 700 }}>
                                          {formatScore(score.value)}
                                        </Typography>
                                      </Box>
                                      <LinearProgress
                                        variant="determinate"
                                        value={Math.max(0, Math.min(100, score.value * 100))}
                                        color={score.label === 'Composite' ? 'success' : 'primary'}
                                        sx={{ height: 6, borderRadius: 99, bgcolor: 'rgba(255,255,255,0.1)' }}
                                      />
                                    </Box>
                                  ))}
                                </Stack>
                                <Typography variant="caption" color="text.secondary">
                                  {item.rationale.explanation}
                                </Typography>
                              </Box>
                            </TableCell>
                          </TableRow>
                        )}
                      </Fragment>
                    ))}
                  </TableBody>
                </Table>
              </Paper>
            </Box>

            <CallPlan outreach={brief.outreach} asOf={brief.asOf} />

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
