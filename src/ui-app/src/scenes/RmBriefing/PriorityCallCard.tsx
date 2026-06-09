import { Box, Chip, Paper, Typography } from '@mui/material';
import type { CallTagKind, PriorityCall } from '../../api/client';

// Left-border accent per priority band, mirroring the ground-truth briefing cards.
const BAND_COLOR: Record<number, string> = {
  1: '#ff5a5a', // red
  2: '#ffb547', // orange
  3: '#ffd23f', // amber
  4: '#3ddc97', // green
};

const TAG_COLOR: Record<CallTagKind, 'error' | 'warning' | 'success' | 'info' | 'default' | 'secondary'> = {
  escalated: 'error',
  'in-progress': 'warning',
  followup: 'info',
  closing: 'success',
  stuck: 'default',
  event: 'secondary',
};

function formatRevenue(mm?: number): string {
  return mm == null ? '' : `$${mm % 1 === 0 ? mm.toFixed(0) : mm.toFixed(1)}M revenue`;
}

interface PriorityCallCardProps {
  call: PriorityCall;
}

export default function PriorityCallCard({ call }: PriorityCallCardProps) {
  const accent = BAND_COLOR[call.priority] ?? BAND_COLOR[4];
  const meta = [
    call.customerId,
    call.industrySector,
    [call.hqCity, call.state].filter(Boolean).join(', '),
    formatRevenue(call.annualRevenueMm),
    call.riskRating ? `Risk ${call.riskRating}` : '',
  ]
    .filter(Boolean)
    .join(' · ');

  return (
    <Paper
      data-testid={`priority-call-${call.customerId}`}
      sx={{
        p: 2,
        borderLeft: `5px solid ${accent}`,
        bgcolor: 'rgba(255,255,255,0.02)',
      }}
    >
      <Box sx={{ display: 'flex', alignItems: 'baseline', justifyContent: 'space-between', gap: 2 }}>
        <Typography variant="subtitle1" sx={{ fontWeight: 700 }}>
          {call.rank}. {call.customerName}
        </Typography>
        <Chip
          label={`Score ${call.score}`}
          size="small"
          sx={{ fontWeight: 700, bgcolor: 'rgba(255,255,255,0.08)' }}
        />
      </Box>

      <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mt: 0.25 }}>
        {meta}
      </Typography>

      <Box sx={{ display: 'flex', flexWrap: 'wrap', gap: 0.75, mt: 1 }}>
        {call.tags.map((tag, idx) => (
          <Chip
            key={idx}
            label={tag.label}
            size="small"
            color={TAG_COLOR[tag.kind]}
            variant={tag.kind === 'stuck' ? 'outlined' : 'filled'}
            sx={{ fontSize: '11px', fontWeight: 600, height: 20 }}
          />
        ))}
      </Box>

      {call.reasons.length > 0 && (
        <Box component="ul" sx={{ pl: 2.5, my: 1, '& li': { mb: 0.5 } }}>
          {call.reasons.map((reason, idx) => (
            <Typography component="li" key={idx} variant="body2" color="text.secondary">
              {reason}
            </Typography>
          ))}
        </Box>
      )}

      {call.drivingEvents && call.drivingEvents.length > 0 && (
        <Box
          data-testid={`driving-events-${call.customerId}`}
          sx={{
            mt: 1,
            p: 1,
            borderRadius: 1,
            border: '1px solid rgba(124,92,255,0.35)',
            bgcolor: 'rgba(124,92,255,0.08)',
          }}
        >
          <Typography
            variant="caption"
            sx={{ fontWeight: 700, letterSpacing: '0.08em', color: 'secondary.main' }}
          >
            ⚡ DRIVING EVENTS
          </Typography>
          <Box component="ul" sx={{ pl: 2.5, my: 0.5, '& li': { mb: 0.25 } }}>
            {call.drivingEvents.map((ev) => (
              <Typography component="li" key={ev.eventId} variant="body2" color="text.secondary">
                {ev.rationale}
              </Typography>
            ))}
          </Box>
        </Box>
      )}

      <Typography variant="body2" sx={{ mt: 1 }}>
        <Box component="span" sx={{ fontWeight: 700, color: 'primary.main' }}>
          Suggested action:{' '}
        </Box>
        {call.suggestedAction}
      </Typography>
    </Paper>
  );
}
