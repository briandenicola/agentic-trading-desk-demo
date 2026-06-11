import { useCallback, useEffect, useState } from 'react';
import { Alert, Box, Button, Container, Grid, Paper, Stack, Typography } from '@mui/material';
import BoltOutlinedIcon from '@mui/icons-material/BoltOutlined';
import CampaignOutlinedIcon from '@mui/icons-material/CampaignOutlined';
import FormatListBulletedRoundedIcon from '@mui/icons-material/FormatListBulletedRounded';
import { ingestNews, listEvents, type AdminNewsSubmission, type MarketEvent } from '../../api/client';
import CockpitNav from '../../components/CockpitNav';
import SectionTitle from '../../components/SectionTitle';
import AiInsightPanel from '../../components/AiInsightPanel';
import { mint } from '../../theme/theme';
import NewsForm from './NewsForm';
import EventList from './EventList';

/**
 * The marquee Institutional Sales & Trading event: an overnight AI-capex upgrade that hits the
 * AI compute basket Theo's funds hold. Injecting it re-ranks the trading-desk call list live
 * (Hyperion + Tradewinds jump to the top) — the "highly visible update from /admin" the desk
 * asked for. Routed through the same ingestion path as a real feed (FR-016).
 */
const TD_MARQUEE_INJECT: AdminNewsSubmission = {
  headline: 'AI-capex upgrade: hyperscaler datacenter spend revised sharply higher',
  summary:
    'Overnight prints lift FY26 hyperscaler capex guidance, fuelling the AI compute basket. ' +
    'Quartzite Semiconductors and Nimbus Cloud Holdings lead; desks are reloading the names.',
  source: 'Trading Desk — Morning Wire',
  severity: 'high',
  type: 'sector',
  direction: 'positive',
  affectedEntities: {
    tickers: ['SEC-3003', 'SEC-3002'],
    issuers: ['Quartzite Semiconductors', 'Nimbus Cloud Holdings'],
    sectors: ['Technology'],
  },
};

/**
 * Admin cockpit (002 US3). Operators inject intraday news items that flow through the SAME
 * ingestion + reactive path as a real feed (FR-016) so open briefings react within ~10s. The
 * event feed refreshes after each successful injection.
 */
export default function AdminScene() {
  const [events, setEvents] = useState<MarketEvent[]>([]);
  const [submitting, setSubmitting] = useState(false);
  const [notice, setNotice] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  const refresh = useCallback(async () => {
    try {
      const list = await listEvents();
      setEvents([...list].reverse());
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load events');
    }
  }, []);

  useEffect(() => {
    void refresh();
  }, [refresh]);

  const handleSubmit = async (submission: AdminNewsSubmission) => {
    setSubmitting(true);
    setNotice(null);
    setError(null);
    try {
      const stored = await ingestNews(submission);
      setNotice(`Injected "${stored.headline}" (${stored.id}). Open briefings will react shortly.`);
      await refresh();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to inject news item');
    } finally {
      setSubmitting(false);
    }
  };

  const handleMarqueeInject = () => handleSubmit(TD_MARQUEE_INJECT);

  return (
    <Box sx={{ minHeight: '100vh', bgcolor: 'background.default' }}>
      <CockpitNav />

      <Container maxWidth="lg" sx={{ py: 4 }}>
        <Box sx={{ mb: 3 }}>
          <Typography variant="overline" color="text.secondary" sx={{ fontSize: '12px' }}>
            Operations · Event Injection
          </Typography>
          <Typography variant="h4" sx={{ fontWeight: 700, color: '#e8eef5' }}>
            News Desk
          </Typography>
          <Typography variant="body2" sx={{ color: mint.textDim, mt: 0.5 }}>
            Inject intraday news the agents react to. Items use the same ingestion path as a live feed.
          </Typography>
        </Box>

        {notice && (
          <Alert severity="success" sx={{ mb: 2 }} data-testid="admin-notice">
            {notice}
          </Alert>
        )}
        {error && (
          <Alert severity="error" sx={{ mb: 2 }} data-testid="admin-error">
            {error}
          </Alert>
        )}

        <Grid container spacing={3}>
          <Grid size={{ xs: 12, md: 7 }}>
            <Paper sx={{ p: 3, mb: 3, borderLeft: `3px solid ${mint.cyan}` }}>
              <SectionTitle icon={<BoltOutlinedIcon />} color="cyan" caption="trading desk · one-click">
                Marquee Re-Rank
              </SectionTitle>
              <Typography variant="body2" sx={{ color: '#cdd6e6', mt: 1.5 }}>
                Inject the overnight <Box component="span" sx={{ color: mint.cyan }}>AI-capex upgrade</Box> that
                hits the AI compute basket. Open trading-desk briefings re-rank within ~10s —
                Hyperion &amp; Tradewinds jump to the top of the call list.
              </Typography>
              <Button
                onClick={handleMarqueeInject}
                disabled={submitting}
                variant="contained"
                startIcon={<BoltOutlinedIcon />}
                data-testid="admin-marquee-inject"
                sx={{ mt: 2, fontWeight: 600 }}
              >
                {submitting ? 'Injecting…' : 'Inject AI-capex breaking print'}
              </Button>
            </Paper>

            <Paper sx={{ p: 3 }}>
              <SectionTitle icon={<CampaignOutlinedIcon />} caption="compose & inject">
                Inject News Item
              </SectionTitle>
              <Box sx={{ mt: 2 }}>
                <NewsForm onSubmit={handleSubmit} submitting={submitting} />
              </Box>
            </Paper>

            <Box sx={{ mt: 3 }}>
              <AiInsightPanel title="How injection works">
                <Stack spacing={1}>
                  <Typography variant="body2" sx={{ color: '#cdd6e6' }}>
                    Injected items flow through the same ingestion path as a live feed — tagged
                    <Box component="span" sx={{ color: mint.violetBright }}> origin: admin</Box> ·
                    <Box component="span" sx={{ color: mint.cyan }}> scope: intraday</Box>.
                  </Typography>
                  <Typography variant="body2" sx={{ color: '#cdd6e6' }}>
                    The reactive poller detects the event and the specialist fan-out re-ranks open
                    briefings within ~10 seconds.
                  </Typography>
                </Stack>
              </AiInsightPanel>
            </Box>
          </Grid>
          <Grid size={{ xs: 12, md: 5 }}>
            <Paper sx={{ p: 3 }}>
              <SectionTitle icon={<FormatListBulletedRoundedIcon />} color="violet" caption={`${events.length} events`}>
                Event Store
              </SectionTitle>
              <Box sx={{ mt: 2 }}>
                <Stack spacing={1.5}>
                  <EventList events={events} />
                </Stack>
              </Box>
            </Paper>
          </Grid>
        </Grid>
      </Container>
    </Box>
  );
}
