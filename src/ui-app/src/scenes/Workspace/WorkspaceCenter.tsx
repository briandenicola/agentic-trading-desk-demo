import { Box, Chip, CircularProgress, Stack, Typography } from '@mui/material';
import ChatBubbleRoundedIcon from '@mui/icons-material/ChatBubbleRounded';
import BoltRoundedIcon from '@mui/icons-material/BoltRounded';
import ReportProblemOutlinedIcon from '@mui/icons-material/ReportProblemOutlined';
import PaidOutlinedIcon from '@mui/icons-material/PaidOutlined';
import PublicOutlinedIcon from '@mui/icons-material/PublicOutlined';
import RefreshRoundedIcon from '@mui/icons-material/RefreshRounded';
import { mint } from '../../theme/theme';
import type { CallTagKind, MarketEvent, PriorityCall, RmBriefing } from '../../api/client';
import { useWorkspaceLive } from './useWorkspaceLive';
import { useChatDock } from './ChatOverlay';

const BAND_COLOR: Record<number, string> = {
  1: mint.red,
  2: '#ffb547',
  3: mint.amber,
  4: mint.green,
};

const TAG_COLOR: Record<CallTagKind, string> = {
  escalated: mint.red,
  'in-progress': mint.amber,
  followup: mint.cyan,
  closing: mint.green,
  stuck: mint.textDim,
  event: mint.violetBright,
};

const SEVERITY_COLOR: Record<MarketEvent['severity'], string> = {
  high: mint.red,
  medium: mint.amber,
  low: mint.cyan,
};

function fmtMm(mm: number): string {
  return `$${mm >= 1000 ? `${(mm / 1000).toFixed(1)}B` : `${mm.toFixed(mm % 1 === 0 ? 0 : 1)}M`}`;
}

function Card({ children, sx }: { children: React.ReactNode; sx?: object }) {
  return (
    <Box sx={{ p: 2, borderRadius: 3, border: `1px solid ${mint.border}`, background: mint.paper, ...sx }}>
      {children}
    </Box>
  );
}

function SectionLabel({ icon, children }: { icon?: React.ReactNode; children: React.ReactNode }) {
  return (
    <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.75, mb: 1.25 }}>
      {icon && <Box sx={{ color: mint.violetBright, display: 'flex' }}>{icon}</Box>}
      <Typography sx={{ fontSize: 12, fontWeight: 800, letterSpacing: '0.8px', color: mint.text }}>
        {children}
      </Typography>
    </Box>
  );
}

/** The big "HIGH PRIORITY" card — the #1 ranked call, with a seeded Open Chat action. */
function PriorityHero({ call }: { call: PriorityCall }) {
  const { openChat } = useChatDock();
  const { pulse } = useWorkspaceLive();
  const accent = BAND_COLOR[call.priority] ?? mint.green;
  const meta = [call.customerId, call.industrySector, [call.hqCity, call.state].filter(Boolean).join(', ')]
    .filter(Boolean)
    .join(' · ');

  return (
    <Box
      key={pulse}
      sx={{
        p: 2.25,
        borderRadius: 3,
        border: `1px solid ${accent}66`,
        background: `linear-gradient(160deg, ${accent}1f, ${mint.paper} 65%)`,
        '@keyframes heroFlash': {
          '0%': { boxShadow: `0 0 0 0 ${accent}00` },
          '30%': { boxShadow: `0 0 0 4px ${accent}55` },
          '100%': { boxShadow: `0 0 0 0 ${accent}00` },
        },
        animation: pulse > 0 ? 'heroFlash 1.6s ease-out 2' : 'none',
      }}
    >
      <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', mb: 1 }}>
        <Chip
          label="HIGH PRIORITY"
          size="small"
          sx={{ height: 22, fontSize: 10, fontWeight: 800, letterSpacing: '0.5px', color: '#fff', bgcolor: accent }}
        />
        <Chip
          label={`Score ${call.score}`}
          size="small"
          sx={{ height: 22, fontSize: 11, fontWeight: 700, color: mint.text, bgcolor: mint.borderSoft }}
        />
      </Box>

      <Typography sx={{ fontSize: 20, fontWeight: 800, color: mint.text, lineHeight: 1.2 }}>
        {call.rank}. {call.customerName}
      </Typography>
      <Typography sx={{ fontSize: 12, color: mint.textDim, mt: 0.5 }}>{meta}</Typography>

      <Box sx={{ display: 'flex', flexWrap: 'wrap', gap: 0.75, mt: 1.25 }}>
        {call.tags.map((tag, i) => {
          const c = TAG_COLOR[tag.kind];
          return (
            <Chip
              key={i}
              label={tag.label}
              size="small"
              sx={{ height: 20, fontSize: 10, fontWeight: 700, color: c, bgcolor: `${c}1f`, border: `1px solid ${c}55` }}
            />
          );
        })}
      </Box>

      {call.reasons.length > 0 && (
        <Box component="ul" sx={{ pl: 2.25, my: 1.25, '& li': { mb: 0.5 } }}>
          {call.reasons.slice(0, 3).map((r, i) => (
            <Typography component="li" key={i} sx={{ fontSize: 12.5, color: mint.textDim, lineHeight: 1.5 }}>
              {r}
            </Typography>
          ))}
        </Box>
      )}

      <Box sx={{ p: 1.25, borderRadius: 2, border: `1px solid ${mint.border}`, background: mint.bgAlt, mb: 1.5 }}>
        <Typography sx={{ fontSize: 11, fontWeight: 800, letterSpacing: '0.6px', color: mint.violetBright, mb: 0.25 }}>
          SUGGESTED FIRST ACTION
        </Typography>
        <Typography sx={{ fontSize: 12.5, color: mint.text, lineHeight: 1.5 }}>{call.suggestedAction}</Typography>
      </Box>

      <Box
        onClick={() =>
          openChat(
            `Tell me about ${call.customerName} (${call.customerId}) — why are they my top priority call this morning, and what should I lead with?`,
          )
        }
        role="button"
        aria-label={`Open chat about ${call.customerName}`}
        sx={{
          display: 'inline-flex',
          alignItems: 'center',
          gap: 1,
          px: 2,
          py: 1,
          borderRadius: 99,
          cursor: 'pointer',
          color: '#fff',
          fontWeight: 700,
          fontSize: 13,
          background: `linear-gradient(135deg, ${mint.violet}, ${mint.magenta})`,
          '&:hover': { filter: 'brightness(1.08)' },
        }}
      >
        <ChatBubbleRoundedIcon sx={{ fontSize: 16 }} />
        Open Chat
      </Box>
    </Box>
  );
}

function EventsInPlay({ events }: { events: MarketEvent[] }) {
  return (
    <Card>
      <SectionLabel icon={<BoltRoundedIcon sx={{ fontSize: 16 }} />}>EVENTS IN PLAY</SectionLabel>
      <Stack spacing={1}>
        {events.map((ev) => {
          const c = SEVERITY_COLOR[ev.severity];
          return (
            <Box
              key={ev.id}
              sx={{
                p: 1.25,
                borderRadius: 2,
                border: `1px solid ${mint.borderSoft}`,
                background: mint.paperHi,
                borderLeft: `3px solid ${c}`,
              }}
            >
              <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.75, mb: 0.5, flexWrap: 'wrap' }}>
                <Chip
                  label={ev.severity.toUpperCase()}
                  size="small"
                  sx={{ height: 17, fontSize: 9, fontWeight: 800, color: '#04101c', bgcolor: c }}
                />
                {ev.scope && (
                  <Chip
                    label={ev.scope}
                    size="small"
                    sx={{ height: 17, fontSize: 9, fontWeight: 700, color: mint.textDim, bgcolor: mint.borderSoft }}
                  />
                )}
              </Box>
              <Typography sx={{ fontSize: 12.5, fontWeight: 700, color: mint.text, lineHeight: 1.4 }}>
                {ev.headline}
              </Typography>
              <Typography sx={{ fontSize: 11.5, color: mint.textDim, mt: 0.25, lineHeight: 1.45 }}>
                {ev.summary}
              </Typography>
            </Box>
          );
        })}
      </Stack>
    </Card>
  );
}

function KpiPanel({ brief }: { brief: RmBriefing }) {
  const { kpis, portfolio } = brief;
  const items = [
    { label: 'Customers', value: `${portfolio.customerCount}` },
    { label: 'Exposure', value: fmtMm(portfolio.totalExposureMm) },
    { label: 'Deposits', value: fmtMm(portfolio.totalDepositsMm) },
    { label: 'Open pipeline', value: `${kpis.openPipelineCount} · ${fmtMm(kpis.openPipelineAmountMm)}` },
    { label: 'Closing ≤14d', value: `${kpis.closingWithin14Days}` },
    { label: 'Complaints', value: `${kpis.activeComplaints}` },
  ];
  return (
    <Card>
      <Box sx={{ display: 'grid', gap: 1.25, gridTemplateColumns: { xs: 'repeat(2, 1fr)', sm: 'repeat(3, 1fr)' } }}>
        {items.map((it) => (
          <Box key={it.label}>
            <Typography sx={{ fontSize: 10.5, color: mint.textDim, letterSpacing: '0.5px' }}>
              {it.label.toUpperCase()}
            </Typography>
            <Typography sx={{ fontSize: 15, fontWeight: 800, color: mint.text }}>{it.value}</Typography>
          </Box>
        ))}
      </Box>
    </Card>
  );
}

function ComplaintsAndPipeline({ brief }: { brief: RmBriefing }) {
  return (
    <Box sx={{ display: 'grid', gap: 1.75, gridTemplateColumns: { xs: '1fr', md: '1fr 1fr' } }}>
      <Card>
        <SectionLabel icon={<ReportProblemOutlinedIcon sx={{ fontSize: 16 }} />}>ACTIVE COMPLAINTS</SectionLabel>
        <Stack spacing={1}>
          {brief.complaintsSnapshot.length === 0 && (
            <Typography sx={{ fontSize: 12, color: mint.textDim }}>No active complaints.</Typography>
          )}
          {brief.complaintsSnapshot.map((c) => (
            <Box key={c.complaintId} sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 1 }}>
              <Box sx={{ minWidth: 0 }}>
                <Typography sx={{ fontSize: 12.5, fontWeight: 700, color: mint.text }} noWrap>
                  {c.customerName}
                </Typography>
                <Typography sx={{ fontSize: 11, color: mint.textDim }} noWrap>
                  {c.category ?? '—'}
                  {c.severity ? ` · ${c.severity}` : ''}
                </Typography>
              </Box>
              <Chip
                label={c.status}
                size="small"
                sx={{ height: 19, fontSize: 9.5, fontWeight: 700, color: mint.text, bgcolor: mint.borderSoft }}
              />
            </Box>
          ))}
        </Stack>
      </Card>

      <Card>
        <SectionLabel icon={<PaidOutlinedIcon sx={{ fontSize: 16 }} />}>PIPELINE CLOSING ≤14 DAYS</SectionLabel>
        <Stack spacing={1}>
          {brief.pipelineClosing.length === 0 && (
            <Typography sx={{ fontSize: 12, color: mint.textDim }}>Nothing closing in the next 14 days.</Typography>
          )}
          {brief.pipelineClosing.map((p) => (
            <Box key={p.opportunityId} sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 1 }}>
              <Box sx={{ minWidth: 0 }}>
                <Typography sx={{ fontSize: 12.5, fontWeight: 700, color: mint.text }} noWrap>
                  {p.customerName}
                </Typography>
                <Typography sx={{ fontSize: 11, color: mint.textDim }} noWrap>
                  {p.productType ?? '—'}
                  {p.expectedCloseDate ? ` · ${p.expectedCloseDate}` : ''}
                </Typography>
              </Box>
              <Typography sx={{ fontSize: 13, fontWeight: 800, color: mint.green }}>{fmtMm(p.amountMm)}</Typography>
            </Box>
          ))}
        </Stack>
      </Card>
    </Box>
  );
}

function MacroPanel({ brief }: { brief: RmBriefing }) {
  if (brief.macroSnapshot.length === 0) return null;
  return (
    <Card>
      <SectionLabel icon={<PublicOutlinedIcon sx={{ fontSize: 16 }} />}>MACRO SNAPSHOT</SectionLabel>
      <Stack spacing={1.25}>
        {brief.macroSnapshot.map((m, i) => (
          <Box key={i}>
            <Typography sx={{ fontSize: 12.5, fontWeight: 700, color: mint.text }}>{m.headline}</Typography>
            <Typography sx={{ fontSize: 11.5, color: mint.textDim, lineHeight: 1.45 }}>{m.detail}</Typography>
          </Box>
        ))}
      </Stack>
    </Card>
  );
}

/** Center column: HIGH PRIORITY hero + Events in Play + RM detail panels, all agent-driven. */
export default function WorkspaceCenter() {
  const { brief, briefLoading, briefError, reloadBrief } = useWorkspaceLive();

  if (!brief) {
    return (
      <Card sx={{ minHeight: 260, display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
        {briefError ? (
          <Stack spacing={1.5} sx={{ alignItems: 'center' }}>
            <Typography sx={{ fontSize: 13, color: mint.red }}>{briefError}</Typography>
            <Box
              onClick={reloadBrief}
              role="button"
              sx={{
                display: 'inline-flex',
                alignItems: 'center',
                gap: 0.75,
                px: 2,
                py: 0.75,
                borderRadius: 2,
                cursor: 'pointer',
                color: mint.text,
                border: `1px solid ${mint.border}`,
                '&:hover': { background: `${mint.violet}14` },
              }}
            >
              <RefreshRoundedIcon sx={{ fontSize: 16 }} /> Retry
            </Box>
          </Stack>
        ) : (
          <Stack spacing={1.5} sx={{ alignItems: 'center', color: mint.textDim }}>
            <CircularProgress size={22} />
            <Typography sx={{ fontSize: 13 }}>{briefLoading ? 'Running your daily briefing…' : 'Loading…'}</Typography>
          </Stack>
        )}
      </Card>
    );
  }

  const top = brief.priorityCallList[0];
  const events = brief.eventsConsidered ?? [];

  return (
    <Stack spacing={2}>
      {top && <PriorityHero call={top} />}
      {events.length > 0 && <EventsInPlay events={events} />}
      <KpiPanel brief={brief} />
      <ComplaintsAndPipeline brief={brief} />
      <MacroPanel brief={brief} />
    </Stack>
  );
}
