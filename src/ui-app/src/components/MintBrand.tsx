import { Box, Typography } from '@mui/material';
import { mint } from '../theme/theme';

interface MintBrandProps {
  size?: 'sm' | 'md';
}

/**
 * M.INT — Markets Intelligence logo lockup. The "M" carries a magenta→violet
 * gradient, ".INT" is solid white, with a letter-spaced subtitle beneath.
 */
export default function MintBrand({ size = 'md' }: MintBrandProps) {
  const logoSize = size === 'md' ? 30 : 24;
  const subSize = size === 'md' ? '10px' : '9px';

  return (
    <Box>
      <Box sx={{ display: 'flex', alignItems: 'baseline', lineHeight: 1 }}>
        <Typography
          component="span"
          sx={{
            fontSize: logoSize,
            fontWeight: 800,
            letterSpacing: '-1px',
            backgroundImage: `linear-gradient(135deg, ${mint.blue} 0%, ${mint.cyan} 100%)`,
            WebkitBackgroundClip: 'text',
            backgroundClip: 'text',
            color: 'transparent',
          }}
        >
          M
        </Typography>
        <Typography
          component="span"
          sx={{ fontSize: logoSize, fontWeight: 800, letterSpacing: '-1px', color: '#ffffff' }}
        >
          .INT
        </Typography>
      </Box>
      <Typography
        component="div"
        sx={{
          fontSize: subSize,
          fontWeight: 700,
          letterSpacing: '3.5px',
          color: mint.textDim,
          mt: 0.25,
        }}
      >
        MARKETS INTELLIGENCE
      </Typography>
    </Box>
  );
}
