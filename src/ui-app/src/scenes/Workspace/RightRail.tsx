import { useState } from 'react';
import type { ReactNode } from 'react';
import { Box, Chip, Stack, Typography } from '@mui/material';
import ChatBubbleOutlineRoundedIcon from '@mui/icons-material/ChatBubbleOutlineRounded';
import VisibilityOutlinedIcon from '@mui/icons-material/VisibilityOutlined';
import DownloadRoundedIcon from '@mui/icons-material/DownloadRounded';
import { mint } from '../../theme/theme';
import { workspace, type ActivityItem, type MatchedNews } from './workspaceData';

const SOURCE_COLOR: Record<MatchedNews['source'], string> = {
  Bloomberg: mint.amber,
  WSJ: mint.cyan,
  Reuters: mint.violetBright,
  CNBC: mint.green,
};

const TAG_COLOR: Record<MatchedNews['tagKind'], string> = {
  impacts: mint.red,
  holds: mint.green,
  macro: mint.cyan,
};

const ACTIVITY_ICON: Record<ActivityItem['icon'], ReactNode> = {
  chat: <ChatBubbleOutlineRoundedIcon sx={{ fontSize: 16 }} />,
  view: <VisibilityOutlinedIcon sx={{ fontSize: 16 }} />,
  download: <DownloadRoundedIcon sx={{ fontSize: 16 }} />,
};

function NewsRow({ item }: { item: MatchedNews }) {
  return (
    <Box sx={{ py: 1.25, borderBottom: `1px solid ${mint.borderSoft}` }}>
      <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', mb: 0.5 }}>
        <Box
          sx={{
            px: 0.75,
            py: 0.1,
            borderRadius: 1,
            fontSize: 9,
            fontWeight: 800,
            letterSpacing: '0.5px',
            color: '#04101c',
            bgcolor: SOURCE_COLOR[item.source],
          }}
        >
          {item.source}
        </Box>
        <Typography sx={{ fontSize: 10, color: mint.textDim }}>{item.time}</Typography>
      </Box>
      <Typography sx={{ fontSize: 12.5, fontWeight: 600, color: mint.text, lineHeight: 1.4 }}>
        {item.headline}
      </Typography>
      <Chip
        label={item.tagLabel}
        size="small"
        sx={{
          mt: 0.75,
          height: 18,
          fontSize: 9,
          fontWeight: 700,
          color: TAG_COLOR[item.tagKind],
          bgcolor: `${TAG_COLOR[item.tagKind]}1f`,
          border: `1px solid ${TAG_COLOR[item.tagKind]}55`,
        }}
      />
    </Box>
  );
}

/** Right rail: tabbed "News Matched to {client}" + a client activity log. */
export default function RightRail() {
  const [tab, setTab] = useState<(typeof workspace.newsTabs)[number]>('All');
  const news = workspace.matchedNews.filter((n) => tab === 'All' || n.category === tab);

  return (
    <Stack spacing={2}>
      <Box sx={{ p: 1.75, borderRadius: 3, border: `1px solid ${mint.border}`, background: mint.paper }}>
        <Typography sx={{ fontSize: 12, fontWeight: 700, letterSpacing: '0.6px', color: mint.text, mb: 1 }}>
          NEWS MATCHED TO {workspace.client.name.toUpperCase()}
        </Typography>
        <Box sx={{ display: 'flex', flexWrap: 'wrap', gap: 0.5, mb: 0.5 }}>
          {workspace.newsTabs.map((t) => {
            const active = t === tab;
            return (
              <Box
                key={t}
                onClick={() => setTab(t)}
                sx={{
                  px: 1,
                  py: 0.35,
                  borderRadius: 1.5,
                  fontSize: 11,
                  fontWeight: 600,
                  cursor: 'pointer',
                  color: active ? mint.text : mint.textDim,
                  background: active ? `${mint.violet}29` : 'transparent',
                  border: `1px solid ${active ? mint.border : 'transparent'}`,
                }}
              >
                {t}
              </Box>
            );
          })}
        </Box>
        {news.length > 0 ? (
          news.map((n) => <NewsRow key={n.id} item={n} />)
        ) : (
          <Typography sx={{ fontSize: 12, color: mint.textDim, py: 2 }}>No matched news in this category.</Typography>
        )}
        <Typography sx={{ fontSize: 12, fontWeight: 600, color: mint.cyan, textAlign: 'center', cursor: 'pointer', mt: 1 }}>
          View All Matched News
        </Typography>
      </Box>

      <Box sx={{ p: 1.75, borderRadius: 3, border: `1px solid ${mint.border}`, background: mint.paper }}>
        <Typography sx={{ fontSize: 12, fontWeight: 700, letterSpacing: '0.6px', color: mint.text, mb: 1 }}>
          CLIENT ACTIVITY
        </Typography>
        <Stack spacing={1.25}>
          {workspace.activity.map((a) => (
            <Box key={a.id} sx={{ display: 'flex', alignItems: 'center', gap: 1.25 }}>
              <Box
                sx={{
                  width: 30,
                  height: 30,
                  borderRadius: 1.5,
                  flexShrink: 0,
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'center',
                  color: mint.violetBright,
                  border: `1px solid ${mint.border}`,
                  background: mint.paperHi,
                }}
              >
                {ACTIVITY_ICON[a.icon]}
              </Box>
              <Box sx={{ flex: 1, minWidth: 0 }}>
                <Typography sx={{ fontSize: 12.5, fontWeight: 600, color: mint.text }} noWrap>
                  {a.title}
                </Typography>
                <Typography sx={{ fontSize: 11, color: mint.textDim }} noWrap>
                  {a.subtitle}
                </Typography>
              </Box>
              <Typography sx={{ fontSize: 10, color: mint.textDim, whiteSpace: 'nowrap' }}>{a.time}</Typography>
            </Box>
          ))}
        </Stack>
        <Typography sx={{ fontSize: 12, fontWeight: 600, color: mint.cyan, textAlign: 'center', cursor: 'pointer', mt: 1.25 }}>
          View All Activity
        </Typography>
      </Box>
    </Stack>
  );
}
