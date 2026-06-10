import { Box, Chip, Stack, Typography } from '@mui/material';
import RssFeedRoundedIcon from '@mui/icons-material/RssFeedRounded';
import { mint } from '../../theme/theme';
import type { AlertPriority } from './workspaceData';
import { useWorkspaceLive } from './useWorkspaceLive';

const PRIORITY_COLOR: Record<AlertPriority, string> = {
  HIGH: mint.red,
  MEDIUM: mint.amber,
  LOW: mint.cyan,
  DONE: mint.green,
};

const SEVERITY_COLOR: Record<string, string> = {
  high: mint.red,
  medium: mint.amber,
  low: mint.cyan,
};

/** Newsfeed: live incoming events (flashing on arrival) over the briefing's baseline signals. */
export default function NewsfeedColumn() {
  const { liveItems, brief } = useWorkspaceLive();
  const baseline = brief?.eventsConsidered ?? [];

  return (
    <Box
      sx={{
        p: 1.75,
        borderRadius: 3,
        border: `1px solid ${mint.border}`,
        background: mint.paper,
        '@keyframes feedFlash': {
          '0%': { borderColor: mint.violetBright, background: `${mint.violet}26` },
          '100%': { borderColor: mint.borderSoft, background: mint.paperHi },
        },
      }}
    >
      <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.75, mb: 1.25 }}>
        <RssFeedRoundedIcon sx={{ fontSize: 16, color: mint.violetBright }} />
        <Typography sx={{ fontSize: 12, fontWeight: 800, letterSpacing: '0.6px', color: mint.text }}>
          LIVE NEWSFEED
        </Typography>
      </Box>

      <Stack spacing={1}>
        {liveItems.map((item) => {
          const c = PRIORITY_COLOR[item.priority];
          return (
            <Box
              key={item.id}
              sx={{
                p: 1.25,
                borderRadius: 2,
                border: `1px solid ${mint.borderSoft}`,
                background: mint.paperHi,
                borderLeft: `3px solid ${c}`,
                animation: item.isNew ? 'feedFlash 4s ease-out' : 'none',
              }}
            >
              <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 1, mb: 0.5 }}>
                <Chip
                  label={item.priority}
                  size="small"
                  sx={{ height: 17, fontSize: 9, fontWeight: 800, color: '#04101c', bgcolor: c }}
                />
                <Typography sx={{ fontSize: 10, color: mint.textDim }}>{item.time}</Typography>
              </Box>
              <Typography sx={{ fontSize: 12.5, fontWeight: 700, color: mint.text, lineHeight: 1.4 }}>
                {item.headline}
              </Typography>
              {item.eventCount > 0 && (
                <Typography sx={{ fontSize: 10.5, color: mint.textDim, mt: 0.25 }}>
                  {item.eventCount} event{item.eventCount === 1 ? '' : 's'} re-ranked your book
                </Typography>
              )}
            </Box>
          );
        })}

        {liveItems.length > 0 && baseline.length > 0 && (
          <Typography sx={{ fontSize: 10, fontWeight: 700, letterSpacing: '0.6px', color: mint.textDim, mt: 0.5 }}>
            OVERNIGHT SIGNALS
          </Typography>
        )}

        {baseline.map((ev) => {
          const c = SEVERITY_COLOR[ev.severity] ?? mint.cyan;
          return (
            <Box
              key={ev.id}
              sx={{
                p: 1.25,
                borderRadius: 2,
                border: `1px solid ${mint.borderSoft}`,
                background: mint.bgAlt,
                borderLeft: `3px solid ${c}`,
              }}
            >
              <Typography sx={{ fontSize: 12, fontWeight: 700, color: mint.text, lineHeight: 1.4 }}>
                {ev.headline}
              </Typography>
              <Typography sx={{ fontSize: 11, color: mint.textDim, mt: 0.25, lineHeight: 1.45 }}>
                {ev.summary}
              </Typography>
            </Box>
          );
        })}

        {liveItems.length === 0 && baseline.length === 0 && (
          <Typography sx={{ fontSize: 12, color: mint.textDim }}>
            Watching the wire. New market events will appear here live.
          </Typography>
        )}
      </Stack>
    </Box>
  );
}
