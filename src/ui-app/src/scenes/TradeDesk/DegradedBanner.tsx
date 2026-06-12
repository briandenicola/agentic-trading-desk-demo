import { Alert, AlertTitle, Typography } from '@mui/material';
import type { TdBriefing } from '../../api/client';

/**
 * Honest "degraded" banner: the LIVE Foundry agent was unavailable (or returned no usable call
 * list), so this briefing came from the deterministic safety net instead. We surface that plainly
 * rather than passing the fallback off as a normal LIVE result. Renders nothing when the briefing
 * is a genuine LIVE/DEMO response.
 */
export default function DegradedBanner({ brief }: { brief: TdBriefing | null | undefined }) {
  if (!brief?.degraded) {
    return null;
  }

  return (
    <Alert severity="error" variant="filled" sx={{ mb: 2.5 }} data-testid="td-degraded-banner">
      <AlertTitle sx={{ fontWeight: 700 }}>LIVE agent unavailable — showing deterministic fallback</AlertTitle>
      <Typography variant="body2">
        {brief.degradedReason ??
          'The Foundry agent could not produce this briefing, so it was reconstructed deterministically from the systems-of-record. Results are correct but not agent-generated.'}
      </Typography>
    </Alert>
  );
}
