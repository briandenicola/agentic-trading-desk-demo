import { Box, Stack, Typography } from '@mui/material';
import KeyboardArrowDownRoundedIcon from '@mui/icons-material/KeyboardArrowDownRounded';
import VerifiedUserRoundedIcon from '@mui/icons-material/VerifiedUserRounded';
import LockRoundedIcon from '@mui/icons-material/LockRounded';
import GavelRoundedIcon from '@mui/icons-material/GavelRounded';
import MintBrand from '../../components/MintBrand';
import LiveAlertBanner from '../../components/LiveAlertBanner';
import { mint } from '../../theme/theme';
import { workspace } from './workspaceData';
import LaunchPadSidebar from './LaunchPadSidebar';
import NewsfeedColumn from './NewsfeedColumn';
import CommandCenter from './CommandCenter';
import RightRail from './RightRail';
import PlaybooksBar from './PlaybooksBar';
import LiveStatusPill from './LiveStatusPill';
import ToastHost from './ToastHost';
import { WorkspaceLiveProvider, useWorkspaceLive } from './useWorkspaceLive';

function UserProfile() {
  const initials = workspace.user.name
    .split(' ')
    .map((p) => p[0])
    .join('');
  return (
    <Box sx={{ display: 'flex', alignItems: 'center', gap: 1.25, cursor: 'pointer' }}>
      <Box sx={{ textAlign: 'right', display: { xs: 'none', sm: 'block' } }}>
        <Typography sx={{ fontSize: 13, fontWeight: 700, color: mint.text, lineHeight: 1.2 }}>
          {workspace.user.name}
        </Typography>
        <Typography sx={{ fontSize: 11, color: mint.textDim, lineHeight: 1.2 }}>{workspace.user.role}</Typography>
      </Box>
      <Box
        sx={{
          width: 38,
          height: 38,
          borderRadius: '50%',
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
          fontSize: 13,
          fontWeight: 800,
          color: '#fff',
          background: `linear-gradient(135deg, ${mint.violet}, ${mint.magenta})`,
        }}
      >
        {initials}
      </Box>
      <KeyboardArrowDownRoundedIcon sx={{ fontSize: 18, color: mint.textDim }} />
    </Box>
  );
}

function FooterStrip() {
  return (
    <Box
      sx={{
        mt: 1,
        px: 2.5,
        py: 1.75,
        borderRadius: 3,
        border: `1px solid ${mint.border}`,
        background: mint.paper,
        display: 'flex',
        flexWrap: 'wrap',
        alignItems: 'center',
        justifyContent: 'space-between',
        gap: 2,
      }}
    >
      <Box sx={{ display: 'flex', alignItems: 'center', flexWrap: 'wrap', gap: 1 }}>
        {workspace.processSteps.map((step, i) => (
          <Box key={step} sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
            <Typography sx={{ fontSize: 11, fontWeight: 700, letterSpacing: '1px', color: mint.violetBright }}>
              {step.toUpperCase()}
            </Typography>
            {i < workspace.processSteps.length - 1 && (
              <Box sx={{ width: 16, height: 1, background: mint.border }} />
            )}
          </Box>
        ))}
      </Box>

      <Typography
        sx={{
          fontSize: 11,
          fontWeight: 700,
          letterSpacing: '2px',
          color: mint.textDim,
          textAlign: 'center',
          flex: { xs: '1 1 100%', md: '0 1 auto' },
          order: { xs: 3, md: 2 },
        }}
      >
        M.INT — MARKETS INTELLIGENCE THAT WORKS FOR YOU.
      </Typography>

      <Stack direction="row" spacing={1.5} sx={{ alignItems: 'center', order: { xs: 2, md: 3 } }}>
        {[
          { label: 'Secure', icon: <VerifiedUserRoundedIcon sx={{ fontSize: 13 }} /> },
          { label: 'Private', icon: <LockRoundedIcon sx={{ fontSize: 13 }} /> },
          { label: 'Compliant', icon: <GavelRoundedIcon sx={{ fontSize: 13 }} /> },
        ].map((c) => (
          <Box key={c.label} sx={{ display: 'flex', alignItems: 'center', gap: 0.5, color: mint.green }}>
            {c.icon}
            <Typography sx={{ fontSize: 11, fontWeight: 600, color: mint.textDim }}>{c.label}</Typography>
          </Box>
        ))}
      </Stack>
    </Box>
  );
}

/**
 * M.INT workspace shell — the primary product screen (assets/Designer Layout.png).
 * Composes the Launch Pad sidebar, the M.INT newsfeed, the AI command center with
 * its configurable panel grid, the matched-news rail, the playbooks bar and the
 * process footer. Front-end first: the command bar is wired to the real /api/chat,
 * and a live SSE subscription drives intraday highlighting across the shell.
 */
export default function WorkspaceScene() {
  return (
    <WorkspaceLiveProvider>
      <WorkspaceShell />
      <ToastHost />
    </WorkspaceLiveProvider>
  );
}

function WorkspaceShell() {
  const { alert, dismissAlert } = useWorkspaceLive();
  return (
    <Box sx={{ minHeight: '100vh', background: mint.bg, color: mint.text, p: { xs: 1.5, md: 2.5 } }}>
      {/* Header */}
      <Box
        sx={{
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'space-between',
          gap: 2,
          mb: 2,
          px: 2.5,
          py: 1.5,
          borderRadius: 3,
          border: `1px solid ${mint.border}`,
          background: mint.paper,
        }}
      >
        <Stack direction="row" spacing={2.5} sx={{ alignItems: 'center', minWidth: 0 }}>
          <MintBrand size="md" />
          <Box sx={{ display: { xs: 'none', lg: 'block' }, borderLeft: `1px solid ${mint.border}`, pl: 2.5 }}>
            <Typography sx={{ fontSize: 12, fontWeight: 800, letterSpacing: '0.5px', color: mint.text, lineHeight: 1.3 }}>
              {workspace.tagline.line1}
            </Typography>
            <Typography sx={{ fontSize: 12, fontWeight: 800, letterSpacing: '0.5px', color: mint.violetBright, lineHeight: 1.3 }}>
              {workspace.tagline.line2}
            </Typography>
          </Box>
        </Stack>
        <Stack direction="row" spacing={2} sx={{ alignItems: 'center' }}>
          <LiveStatusPill />
          <UserProfile />
        </Stack>
      </Box>

      {/* Live intraday alert banner (spans above the grid) */}
      <LiveAlertBanner alert={alert} onDismiss={dismissAlert} />

      {/* Main 4-column grid */}
      <Box
        sx={{
          display: 'grid',
          gap: 2,
          alignItems: 'start',
          gridTemplateColumns: {
            xs: '1fr',
            lg: '220px 280px 1fr',
            xl: '230px 300px 1fr 320px',
          },
        }}
      >
        <Box sx={{ display: { xs: 'none', lg: 'block' } }}>
          <LaunchPadSidebar activeNav="Home" />
        </Box>
        <Box sx={{ display: { xs: 'none', lg: 'block' } }}>
          <NewsfeedColumn />
        </Box>
        <CommandCenter />
        <Box sx={{ display: { xs: 'none', xl: 'block' } }}>
          <RightRail />
        </Box>
      </Box>

      {/* Bottom bar */}
      <Box sx={{ mt: 2 }}>
        <PlaybooksBar />
      </Box>

      <FooterStrip />
    </Box>
  );
}
