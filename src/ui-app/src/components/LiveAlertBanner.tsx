import { Box, Chip, Collapse, IconButton, Paper, Stack, Typography } from '@mui/material';
import CloseRoundedIcon from '@mui/icons-material/CloseRounded';
import BoltOutlinedIcon from '@mui/icons-material/BoltOutlined';
import { mint } from '../theme/theme';
import type { LiveAlert } from '../api/client';

interface LiveAlertBannerProps {
  alert: LiveAlert | null;
  onDismiss?: () => void;
}

const PRIORITY_STYLE: Record<
  LiveAlert['priority'],
  { color: string; label: string }
> = {
  urgent: { color: mint.red, label: 'URGENT' },
  notice: { color: mint.amber, label: 'NOTICE' },
  info: { color: mint.cyan, label: 'INFO' },
};

/**
 * Live event banner (002 US2, FR-011) driven by the reactive SSE stream. Slides in when a new
 * intraday event re-synthesizes the briefing; severity maps to colour (info/notice/urgent), and
 * a `noImpact` update renders as a low-key "no portfolio impact" note. Styled with the M.INT
 * design language.
 */
export default function LiveAlertBanner({ alert, onDismiss }: LiveAlertBannerProps) {
  const open = alert !== null;
  const priority = alert ? PRIORITY_STYLE[alert.priority] : PRIORITY_STYLE.info;
  const accent = alert?.noImpact ? mint.textDim : priority.color;

  return (
    <Collapse in={open} unmountOnExit>
      <Paper
        data-testid="live-alert-banner"
        sx={{
          p: 1.5,
          mb: 2,
          display: 'flex',
          alignItems: 'center',
          gap: 1.5,
          borderColor: `${accent}55`,
          backgroundImage: `linear-gradient(90deg, ${accent}1f, transparent 70%)`,
          position: 'relative',
          overflow: 'hidden',
          '&::before': {
            content: '""',
            position: 'absolute',
            left: 0,
            top: 0,
            bottom: 0,
            width: 3,
            background: accent,
          },
        }}
      >
        <BoltOutlinedIcon sx={{ color: accent, fontSize: 20 }} />
        <Stack spacing={0.25} sx={{ flex: 1, minWidth: 0 }}>
          <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, flexWrap: 'wrap' }}>
            <Chip
              data-testid="live-alert-priority"
              label={alert?.noImpact ? 'NO IMPACT' : priority.label}
              size="small"
              sx={{
                height: 20,
                fontSize: '10px',
                fontWeight: 700,
                color: accent,
                bgcolor: `${accent}1f`,
                border: `1px solid ${accent}55`,
              }}
            />
            <Typography variant="caption" sx={{ color: mint.textDim, fontWeight: 600 }}>
              Live update · {alert?.eventIds.length ?? 0} new event
              {(alert?.eventIds.length ?? 0) === 1 ? '' : 's'}
            </Typography>
          </Box>
          <Typography variant="body2" sx={{ fontWeight: 600 }}>
            {alert?.headline}
          </Typography>
        </Stack>
        {onDismiss && (
          <IconButton
            size="small"
            onClick={onDismiss}
            aria-label="Dismiss live alert"
            sx={{ color: mint.textDim }}
          >
            <CloseRoundedIcon fontSize="small" />
          </IconButton>
        )}
      </Paper>
    </Collapse>
  );
}
