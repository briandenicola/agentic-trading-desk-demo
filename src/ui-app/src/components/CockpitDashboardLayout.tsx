import type { ReactNode } from 'react';
import { Box, Chip, Divider, Paper, Stack, Typography } from '@mui/material';
import PersonOutlineRoundedIcon from '@mui/icons-material/PersonOutlineRounded';
import ShowChartRoundedIcon from '@mui/icons-material/ShowChartRounded';
import PublicRoundedIcon from '@mui/icons-material/PublicRounded';
import AutoAwesomeIcon from '@mui/icons-material/AutoAwesome';
import { mint } from '../theme/theme';
import SectionTitle from './SectionTitle';
import LiveAlertBanner from './LiveAlertBanner';
import type { LiveAlert, MorningBrief } from '../api/client';

interface CockpitDashboardLayoutProps {
  brief: MorningBrief;
  liveAlert?: LiveAlert | null;
}

// ---------------------------------------------------------------------------
// Small M.INT primitives local to the dashboard (built on theme.ts + SectionTitle).
// ---------------------------------------------------------------------------

function ColumnHeader({
  index,
  title,
  subtitle,
  accent,
  icon,
}: {
  index: number;
  title: string;
  subtitle: string;
  accent: string;
  icon: ReactNode;
}) {
  return (
    <Box
      sx={{
        p: 2,
        borderRadius: 2,
        border: `1px solid ${mint.border}`,
        background: `linear-gradient(135deg, ${accent}1f, transparent 70%)`,
        mb: 2,
      }}
    >
      <Box sx={{ display: 'flex', alignItems: 'center', gap: 1.5 }}>
        <Box
          sx={{
            width: 26,
            height: 26,
            borderRadius: '50%',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            background: accent,
            color: '#04101c',
            fontWeight: 800,
            fontSize: 14,
          }}
        >
          {index}
        </Box>
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, color: accent }}>
          {icon}
          <Typography sx={{ fontWeight: 800, letterSpacing: '1px', textTransform: 'uppercase', fontSize: 15 }}>
            {title}
          </Typography>
        </Box>
      </Box>
      <Typography variant="body2" sx={{ color: mint.textDim, mt: 0.5 }}>
        {subtitle}
      </Typography>
    </Box>
  );
}

function Panel({
  title,
  caption,
  color = 'cyan',
  children,
  testId,
}: {
  title: string;
  caption?: ReactNode;
  color?: 'cyan' | 'violet';
  children: ReactNode;
  testId?: string;
}) {
  return (
    <Paper sx={{ p: 2 }} data-testid={testId}>
      <SectionTitle color={color} caption={caption}>
        {title}
      </SectionTitle>
      <Box sx={{ mt: 1.5 }}>{children}</Box>
    </Paper>
  );
}

function FeedRow({
  primary,
  secondary,
  meta,
  badge,
}: {
  primary: string;
  secondary?: string;
  meta?: string;
  badge?: { label: string; color: string };
}) {
  return (
    <Box sx={{ py: 1, borderBottom: `1px solid ${mint.borderSoft}` }}>
      <Box sx={{ display: 'flex', alignItems: 'baseline', gap: 1, justifyContent: 'space-between' }}>
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, minWidth: 0 }}>
          {badge && (
            <Chip
              label={badge.label}
              size="small"
              sx={{ height: 18, fontSize: '10px', fontWeight: 700, color: '#04101c', background: badge.color }}
            />
          )}
          <Typography variant="body2" sx={{ fontWeight: 600, color: '#e8eef5' }} noWrap>
            {primary}
          </Typography>
        </Box>
        {meta && (
          <Typography variant="caption" sx={{ color: mint.textDim, whiteSpace: 'nowrap' }}>
            {meta}
          </Typography>
        )}
      </Box>
      {secondary && (
        <Typography variant="caption" sx={{ color: mint.textDim, display: 'block', mt: 0.25 }}>
          {secondary}
        </Typography>
      )}
    </Box>
  );
}

function InsightList({ items }: { items: string[] }) {
  return (
    <Stack spacing={1}>
      {items.map((text, i) => (
        <Box key={i} sx={{ display: 'flex', gap: 1, alignItems: 'flex-start' }}>
          <AutoAwesomeIcon sx={{ fontSize: 14, color: mint.violetBright, mt: '3px' }} />
          <Typography variant="body2" sx={{ color: '#cdd6e6' }}>
            {text}
          </Typography>
        </Box>
      ))}
    </Stack>
  );
}

const severityColor = (severity?: string): string =>
  severity === 'high' ? mint.red : severity === 'low' ? mint.green : mint.amber;

const concernColor = (kind: string): string =>
  kind === 'sell' ? mint.red : kind === 'warm' ? mint.amber : mint.cyan;

/**
 * M.INT 3-column cockpit dashboard (002 US4 / Phase 7, per assets/Designer Layout.png).
 * A presentational shell — Client View · Ticker View · Overall View (Morning Call) — driven by
 * the same {@link MorningBrief} DTO both modes return. It mounts the {@link LiveAlertBanner} and
 * renders the (already re-ranked) brief, so routing an SSE live-update DTO into it re-ranks the
 * columns in place (FR-010/FR-011). Built from theme.ts + SectionTitle/AiInsightPanel — no new
 * theme, no duplicated primitives.
 */
export default function CockpitDashboardLayout({ brief, liveAlert }: CockpitDashboardLayoutProps) {
  const events = brief.eventsConsidered ?? [];
  const clients = brief.mostAffectedClients ?? [];
  const focusClient = clients[0];
  const topPriorities = (brief.outreach ?? []).slice(0, 4);
  const doneInsights = brief.reasoning.filter((r) => r.status === 'done').map((r) => r.text);

  const tickerEvent = events.find((e) => (e.affectedEntities?.tickers?.length ?? 0) > 0);
  const tickerName = tickerEvent?.affectedEntities?.tickers?.[0] ?? 'Portfolio';

  return (
    <Box>
      {liveAlert && (
        <Box sx={{ mb: 2 }} data-testid="cockpit-live-alert">
          <LiveAlertBanner alert={liveAlert} />
        </Box>
      )}

      <Box
        sx={{
          display: 'grid',
          gap: 2.5,
          gridTemplateColumns: { xs: '1fr', lg: 'repeat(3, 1fr)' },
          alignItems: 'start',
        }}
      >
        {/* -------------------------------------------------- Column 1: Client View */}
        <Stack spacing={2} data-testid="cockpit-client-view">
          <ColumnHeader
            index={1}
            title="Client View"
            subtitle="Everything you need to know about one client."
            accent={mint.violetBright}
            icon={<PersonOutlineRoundedIcon sx={{ fontSize: 18 }} />}
          />

          {focusClient ? (
            <Panel title={`Client: ${focusClient.name}`} caption={focusClient.tier} color="violet" testId="cockpit-focus-client">
              <Stack direction="row" spacing={3} sx={{ mb: 1 }}>
                <Box>
                  <Typography variant="caption" sx={{ color: mint.textDim }}>
                    Exposure
                  </Typography>
                  <Typography sx={{ fontWeight: 700, color: '#e8eef5' }}>{focusClient.exposure}</Typography>
                </Box>
                <Box>
                  <Typography variant="caption" sx={{ color: mint.textDim }}>
                    Concern
                  </Typography>
                  <Chip
                    label={focusClient.concern.label}
                    size="small"
                    sx={{ height: 20, fontWeight: 700, color: '#04101c', background: concernColor(focusClient.concern.kind) }}
                  />
                </Box>
              </Stack>
            </Panel>
          ) : (
            <Panel title="Client View" color="violet">
              <Typography variant="body2" sx={{ color: mint.textDim }}>
                Run the morning call to surface the most-affected client.
              </Typography>
            </Panel>
          )}

          <Panel title="News Matched To Client" caption="AI-derived" testId="cockpit-client-news">
            {(focusClient?.drivingEvents ?? []).length > 0 ? (
              (focusClient!.drivingEvents ?? []).map((d) => (
                <FeedRow key={d.eventId} primary={d.headline} secondary={d.rationale} meta={`+${Math.round(d.contribution)}`} />
              ))
            ) : (
              <Typography variant="body2" sx={{ color: mint.textDim }}>
                No client-specific event drivers right now.
              </Typography>
            )}
          </Panel>

          <Panel title="AI Insights" color="violet">
            <InsightList items={doneInsights.length ? doneInsights : ['Agent reasoning will appear here.']} />
          </Panel>
        </Stack>

        {/* -------------------------------------------------- Column 2: Ticker View */}
        <Stack spacing={2} data-testid="cockpit-ticker-view">
          <ColumnHeader
            index={2}
            title="Ticker View"
            subtitle="All intelligence for one security across the street."
            accent={mint.cyan}
            icon={<ShowChartRoundedIcon sx={{ fontSize: 18 }} />}
          />

          <Panel title={`Security: ${tickerName}`} caption="market strip" testId="cockpit-ticker">
            <Stack spacing={0.5}>
              {brief.marketStrip.map((m) => (
                <Box key={m.label} sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'baseline' }}>
                  <Typography variant="caption" sx={{ color: mint.textDim }}>
                    {m.label}
                  </Typography>
                  <Typography variant="body2" sx={{ fontWeight: 600, color: m.direction === 'down' ? mint.red : m.direction === 'up' ? mint.green : '#e8eef5' }}>
                    {m.value} {m.change ?? ''}
                  </Typography>
                </Box>
              ))}
            </Stack>
          </Panel>

          <Panel title="News On Security" testId="cockpit-ticker-news">
            {events.length > 0 ? (
              events.slice(0, 5).map((e) => (
                <FeedRow
                  key={e.id}
                  primary={e.headline}
                  secondary={e.source}
                  badge={{ label: e.severity, color: severityColor(e.severity) }}
                  meta={e.scope === 'intraday' ? 'intraday' : 'overnight'}
                />
              ))
            ) : (
              <Typography variant="body2" sx={{ color: mint.textDim }}>
                No events on the tape.
              </Typography>
            )}
          </Panel>

          <Panel title="AI Insights" color="violet">
            <InsightList
              items={
                events.length
                  ? events.slice(0, 3).map((e) => `${e.headline} — ${e.direction ?? 'neutral'} signal.`)
                  : ['Security intelligence will appear here.']
              }
            />
          </Panel>
        </Stack>

        {/* -------------------------------------------------- Column 3: Overall View */}
        <Stack spacing={2} data-testid="cockpit-overall-view">
          <ColumnHeader
            index={3}
            title="Overall View (Morning Call)"
            subtitle="What matters today across the market and your clients."
            accent={mint.green}
            icon={<PublicRoundedIcon sx={{ fontSize: 18 }} />}
          />

          <Panel title="Major Market News" caption={`${events.length} considered`} testId="cockpit-market-news">
            {events.length > 0 ? (
              events.slice(0, 4).map((e) => (
                <FeedRow
                  key={e.id}
                  primary={e.headline}
                  secondary={e.summary}
                  badge={{ label: e.severity, color: severityColor(e.severity) }}
                />
              ))
            ) : (
              <Typography variant="body2" sx={{ color: mint.textDim }}>
                No overnight news.
              </Typography>
            )}
          </Panel>

          <Panel title="Trading Commentary" color="violet">
            <Typography variant="body2" sx={{ color: '#cdd6e6', mb: 1 }}>
              {brief.macroNarrative.summary}
            </Typography>
            <Typography variant="caption" sx={{ color: mint.textDim }}>
              {brief.macroNarrative.whyItMatters}
            </Typography>
          </Panel>

          <Panel title="Client Overview" caption={`${clients.length} clients`} testId="cockpit-client-overview">
            <Stack divider={<Divider sx={{ borderColor: mint.borderSoft }} />}>
              {clients.map((c) => (
                <Box key={c.cid} sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', py: 0.75 }}>
                  <Box sx={{ minWidth: 0 }}>
                    <Typography variant="body2" sx={{ fontWeight: 600, color: '#e8eef5' }} noWrap>
                      {c.name}
                    </Typography>
                    <Typography variant="caption" sx={{ color: mint.textDim }}>
                      {c.tier} · {(c.drivingEvents?.length ?? 0)} drivers
                    </Typography>
                  </Box>
                  <Typography variant="body2" sx={{ color: mint.cyan, whiteSpace: 'nowrap' }}>
                    {c.exposure}
                  </Typography>
                </Box>
              ))}
            </Stack>
          </Panel>

          <Panel title="Top Priorities" color="violet" caption="prioritized outreach" testId="cockpit-top-priorities">
            <Stack spacing={1}>
              {topPriorities.length > 0 ? (
                topPriorities.map((o) => (
                  <Box key={o.cid} sx={{ display: 'flex', gap: 1.5, alignItems: 'flex-start' }}>
                    <Box
                      sx={{
                        width: 20,
                        height: 20,
                        borderRadius: '50%',
                        background: mint.violet,
                        color: '#fff',
                        fontSize: 12,
                        fontWeight: 700,
                        display: 'flex',
                        alignItems: 'center',
                        justifyContent: 'center',
                        flexShrink: 0,
                      }}
                    >
                      {o.rank}
                    </Box>
                    <Box sx={{ minWidth: 0 }}>
                      <Typography variant="body2" sx={{ fontWeight: 600, color: '#e8eef5' }}>
                        {o.name}
                      </Typography>
                      <Typography variant="caption" sx={{ color: mint.textDim }}>
                        {o.suggestedTopic}
                      </Typography>
                    </Box>
                  </Box>
                ))
              ) : (
                <Typography variant="body2" sx={{ color: mint.textDim }}>
                  No prioritized outreach yet.
                </Typography>
              )}
            </Stack>
          </Panel>
        </Stack>
      </Box>

      <Typography variant="caption" sx={{ color: mint.textDim, display: 'block', mt: 2, textAlign: 'center' }}>
        Click any item to drill in · Fictional data · DEMO/LIVE mode-blind
      </Typography>
    </Box>
  );
}
