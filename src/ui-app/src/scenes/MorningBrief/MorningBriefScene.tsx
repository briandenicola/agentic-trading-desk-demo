import { Box, Container, Typography, Paper } from '@mui/material';

// Phase 1 scaffold placeholder. The full market strip, agent-reasoning steps,
// macro-narrative card, most-affected-clients table, and "Run morning brief"
// action are built in Phase 3 (T021) against src/api/client.ts.
export default function MorningBriefScene() {
  return (
    <Container maxWidth="lg" sx={{ py: 4 }}>
      <Typography variant="overline" color="text.secondary">
        Morning · 7:30 AM — Pre-market planning
      </Typography>
      <Typography variant="h4" gutterBottom>
        “What do I need to know this morning?”
      </Typography>
      <Paper sx={{ p: 3, mt: 2 }}>
        <Typography variant="body1">
          Cockpit scaffold ready. The morning-brief scene wires to{' '}
          <code>POST /api/agent/morning-brief</code> in Phase 3 (T021).
        </Typography>
      </Paper>
      <Box sx={{ mt: 3 }}>
        <Typography variant="caption" color="text.secondary">
          Demo 1 of 5 · fictional data.
        </Typography>
      </Box>
    </Container>
  );
}
