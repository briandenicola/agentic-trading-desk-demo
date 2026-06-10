import type { ReactNode } from 'react';
import { Box, Typography } from '@mui/material';
import { mint } from '../theme/theme';

interface SectionTitleProps {
  icon?: ReactNode;
  children: ReactNode;
  /** Optional muted trailing descriptor rendered inline after the title. */
  caption?: ReactNode;
  color?: 'cyan' | 'violet';
}

/**
 * Cyan/violet uppercase section heading in the M.INT terminal style. Renders an
 * accessible <h6> so the visible title remains queryable by role.
 */
export default function SectionTitle({ icon, children, caption, color = 'cyan' }: SectionTitleProps) {
  const accent = color === 'cyan' ? mint.cyan : mint.violet;

  return (
    <Typography
      variant="h6"
      sx={{
        display: 'flex',
        alignItems: 'center',
        gap: 1,
        color: accent,
        textTransform: 'uppercase',
        letterSpacing: '1px',
        fontSize: '14px',
        fontWeight: 700,
      }}
    >
      {icon && (
        <Box component="span" aria-hidden sx={{ display: 'inline-flex', color: accent, fontSize: 18 }}>
          {icon}
        </Box>
      )}
      <span>{children}</span>
      {caption && (
        <Box
          component="span"
          sx={{
            textTransform: 'none',
            letterSpacing: 0,
            fontSize: '12px',
            fontWeight: 400,
            color: mint.textDim,
          }}
        >
          {caption}
        </Box>
      )}
    </Typography>
  );
}
