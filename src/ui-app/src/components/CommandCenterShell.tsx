import { useEffect, useMemo, useState, type ReactNode } from 'react';
import { Box, Typography } from '@mui/material';
import { Link, useLocation } from 'react-router-dom';
import ShowChartRoundedIcon from '@mui/icons-material/ShowChartRounded';
import WbSunnyRoundedIcon from '@mui/icons-material/WbSunnyRounded';
import SmartToyRoundedIcon from '@mui/icons-material/SmartToyRounded';
import CampaignRoundedIcon from '@mui/icons-material/CampaignRounded';
import AccountBalanceRoundedIcon from '@mui/icons-material/AccountBalanceRounded';
import AppsRoundedIcon from '@mui/icons-material/AppsRounded';
import { mint } from '../theme/theme';
import type { MarketStripItem } from '../api/client';

export interface ShellKpi {
  label: string;
  value: ReactNode;
  delta?: string;
  deltaColor?: string;
  valueColor?: string;
  to?: string;
}

interface NavItem {
  label: string;
  sub: string;
  to: string;
  icon: ReactNode;
  badge?: number;
  badgeColor?: string;
  match: (path: string) => boolean;
}

interface NavGroup {
  heading: string;
  items: NavItem[];
}

export interface CommandCenterShellProps {
  mode?: 'DEMO' | 'LIVE';
  asOf?: string;
  /** KPI tiles shown in the stats bar. */
  kpis?: ShellKpi[];
  /** Market quote chips shown on the right of the stats bar. */
  marketChips?: MarketStripItem[];
  /** Headlines that scroll across the live news ticker. */
  tickerItems?: string[];
  /** Priority-call count (badge on the Morning Brief nav item). */
  priorityCount?: number;
  children: ReactNode;
}

const dirColor = (d: string) => (d === 'up' ? mint.green : d === 'down' ? mint.red : mint.textDim);

function useClock(): string {
  const [now, setNow] = useState(() => new Date());
  useEffect(() => {
    const id = setInterval(() => setNow(new Date()), 1000);
    return () => clearInterval(id);
  }, []);
  return now.toLocaleTimeString('en-US', { hour12: false });
}

/**
 * Bloomberg-terminal "command center" chrome for the Institutional Sales &
 * Trading workspace (mirrors assets/mint-v4.html): fixed header with the brand
 * lockup + live clock + mode tag, a KPI stats bar with market quotes, a
 * scrolling live-feed ticker, and a grouped left sidebar nav. The scene content
 * is rendered in a scrollable main area. Mode-blind — every scene drives the
 * same TdBriefing whether DEMO or LIVE.
 */
export default function CommandCenterShell({
  mode = 'DEMO',
  asOf,
  kpis = [],
  marketChips = [],
  tickerItems = [],
  priorityCount,
  children,
}: CommandCenterShellProps) {
  const { pathname } = useLocation();
  const clock = useClock();
  const isLive = mode === 'LIVE';

  const groups: NavGroup[] = useMemo(
    () => [
      {
        heading: 'Scenes',
        items: [
          {
            label: 'Trading Desk',
            sub: 'Morning plan',
            to: '/desk',
            icon: <ShowChartRoundedIcon sx={{ fontSize: 16 }} />,
            match: (p) => p === '/desk',
          },
          {
            label: 'Morning Brief',
            sub: 'Outreach plan',
            to: '/desk/morning-brief',
            icon: <WbSunnyRoundedIcon sx={{ fontSize: 16 }} />,
            badge: priorityCount,
            badgeColor: mint.blue,
            match: (p) => p.startsWith('/desk/morning-brief'),
          },
        ],
      },
      {
        heading: 'Intelligence',
        items: [
          {
            label: 'Desk Copilot',
            sub: 'AI chat',
            to: '/chat',
            icon: <SmartToyRoundedIcon sx={{ fontSize: 16 }} />,
            match: (p) => p.startsWith('/chat'),
          },
          {
            label: 'News Desk',
            sub: 'Inject events',
            to: '/admin',
            icon: <CampaignRoundedIcon sx={{ fontSize: 16 }} />,
            match: (p) => p.startsWith('/admin'),
          },
        ],
      },
      {
        heading: 'Workspaces',
        items: [
          {
            label: 'Commercial Banking',
            sub: 'RM workspace',
            to: '/cb',
            icon: <AccountBalanceRoundedIcon sx={{ fontSize: 16 }} />,
            match: (p) => p === '/cb',
          },
          {
            label: 'Switch workspace',
            sub: 'Landing',
            to: '/',
            icon: <AppsRoundedIcon sx={{ fontSize: 16 }} />,
            match: (p) => p === '/',
          },
        ],
      },
    ],
    [priorityCount],
  );

  const ticker = tickerItems.length > 0 ? tickerItems : ['Markets intelligence · fictional data · DEMO / LIVE mode-blind'];

  return (
    <Box
      sx={{
        height: '100vh',
        display: 'flex',
        flexDirection: 'column',
        overflow: 'hidden',
        bgcolor: mint.bg,
        color: mint.text,
      }}
    >
      {/* HEADER */}
      <Box
        sx={{
          flexShrink: 0,
          height: 48,
          px: 2,
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'space-between',
          background: `linear-gradient(135deg, ${mint.bgAlt} 0%, #0a1628 100%)`,
          borderBottom: `1px solid ${mint.borderAccent}`,
        }}
      >
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 1.5 }}>
          <Typography
            component="span"
            sx={{
              fontSize: 18,
              fontWeight: 900,
              letterSpacing: '-0.5px',
              backgroundImage: `linear-gradient(135deg, ${mint.blue}, ${mint.cyan})`,
              WebkitBackgroundClip: 'text',
              backgroundClip: 'text',
              color: 'transparent',
            }}
          >
            M.INT
          </Typography>
          <Typography
            sx={{
              fontSize: 9,
              color: mint.textFaint,
              letterSpacing: '1px',
              textTransform: 'uppercase',
              borderLeft: `1px solid ${mint.borderHard}`,
              pl: 1.5,
              display: { xs: 'none', md: 'block' },
            }}
          >
            Fixed Income Credit · Intelligence Desk
          </Typography>
          <Tag color={isLive ? mint.green : mint.gold}>{isLive ? '⬤ LIVE' : '⬤ DEMO'}</Tag>
        </Box>
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 1.25 }}>
          {asOf && (
            <Typography sx={{ fontSize: 9, color: mint.textFaint, display: { xs: 'none', sm: 'block' } }}>
              As of {asOf}
            </Typography>
          )}
          <Typography
            sx={{ fontSize: 12, fontWeight: 700, color: mint.cyan, fontVariantNumeric: 'tabular-nums', letterSpacing: '0.5px' }}
          >
            {clock}
          </Typography>
          <Tag color={mint.purple}>AI COPILOT</Tag>
          <Box
            sx={{
              width: 26,
              height: 26,
              borderRadius: '50%',
              background: `linear-gradient(135deg, ${mint.blue}, ${mint.purple})`,
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              fontSize: 10,
              fontWeight: 800,
              color: '#fff',
            }}
          >
            TW
          </Box>
        </Box>
      </Box>

      {/* STATS BAR */}
      {(kpis.length > 0 || marketChips.length > 0) && (
        <Box
          sx={{
            flexShrink: 0,
            minHeight: 38,
            px: 2,
            display: 'flex',
            alignItems: 'center',
            gap: 2.5,
            bgcolor: mint.bgAlt,
            borderBottom: `1px solid ${mint.borderHard}`,
            overflowX: 'auto',
            overflowY: 'hidden',
          }}
        >
          {kpis.map((k) => {
            const tile = (
              <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
                <Box>
                  <Typography sx={{ fontSize: 8, color: mint.textFaint, textTransform: 'uppercase', letterSpacing: '0.5px' }}>
                    {k.label}
                  </Typography>
                  <Typography sx={{ fontSize: 14, fontWeight: 800, color: k.valueColor ?? mint.text, lineHeight: 1.2 }}>
                    {k.value}
                  </Typography>
                </Box>
                {k.delta && (
                  <Typography sx={{ fontSize: 8, fontWeight: 700, color: k.deltaColor ?? mint.green }}>{k.delta}</Typography>
                )}
              </Box>
            );
            return k.to ? (
              <Box
                key={k.label}
                component={Link}
                to={k.to}
                sx={{ textDecoration: 'none', flexShrink: 0, py: 0.5, px: 0.75, borderRadius: 1, '&:hover': { bgcolor: mint.paperHi } }}
              >
                {tile}
              </Box>
            ) : (
              <Box key={k.label} sx={{ flexShrink: 0 }}>
                {tile}
              </Box>
            );
          })}

          {marketChips.length > 0 && (
            <Box sx={{ ml: 'auto', display: 'flex', alignItems: 'center', gap: 0, flexShrink: 0 }}>
              {marketChips.map((m) => (
                <Box key={m.label} sx={{ px: 1.25, borderLeft: `1px solid ${mint.borderHard}` }}>
                  <Typography sx={{ fontSize: 8, color: mint.textFaint, textTransform: 'uppercase', letterSpacing: '0.5px' }}>
                    {m.label}
                  </Typography>
                  <Typography sx={{ fontSize: 11, color: mint.text, fontVariantNumeric: 'tabular-nums' }}>
                    {m.value}{' '}
                    {m.change && (
                      <Box component="span" sx={{ fontSize: 8, color: dirColor(m.direction) }}>
                        {m.direction === 'up' ? '▲' : m.direction === 'down' ? '▼' : '·'} {m.change}
                      </Box>
                    )}
                  </Typography>
                </Box>
              ))}
            </Box>
          )}
        </Box>
      )}

      {/* NEWS TICKER */}
      <Box
        sx={{
          flexShrink: 0,
          height: 24,
          display: 'flex',
          alignItems: 'center',
          overflow: 'hidden',
          bgcolor: '#060d1a',
          borderBottom: `1px solid ${mint.borderHard}`,
        }}
      >
        <Typography
          sx={{
            fontSize: 8,
            fontWeight: 700,
            color: mint.blue,
            px: 1.25,
            borderRight: `1px solid ${mint.borderHard}`,
            whiteSpace: 'nowrap',
            textTransform: 'uppercase',
            letterSpacing: '0.5px',
            flexShrink: 0,
          }}
        >
          📡 Live Feed
        </Typography>
        <Box sx={{ flex: 1, overflow: 'hidden', position: 'relative', height: '100%' }}>
          <Box
            sx={{
              display: 'inline-flex',
              alignItems: 'center',
              height: '100%',
              whiteSpace: 'nowrap',
              animation: 'ccTicker 80s linear infinite',
              '@keyframes ccTicker': { from: { transform: 'translateX(0)' }, to: { transform: 'translateX(-50%)' } },
            }}
          >
            {[...ticker, ...ticker].map((t, i) => (
              <Box
                key={i}
                sx={{
                  display: 'flex',
                  alignItems: 'center',
                  px: 1.75,
                  borderRight: `1px solid ${mint.borderSoft}`,
                  fontSize: 8.5,
                  color: mint.textDim,
                }}
              >
                {t}
              </Box>
            ))}
          </Box>
        </Box>
      </Box>

      {/* WRAP: sidebar + main */}
      <Box sx={{ display: 'flex', flex: 1, overflow: 'hidden' }}>
        {/* SIDEBAR */}
        <Box
          sx={{
            width: 184,
            flexShrink: 0,
            bgcolor: mint.bgAlt,
            borderRight: `1px solid ${mint.borderHard}`,
            overflowY: 'auto',
            display: { xs: 'none', md: 'block' },
          }}
        >
          {groups.map((g) => (
            <Box key={g.heading}>
              <Typography
                sx={{
                  px: 1.25,
                  py: 1,
                  fontSize: 7.5,
                  fontWeight: 700,
                  color: mint.textFaint,
                  textTransform: 'uppercase',
                  letterSpacing: '1px',
                  borderBottom: `1px solid ${mint.borderHard}`,
                }}
              >
                {g.heading}
              </Typography>
              {g.items.map((it) => {
                const active = it.match(pathname);
                return (
                  <Box
                    key={it.to + it.label}
                    component={Link}
                    to={it.to}
                    sx={{
                      px: 1.25,
                      py: 0.75,
                      minHeight: 34,
                      display: 'flex',
                      alignItems: 'center',
                      gap: 1,
                      textDecoration: 'none',
                      color: active ? mint.blue : mint.textDim,
                      bgcolor: active ? `${mint.blue}1a` : 'transparent',
                      borderLeft: `3px solid ${active ? mint.blue : 'transparent'}`,
                      transition: 'all .15s',
                      '&:hover': { bgcolor: mint.paperHi, color: mint.text },
                    }}
                  >
                    <Box sx={{ width: 18, textAlign: 'center', flexShrink: 0, display: 'flex', justifyContent: 'center' }}>
                      {it.icon}
                    </Box>
                    <Box sx={{ flex: 1, minWidth: 0 }}>
                      <Typography sx={{ fontSize: 10, fontWeight: active ? 700 : 600, lineHeight: 1.2 }}>
                        {it.label}
                      </Typography>
                      <Typography sx={{ fontSize: 7.5, color: mint.textFaint, lineHeight: 1.2 }}>{it.sub}</Typography>
                    </Box>
                    {it.badge != null && it.badge > 0 && (
                      <Box
                        sx={{
                          borderRadius: 5,
                          px: 0.75,
                          fontSize: 8,
                          fontWeight: 700,
                          color: it.badgeColor ?? mint.blue,
                          bgcolor: `${it.badgeColor ?? mint.blue}33`,
                        }}
                      >
                        {it.badge}
                      </Box>
                    )}
                  </Box>
                );
              })}
            </Box>
          ))}
        </Box>

        {/* MAIN (scrollable) */}
        <Box sx={{ flex: 1, overflowY: 'auto', overflowX: 'hidden' }}>{children}</Box>
      </Box>
    </Box>
  );
}

function Tag({ color, children }: { color: string; children: ReactNode }) {
  return (
    <Box
      component="span"
      sx={{
        px: 0.75,
        py: '2px',
        borderRadius: '3px',
        fontSize: 7,
        fontWeight: 700,
        letterSpacing: '0.5px',
        textTransform: 'uppercase',
        color,
        bgcolor: `${color}26`,
        border: `1px solid ${color}4d`,
        whiteSpace: 'nowrap',
      }}
    >
      {children}
    </Box>
  );
}
