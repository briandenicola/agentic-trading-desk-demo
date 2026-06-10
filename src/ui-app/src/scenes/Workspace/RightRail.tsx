import { Box, Chip, Stack, Typography } from '@mui/material';
import PhoneInTalkRoundedIcon from '@mui/icons-material/PhoneInTalkRounded';
import FlagRoundedIcon from '@mui/icons-material/FlagRounded';
import { mint } from '../../theme/theme';
import type { PriorityCall } from '../../api/client';
import { useWorkspaceLive } from './useWorkspaceLive';
import { useChatDock } from './ChatOverlay';

const BAND_COLOR: Record<number, string> = {
  1: mint.red,
  2: '#ffb547',
  3: mint.amber,
  4: mint.green,
};

function OutreachRow({ call }: { call: PriorityCall }) {
  const { openChat } = useChatDock();
  const accent = BAND_COLOR[call.priority] ?? mint.green;
  return (
    <Box
      onClick={() => openChat(`Brief me on ${call.customerName} (${call.customerId}) for my outreach call.`)}
      role="button"
      aria-label={`Open chat about ${call.customerName}`}
      sx={{
        p: 1.25,
        borderRadius: 2,
        border: `1px solid ${mint.borderSoft}`,
        background: mint.paperHi,
        borderLeft: `3px solid ${accent}`,
        cursor: 'pointer',
        '&:hover': { borderColor: mint.border },
      }}
    >
      <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 1 }}>
        <Typography sx={{ fontSize: 12.5, fontWeight: 700, color: mint.text }} noWrap>
          {call.rank}. {call.customerName}
        </Typography>
        <Chip
          label={call.score}
          size="small"
          sx={{ height: 18, fontSize: 9.5, fontWeight: 800, color: mint.text, bgcolor: mint.borderSoft }}
        />
      </Box>
      {call.tags.length > 0 && (
        <Typography sx={{ fontSize: 10.5, color: mint.textDim, mt: 0.25 }} noWrap>
          {call.tags.map((t) => t.label).join(' · ')}
        </Typography>
      )}
      <Typography sx={{ fontSize: 11.5, color: mint.textDim, mt: 0.5, lineHeight: 1.4 }}>
        {call.suggestedAction}
      </Typography>
    </Box>
  );
}

/** Right rail: the ranked outreach list (ranks 2+) plus the suggested first action. */
export default function RightRail() {
  const { brief } = useWorkspaceLive();
  if (!brief) return null;
  const rest = brief.priorityCallList.slice(1);

  return (
    <Stack spacing={2}>
      <Box sx={{ p: 1.75, borderRadius: 3, border: `1px solid ${mint.border}`, background: mint.paper }}>
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.75, mb: 1.25 }}>
          <PhoneInTalkRoundedIcon sx={{ fontSize: 16, color: mint.violetBright }} />
          <Typography sx={{ fontSize: 12, fontWeight: 800, letterSpacing: '0.6px', color: mint.text }}>
            PRIORITIZED OUTREACH
          </Typography>
        </Box>
        <Stack spacing={1}>
          {rest.length === 0 ? (
            <Typography sx={{ fontSize: 12, color: mint.textDim }}>No further calls ranked.</Typography>
          ) : (
            rest.map((c) => <OutreachRow key={c.customerId} call={c} />)
          )}
        </Stack>
      </Box>

      <Box
        sx={{
          p: 1.75,
          borderRadius: 3,
          border: `1px solid ${mint.violet}59`,
          background: `linear-gradient(135deg, ${mint.violet}24, ${mint.cyan}12)`,
        }}
      >
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.75, mb: 0.75 }}>
          <FlagRoundedIcon sx={{ fontSize: 16, color: mint.violetBright }} />
          <Typography sx={{ fontSize: 11, fontWeight: 800, letterSpacing: '0.6px', color: mint.violetBright }}>
            SUGGESTED FIRST ACTION
          </Typography>
        </Box>
        <Typography sx={{ fontSize: 12.5, color: mint.text, lineHeight: 1.5 }}>{brief.suggestedFirstAction}</Typography>
      </Box>
    </Stack>
  );
}
