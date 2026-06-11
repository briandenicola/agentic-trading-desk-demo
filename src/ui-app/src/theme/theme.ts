import { createTheme, alpha } from '@mui/material/styles';

/**
 * M.INT — Markets Intelligence design language.
 * Dark "Fixed Income Credit command-center" aesthetic (mirrors assets/mint-v4.html):
 * deep navy surfaces, a blue→cyan brand gradient, a violet AI accent, dense
 * terminal typography with tabular numerals and color-coded financial data.
 */

// Brand palette — tuned to the mint-v4 command-center mockup.
// NOTE: `violet`/`violetBright`/`magenta` are kept as keys for backwards
// compatibility but now resolve to the command-center blue/purple so existing
// components pick up the new look without per-file edits.
export const mint = {
  blue: '#3b82f6',
  blueBright: '#60a5fa',
  cyan: '#22d3ee',
  cyanDeep: '#0ea5b7',
  green: '#10b981',
  red: '#ef4444',
  amber: '#f59e0b',
  gold: '#eab308',
  purple: '#8b5cf6',
  // Back-compat aliases (primary accent → blue, AI accent → purple).
  violet: '#3b82f6',
  violetBright: '#60a5fa',
  magenta: '#8b5cf6',
  bg: '#0a0f1e',
  bgAlt: '#0d1428',
  paper: '#111827',
  paperHi: '#1a2640',
  border: 'rgba(59,130,246,0.18)',
  borderHard: 'rgba(30,50,90,0.6)',
  borderAccent: 'rgba(59,130,246,0.3)',
  borderSoft: 'rgba(255,255,255,0.07)',
  text: '#f0f4ff',
  textDim: '#94a3b8',
  textFaint: '#64748b',
} as const;

const fontStack =
  '"Inter", "Segoe UI Variable", "Segoe UI", system-ui, -apple-system, "Helvetica Neue", Arial, sans-serif';

export const theme = createTheme({
  palette: {
    mode: 'dark',
    primary: { main: mint.blue, light: mint.blueBright, contrastText: '#ffffff' },
    secondary: { main: mint.cyan, dark: mint.cyanDeep, contrastText: '#04222b' },
    success: { main: mint.green },
    warning: { main: mint.amber },
    error: { main: mint.red },
    info: { main: mint.cyan },
    background: { default: mint.bg, paper: mint.paper },
    text: { primary: mint.text, secondary: mint.textDim },
    divider: mint.borderSoft,
  },
  shape: { borderRadius: 8 },
  typography: {
    fontFamily: fontStack,
    h3: { fontWeight: 800, letterSpacing: '-0.6px', fontSize: '1.85rem' },
    h4: { fontWeight: 800, letterSpacing: '-0.4px', fontSize: '1.45rem' },
    h5: { fontWeight: 700, letterSpacing: '-0.3px', fontSize: '1.2rem' },
    h6: { fontWeight: 700, letterSpacing: '0.2px', fontSize: '1rem' },
    subtitle1: { fontWeight: 600 },
    body1: { fontSize: '0.875rem' },
    body2: { fontSize: '0.8125rem' },
    caption: { fontSize: '0.6875rem' },
    overline: { letterSpacing: '1.4px', fontWeight: 700, fontSize: '10px' },
    button: { textTransform: 'none', fontWeight: 600 },
  },
  components: {
    MuiCssBaseline: {
      styleOverrides: {
        body: {
          backgroundColor: mint.bg,
          backgroundImage: [
            `radial-gradient(1200px 600px at 12% -10%, ${alpha(mint.blue, 0.14)}, transparent 60%)`,
            `radial-gradient(1000px 500px at 100% 0%, ${alpha(mint.cyan, 0.09)}, transparent 55%)`,
            `linear-gradient(180deg, ${mint.bg} 0%, ${mint.bgAlt} 100%)`,
          ].join(','),
          backgroundAttachment: 'fixed',
          fontVariantNumeric: 'tabular-nums',
          WebkitFontSmoothing: 'antialiased',
        },
        '*::-webkit-scrollbar': { width: 9, height: 9 },
        '*::-webkit-scrollbar-thumb': {
          background: alpha(mint.blue, 0.4),
          borderRadius: 8,
        },
        '*::-webkit-scrollbar-track': { background: 'transparent' },
      },
    },
    MuiPaper: {
      defaultProps: { elevation: 0 },
      styleOverrides: {
        root: {
          backgroundColor: mint.paper,
          backgroundImage: `linear-gradient(180deg, ${alpha('#ffffff', 0.02)}, transparent 60%)`,
          border: `1px solid ${mint.border}`,
          borderRadius: 10,
          backdropFilter: 'blur(2px)',
        },
      },
    },
    MuiAppBar: {
      defaultProps: { elevation: 0 },
      styleOverrides: {
        root: { backgroundColor: 'transparent', backgroundImage: 'none' },
      },
    },
    MuiButton: {
      defaultProps: { disableElevation: true },
      styleOverrides: {
        root: { borderRadius: 10 },
        outlined: { borderColor: mint.border },
      },
      variants: [
        {
          props: { variant: 'contained', color: 'primary' },
          style: {
            background: `linear-gradient(135deg, ${mint.violet} 0%, #5b8cff 100%)`,
            boxShadow: `0 6px 20px ${alpha(mint.violet, 0.35)}`,
            '&:hover': {
              background: `linear-gradient(135deg, ${mint.violetBright} 0%, #6f9bff 100%)`,
            },
          },
        },
      ],
    },
    MuiChip: {
      styleOverrides: {
        root: { borderRadius: 8, fontWeight: 600 },
        outlined: { borderColor: mint.border },
      },
    },
    MuiTableCell: {
      styleOverrides: {
        root: { borderBottomColor: mint.borderSoft, padding: '8px 12px', fontVariantNumeric: 'tabular-nums' },
        head: {
          color: mint.textDim,
          textTransform: 'uppercase',
          letterSpacing: '0.7px',
          fontSize: '10px',
          fontWeight: 700,
          padding: '6px 12px',
          borderBottom: `1px solid ${mint.borderAccent}`,
        },
      },
    },
    MuiTableRow: {
      styleOverrides: {
        root: { '&:hover': { backgroundColor: alpha(mint.violet, 0.05) } },
      },
    },
    MuiAlert: {
      styleOverrides: {
        root: { borderRadius: 12, border: `1px solid ${mint.borderSoft}` },
      },
    },
    MuiTab: {
      styleOverrides: {
        root: { textTransform: 'none', fontWeight: 600, minHeight: 44, letterSpacing: '0.2px' },
      },
    },
    MuiLinearProgress: {
      styleOverrides: { root: { borderRadius: 99 } },
    },
  },
});
