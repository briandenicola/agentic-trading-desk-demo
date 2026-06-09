import { useCallback, useEffect, useState } from 'react';
import { Alert, Box, Container, Grid, Paper, Stack, Typography } from '@mui/material';
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
