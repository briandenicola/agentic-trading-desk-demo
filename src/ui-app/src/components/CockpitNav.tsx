import { AppBar, Box, Tab, Tabs, Toolbar, Typography } from '@mui/material';
import { Link, useLocation } from 'react-router-dom';

const TABS = [
  { label: 'RM Daily Briefing', to: '/' },
  { label: 'Trading Morning Brief', to: '/morning-brief' },
];

/**
 * Top cockpit navigation. The RM Daily Briefing is the PRIMARY scene (route `/`); the
 * municipal/trading morning brief is the secondary scene. Mode-blind: both scenes call
 * the orchestration API which returns DEMO or LIVE transparently.
 */
export default function CockpitNav() {
  const { pathname } = useLocation();
  const active = pathname.startsWith('/morning-brief') ? '/morning-brief' : '/';

  return (
    <AppBar position="static" color="default" elevation={0} sx={{ bgcolor: 'rgba(0,0,0,0.4)', borderBottom: '1px solid rgba(255,255,255,0.1)' }}>
      <Toolbar variant="dense" sx={{ gap: 3 }}>
        <Typography variant="subtitle1" sx={{ fontWeight: 700, letterSpacing: '0.5px', color: 'primary.main' }}>
          Client&nbsp;CV
        </Typography>
        <Box sx={{ flex: 1 }}>
          <Tabs value={active} textColor="inherit" slotProps={{ indicator: { sx: { bgcolor: 'primary.main' } } }}>
            {TABS.map((t) => (
              <Tab
                key={t.to}
                label={t.label}
                value={t.to}
                component={Link}
                to={t.to}
                sx={{ textTransform: 'none', fontWeight: 500, minHeight: 48 }}
              />
            ))}
          </Tabs>
        </Box>
        <Typography variant="caption" color="text.secondary">
          Fictional data · DEMO/LIVE mode-blind
        </Typography>
      </Toolbar>
    </AppBar>
  );
}
