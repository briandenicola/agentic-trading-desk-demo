import { useState } from 'react';
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
import { runRmBriefing, type RmBriefing } from '../../api/client';
import CockpitNav from '../../components/CockpitNav';
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
  const [brief, setBrief] = useState<RmBriefing | null>(null);

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
            startIcon={loading ? <CircularProgress size={16} /> : <span>▶</span>}
            sx={{ textTransform: 'none', fontWeight: 500 }}
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
                  sx={{ bgcolor: 'rgba(255,255,255,0.03)', borderColor: 'rgba(255,255,255,0.12)' }}
                />
              ))}
            </Box>

            <Paper sx={{ p: 3 }}>
              <Typography variant="h6" sx={{ mb: 2, fontWeight: 500 }}>
                ✦ Agent reasoning
              </Typography>
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
            </Paper>

            <Box>
              <Typography variant="h6" sx={{ mb: 1.5, fontWeight: 500 }}>
                📞 Prioritized call list
                <Typography component="span" variant="body2" color="text.secondary">
                  {' '}
                  — ranked by complaints, follow-ups, closing &amp; stuck deals
                </Typography>
              </Typography>
              <Stack spacing={1.5}>
                {brief.priorityCallList.map((call) => (
                  <PriorityCallCard key={call.customerId} call={call} />
                ))}
              </Stack>
            </Box>

            <Box sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr', md: '1fr 1fr' }, gap: 2 }}>
              <Paper sx={{ height: '100%' }}>
                <Box sx={{ p: 2, borderBottom: '1px solid rgba(255,255,255,0.1)' }}>
                  <Typography variant="h6" sx={{ fontWeight: 500 }}>
                    ⚠️ Active complaints
                  </Typography>
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
                <Box sx={{ p: 2, borderBottom: '1px solid rgba(255,255,255,0.1)' }}>
                  <Typography variant="h6" sx={{ fontWeight: 500 }}>
                    💰 Pipeline closing ≤14 days
                  </Typography>
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
              <Typography variant="h6" sx={{ mb: 2, fontWeight: 500 }}>
                🌐 Macro snapshot
              </Typography>
              <Stack spacing={1.5}>
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

            <Alert
              icon={<span>👉</span>}
              severity="info"
              sx={{ bgcolor: 'rgba(79,140,255,0.12)', border: '1px solid rgba(79,140,255,0.3)' }}
            >
              <Typography variant="body2" sx={{ fontWeight: 700, mb: 0.5 }}>
                Suggested first action
              </Typography>
              <Typography variant="body2">{brief.suggestedFirstAction}</Typography>
            </Alert>

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
