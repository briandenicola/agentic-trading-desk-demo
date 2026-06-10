import type { ReactNode } from 'react';
import { Box, Paper, Typography } from '@mui/material';
import { mint } from '../../theme/theme';
import type { ExposureSegment } from './workspaceData';

/**
 * M.INT card with an optional uppercase title, trailing action affordance and a
 * thin gradient top edge — the base surface every workspace panel is built on.
 */
export function WorkspacePanel({
  title,
  action,
  children,
  noPad,
  testId,
}: {
  title?: ReactNode;
  action?: ReactNode;
  children: ReactNode;
  noPad?: boolean;
  testId?: string;
}) {
  return (
    <Paper
      data-testid={testId}
      sx={{
        height: '100%',
        display: 'flex',
        flexDirection: 'column',
        border: `1px solid ${mint.border}`,
        background: mint.paper,
        overflow: 'hidden',
      }}
    >
      {title && (
        <Box
          sx={{
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'space-between',
            px: 2,
            py: 1.25,
            borderBottom: `1px solid ${mint.borderSoft}`,
          }}
        >
          <Typography
            sx={{
              fontSize: 11,
              fontWeight: 700,
              letterSpacing: '1.1px',
              textTransform: 'uppercase',
              color: mint.textDim,
            }}
          >
            {title}
          </Typography>
          {action}
        </Box>
      )}
      <Box sx={{ flex: 1, p: noPad ? 0 : 2 }}>{children}</Box>
    </Paper>
  );
}

/**
 * Donut built from a CSS conic-gradient ring (no chart dependency). Segment
 * percentages are laid head-to-tail; the hollow centre carries the total label.
 */
export function ExposureDonut({
  segments,
  centerValue,
  centerLabel,
  size = 150,
}: {
  segments: ExposureSegment[];
  centerValue: string;
  centerLabel: string;
  size?: number;
}) {
  let acc = 0;
  const stops = segments
    .map((s) => {
      const start = acc;
      acc += s.pct;
      return `${s.color} ${start}% ${acc}%`;
    })
    .join(', ');
  const ring = size * 0.27;

  return (
    <Box
      role="img"
      aria-label={`${centerLabel} ${centerValue}`}
      sx={{
        width: size,
        height: size,
        borderRadius: '50%',
        flexShrink: 0,
        background: `conic-gradient(${stops})`,
        position: 'relative',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
      }}
    >
      <Box
        sx={{
          position: 'absolute',
          inset: ring,
          borderRadius: '50%',
          background: mint.paper,
          display: 'flex',
          flexDirection: 'column',
          alignItems: 'center',
          justifyContent: 'center',
        }}
      >
        <Typography sx={{ fontSize: size * 0.13, fontWeight: 800, color: mint.text, lineHeight: 1 }}>
          {centerValue}
        </Typography>
        <Typography sx={{ fontSize: 9, color: mint.textDim, mt: 0.25 }}>{centerLabel}</Typography>
      </Box>
    </Box>
  );
}

/**
 * Area sparkline rendered as inline SVG with a violet→transparent fill. Points
 * are normalised to the series' own min/max so the curve always fills the frame.
 */
export function Sparkline({
  series,
  width = 230,
  height = 92,
  stroke = mint.green,
}: {
  series: readonly number[];
  width?: number;
  height?: number;
  stroke?: string;
}) {
  const min = Math.min(...series);
  const max = Math.max(...series);
  const span = max - min || 1;
  const pad = 6;
  const stepX = (width - pad * 2) / (series.length - 1);
  const points = series.map((v, i) => {
    const x = pad + i * stepX;
    const y = pad + (1 - (v - min) / span) * (height - pad * 2);
    return [x, y] as const;
  });
  const line = points.map(([x, y]) => `${x},${y}`).join(' ');
  const area = `${pad},${height - pad} ${line} ${width - pad},${height - pad}`;
  const gradId = 'spark-fill';

  return (
    <svg width="100%" height={height} viewBox={`0 0 ${width} ${height}`} preserveAspectRatio="none" aria-hidden>
      <defs>
        <linearGradient id={gradId} x1="0" y1="0" x2="0" y2="1">
          <stop offset="0%" stopColor={stroke} stopOpacity="0.35" />
          <stop offset="100%" stopColor={stroke} stopOpacity="0" />
        </linearGradient>
      </defs>
      <polygon points={area} fill={`url(#${gradId})`} />
      <polyline points={line} fill="none" stroke={stroke} strokeWidth={2} strokeLinejoin="round" strokeLinecap="round" />
      {points.map(([x, y], i) => (
        <circle key={i} cx={x} cy={y} r={i === points.length - 1 ? 3 : 0} fill={stroke} />
      ))}
    </svg>
  );
}

export const relevanceColor = (level: 'High' | 'Medium' | 'Low'): string =>
  level === 'High' ? mint.green : level === 'Medium' ? mint.amber : mint.textDim;

export const riskColor = (level: 'High' | 'Moderate' | 'Low'): string =>
  level === 'High' ? mint.red : level === 'Moderate' ? mint.amber : mint.green;
