import { Box, Chip } from '@mui/material';
import type { MarketStripItem } from '../../api/client';

interface MarketStripProps {
  items: MarketStripItem[];
}

export default function MarketStrip({ items }: MarketStripProps) {
  return (
    <Box
      sx={{
        display: 'flex',
        flexWrap: 'wrap',
        gap: 1.5,
        px: 3,
        py: 1.5,
        bgcolor: 'rgba(0,0,0,0.3)',
        borderBottom: '1px solid rgba(255,255,255,0.1)',
      }}
    >
      {items.map((item, idx) => {
        const arrow =
          item.direction === 'up' ? '▲' : item.direction === 'down' ? '▼' : '';

        return (
          <Chip
            key={idx}
            label={
              <>
                <strong>{item.label}</strong> {item.value}{' '}
                {item.change && (
                  <span style={{ color: item.direction === 'up' ? '#3ddc97' : item.direction === 'down' ? '#ff5a5a' : '#888' }}>
                    {arrow} {item.change}
                  </span>
                )}
              </>
            }
            variant="outlined"
            sx={{
              bgcolor: 'rgba(255,255,255,0.03)',
              borderColor: 'rgba(255,255,255,0.1)',
              fontSize: '13px',
              '& .MuiChip-label': {
                px: 1.5,
              },
            }}
          />
        );
      })}
    </Box>
  );
}

