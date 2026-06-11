import { AppBar, Box, Chip, Stack, Tab, Tabs, Toolbar, Typography } from '@mui/material';
import AppsRoundedIcon from '@mui/icons-material/AppsRounded';
import { Link, useLocation } from 'react-router-dom';
import MintBrand from './MintBrand';
import { mint } from '../theme/theme';

const TABS = [
  { label: 'Trading Desk', to: '/desk' },
  { label: 'Morning Brief', to: '/desk/morning-brief' },
  { label: 'News Desk', to: '/admin' },
  { label: 'AI Chat', to: '/chat' },
];

/**
 * Header for the Institutional Sales & Trading workspace. Brand lockup + desk
 * persona, the trading scene tabs, and a "switch workspace" link back to the
 * landing chooser. Mode-blind: scenes call the orchestration API (DEMO/LIVE).
 */
export default function TdNav() {
  const { pathname } = useLocation();
  const active = pathname.startsWith('/desk/morning-brief')
    ? '/desk/morning-brief'
    : pathname.startsWith('/admin')
      ? '/admin'
      : pathname.startsWith('/chat')
        ? '/chat'
        : '/desk';

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
        <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 2, flexWrap: 'wrap' }}>
          <Stack direction="row" spacing={2} sx={{ alignItems: 'center' }}>
            <MintBrand />
            <Chip
              label="INSTITUTIONAL SALES & TRADING"
              size="small"
              sx={{
                display: { xs: 'none', md: 'flex' },
                fontWeight: 700,
                fontSize: 10,
                letterSpacing: '1px',
                color: mint.cyan,
                bgcolor: `${mint.cyan}1f`,
                border: `1px solid ${mint.cyan}3a`,
              }}
            />
          </Stack>

          <Box
            component={Link}
            to="/"
            sx={{
              display: 'flex',
              alignItems: 'center',
              gap: 0.75,
              textDecoration: 'none',
              color: mint.textDim,
              '&:hover': { color: mint.text },
            }}
          >
            <AppsRoundedIcon sx={{ fontSize: 18 }} />
            <Typography sx={{ fontSize: 12, fontWeight: 600 }}>Switch workspace</Typography>
          </Box>
        </Box>
      </Box>

      <Toolbar variant="dense" sx={{ px: { xs: 2, md: 4 }, minHeight: 48 }}>
        <Tabs
          value={active}
          textColor="inherit"
          variant="scrollable"
          allowScrollButtonsMobile
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
