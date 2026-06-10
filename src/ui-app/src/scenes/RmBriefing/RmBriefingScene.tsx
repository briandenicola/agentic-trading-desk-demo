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
import PhoneInTalkOutlinedIcon from '@mui/icons-material/PhoneInTalkOutlined';
import ReportProblemOutlinedIcon from '@mui/icons-material/ReportProblemOutlined';
import PaidOutlinedIcon from '@mui/icons-material/PaidOutlined';
import PublicOutlinedIcon from '@mui/icons-material/PublicOutlined';
import BoltOutlinedIcon from '@mui/icons-material/BoltOutlined';
import PlayArrowRoundedIcon from '@mui/icons-material/PlayArrowRounded';
import { runRmBriefing, subscribeToEvents, type LiveAlert, type RmBriefing } from '../../api/client';
import { usePersistentState } from '../../hooks/usePersistentState';
import CockpitNav from '../../components/CockpitNav';
import SectionTitle from '../../components/SectionTitle';
import AiInsightPanel from '../../components/AiInsightPanel';
import LiveAlertBanner from '../../components/LiveAlertBanner';
import { mint } from '../../theme/theme';
import PriorityCallCard from './PriorityCallCard';

function fmtMm(mm: number): string {
  return `$${mm >= 1000 ? `${(mm / 1000).toFixed(1)}B` : `${mm.toFixed(mm % 1 === 0 ? 0 : 1)}M`}`;
}

const statusColor = (status: string): 'error' | 'warning' | 'success' | 'default' => {
  const s = status.toLowerCase();
  if (s.includes('escalat')) return 'error';
  if (s.includes('progress')) return 'warning';
  if (s.includes('resolv')) return 'success';
  return 'default';
};

export default function RmBriefingScene() {
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [brief, setBrief] = usePersistentState<RmBriefing | null>('rm-briefing/brief', null);
  const [liveAlert, setLiveAlert] = useState<LiveAlert | null>(null);

  const handleRun = async () => {
    setLoading(true);
    setError(null);
    try {
      const result = await runRmBriefing({ payload: { rmId: 'RM-104', date: '2026-05-14' } });
      setBrief(result);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to run RM daily briefing');
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
    void handleRun();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  // Reactive live push (002 US2): once a briefing is on screen, hold an SSE subscription that
  // applies each re-synthesized DTO in place and surfaces a live alert banner (FR-010/FR-011).
  const hasBrief = brief !== null;
  useEffect(() => {
    if (!hasBrief) return;
    const unsubscribe = subscribeToEvents<RmBriefing>(
      'rm-briefing',
      {
        onUpdate: (update) => {
          setBrief(update.briefing);
          setLiveAlert(update.alert);
        },
      },
      { persona: 'RM-104' },
    );
    return unsubscribe;
  }, [hasBrief]);

  const kpis = brief?.kpis;
  const kpiChips = brief
    ? [
        { label: 'Portfolio', value: `${brief.portfolio.customerCount} customers` },
        { label: 'Total exposure', value: fmtMm(brief.portfolio.totalExposureMm) },
        { label: 'Total deposits', value: fmtMm(brief.portfolio.totalDepositsMm) },
        { label: 'Yesterday', value: `${kpis!.yesterdayTouchpoints} touchpoints` },
        { label: 'Open pipeline', value: `${kpis!.openPipelineCount} · ${fmtMm(kpis!.openPipelineAmountMm)}` },
        { label: 'Closing ≤14d', value: `${kpis!.closingWithin14Days}` },
        { label: 'Active complaints', value: `${kpis!.activeComplaints}` },
      ]
    : [];

  return (
    <Box sx={{ minHeight: '100vh', bgcolor: 'background.default' }}>
      <CockpitNav />

      <Container maxWidth="lg" sx={{ py: 4 }}>
        <Box sx={{ mb: 3 }}>
          <Typography variant="overline" color="text.secondary" sx={{ fontSize: '12px' }}>
            Morning · Commercial Banking — Relationship Manager
          </Typography>
          <Typography variant="h3" sx={{ mt: 1, mb: 2, fontWeight: 500 }}>
            {brief ? brief.greeting : 'RM Daily Briefing'}
          </Typography>
          {brief ? (
            <Typography variant="body1" color="text.secondary" sx={{ maxWidth: '820px' }}>
              {brief.rm.name}
              {brief.rm.title ? ` · ${brief.rm.title}` : ''}
              {brief.rm.territory ? ` · ${brief.rm.territory}` : ''} — {brief.portfolio.customerCount} customers,{' '}
              {fmtMm(brief.portfolio.totalExposureMm)} exposure, {fmtMm(brief.portfolio.totalDepositsMm)} deposits.
            </Typography>
          ) : (
            <Typography variant="body1" color="text.secondary" sx={{ maxWidth: '820px' }}>
              The assistant scores your book on complaints, overdue follow-ups, closing opportunities and stuck deals,
              then builds a ranked, prioritized call list — so you know exactly who to call first this morning.
            </Typography>
          )}
        </Box>

        <Box sx={{ mb: 3, display: 'flex', alignItems: 'center', gap: 2 }}>
          <Typography variant="body1" sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
            <span style={{ opacity: 0.5 }}>✦</span>
            <span>Who do I need to call first this morning?</span>
          </Typography>
          <Button
            variant="contained"
            onClick={handleRun}
            disabled={loading}
            startIcon={loading ? <CircularProgress size={16} /> : <PlayArrowRoundedIcon />}
            sx={{ textTransform: 'none', fontWeight: 600 }}
          >
            {loading ? 'Running...' : 'Run daily briefing'}
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
          <Stack spacing={2} data-testid="rm-briefing">
            <LiveAlertBanner alert={liveAlert} onDismiss={() => setLiveAlert(null)} />
            <Box sx={{ display: 'flex', flexWrap: 'wrap', gap: 1 }}>
              {kpiChips.map((k) => (
                <Chip
                  key={k.label}
                  label={
                    <>
                      <Box component="span" sx={{ color: 'text.secondary' }}>
                        {k.label}:{' '}
                      </Box>
                      <strong>{k.value}</strong>
                    </>
                  }
                  variant="outlined"
                  sx={{
                    height: 30,
                    bgcolor: `${mint.violet}14`,
                    borderColor: mint.border,
                    '& .MuiChip-label': { px: 1.5 },
                  }}
                />
              ))}
            </Box>

            <AiInsightPanel title="Agent reasoning">
              <Box component="ul" sx={{ listStyle: 'none', p: 0, m: 0 }}>
                {brief.reasoning.map((step, idx) => (
                  <Box component="li" key={idx} sx={{ display: 'flex', gap: 1.5, mb: 1.5, '&:last-child': { mb: 0 } }}>
                    <Typography
                      component="span"
                      sx={{ color: step.status === 'done' ? 'success.main' : 'text.disabled', fontSize: '14px', minWidth: '16px' }}
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

            <Box>
              <SectionTitle
                icon={<PhoneInTalkOutlinedIcon fontSize="inherit" />}
                caption="— ranked by complaints, follow-ups, closing & stuck deals"
              >
                Prioritized call list
              </SectionTitle>
              <Stack spacing={1.5} sx={{ mt: 1.5 }}>
                {brief.priorityCallList.map((call) => (
                  <PriorityCallCard key={call.customerId} call={call} />
                ))}
              </Stack>
            </Box>

            {brief.eventsConsidered && brief.eventsConsidered.length > 0 && (
              <Box data-testid="events-considered">
                <SectionTitle
                  icon={<BoltOutlinedIcon fontSize="inherit" />}
                  caption="— overnight & intraday signals the agent weighed into the ranking"
                >
                  Events considered ({brief.eventsConsidered.length})
                </SectionTitle>
                <Stack spacing={1} sx={{ mt: 1.5 }}>
                  {brief.eventsConsidered.map((ev) => (
                    <Paper
                      key={ev.id}
                      data-testid={`event-${ev.id}`}
                      sx={{ p: 1.5, bgcolor: 'rgba(255,255,255,0.02)' }}
                    >
                      <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, flexWrap: 'wrap' }}>
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
                      <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mt: 0.5 }}>
                        {ev.summary}
                      </Typography>
                    </Paper>
                  ))}
                </Stack>
              </Box>
            )}

            <Box sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr', md: '1fr 1fr' }, gap: 2 }}>
              <Paper sx={{ height: '100%' }}>
                <Box sx={{ p: 2, borderBottom: `1px solid ${mint.border}` }}>
                  <SectionTitle icon={<ReportProblemOutlinedIcon fontSize="inherit" />}>
                    Active complaints
                  </SectionTitle>
                  <Typography variant="caption" color="text.secondary">
                    Open service issues across the book
                  </Typography>
                </Box>
                <Table size="small">
                  <TableHead>
                    <TableRow>
                      <TableCell>Customer</TableCell>
                      <TableCell>Category</TableCell>
                      <TableCell>Status</TableCell>
                    </TableRow>
                  </TableHead>
                  <TableBody>
                    {brief.complaintsSnapshot.map((c) => (
                      <TableRow key={c.complaintId}>
                        <TableCell>
                          <Typography variant="body2" sx={{ fontWeight: 600 }}>
                            {c.customerName}
                          </Typography>
                          {c.dateFiled && (
                            <Typography variant="caption" color="text.secondary">
                              Filed {c.dateFiled}
                            </Typography>
                          )}
                        </TableCell>
                        <TableCell>
                          <Typography variant="body2">{c.category ?? '—'}</Typography>
                          {c.severity && (
                            <Typography variant="caption" color="text.secondary">
                              {c.severity}
                            </Typography>
                          )}
                        </TableCell>
                        <TableCell>
                          <Chip label={c.status} size="small" color={statusColor(c.status)} sx={{ fontSize: '11px' }} />
                        </TableCell>
                      </TableRow>
                    ))}
                    {brief.complaintsSnapshot.length === 0 && (
                      <TableRow>
                        <TableCell colSpan={3}>
                          <Typography variant="body2" color="text.secondary">
                            No active complaints.
                          </Typography>
                        </TableCell>
                      </TableRow>
                    )}
                  </TableBody>
                </Table>
              </Paper>

              <Paper sx={{ height: '100%' }}>
                <Box sx={{ p: 2, borderBottom: `1px solid ${mint.border}` }}>
                  <SectionTitle icon={<PaidOutlinedIcon fontSize="inherit" />}>
                    Pipeline closing ≤14 days
                  </SectionTitle>
                  <Typography variant="caption" color="text.secondary">
                    Opportunities with near-term expected close
                  </Typography>
                </Box>
                <Table size="small">
                  <TableHead>
                    <TableRow>
                      <TableCell>Customer</TableCell>
                      <TableCell>Product</TableCell>
                      <TableCell align="right">Amount</TableCell>
                      <TableCell>Close</TableCell>
                    </TableRow>
                  </TableHead>
                  <TableBody>
                    {brief.pipelineClosing.map((p) => (
                      <TableRow key={p.opportunityId}>
                        <TableCell>
                          <Typography variant="body2" sx={{ fontWeight: 600 }}>
                            {p.customerName}
                          </Typography>
                          {p.stage && (
                            <Typography variant="caption" color="text.secondary">
                              {p.stage}
                            </Typography>
                          )}
                        </TableCell>
                        <TableCell>
                          <Typography variant="body2">{p.productType ?? '—'}</Typography>
                        </TableCell>
                        <TableCell align="right">
                          <Typography variant="body2" sx={{ fontWeight: 600 }}>
                            {fmtMm(p.amountMm)}
                          </Typography>
                        </TableCell>
                        <TableCell>
                          <Typography variant="body2" color="text.secondary">
                            {p.expectedCloseDate ?? '—'}
                          </Typography>
                        </TableCell>
                      </TableRow>
                    ))}
                    {brief.pipelineClosing.length === 0 && (
                      <TableRow>
                        <TableCell colSpan={4}>
                          <Typography variant="body2" color="text.secondary">
                            Nothing closing in the next 14 days.
                          </Typography>
                        </TableCell>
                      </TableRow>
                    )}
                  </TableBody>
                </Table>
              </Paper>
            </Box>

            <Paper sx={{ p: 3 }}>
              <SectionTitle icon={<PublicOutlinedIcon fontSize="inherit" />}>Macro snapshot</SectionTitle>
              <Stack spacing={1.5} sx={{ mt: 2 }}>
                {brief.macroSnapshot.map((m, idx) => (
                  <Box key={idx}>
                    <Typography variant="body2" sx={{ fontWeight: 600 }}>
                      {m.headline}
                    </Typography>
                    <Typography variant="body2" color="text.secondary">
                      {m.detail}
                    </Typography>
                  </Box>
                ))}
              </Stack>
            </Paper>

            <Paper
              sx={{
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
                <Typography
                  variant="overline"
                  sx={{ color: mint.violetBright, display: 'block', lineHeight: 1.6 }}
                >
                  Suggested first action
                </Typography>
                <Typography variant="body2">{brief.suggestedFirstAction}</Typography>
              </Box>
            </Paper>

            {brief.notes && brief.notes.length > 0 && (
              <Alert severity="warning" sx={{ fontSize: '13px' }}>
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
            RM Daily Briefing · fictional data.
          </Typography>
        </Box>
      </Container>
    </Box>
  );
}
