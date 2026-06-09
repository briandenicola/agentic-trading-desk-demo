import { AppBar, Box, Stack, Tab, Tabs, Toolbar, Typography } from '@mui/material';
import RocketLaunchOutlinedIcon from '@mui/icons-material/RocketLaunchOutlined';
import NotificationsActiveOutlinedIcon from '@mui/icons-material/NotificationsActiveOutlined';
import ChatBubbleOutlineOutlinedIcon from '@mui/icons-material/ChatBubbleOutlineOutlined';
import TuneOutlinedIcon from '@mui/icons-material/TuneOutlined';
import { Link, useLocation } from 'react-router-dom';
import MintBrand from './MintBrand';
import { mint } from '../theme/theme';

const TABS = [
  { label: 'RM Daily Briefing', to: '/' },
  { label: 'Trading Morning Brief', to: '/morning-brief' },
];

const WORKSPACE = [
  { label: 'Launch Pad', icon: <RocketLaunchOutlinedIcon fontSize="small" />, badge: false },
  { label: 'Newsfeed / Alerts', icon: <NotificationsActiveOutlinedIcon fontSize="small" />, badge: true },
  { label: 'AI Chat', icon: <ChatBubbleOutlineOutlinedIcon fontSize="small" />, badge: false },
  { label: 'Control Panel', icon: <TuneOutlinedIcon fontSize="small" />, badge: false },
];

/**
 * M.INT cockpit header. Brand lockup + intelligence tagline + persistent workspace
 * elements, over the scene tabs. The RM Daily Briefing is PRIMARY (route `/`); the
 * trading morning brief is secondary. Mode-blind: both scenes call the orchestration
 * API which returns DEMO or LIVE transparently.
 */
export default function CockpitNav() {
  const { pathname } = useLocation();
  const active = pathname.startsWith('/morning-brief') ? '/morning-brief' : '/';

  return (
    <AppBar
      position="sticky"
      sx={{
        backdropFilter: 'blur(8px)',
        backgroundColor: 'rgba(7,11,22,0.72)',
        borderBottom: `1px solid ${mint.border}`,
      }}
    >
      <Box sx={{ px: { xs: 2, md: 4 }, pt: 1.5 }}>
        <Box
          sx={{
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'space-between',
            gap: 2,
            flexWrap: 'wrap',
          }}
        >
          <MintBrand />

          <Typography
            sx={{
              display: { xs: 'none', lg: 'block' },
              flex: 1,
              textAlign: 'center',
              fontWeight: 700,
              fontSize: '15px',
              letterSpacing: '0.3px',
              color: mint.text,
            }}
          >
            AI-Driven Markets Intelligence.{' '}
            <Box component="span" sx={{ color: mint.violetBright }}>
              The Right Context.
            </Box>{' '}
            <Box component="span" sx={{ color: mint.cyan }}>
              In Real Time.
            </Box>
          </Typography>

          <Stack
            direction="row"
            spacing={2.5}
            sx={{ display: { xs: 'none', md: 'flex' }, alignItems: 'flex-start' }}
          >
            {WORKSPACE.map((w) => (
              <Stack key={w.label} spacing={0.25} sx={{ minWidth: 56, alignItems: 'center' }}>
                <Box sx={{ position: 'relative', color: mint.textDim }}>
                  {w.icon}
                  {w.badge && (
                    <Box
                      sx={{
                        position: 'absolute',
                        top: -2,
                        right: -2,
                        width: 7,
                        height: 7,
                        borderRadius: '50%',
                        bgcolor: mint.violetBright,
                        boxShadow: `0 0 6px ${mint.violetBright}`,
                      }}
                    />
                  )}
                </Box>
                <Typography sx={{ fontSize: '9px', color: mint.textDim, whiteSpace: 'nowrap' }}>
                  {w.label}
                </Typography>
              </Stack>
            ))}
          </Stack>
        </Box>
      </Box>

      <Toolbar variant="dense" sx={{ px: { xs: 2, md: 4 }, minHeight: 48 }}>
        <Tabs
          value={active}
          textColor="inherit"
          slotProps={{ indicator: { sx: { bgcolor: mint.cyan, height: 3, borderRadius: 3 } } }}
        >
          {TABS.map((t) => (
            <Tab key={t.to} label={t.label} value={t.to} component={Link} to={t.to} />
          ))}
        </Tabs>
        <Box sx={{ flex: 1 }} />
        <Typography variant="caption" sx={{ color: mint.textDim, display: { xs: 'none', sm: 'block' } }}>
          Fictional data · DEMO/LIVE mode-blind
        </Typography>
      </Toolbar>
    </AppBar>
  );
}
