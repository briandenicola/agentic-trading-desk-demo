import { useEffect, useState, type ReactNode } from 'react';
import { Box, Typography } from '@mui/material';
import { Link } from 'react-router-dom';
import ShowChartRoundedIcon from '@mui/icons-material/ShowChartRounded';
import AccountBalanceRoundedIcon from '@mui/icons-material/AccountBalanceRounded';
import ArrowForwardRoundedIcon from '@mui/icons-material/ArrowForwardRounded';
import RadarRoundedIcon from '@mui/icons-material/RadarRounded';
import { mint } from '../../theme/theme';

interface DeskCard {
  to: string;
  badge: string;
  badgeColor: string;
  icon: ReactNode;
  title: string;
  blurb: string;
  persona: string;
  points: string[];
  featured?: { to: string; label: string };
}

const CARDS: DeskCard[] = [
  {
    to: '/desk',
    badge: 'DEMO FOCUS',
    badgeColor: mint.cyan,
    icon: <ShowChartRoundedIcon sx={{ fontSize: 26 }} />,
    title: 'Institutional Sales & Trading',
    blurb:
      'Morning planning and prioritized client outreach for a coverage salesperson — ranked by overnight research, open RFQs, client inquiries and our inventory axes.',
    persona: 'Theo Wexler · Hedge-fund coverage',
    points: [
      'Prioritized client call list with live re-rank',
      'Inventory axe board matched to client demand',
      'New Issue Radar: who to call when a deal prints',
    ],
    featured: { to: '/desk/new-issue', label: 'New Issue Radar' },
  },
  {
    to: '/cb',
    badge: 'ALSO AVAILABLE',
    badgeColor: mint.violetBright,
    icon: <AccountBalanceRoundedIcon sx={{ fontSize: 26 }} />,
    title: 'Commercial Banking RM',
    blurb:
      'The relationship-manager daily briefing — a ranked call list across complaints, overdue follow-ups, closing opportunities and stuck deals for a regional book.',
    persona: 'Marcus Johnson · Midwest book',
    points: [
      'RM daily briefing + prioritized outreach',
      'Reactive event cockpit + News Desk inject',
      'Grounded AI chat assistant',
    ],
  },
];

const MONO = "'JetBrains Mono', 'SF Mono', 'Cascadia Code', Consolas, monospace";

function useClock(): string {
  const [now, setNow] = useState(() => new Date());
  useEffect(() => {
    const id = setInterval(() => setNow(new Date()), 1000);
    return () => clearInterval(id);
  }, []);
  return now.toLocaleTimeString('en-US', { hour12: false });
}

function StatusTag({ color, children }: { color: string; children: ReactNode }) {
  return (
    <Box
      component="span"
      sx={{
        px: 0.75,
        py: '2px',
        borderRadius: '3px',
        fontSize: 8,
        fontWeight: 700,
        letterSpacing: '0.5px',
        textTransform: 'uppercase',
        fontFamily: MONO,
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

export default function LandingScene() {
  const clock = useClock();

  return (
    <Box
      sx={{
        minHeight: '100vh',
        bgcolor: mint.bg,
        color: mint.text,
        display: 'flex',
        flexDirection: 'column',
      }}
    >
      {/* TERMINAL HEADER */}
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
              fontFamily: MONO,
              color: mint.textFaint,
              letterSpacing: '1px',
              textTransform: 'uppercase',
              borderLeft: `1px solid ${mint.borderHard}`,
              pl: 1.5,
              display: { xs: 'none', md: 'block' },
            }}
          >
            Markets Intelligence · Workspace Selector
          </Typography>
        </Box>
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 1.25 }}>
          <StatusTag color={mint.green}>⬤ Systems Online</StatusTag>
          <Typography
            sx={{ fontSize: 12, fontWeight: 700, color: mint.cyan, fontFamily: MONO, letterSpacing: '0.5px' }}
          >
            {clock}
          </Typography>
        </Box>
      </Box>

      {/* BOOT STRIP */}
      <Box
        sx={{
          flexShrink: 0,
          px: 2,
          py: 0.75,
          bgcolor: '#060d1a',
          borderBottom: `1px solid ${mint.borderHard}`,
          display: 'flex',
          alignItems: 'center',
          gap: 1.5,
          flexWrap: 'wrap',
        }}
      >
        <Typography sx={{ fontSize: 9, fontFamily: MONO, color: mint.green }}>
          {'>'} initializing markets-intelligence desk…
        </Typography>
        <Typography sx={{ fontSize: 9, fontFamily: MONO, color: mint.textDim }}>
          agents: ONLINE · tools: ONLINE · data: fictional
        </Typography>
        <StatusTag color={mint.gold}>DEMO / LIVE mode-blind</StatusTag>
      </Box>

      {/* BODY */}
      <Box
        sx={{
          flex: 1,
          overflowY: 'auto',
          px: { xs: 2, md: 4 },
          py: { xs: 4, md: 6 },
          display: 'flex',
          flexDirection: 'column',
          alignItems: 'center',
        }}
      >
        <Box sx={{ textAlign: 'center', maxWidth: 760, mb: { xs: 4, md: 5 } }}>
          <Typography
            sx={{ fontSize: 9, fontFamily: MONO, color: mint.blue, letterSpacing: '3px', textTransform: 'uppercase' }}
          >
            // select a workspace
          </Typography>
          <Typography variant="h3" sx={{ mt: 1.5, fontWeight: 800 }}>
            Markets intelligence that works for you.
          </Typography>
          <Typography variant="body1" sx={{ color: mint.textDim, maxWidth: 620, mx: 'auto', mt: 1.5 }}>
            Each workspace turns overnight signals into a ranked, explainable plan for who to call
            first this morning — and re-ranks in real time as the desk breaks news.
          </Typography>
        </Box>

        <Box
          sx={{
            display: 'grid',
            gap: { xs: 2.5, md: 2.5 },
            width: '100%',
            maxWidth: 960,
            gridTemplateColumns: { xs: '1fr', md: '1fr 1fr' },
          }}
        >
          {CARDS.map((card) => (
            <Box
              key={card.to}
              component={Link}
              to={card.to}
              data-testid={`desk-card-${card.to.replace('/', '')}`}
              sx={{
                textDecoration: 'none',
                color: 'inherit',
                display: 'flex',
                flexDirection: 'column',
                p: 0,
                borderRadius: 2,
                border: `1px solid ${mint.borderHard}`,
                background: mint.paper,
                overflow: 'hidden',
                transition: 'transform 160ms ease, border-color 160ms ease, box-shadow 160ms ease',
                '&:hover': {
                  transform: 'translateY(-3px)',
                  borderColor: card.badgeColor,
                  boxShadow: `0 16px 40px rgba(0,0,0,0.5)`,
                },
              }}
            >
              {/* panel title bar */}
              <Box
                sx={{
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'space-between',
                  px: 1.5,
                  py: 1,
                  bgcolor: mint.bgAlt,
                  borderBottom: `1px solid ${mint.borderHard}`,
                }}
              >
                <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
                  <Box sx={{ color: card.badgeColor, display: 'flex' }}>{card.icon}</Box>
                  <Typography
                    sx={{ fontSize: 9, fontFamily: MONO, color: mint.textFaint, letterSpacing: '0.5px' }}
                  >
                    {card.to}
                  </Typography>
                </Box>
                <StatusTag color={card.badgeColor}>{card.badge}</StatusTag>
              </Box>

              <Box sx={{ p: { xs: 2, md: 2.5 }, display: 'flex', flexDirection: 'column', flex: 1 }}>
                <Typography variant="h5" sx={{ fontWeight: 800 }}>
                  {card.title}
                </Typography>
                <Typography
                  sx={{ fontSize: 11, fontFamily: MONO, fontWeight: 700, letterSpacing: '0.4px', color: card.badgeColor, mt: 0.5 }}
                >
                  {card.persona}
                </Typography>
                <Typography variant="body2" sx={{ color: mint.textDim, mt: 1.5, flex: 1 }}>
                  {card.blurb}
                </Typography>

                <Box sx={{ mt: 2, display: 'flex', flexDirection: 'column', gap: 0.75 }}>
                  {card.points.map((p) => (
                    <Box key={p} sx={{ display: 'flex', alignItems: 'flex-start', gap: 1 }}>
                      <Box
                        component="span"
                        sx={{ color: card.badgeColor, fontSize: 11, fontFamily: MONO, lineHeight: '20px' }}
                      >
                        ▸
                      </Box>
                      <Typography variant="body2" sx={{ color: mint.text }}>
                        {p}
                      </Typography>
                    </Box>
                  ))}
                </Box>

                <Box
                  sx={{
                    display: 'flex',
                    alignItems: 'center',
                    gap: 1.5,
                    mt: 2.5,
                    pt: 2,
                    borderTop: `1px solid ${mint.borderSoft}`,
                  }}
                >
                  <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.75, color: card.badgeColor, fontWeight: 700 }}>
                    <Typography sx={{ fontSize: 13, fontWeight: 700 }}>Enter workspace</Typography>
                    <ArrowForwardRoundedIcon sx={{ fontSize: 16 }} />
                  </Box>
                  {card.featured && (
                    <Box
                      component={Link}
                      to={card.featured.to}
                      onClick={(e) => e.stopPropagation()}
                      sx={{
                        ml: 'auto',
                        display: 'flex',
                        alignItems: 'center',
                        gap: 0.5,
                        px: 1,
                        py: 0.5,
                        borderRadius: 1.5,
                        textDecoration: 'none',
                        color: mint.gold,
                        bgcolor: `${mint.gold}1a`,
                        border: `1px solid ${mint.gold}44`,
                        fontSize: 11,
                        fontWeight: 700,
                        '&:hover': { bgcolor: `${mint.gold}2e` },
                      }}
                    >
                      <RadarRoundedIcon sx={{ fontSize: 14 }} />
                      {card.featured.label}
                    </Box>
                  )}
                </Box>
              </Box>
            </Box>
          ))}
        </Box>

        <Typography
          sx={{ mt: { xs: 4, md: 6 }, fontSize: 10, fontFamily: MONO, letterSpacing: '2px', color: mint.textFaint }}
        >
          M.INT — MARKETS INTELLIGENCE THAT WORKS FOR YOU.
        </Typography>
      </Box>
    </Box>
  );
}
