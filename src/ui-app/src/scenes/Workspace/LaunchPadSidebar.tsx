import { Box, Button, Stack, Typography } from '@mui/material';
import HomeOutlinedIcon from '@mui/icons-material/HomeOutlined';
import GridViewOutlinedIcon from '@mui/icons-material/GridViewOutlined';
import SmartToyOutlinedIcon from '@mui/icons-material/SmartToyOutlined';
import TextSnippetOutlinedIcon from '@mui/icons-material/TextSnippetOutlined';
import ScheduleOutlinedIcon from '@mui/icons-material/ScheduleOutlined';
import StorageOutlinedIcon from '@mui/icons-material/StorageOutlined';
import VisibilityOutlinedIcon from '@mui/icons-material/VisibilityOutlined';
import SettingsOutlinedIcon from '@mui/icons-material/SettingsOutlined';
import AddCommentOutlinedIcon from '@mui/icons-material/AddCommentOutlined';
import AddBoxOutlinedIcon from '@mui/icons-material/AddBoxOutlined';
import AutoStoriesOutlinedIcon from '@mui/icons-material/AutoStoriesOutlined';
import GroupsOutlinedIcon from '@mui/icons-material/GroupsOutlined';
import { Link } from 'react-router-dom';
import type { ReactNode } from 'react';
import { mint } from '../../theme/theme';
import { workspace, type NavIcon, type QuickActionIcon } from './workspaceData';

const NAV_ICON: Record<NavIcon, ReactNode> = {
  home: <HomeOutlinedIcon fontSize="small" />,
  scenes: <GridViewOutlinedIcon fontSize="small" />,
  agents: <SmartToyOutlinedIcon fontSize="small" />,
  prompts: <TextSnippetOutlinedIcon fontSize="small" />,
  scheduled: <ScheduleOutlinedIcon fontSize="small" />,
  data: <StorageOutlinedIcon fontSize="small" />,
  watchlists: <VisibilityOutlinedIcon fontSize="small" />,
  settings: <SettingsOutlinedIcon fontSize="small" />,
};

const QUICK_ICON: Record<QuickActionIcon, ReactNode> = {
  prompt: <AddCommentOutlinedIcon sx={{ fontSize: 16 }} />,
  panel: <AddBoxOutlinedIcon sx={{ fontSize: 16 }} />,
  playbook: <AutoStoriesOutlinedIcon sx={{ fontSize: 16 }} />,
  agents: <GroupsOutlinedIcon sx={{ fontSize: 16 }} />,
};

function GroupLabel({ children }: { children: ReactNode }) {
  return (
    <Typography
      sx={{ fontSize: 10, fontWeight: 700, letterSpacing: '1.4px', color: mint.violetBright, mb: 1 }}
    >
      {children}
    </Typography>
  );
}

/**
 * Left "Launch Pad" rail: primary navigation, the active-scene card and quick
 * actions. Home is active; Scenes / Playbooks links to the secondary cockpit.
 */
export default function LaunchPadSidebar({ activeNav = 'Home' }: { activeNav?: string }) {
  return (
    <Stack spacing={3}>
      <Box>
        <GroupLabel>LAUNCH PAD</GroupLabel>
        <Stack spacing={0.5}>
          {workspace.nav.map((item) => {
            const active = item.label === activeNav;
            const row = (
              <Box
                sx={{
                  display: 'flex',
                  alignItems: 'center',
                  gap: 1.25,
                  px: 1.25,
                  py: 1,
                  borderRadius: 2,
                  cursor: item.to ? 'pointer' : 'default',
                  color: active ? mint.text : mint.textDim,
                  background: active ? `${mint.violet}22` : 'transparent',
                  border: `1px solid ${active ? mint.border : 'transparent'}`,
                  '&:hover': { background: `${mint.violet}14`, color: mint.text },
                }}
              >
                {NAV_ICON[item.icon]}
                <Typography sx={{ fontSize: 13, fontWeight: active ? 700 : 500 }}>
                  {item.label}
                </Typography>
              </Box>
            );
            return item.to ? (
              <Box key={item.label} component={Link} to={item.to} sx={{ textDecoration: 'none' }}>
                {row}
              </Box>
            ) : (
              <Box key={item.label}>{row}</Box>
            );
          })}
        </Stack>
      </Box>

      <Box
        sx={{
          p: 1.75,
          borderRadius: 2,
          border: `1px solid ${mint.border}`,
          background: `linear-gradient(135deg, ${mint.violet}1f, transparent 70%)`,
        }}
      >
        <GroupLabel>ACTIVE SCENE</GroupLabel>
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
          <Box sx={{ width: 8, height: 8, borderRadius: '50%', bgcolor: mint.green, boxShadow: `0 0 8px ${mint.green}` }} />
          <Typography sx={{ fontSize: 14, fontWeight: 700, color: mint.text }}>
            {workspace.activeScene.name}
          </Typography>
        </Box>
        <Typography sx={{ fontSize: 11, color: mint.textDim, mt: 0.5 }}>
          Started {workspace.activeScene.startedAt}
        </Typography>
        <Button
          fullWidth
          size="small"
          variant="outlined"
          sx={{ mt: 1.25, textTransform: 'none', fontWeight: 600, borderColor: mint.border, color: mint.text }}
        >
          Change Scene
        </Button>
      </Box>

      <Box>
        <GroupLabel>QUICK ACTIONS</GroupLabel>
        <Stack spacing={0.5}>
          {workspace.quickActions.map((qa) => (
            <Box
              key={qa.label}
              sx={{
                display: 'flex',
                alignItems: 'center',
                gap: 1.25,
                px: 1.25,
                py: 0.85,
                borderRadius: 2,
                color: mint.textDim,
                cursor: 'pointer',
                '&:hover': { background: `${mint.violet}14`, color: mint.text },
              }}
            >
              {QUICK_ICON[qa.icon]}
              <Typography sx={{ fontSize: 13 }}>{qa.label}</Typography>
            </Box>
          ))}
        </Stack>
      </Box>
    </Stack>
  );
}
