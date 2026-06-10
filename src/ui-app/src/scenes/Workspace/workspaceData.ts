// ---------------------------------------------------------------------------
// M.INT workspace — static chrome only. All business data on the main page now
// comes from the real RM Daily Briefing agent (see useWorkspaceLive); this file
// keeps just the brand copy and the shared live-priority type.
// ---------------------------------------------------------------------------

export type AlertPriority = 'HIGH' | 'MEDIUM' | 'LOW' | 'DONE';

export const workspace = {
  tagline: {
    line1: 'AI-DRIVEN MARKETS INTELLIGENCE.',
    line2: 'THE RIGHT CONTEXT. IN REAL TIME.',
  },
  processSteps: ['Scan', 'Detect', 'Understand', 'Match', 'Configure', 'Act'],
} as const;

export type Workspace = typeof workspace;
