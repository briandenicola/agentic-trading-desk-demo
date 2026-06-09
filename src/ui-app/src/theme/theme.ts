import { createTheme, alpha } from '@mui/material/styles';

/**
 * M.INT — Markets Intelligence design language.
 * Dark navy "intelligence terminal" aesthetic: violet + cyan accents, glowing
 * gradient surfaces, color-coded financial data. Mirrors assets/Designer Layout.png.
 */

// Brand palette
export const mint = {
  violet: '#7c5cff',
  violetBright: '#a855f7',
  magenta: '#c026d3',
  cyan: '#22d3ee',
  cyanDeep: '#0ea5b7',
  green: '#34d399',
  red: '#fb5a6a',
  amber: '#fbbf24',
  bg: '#070b16',
  bgAlt: '#0a0f1f',
  paper: '#0e1424',
  paperHi: '#131a2e',
  border: 'rgba(124,92,255,0.16)',
  borderSoft: 'rgba(255,255,255,0.07)',
  text: '#e8edf7',
  textDim: '#8593ad',
} as const;

const fontStack =
  '"Inter", "Segoe UI Variable", "Segoe UI", system-ui, -apple-system, "Helvetica Neue", Arial, sans-serif';

export const theme = createTheme({
  palette: {
    mode: 'dark',
    primary: { main: mint.violet, light: mint.violetBright, contrastText: '#ffffff' },
    secondary: { main: mint.cyan, dark: mint.cyanDeep, contrastText: '#04222b' },
    success: { main: mint.green },
    warning: { main: mint.amber },
    error: { main: mint.red },
    info: { main: mint.cyan },
    background: { default: mint.bg, paper: mint.paper },
    text: { primary: mint.text, secondary: mint.textDim },
    divider: mint.borderSoft,
  },
  shape: { borderRadius: 12 },
  typography: {
    fontFamily: fontStack,
    h3: { fontWeight: 700, letterSpacing: '-0.5px' },
    h4: { fontWeight: 700, letterSpacing: '-0.4px' },
    h6: { fontWeight: 700, letterSpacing: '0.2px' },
    subtitle1: { fontWeight: 600 },
    overline: { letterSpacing: '1.6px', fontWeight: 700, fontSize: '11px' },
    button: { textTransform: 'none', fontWeight: 600 },
  },
  components: {
    MuiCssBaseline: {
      styleOverrides: {
        body: {
          backgroundColor: mint.bg,
          backgroundImage: [
            `radial-gradient(1200px 600px at 12% -10%, ${alpha(mint.violet, 0.16)}, transparent 60%)`,
            `radial-gradient(1000px 500px at 100% 0%, ${alpha(mint.cyan, 0.1)}, transparent 55%)`,
            `linear-gradient(180deg, ${mint.bg} 0%, ${mint.bgAlt} 100%)`,
          ].join(','),
          backgroundAttachment: 'fixed',
          WebkitFontSmoothing: 'antialiased',
        },
        '*::-webkit-scrollbar': { width: 10, height: 10 },
        '*::-webkit-scrollbar-thumb': {
          background: alpha(mint.violet, 0.4),
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
          backgroundImage: `linear-gradient(180deg, ${alpha('#ffffff', 0.025)}, transparent 60%)`,
          border: `1px solid ${mint.border}`,
          borderRadius: 14,
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
        root: { borderBottomColor: mint.borderSoft },
        head: {
          color: mint.textDim,
          textTransform: 'uppercase',
          letterSpacing: '0.7px',
          fontSize: '11px',
          fontWeight: 700,
          borderBottom: `1px solid ${mint.border}`,
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
