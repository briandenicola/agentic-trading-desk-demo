import { Box, LinearProgress, Stack, Typography } from '@mui/material';
import AutoAwesomeRoundedIcon from '@mui/icons-material/AutoAwesomeRounded';
import { mint } from '../../theme/theme';
import { workspace } from './workspaceData';
import { ExposureDonut, Sparkline, WorkspacePanel, relevanceColor, riskColor } from './WorkspaceViz';

const ViewAll = ({ label = 'View Full Breakdown' }: { label?: string }) => (
  <Typography sx={{ fontSize: 11.5, fontWeight: 600, color: mint.cyan, cursor: 'pointer', textAlign: 'center', mt: 1 }}>
    {label}
  </Typography>
);

const ytdColor = (v: number) => (v >= 0 ? mint.green : mint.red);

export function ClientOverviewPanel() {
  const c = workspace.client;
  const rows: [string, string][] = [
    ['Relationship Since', c.relationshipSince],
    ['AUM', c.aum],
    ['Risk Profile', c.riskProfile],
    ['Objective', c.objective],
  ];
  return (
    <WorkspacePanel title="Client Overview" testId="ws-client-overview">
      <Box sx={{ display: 'flex', alignItems: 'center', gap: 1.5, mb: 1.5 }}>
        <Box
          sx={{
            width: 42,
            height: 42,
            borderRadius: 2,
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            fontWeight: 800,
            fontSize: 12,
            color: '#04101c',
            background: `linear-gradient(135deg, ${mint.cyan}, ${mint.violet})`,
          }}
        >
          {c.name.slice(0, 4).toUpperCase()}
        </Box>
        <Box>
          <Typography sx={{ fontSize: 15, fontWeight: 700, color: mint.text }}>{c.name}</Typography>
          <Typography sx={{ fontSize: 12, color: mint.textDim }}>{c.contact}</Typography>
        </Box>
      </Box>
      <Stack spacing={0.9}>
        {rows.map(([k, v]) => (
          <Box key={k} sx={{ display: 'flex', justifyContent: 'space-between' }}>
            <Typography sx={{ fontSize: 12, color: mint.textDim }}>{k}</Typography>
            <Typography sx={{ fontSize: 12, fontWeight: 600, color: mint.text }}>{v}</Typography>
          </Box>
        ))}
      </Stack>
      <ViewAll label="View Client Profile" />
    </WorkspacePanel>
  );
}

export function PortfolioExposurePanel() {
  const e = workspace.exposure;
  return (
    <WorkspacePanel title="Portfolio Exposure" testId="ws-exposure">
      <Box sx={{ display: 'flex', alignItems: 'center', gap: 2 }}>
        <ExposureDonut segments={e.segments} centerValue={e.total} centerLabel={e.totalLabel} size={140} />
        <Stack spacing={0.65} sx={{ flex: 1 }}>
          {e.segments.map((s) => (
            <Box key={s.label} sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
              <Box sx={{ width: 9, height: 9, borderRadius: '2px', bgcolor: s.color }} />
              <Typography sx={{ fontSize: 12, color: mint.textDim, flex: 1 }}>{s.label}</Typography>
              <Typography sx={{ fontSize: 12, fontWeight: 700, color: mint.text }}>{s.pct}%</Typography>
            </Box>
          ))}
        </Stack>
      </Box>
      <ViewAll />
    </WorkspacePanel>
  );
}

export function PerformancePanel() {
  const p = workspace.performance;
  return (
    <WorkspacePanel title="Performance (Net)" testId="ws-performance">
      <Stack direction="row" spacing={2} sx={{ mb: 1 }}>
        {p.rows.map((r) => (
          <Box key={r.label}>
            <Typography sx={{ fontSize: 10, color: mint.textDim }}>{r.label}</Typography>
            <Typography sx={{ fontSize: 16, fontWeight: 800, color: mint.green }}>{r.value}</Typography>
          </Box>
        ))}
      </Stack>
      <Sparkline series={p.series} />
      <Box sx={{ display: 'flex', justifyContent: 'space-between', mt: 0.5 }}>
        {p.months.map((m) => (
          <Typography key={m} sx={{ fontSize: 9, color: mint.textDim }}>
            {m}
          </Typography>
        ))}
      </Box>
      <ViewAll label="View Performance" />
    </WorkspacePanel>
  );
}

export function TopHoldingsPanel() {
  return (
    <WorkspacePanel title="Top Holdings" testId="ws-holdings">
      <Box sx={{ display: 'flex', fontSize: 10, color: mint.textDim, mb: 0.75 }}>
        <Box sx={{ flex: 1 }}>Holding</Box>
        <Box sx={{ width: 52, textAlign: 'right' }}>Weight</Box>
        <Box sx={{ width: 62, textAlign: 'right' }}>YTD (Price)</Box>
      </Box>
      <Stack spacing={0.9}>
        {workspace.holdings.map((h) => (
          <Box key={h.ticker} sx={{ display: 'flex', alignItems: 'center' }}>
            <Box sx={{ flex: 1, minWidth: 0 }}>
              <Typography sx={{ fontSize: 12.5, fontWeight: 700, color: mint.text }} noWrap>
                {h.name} ({h.ticker})
              </Typography>
            </Box>
            <Typography sx={{ width: 52, textAlign: 'right', fontSize: 12, color: mint.text }}>{h.weight}%</Typography>
            <Typography sx={{ width: 62, textAlign: 'right', fontSize: 12, fontWeight: 700, color: ytdColor(h.ytd) }}>
              {h.ytd >= 0 ? '+' : ''}
              {h.ytd}%
            </Typography>
          </Box>
        ))}
      </Stack>
      <ViewAll label="View All Holdings" />
    </WorkspacePanel>
  );
}

export function RiskAnalyticsPanel() {
  const r = workspace.risk;
  return (
    <WorkspacePanel title="Risk Analytics" testId="ws-risk">
      <Box sx={{ display: 'flex', alignItems: 'center', gap: 2, mb: 1.25 }}>
        <Box>
          <Typography sx={{ fontSize: 30, fontWeight: 800, color: mint.amber, lineHeight: 1 }}>{r.score}</Typography>
          <Typography sx={{ fontSize: 11, color: mint.textDim }}>{r.band}</Typography>
        </Box>
        <Box sx={{ flex: 1 }}>
          <Typography sx={{ fontSize: 10, color: mint.textDim, mb: 0.5 }}>Top Risk Factors</Typography>
          <Stack spacing={0.65}>
            {r.factors.map((f) => (
              <Box key={f.label} sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
                <Box sx={{ width: 7, height: 7, borderRadius: '50%', bgcolor: riskColor(f.level) }} />
                <Typography sx={{ fontSize: 12, color: mint.textDim, flex: 1 }} noWrap>
                  {f.label}
                </Typography>
                <Typography sx={{ fontSize: 11, fontWeight: 700, color: riskColor(f.level) }}>({f.level})</Typography>
              </Box>
            ))}
          </Stack>
        </Box>
      </Box>
      <LinearProgress
        variant="determinate"
        value={r.score}
        sx={{
          height: 6,
          borderRadius: 3,
          bgcolor: mint.borderSoft,
          '& .MuiLinearProgress-bar': { background: `linear-gradient(90deg, ${mint.green}, ${mint.amber}, ${mint.red})` },
        }}
      />
      <ViewAll label="View Full Analysis" />
    </WorkspacePanel>
  );
}

export function WorkspaceInsightsPanel() {
  return (
    <WorkspacePanel title="AI Insights" testId="ws-insights">
      <Stack spacing={1.25}>
        {workspace.insights.map((text, i) => (
          <Box key={i} sx={{ display: 'flex', gap: 1, alignItems: 'flex-start' }}>
            <AutoAwesomeRoundedIcon sx={{ fontSize: 14, color: mint.violetBright, mt: '2px' }} />
            <Typography sx={{ fontSize: 12.5, color: '#cdd6e6', lineHeight: 1.45 }}>{text}</Typography>
          </Box>
        ))}
      </Stack>
      <ViewAll label="View All Insights" />
    </WorkspacePanel>
  );
}

export function AxesOfferingsPanel() {
  return (
    <WorkspacePanel title="Axes / Offerings" testId="ws-axes">
      <Box sx={{ display: 'flex', fontSize: 10, color: mint.textDim, mb: 0.75 }}>
        <Box sx={{ flex: 1 }}>Opportunity</Box>
        <Box sx={{ width: 70, textAlign: 'right' }}>Relevance</Box>
      </Box>
      <Stack spacing={0.95}>
        {workspace.axes.map((a) => (
          <Box key={a.opportunity} sx={{ display: 'flex', alignItems: 'center' }}>
            <Typography sx={{ flex: 1, fontSize: 12.5, color: mint.text }} noWrap>
              {a.opportunity}
            </Typography>
            <Typography sx={{ width: 70, textAlign: 'right', fontSize: 12, fontWeight: 700, color: relevanceColor(a.relevance) }}>
              {a.relevance}
            </Typography>
          </Box>
        ))}
      </Stack>
      <ViewAll label="View All Offerings" />
    </WorkspacePanel>
  );
}
