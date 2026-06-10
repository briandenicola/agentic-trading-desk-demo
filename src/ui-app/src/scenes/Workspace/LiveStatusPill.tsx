import { Box, Typography } from '@mui/material';
import { mint } from '../../theme/theme';
import { useWorkspaceLive } from './useWorkspaceLive';

/**
 * Header LIVE status pill. A pulsing dot signals the open SSE subscription; when
 * intraday events have arrived unseen, an unread count rides alongside it.
 */
export default function LiveStatusPill() {
  const { connected, unread } = useWorkspaceLive();
  const color = connected ? mint.green : mint.textDim;

  return (
    <Box
      sx={{
        display: 'flex',
        alignItems: 'center',
        gap: 0.75,
        px: 1.25,
        py: 0.5,
        borderRadius: 2,
        border: `1px solid ${color}55`,
        background: `${color}14`,
      }}
    >
      <Box
        sx={{
          width: 8,
          height: 8,
          borderRadius: '50%',
          bgcolor: color,
          '@keyframes mintLivePulse': {
            '0%, 100%': { opacity: 1, boxShadow: `0 0 0 0 ${color}88` },
            '50%': { opacity: 0.35, boxShadow: `0 0 0 4px ${color}00` },
          },
          animation: connected ? 'mintLivePulse 1.4s ease-in-out infinite' : 'none',
        }}
      />
      <Typography sx={{ fontSize: 11, fontWeight: 800, letterSpacing: '1px', color }}>
        {connected ? 'LIVE' : 'OFFLINE'}
      </Typography>
      {unread > 0 && (
        <Box
          sx={{
            minWidth: 16,
            height: 16,
            px: 0.5,
            borderRadius: '8px',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            fontSize: 10,
            fontWeight: 800,
            color: '#04101c',
            bgcolor: mint.red,
          }}
        >
          {unread}
        </Box>
      )}
    </Box>
  );
}
