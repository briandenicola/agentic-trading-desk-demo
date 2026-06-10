import type { ReactNode } from 'react';
import { useNavigate } from 'react-router-dom';
import { Box, Stack, Typography } from '@mui/material';
import StorefrontRoundedIcon from '@mui/icons-material/StorefrontRounded';
import GroupsRoundedIcon from '@mui/icons-material/GroupsRounded';
import ShieldRoundedIcon from '@mui/icons-material/ShieldRounded';
import SwapHorizRoundedIcon from '@mui/icons-material/SwapHorizRounded';
import InsightsRoundedIcon from '@mui/icons-material/InsightsRounded';
import AddRoundedIcon from '@mui/icons-material/AddRounded';
import EventRoundedIcon from '@mui/icons-material/EventRounded';
import ChatBubbleRoundedIcon from '@mui/icons-material/ChatBubbleRounded';
import { mint } from '../../theme/theme';
import { workspace, type Playbook } from './workspaceData';

const PLAYBOOK_ICON: Record<Playbook['icon'], ReactNode> = {
  open: <StorefrontRoundedIcon sx={{ fontSize: 18 }} />,
  client: <GroupsRoundedIcon sx={{ fontSize: 18 }} />,
  risk: <ShieldRoundedIcon sx={{ fontSize: 18 }} />,
  trade: <SwapHorizRoundedIcon sx={{ fontSize: 18 }} />,
  earnings: <InsightsRoundedIcon sx={{ fontSize: 18 }} />,
};

function PlaybookCard({ pb }: { pb: Playbook }) {
  const active = !!pb.active;
  return (
    <Box
      sx={{
        minWidth: 168,
        p: 1.5,
        borderRadius: 2.5,
        cursor: 'pointer',
        border: `1px solid ${active ? mint.violet : mint.border}`,
        background: active ? `${mint.violet}1f` : mint.paper,
        boxShadow: active ? `0 0 0 1px ${mint.violet}55` : 'none',
        transition: 'border-color 120ms ease',
        '&:hover': { borderColor: mint.violetBright },
      }}
    >
      <Box
        sx={{
          width: 32,
          height: 32,
          mb: 1,
          borderRadius: 1.5,
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
          color: active ? '#fff' : mint.violetBright,
          background: active ? `linear-gradient(135deg, ${mint.violet}, ${mint.violetBright})` : mint.paperHi,
          border: `1px solid ${mint.border}`,
        }}
      >
        {PLAYBOOK_ICON[pb.icon]}
      </Box>
      <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.75 }}>
        <Typography sx={{ fontSize: 13, fontWeight: 700, color: mint.text }}>{pb.name}</Typography>
        {active && (
          <Box sx={{ px: 0.6, py: 0.05, borderRadius: 1, fontSize: 8, fontWeight: 800, color: '#04101c', bgcolor: mint.green }}>
            ACTIVE
          </Box>
        )}
      </Box>
      <Typography sx={{ fontSize: 11, color: mint.textDim, mt: 0.5, lineHeight: 1.4 }}>{pb.description}</Typography>
    </Box>
  );
}

/** Bottom bar: Scenes/Playbooks carousel + Upcoming Events + High Priority Alert. */
export default function PlaybooksBar() {
  const navigate = useNavigate();
  return (
    <Box
      sx={{
        display: 'grid',
        gap: 1.75,
        gridTemplateColumns: { xs: '1fr', lg: '1.6fr 1fr 1fr' },
        alignItems: 'stretch',
      }}
    >
      <Box sx={{ p: 1.75, borderRadius: 3, border: `1px solid ${mint.border}`, background: mint.paper }}>
        <Typography sx={{ fontSize: 12, fontWeight: 700, letterSpacing: '0.6px', color: mint.text, mb: 1.25 }}>
          SCENES / PLAYBOOKS
        </Typography>
        <Box sx={{ display: 'flex', gap: 1.25, overflowX: 'auto', pb: 0.5 }}>
          {workspace.playbooks.map((pb) => (
            <PlaybookCard key={pb.id} pb={pb} />
          ))}
          <Box
            sx={{
              minWidth: 150,
              borderRadius: 2.5,
              display: 'flex',
              flexDirection: 'column',
              alignItems: 'center',
              justifyContent: 'center',
              gap: 0.75,
              cursor: 'pointer',
              color: mint.textDim,
              border: `1px dashed ${mint.border}`,
              '&:hover': { color: mint.text, borderColor: mint.violetBright },
            }}
          >
            <AddRoundedIcon />
            <Typography sx={{ fontSize: 12, fontWeight: 600 }}>Create New Playbook</Typography>
          </Box>
        </Box>
      </Box>

      <Box sx={{ p: 1.75, borderRadius: 3, border: `1px solid ${mint.border}`, background: mint.paper }}>
        <Typography sx={{ fontSize: 12, fontWeight: 700, letterSpacing: '0.6px', color: mint.text, mb: 1.25 }}>
          UPCOMING EVENTS
        </Typography>
        <Stack spacing={1.25}>
          {workspace.events.map((ev) => (
            <Box key={ev.id} sx={{ display: 'flex', alignItems: 'center', gap: 1.25 }}>
              <Box
                sx={{
                  width: 30,
                  height: 30,
                  borderRadius: 1.5,
                  flexShrink: 0,
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'center',
                  color: mint.cyan,
                  border: `1px solid ${mint.border}`,
                  background: mint.paperHi,
                }}
              >
                <EventRoundedIcon sx={{ fontSize: 16 }} />
              </Box>
              <Box sx={{ flex: 1, minWidth: 0 }}>
                <Typography sx={{ fontSize: 12.5, fontWeight: 600, color: mint.text }} noWrap>
                  {ev.label}
                </Typography>
                <Typography sx={{ fontSize: 11, color: mint.textDim }}>{ev.time}</Typography>
              </Box>
            </Box>
          ))}
        </Stack>
      </Box>

      <Box
        sx={{
          p: 1.75,
          borderRadius: 3,
          border: `1px solid ${mint.red}66`,
          background: `linear-gradient(160deg, ${mint.red}1f, ${mint.paper})`,
        }}
      >
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, mb: 1 }}>
          <Box sx={{ px: 0.75, py: 0.15, borderRadius: 1, fontSize: 9, fontWeight: 800, color: '#fff', bgcolor: mint.red }}>
            HIGH PRIORITY
          </Box>
          <Typography sx={{ fontSize: 10, color: mint.textDim, ml: 'auto' }}>{workspace.priorityAlert.time}</Typography>
        </Box>
        <Stack direction="row" spacing={1.25} sx={{ alignItems: 'flex-start', mb: 1.25 }}>
          <Box
            sx={{
              width: 34,
              height: 34,
              borderRadius: 1.5,
              flexShrink: 0,
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              color: '#fff',
              background: mint.red,
            }}
          >
            <ChatBubbleRoundedIcon sx={{ fontSize: 18 }} />
          </Box>
          <Box>
            <Typography sx={{ fontSize: 13, fontWeight: 700, color: mint.text }}>{workspace.priorityAlert.title}</Typography>
            <Typography sx={{ fontSize: 11.5, color: mint.textDim }}>{workspace.priorityAlert.subtitle}</Typography>
            <Typography sx={{ fontSize: 11.5, color: mint.textDim, mt: 0.5, lineHeight: 1.4 }}>
              {workspace.priorityAlert.body}
            </Typography>
          </Box>
        </Stack>
        <Stack direction="row" spacing={1}>
          <Box
            onClick={() => navigate('/chat')}
            sx={{
              flex: 1,
              textAlign: 'center',
              py: 0.75,
              borderRadius: 2,
              fontSize: 12,
              fontWeight: 700,
              color: '#fff',
              cursor: 'pointer',
              background: `linear-gradient(135deg, ${mint.violet}, ${mint.violetBright})`,
            }}
          >
            Open Chat
          </Box>
          <Box
            sx={{
              flex: 1,
              textAlign: 'center',
              py: 0.75,
              borderRadius: 2,
              fontSize: 12,
              fontWeight: 600,
              color: mint.text,
              cursor: 'pointer',
              border: `1px solid ${mint.border}`,
            }}
          >
            Configure Workspace
          </Box>
        </Stack>
      </Box>
    </Box>
  );
}
