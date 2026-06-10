import type { ReactNode } from 'react';
import { Box, Chip, Stack, Typography } from '@mui/material';
import FilterListRoundedIcon from '@mui/icons-material/FilterListRounded';
import BoltRoundedIcon from '@mui/icons-material/BoltRounded';
import ChatBubbleOutlineRoundedIcon from '@mui/icons-material/ChatBubbleOutlineRounded';
import ShowChartRoundedIcon from '@mui/icons-material/ShowChartRounded';
import CalendarMonthOutlinedIcon from '@mui/icons-material/CalendarMonthOutlined';
import CheckCircleOutlineRoundedIcon from '@mui/icons-material/CheckCircleOutlineRounded';
import ReceiptLongOutlinedIcon from '@mui/icons-material/ReceiptLongOutlined';
import { mint } from '../../theme/theme';
import { workspace, type AlertPriority, type FeedItem } from './workspaceData';
import { useWorkspaceLive, type LiveFeedItem } from './useWorkspaceLive';

const FEED_ICON: Record<FeedItem['icon'], ReactNode> = {
  chat: <ChatBubbleOutlineRoundedIcon sx={{ fontSize: 18 }} />,
  market: <ShowChartRoundedIcon sx={{ fontSize: 18 }} />,
  fed: <CalendarMonthOutlinedIcon sx={{ fontSize: 18 }} />,
  task: <CheckCircleOutlineRoundedIcon sx={{ fontSize: 18 }} />,
  trade: <ReceiptLongOutlinedIcon sx={{ fontSize: 18 }} />,
};

const PRIORITY: Record<AlertPriority, { color: string; show: boolean }> = {
  HIGH: { color: mint.red, show: true },
  MEDIUM: { color: mint.amber, show: true },
  LOW: { color: mint.cyan, show: true },
  DONE: { color: mint.green, show: false },
};

function FeedCard({ item }: { item: FeedItem }) {
  const p = PRIORITY[item.priority];
  return (
    <Box
      sx={{
        p: 1.5,
        borderRadius: 2,
        border: `1px solid ${mint.borderSoft}`,
        background: mint.paperHi,
        borderLeft: `3px solid ${p.color}`,
        cursor: 'pointer',
        '&:hover': { borderColor: mint.border },
      }}
    >
      <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', mb: 0.5 }}>
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, color: p.color }}>
          {p.show && (
            <Chip
              label={item.priority}
              size="small"
              sx={{ height: 17, fontSize: 9, fontWeight: 800, color: '#04101c', bgcolor: p.color }}
            />
          )}
          {FEED_ICON[item.icon]}
        </Box>
        <Typography sx={{ fontSize: 10, color: mint.textDim }}>{item.time}</Typography>
      </Box>
      <Typography sx={{ fontSize: 13, fontWeight: 700, color: mint.text }}>{item.title}</Typography>
      <Typography sx={{ fontSize: 12, fontWeight: 600, color: mint.textDim }}>{item.subtitle}</Typography>
      <Typography sx={{ fontSize: 12, color: mint.textDim, mt: 0.25 }}>{item.body}</Typography>
    </Box>
  );
}

function LiveFeedCard({ item }: { item: LiveFeedItem }) {
  const color = PRIORITY[item.priority].color;
  return (
    <Box
      sx={{
        p: 1.5,
        borderRadius: 2,
        border: `1px solid ${color}66`,
        background: `linear-gradient(160deg, ${color}1f, ${mint.paperHi})`,
        borderLeft: `3px solid ${color}`,
        cursor: 'pointer',
        '@keyframes mintFeedFlash': {
          '0%': { boxShadow: `0 0 0 0 ${color}00` },
          '25%': { boxShadow: `0 0 0 3px ${color}55` },
          '100%': { boxShadow: `0 0 0 0 ${color}00` },
        },
        animation: item.isNew ? 'mintFeedFlash 1.5s ease-out 2' : 'none',
      }}
    >
      <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', mb: 0.5 }}>
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.75, color }}>
          <Chip
            icon={<BoltRoundedIcon sx={{ fontSize: 12, color: '#04101c !important' }} />}
            label={`LIVE · ${item.priority}`}
            size="small"
            sx={{ height: 17, fontSize: 9, fontWeight: 800, color: '#04101c', bgcolor: color, '& .MuiChip-label': { px: 0.5 } }}
          />
        </Box>
        <Typography sx={{ fontSize: 10, color: mint.textDim }}>{item.time}</Typography>
      </Box>
      <Typography sx={{ fontSize: 13, fontWeight: 700, color: mint.text, lineHeight: 1.4 }}>{item.headline}</Typography>
      <Typography sx={{ fontSize: 11.5, color: mint.textDim, mt: 0.25 }}>
        M.INT matched {item.eventCount} new event{item.eventCount === 1 ? '' : 's'} to your book
      </Typography>
    </Box>
  );
}

/** Center-left "M.INT Newsfeed" alert column. */
export default function NewsfeedColumn() {
  const { liveItems } = useWorkspaceLive();
  return (
    <Stack
      spacing={1.5}
      sx={{ p: 1.75, borderRadius: 3, border: `1px solid ${mint.border}`, background: mint.paper, height: '100%' }}
    >
      <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
        <Typography sx={{ fontSize: 12, fontWeight: 700, letterSpacing: '1px', color: mint.text }}>
          M.INT NEWSFEED
        </Typography>
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.5, color: mint.textDim }}>
          <FilterListRoundedIcon sx={{ fontSize: 15 }} />
          <Typography sx={{ fontSize: 11 }}>Filter</Typography>
        </Box>
      </Box>
      <Stack spacing={1.25}>
        {liveItems.map((item) => (
          <LiveFeedCard key={item.id} item={item} />
        ))}
        {workspace.feed.map((item) => (
          <FeedCard key={item.id} item={item} />
        ))}
      </Stack>
      <Typography
        sx={{ fontSize: 12, fontWeight: 600, color: mint.cyan, textAlign: 'center', cursor: 'pointer', mt: 0.5 }}
      >
        View All Alerts
      </Typography>
    </Stack>
  );
}
