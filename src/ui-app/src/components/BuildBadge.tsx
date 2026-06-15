import { Box } from '@mui/material';

/**
 * Tiny fixed-position build-provenance badge. Shows the git SHA baked into the static
 * bundle at image-build time (VITE_GIT_SHA), so anyone looking at the running UI can read
 * exactly which committed revision is deployed. Mirrors the curl-able /version.json and the
 * APIs' /version endpoints. Renders "dev" for un-stamped local builds.
 */
export default function BuildBadge() {
  const sha = import.meta.env.VITE_GIT_SHA ?? 'dev';
  const buildTime = import.meta.env.VITE_BUILD_TIME ?? '';
  return (
    <Box
      component="a"
      href="/version"
      target="_blank"
      rel="noreferrer"
      data-testid="build-badge"
      title={buildTime ? `Built ${buildTime} UTC` : 'Local dev build'}
      sx={{
        position: 'fixed',
        bottom: 6,
        right: 8,
        zIndex: (t) => t.zIndex.tooltip + 1,
        fontFamily: 'monospace',
        fontSize: 10,
        letterSpacing: '0.04em',
        color: 'rgba(180,200,220,0.45)',
        textDecoration: 'none',
        userSelect: 'all',
        pointerEvents: 'auto',
        '&:hover': { color: 'rgba(180,200,220,0.85)' },
      }}
    >
      build {sha}
    </Box>
  );
}
