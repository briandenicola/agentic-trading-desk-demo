import { Box, Chip, Paper, Stack, Table, TableBody, TableCell, TableHead, TableRow, Typography } from '@mui/material';
import ArrowDropUpRoundedIcon from '@mui/icons-material/ArrowDropUpRounded';
import ArrowDropDownRoundedIcon from '@mui/icons-material/ArrowDropDownRounded';
import RemoveRoundedIcon from '@mui/icons-material/RemoveRounded';
import type {
  InventoryAxe,
  MacroThemeBullet,
  MarketStripItem,
  ReasoningStep,
  TdEventConsidered,
} from '../../api/client';
import SectionTitle from '../../components/SectionTitle';
import { mint } from '../../theme/theme';

const dirColor = (d: string) => (d === 'up' ? mint.green : d === 'down' ? mint.red : mint.textDim);

export function MarketStrip({ items }: { items: MarketStripItem[] }) {
  if (items.length === 0) return null;
  return (
    <Paper
      data-testid="td-market-strip"
      sx={{ p: 1.25, display: 'flex', flexWrap: 'wrap', gap: 1.25, bgcolor: 'rgba(255,255,255,0.02)' }}
    >
      {items.map((s) => {
        const color = dirColor(s.direction);
        const Icon =
          s.direction === 'up' ? ArrowDropUpRoundedIcon : s.direction === 'down' ? ArrowDropDownRoundedIcon : RemoveRoundedIcon;
        return (
          <Box
            key={s.label}
            sx={{
              display: 'flex',
              alignItems: 'center',
              gap: 0.5,
              px: 1.25,
              py: 0.5,
              borderRadius: 2,
              border: `1px solid ${mint.border}`,
              bgcolor: 'rgba(255,255,255,0.02)',
            }}
          >
            <Typography sx={{ fontSize: 12, fontWeight: 700, color: mint.text }}>{s.label}</Typography>
            <Typography sx={{ fontSize: 12, color: mint.textDim }}>{s.value}</Typography>
            <Icon sx={{ fontSize: 18, color }} />
            {s.change && (
              <Typography sx={{ fontSize: 10, fontWeight: 600, color }}>{s.change}</Typography>
            )}
          </Box>
        );
      })}
    </Paper>
  );
}

export function MacroThemes({ themes }: { themes: MacroThemeBullet[] }) {
  if (themes.length === 0) return null;
  return (
    <Paper sx={{ p: 2 }}>
      <SectionTitle>Macro themes in play</SectionTitle>
      <Stack spacing={1.25} sx={{ mt: 1.5 }}>
        {themes.map((t) => (
          <Box key={t.theme}>
            <Typography variant="body2" sx={{ fontWeight: 700, color: mint.cyan }}>
              {t.theme}
            </Typography>
            <Typography variant="body2" sx={{ color: mint.textDim }}>
              {t.detail}
            </Typography>
          </Box>
        ))}
      </Stack>
    </Paper>
  );
}

function fmtSize(n: number): string {
  const abs = Math.abs(n);
  const s = abs >= 1_000_000 ? `${(abs / 1_000_000).toFixed(abs % 1_000_000 === 0 ? 0 : 1)}mm` : abs.toLocaleString();
  return `${n < 0 ? 'short' : 'long'} ${s}`;
}

export function AxeBoard({ axes, limit = 10 }: { axes: InventoryAxe[]; limit?: number }) {
  if (axes.length === 0) return null;
  const shown = axes.slice(0, limit);
  return (
    <Paper data-testid="td-axe-board">
      <Box sx={{ p: 2, borderBottom: `1px solid ${mint.border}` }}>
        <SectionTitle color="violet" caption="— desk inventory matched to client demand">
          Inventory axe board
        </SectionTitle>
      </Box>
      <Table size="small">
        <TableHead>
          <TableRow>
            <TableCell>Security</TableCell>
            <TableCell>Axe</TableCell>
            <TableCell>Position</TableCell>
            <TableCell align="right">Bid / Offer</TableCell>
            <TableCell>Matched clients</TableCell>
          </TableRow>
        </TableHead>
        <TableBody>
          {shown.map((a) => {
            const side = a.axeSide.toLowerCase();
            const sideColor = side === 'buy' ? mint.green : side === 'sell' ? mint.red : mint.amber;
            return (
              <TableRow key={a.securityId}>
                <TableCell>
                  <Typography variant="body2" sx={{ fontWeight: 600 }}>
                    {a.securityName}
                  </Typography>
                  <Typography variant="caption" sx={{ color: mint.textDim }}>
                    {[a.assetClass, a.sector].filter(Boolean).join(' · ')}
                  </Typography>
                </TableCell>
                <TableCell>
                  <Chip
                    label={a.axeSide.toUpperCase()}
                    size="small"
                    sx={{ fontSize: 10, fontWeight: 700, height: 20, color: sideColor, bgcolor: `${sideColor}1f` }}
                  />
                </TableCell>
                <TableCell>
                  <Typography variant="caption" sx={{ color: mint.textDim }}>
                    {fmtSize(a.inventorySize)}
                  </Typography>
                </TableCell>
                <TableCell align="right">
                  <Typography variant="body2">
                    {a.bidPrice != null ? a.bidPrice : '—'} / {a.offerPrice != null ? a.offerPrice : '—'}
                  </Typography>
                </TableCell>
                <TableCell>
                  <Typography variant="caption" sx={{ color: mint.textDim }}>
                    {a.matchedClients.length > 0 ? a.matchedClients.join(', ') : '—'}
                  </Typography>
                </TableCell>
              </TableRow>
            );
          })}
        </TableBody>
      </Table>
    </Paper>
  );
}

const sentimentColor = (s?: string) =>
  s === 'Positive' ? mint.green : s === 'Negative' ? mint.red : s === 'Mixed' ? mint.amber : mint.textDim;

export function EventsConsidered({ events, limit = 12 }: { events: TdEventConsidered[]; limit?: number }) {
  if (events.length === 0) return null;
  const shown = events.slice(0, limit);
  return (
    <Box data-testid="td-events-considered">
      <SectionTitle caption="— overnight & intraday signals the agent weighed">
        Events considered ({events.length})
      </SectionTitle>
      <Stack spacing={1} sx={{ mt: 1.5 }}>
        {shown.map((ev) => {
          const sc = sentimentColor(ev.sentiment);
          return (
            <Paper key={ev.id} sx={{ p: 1.5, bgcolor: 'rgba(255,255,255,0.02)' }}>
              <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, flexWrap: 'wrap' }}>
                <Chip
                  label={ev.kind.toUpperCase()}
                  size="small"
                  sx={{ fontSize: 9, fontWeight: 700, height: 18, color: mint.cyan, bgcolor: `${mint.cyan}1f` }}
                />
                {ev.sentiment && (
                  <Chip
                    label={ev.sentiment}
                    size="small"
                    sx={{ fontSize: 9, fontWeight: 700, height: 18, color: sc, bgcolor: `${sc}1f` }}
                  />
                )}
                <Typography variant="body2" sx={{ fontWeight: 600 }}>
                  {ev.headline}
                </Typography>
              </Box>
              {ev.summary && (
                <Typography variant="caption" sx={{ color: mint.textDim, display: 'block', mt: 0.5 }}>
                  {ev.summary}
                </Typography>
              )}
            </Paper>
          );
        })}
      </Stack>
    </Box>
  );
}

export function AgentReasoning({ steps }: { steps: ReasoningStep[] }) {
  if (steps.length === 0) return null;
  return (
    <Paper
      sx={{
        p: 2,
        backgroundImage: `linear-gradient(135deg, ${mint.violet}1f, ${mint.cyan}0f)`,
        borderColor: `${mint.violet}3a`,
      }}
    >
      <SectionTitle color="violet">Agent reasoning</SectionTitle>
      <Box component="ul" sx={{ listStyle: 'none', p: 0, m: 0, mt: 1.5 }}>
        {steps.map((step, idx) => (
          <Box component="li" key={idx} sx={{ display: 'flex', gap: 1.5, mb: 1, '&:last-child': { mb: 0 } }}>
            <Typography
              component="span"
              sx={{ color: step.status === 'done' ? mint.green : mint.textDim, fontSize: 14, minWidth: 16 }}
            >
              {step.status === 'done' ? '✓' : '◦'}
            </Typography>
            <Typography variant="body2" sx={{ flex: 1 }}>
              {step.text}
            </Typography>
          </Box>
        ))}
      </Box>
    </Paper>
  );
}
