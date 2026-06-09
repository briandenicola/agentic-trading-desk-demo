import { Box, Chip, Stack, Typography } from '@mui/material';
import { mint } from '../../theme/theme';
import type { MarketEvent } from '../../api/client';

interface EventListProps {
  events: MarketEvent[];
}

const SEVERITY_COLOR: Record<string, string> = {
  high: mint.red,
  medium: mint.amber,
  low: mint.green,
};

function entitySummary(e: MarketEvent): string {
  const ae = e.affectedEntities;
  if (!ae) return '—';
  const parts = [
    ...(ae.customerIds ?? []),
    ...(ae.tickers ?? []),
    ...(ae.sectors ?? []),
    ...(ae.issuers ?? []),
  ];
  return parts.length ? parts.join(', ') : '—';
}

/**
 * Read-only feed of the current event store (overnight seeds + injected intraday items),
 * newest first. Admin-injected items are tagged so the operator can confirm ingestion.
 */
export default function EventList({ events }: EventListProps) {
  if (events.length === 0) {
    return (
      <Typography variant="body2" sx={{ color: mint.textDim }} data-testid="event-list-empty">
        No events in the store yet.
      </Typography>
    );
  }

  return (
    <Stack spacing={1.5} data-testid="event-list">
      {events.map((e) => (
        <Box
          key={e.id}
          data-testid={`event-row-${e.id}`}
          sx={{
            border: `1px solid ${mint.border}`,
            borderRadius: 1,
            p: 1.5,
            background: 'rgba(255,255,255,0.02)',
          }}
        >
          <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, mb: 0.5, flexWrap: 'wrap' }}>
            <Chip
              label={e.severity}
              size="small"
              sx={{
                height: 18,
                fontSize: '10px',
                fontWeight: 700,
                color: '#000',
                background: SEVERITY_COLOR[e.severity] ?? mint.textDim,
              }}
            />
            {e.origin === 'admin' && (
              <Chip
                label="admin"
                size="small"
                variant="outlined"
                sx={{ height: 18, fontSize: '10px', color: mint.violetBright, borderColor: mint.violet }}
              />
            )}
            <Chip
              label={e.scope ?? 'overnight'}
              size="small"
              variant="outlined"
              sx={{ height: 18, fontSize: '10px', color: mint.textDim }}
            />
            <Typography variant="caption" sx={{ color: mint.textDim }}>
              {e.type}
            </Typography>
          </Box>
          <Typography variant="body2" sx={{ fontWeight: 600, color: '#e8eef5' }}>
            {e.headline}
          </Typography>
          <Typography variant="caption" sx={{ color: mint.textDim, display: 'block', mt: 0.5 }}>
            {entitySummary(e)}
          </Typography>
        </Box>
      ))}
    </Stack>
  );
}
