import { Box, Chip, Stack, Typography } from '@mui/material';
import { Link } from 'react-router-dom';
import ShowChartRoundedIcon from '@mui/icons-material/ShowChartRounded';
import AccountBalanceRoundedIcon from '@mui/icons-material/AccountBalanceRounded';
import ArrowForwardRoundedIcon from '@mui/icons-material/ArrowForwardRounded';
import type { ReactNode } from 'react';
import MintBrand from '../../components/MintBrand';
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
}

const CARDS: DeskCard[] = [
  {
    to: '/desk',
    badge: 'DEMO FOCUS',
    badgeColor: mint.cyan,
    icon: <ShowChartRoundedIcon sx={{ fontSize: 30 }} />,
    title: 'Institutional Sales & Trading',
    blurb:
      'Morning planning and prioritized client outreach for a coverage salesperson — ranked by overnight research, open RFQs, client inquiries and our inventory axes.',
    persona: 'Theo Wexler · Hedge-fund coverage',
    points: [
      'Prioritized client call list with live re-rank',
      'Inventory axe board matched to client demand',
      'Breaking-print reactivity (AI-capex supercycle)',
    ],
  },
  {
    to: '/cb',
    badge: 'ALSO AVAILABLE',
    badgeColor: mint.violetBright,
    icon: <AccountBalanceRoundedIcon sx={{ fontSize: 30 }} />,
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

export default function LandingScene() {
  return (
    <Box
      sx={{
        minHeight: '100vh',
        background: mint.bg,
        color: mint.text,
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        px: { xs: 2, md: 4 },
        py: { xs: 4, md: 8 },
      }}
    >
      <Stack spacing={1.5} sx={{ alignItems: 'center', textAlign: 'center', maxWidth: 760, mb: { xs: 4, md: 6 } }}>
        <MintBrand size="md" />
        <Typography variant="h3" sx={{ mt: 2, fontWeight: 700 }}>
          Markets intelligence that works for you.
        </Typography>
        <Typography variant="body1" sx={{ color: mint.textDim, maxWidth: 620 }}>
          Pick a workspace. Each one turns overnight signals into a ranked, explainable plan for who
          to call first this morning — and re-ranks in real time as the desk breaks news.
        </Typography>
        <Chip
          label="Fictional data · DEMO / LIVE mode-blind"
          size="small"
          sx={{ mt: 1, bgcolor: `${mint.violet}1f`, borderColor: mint.border, color: mint.textDim }}
          variant="outlined"
        />
      </Stack>

      <Box
        sx={{
          display: 'grid',
          gap: { xs: 2.5, md: 3 },
          width: '100%',
          maxWidth: 920,
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
              p: { xs: 2.5, md: 3 },
              borderRadius: 4,
              border: `1px solid ${mint.border}`,
              background: mint.paper,
              backgroundImage: `linear-gradient(160deg, ${card.badgeColor}14, transparent 55%)`,
              transition: 'transform 160ms ease, border-color 160ms ease, box-shadow 160ms ease',
              '&:hover': {
                transform: 'translateY(-4px)',
                borderColor: card.badgeColor,
                boxShadow: `0 18px 48px rgba(0,0,0,0.5)`,
              },
            }}
          >
            <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
              <Box
                sx={{
                  width: 56,
                  height: 56,
                  borderRadius: 3,
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'center',
                  color: card.badgeColor,
                  background: `${card.badgeColor}1f`,
                  border: `1px solid ${card.badgeColor}3a`,
                }}
              >
                {card.icon}
              </Box>
              <Chip
                label={card.badge}
                size="small"
                sx={{
                  fontWeight: 700,
                  fontSize: 10,
                  letterSpacing: '1px',
                  color: card.badgeColor,
                  bgcolor: `${card.badgeColor}1f`,
                  border: `1px solid ${card.badgeColor}3a`,
                }}
              />
            </Box>

            <Typography variant="h5" sx={{ mt: 2, fontWeight: 700 }}>
              {card.title}
            </Typography>
            <Typography sx={{ fontSize: 12, fontWeight: 700, letterSpacing: '0.4px', color: card.badgeColor, mt: 0.5 }}>
              {card.persona}
            </Typography>
            <Typography variant="body2" sx={{ color: mint.textDim, mt: 1.5, flex: 1 }}>
              {card.blurb}
            </Typography>

            <Stack spacing={0.75} sx={{ mt: 2 }}>
              {card.points.map((p) => (
                <Box key={p} sx={{ display: 'flex', alignItems: 'flex-start', gap: 1 }}>
                  <Box component="span" sx={{ color: card.badgeColor, fontSize: 13, lineHeight: '20px' }}>
                    ▸
                  </Box>
                  <Typography variant="body2" sx={{ color: mint.text }}>
                    {p}
                  </Typography>
                </Box>
              ))}
            </Stack>

            <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.75, mt: 2.5, color: card.badgeColor, fontWeight: 700 }}>
              <Typography sx={{ fontSize: 14, fontWeight: 700 }}>Enter workspace</Typography>
              <ArrowForwardRoundedIcon sx={{ fontSize: 18 }} />
            </Box>
          </Box>
        ))}
      </Box>

      <Typography sx={{ mt: { xs: 4, md: 6 }, fontSize: 11, letterSpacing: '2px', color: mint.textDim }}>
        M.INT — MARKETS INTELLIGENCE THAT WORKS FOR YOU.
      </Typography>
    </Box>
  );
}
