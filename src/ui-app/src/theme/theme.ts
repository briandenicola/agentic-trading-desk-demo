import { createTheme } from '@mui/material/styles';

// Dark cockpit theme approximating mockup/assets/theme.css.
export const theme = createTheme({
  palette: {
    mode: 'dark',
    primary: { main: '#4f8cff' },
    success: { main: '#3ddc97' },
    warning: { main: '#ffb547' },
    error: { main: '#ff5a5a' },
    background: { default: '#0e1116', paper: '#161b22' },
  },
  typography: {
    fontFamily: '"Segoe UI", system-ui, -apple-system, sans-serif',
  },
});
