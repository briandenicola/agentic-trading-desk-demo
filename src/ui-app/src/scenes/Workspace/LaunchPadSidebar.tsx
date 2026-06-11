import { Box, Stack, Typography } from '@mui/material';
import HomeOutlinedIcon from '@mui/icons-material/HomeOutlined';
import DashboardOutlinedIcon from '@mui/icons-material/DashboardOutlined';
import NewspaperOutlinedIcon from '@mui/icons-material/NewspaperOutlined';
import WbSunnyOutlinedIcon from '@mui/icons-material/WbSunnyOutlined';
import { Link, useLocation } from 'react-router-dom';
import type { ReactNode } from 'react';
import { mint } from '../../theme/theme';

interface NavLink {
  label: string;
  to: string;
  icon: ReactNode;
}

const LINKS: NavLink[] = [
  { label: 'Home', to: '/cb', icon: <HomeOutlinedIcon fontSize="small" /> },
  { label: 'Cockpit', to: '/cockpit', icon: <DashboardOutlinedIcon fontSize="small" /> },
  { label: 'News Desk', to: '/admin', icon: <NewspaperOutlinedIcon fontSize="small" /> },
  { label: 'Morning Brief', to: '/morning-brief', icon: <WbSunnyOutlinedIcon fontSize="small" /> },
];

/** Left rail: primary navigation to the real scenes. */
export default function LaunchPadSidebar() {
  const { pathname } = useLocation();
  return (
    <Stack spacing={0.5}>
      <Typography sx={{ fontSize: 10, fontWeight: 700, letterSpacing: '1.4px', color: mint.violetBright, mb: 1 }}>
        WORKSPACE
      </Typography>
      {LINKS.map((item) => {
        const active = item.to === '/cb' ? pathname === '/cb' : pathname.startsWith(item.to);
        return (
          <Box key={item.label} component={Link} to={item.to} sx={{ textDecoration: 'none' }}>
            <Box
              sx={{
                display: 'flex',
                alignItems: 'center',
                gap: 1.25,
                px: 1.25,
                py: 1,
                borderRadius: 2,
                cursor: 'pointer',
                color: active ? mint.text : mint.textDim,
                background: active ? `${mint.violet}22` : 'transparent',
                border: `1px solid ${active ? mint.border : 'transparent'}`,
                '&:hover': { background: `${mint.violet}14`, color: mint.text },
              }}
            >
              {item.icon}
              <Typography sx={{ fontSize: 13, fontWeight: active ? 700 : 500 }}>{item.label}</Typography>
            </Box>
          </Box>
        );
      })}
    </Stack>
  );
}
