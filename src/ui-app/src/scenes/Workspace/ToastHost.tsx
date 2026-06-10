import { Box, IconButton, Stack, Typography } from '@mui/material';
import CloseRoundedIcon from '@mui/icons-material/CloseRounded';
import BoltRoundedIcon from '@mui/icons-material/BoltRounded';
import { mint } from '../../theme/theme';
import type { AlertPriority } from './workspaceData';
import { useWorkspaceLive } from './useWorkspaceLive';

const PRIORITY_COLOR: Record<AlertPriority, string> = {
  HIGH: mint.red,
  MEDIUM: mint.amber,
  LOW: mint.cyan,
  DONE: mint.green,
};

/**
 * Fixed bottom-right toast stack. Each impactful intraday event slides in a toast
 * that self-dismisses (timer lives in the provider) and is colour-coded by priority.
 */
export default function ToastHost() {
  const { toasts, dismissToast } = useWorkspaceLive();
  if (toasts.length === 0) return null;

  return (
    <Stack
      spacing={1.25}
      sx={{ position: 'fixed', right: 20, bottom: 20, zIndex: 1400, width: 320, maxWidth: 'calc(100vw - 40px)' }}
    >
      {toasts.map((t) => {
        const color = PRIORITY_COLOR[t.priority];
        return (
          <Box
            key={t.id}
            sx={{
              display: 'flex',
              alignItems: 'flex-start',
              gap: 1.25,
              p: 1.5,
              borderRadius: 2.5,
              border: `1px solid ${color}66`,
              background: `linear-gradient(160deg, ${color}1f, ${mint.paperHi})`,
              boxShadow: '0 12px 32px rgba(0,0,0,0.45)',
              '@keyframes mintToastIn': {
                from: { transform: 'translateX(24px)', opacity: 0 },
                to: { transform: 'none', opacity: 1 },
              },
              animation: 'mintToastIn 240ms ease-out',
            }}
          >
            <Box
              sx={{
                width: 28,
                height: 28,
                borderRadius: 1.5,
                flexShrink: 0,
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
                color: '#fff',
                background: color,
              }}
            >
              <BoltRoundedIcon sx={{ fontSize: 16 }} />
            </Box>
            <Box sx={{ flex: 1, minWidth: 0 }}>
              <Typography sx={{ fontSize: 10, fontWeight: 800, letterSpacing: '0.6px', color }}>
                LIVE · {t.priority}
              </Typography>
              <Typography sx={{ fontSize: 12.5, fontWeight: 600, color: mint.text, lineHeight: 1.4 }}>
                {t.headline}
              </Typography>
            </Box>
            <IconButton
              size="small"
              aria-label="Dismiss notification"
              onClick={() => dismissToast(t.id)}
              sx={{ color: mint.textDim, p: 0.25 }}
            >
              <CloseRoundedIcon sx={{ fontSize: 16 }} />
            </IconButton>
          </Box>
        );
      })}
    </Stack>
  );
}
