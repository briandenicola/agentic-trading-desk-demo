import { useEffect, useState } from 'react';
import { Alert, Box, Button, CircularProgress, Container, Typography } from '@mui/material';
import PlayArrowRoundedIcon from '@mui/icons-material/PlayArrowRounded';
import { runMorningBrief, subscribeToEvents, type LiveAlert, type MorningBrief } from '../../api/client';
import CockpitNav from '../../components/CockpitNav';
import CockpitDashboardLayout from '../../components/CockpitDashboardLayout';
import { ChatDockProvider } from '../Workspace/ChatOverlay';
import { mint } from '../../theme/theme';

/**
 * M.INT 3-column cockpit scene (Phase 7). Fetches the morning call once, then holds an SSE
 * subscription so intraday events re-rank the Client / Ticker / Overall columns in place and
 * raise the live alert banner (002 US2 + US4, FR-010/FR-011). Mode-blind: DEMO and LIVE return
 * the same DTO.
 */
export default function CockpitScene() {
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [brief, setBrief] = useState<MorningBrief | null>(null);
  const [liveAlert, setLiveAlert] = useState<LiveAlert | null>(null);

  const handleRun = async () => {
    setLoading(true);
    setError(null);
    try {
      const result = await runMorningBrief({ payload: { eventId: 'fed_surprise_hike', date: '2026-06-04' } });
      setBrief(result);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to run the morning call');
    } finally {
      setLoading(false);
    }
  };

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

  return (
    <ChatDockProvider>
      <Box sx={{ minHeight: '100vh', bgcolor: 'background.default' }}>
        <CockpitNav />

        <Container maxWidth={false} sx={{ py: 4, px: { xs: 2, md: 4 } }}>
          <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', mb: 3, flexWrap: 'wrap', gap: 2 }}>
            <Box>
              <Typography variant="overline" color="text.secondary" sx={{ fontSize: '12px' }}>
                Markets Intelligence · Morning Call
              </Typography>
              <Typography variant="h4" sx={{ fontWeight: 700, color: '#e8eef5' }}>
                Cockpit Dashboard
              </Typography>
            </Box>
            <Button
              variant="contained"
              onClick={handleRun}
              disabled={loading}
              startIcon={loading ? <CircularProgress size={16} color="inherit" /> : <PlayArrowRoundedIcon />}
              sx={{ fontWeight: 600 }}
            >
              {loading ? 'Running…' : hasBrief ? 'Re-run morning call' : 'Run morning call'}
            </Button>
          </Box>

          {error && (
            <Alert severity="error" sx={{ mb: 2 }}>
              {error}
            </Alert>
          )}

          {brief ? (
            <CockpitDashboardLayout brief={brief} liveAlert={liveAlert} />
          ) : (
            !loading && (
              <Typography variant="body2" sx={{ color: mint.textDim }}>
                Run the morning call to populate the Client, Ticker, and Overall views.
              </Typography>
            )
          )}
        </Container>
      </Box>
    </ChatDockProvider>
  );
}
