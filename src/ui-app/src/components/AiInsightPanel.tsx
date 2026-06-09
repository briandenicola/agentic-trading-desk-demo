import type { ReactNode } from 'react';
import { Box, Paper, Typography } from '@mui/material';
import AutoAwesomeIcon from '@mui/icons-material/AutoAwesome';
import { mint } from '../theme/theme';

interface AiInsightPanelProps {
  title?: string;
  children: ReactNode;
}

/**
 * Violet-accented "AI INSIGHTS" panel matching the M.INT layout: a sparkle-marked
 * header over a subtly glowing surface. Used for agent reasoning and AI-derived callouts.
 */
export default function AiInsightPanel({ title = 'AI Insights', children }: AiInsightPanelProps) {
  return (
    <Paper
      sx={{
        p: 3,
        position: 'relative',
        overflow: 'hidden',
        backgroundImage: `linear-gradient(180deg, ${mint.violet}14, transparent 55%)`,
        borderColor: `${mint.violet}3d`,
        '&::before': {
          content: '""',
          position: 'absolute',
          left: 0,
          top: 0,
          bottom: 0,
          width: 3,
          background: `linear-gradient(180deg, ${mint.violetBright}, ${mint.violet})`,
        },
      }}
    >
      <Typography
        sx={{
          display: 'flex',
          alignItems: 'center',
          gap: 1,
          mb: 2,
          color: mint.violetBright,
          textTransform: 'uppercase',
          letterSpacing: '1px',
          fontSize: '13px',
          fontWeight: 700,
        }}
      >
        <AutoAwesomeIcon sx={{ fontSize: 16 }} />
        {title}
      </Typography>
      <Box>{children}</Box>
    </Paper>
  );
}
