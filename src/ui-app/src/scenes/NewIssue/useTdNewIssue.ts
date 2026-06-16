import { useCallback, useEffect, useRef, useState } from 'react';
import {
  runTdNewIssue,
  subscribeToEvents,
  type LiveAlert,
  type TdNewIssueStoryboard,
} from '../../api/client';
import { loadPersistentOnce, usePersistentState } from '../../hooks/usePersistentState';

// Demo defaults for the New Issue Radar storyboard: Prairie Green Renewables
// equity (SEC-3601), focus client Crestline Capital (CL-2015), snapshot date.
export const NI_ISSUER_SECURITY = 'SEC-3601';
export const NI_CLIENT = 'CL-2015';
export const NI_DATE = '2026-05-22';

/**
 * Loads the New Issue Radar storyboard once per SPA session and persists it
 * under `key`, so navigating away and back does not re-run the agent. Exposes a
 * `reload` for an explicit refresh. DEMO and LIVE return the same storyboard
 * shape (constitution Principle III).
 */
export function useTdNewIssue(key: string) {
  const [story, setStory] = usePersistentState<TdNewIssueStoryboard | null>(key, null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [liveAlert, setLiveAlert] = useState<LiveAlert | null>(null);
  // The new-issue security currently driving the radar. Defaults to the demo issuer
  // (Prairie Green), but the lead-left board can re-point it at an uploaded deal.
  const [driver, setDriver] = useState<string>(NI_ISSUER_SECURITY);
  const didAutoRun = useRef(false);

  const load = useCallback(
    async (force = false, issuerSecurityId: string = NI_ISSUER_SECURITY, clientId: string = NI_CLIENT) => {
      setLoading(true);
      setError(null);
      try {
        const req = {
          payload: { issuerSecurityId, clientId, date: NI_DATE },
        };
        const result = force
          ? await runTdNewIssue(req)
          : await loadPersistentOnce(key, () => runTdNewIssue(req));
        setStory(result);
      } catch (err) {
        setError(err instanceof Error ? err.message : 'Failed to load the new-issue storyboard');
      } finally {
        setLoading(false);
      }
    },
    [key, setStory],
  );

  // Re-run the radar. Pass an override to drive it from a specific deal/security; omit to
  // re-run the deal currently in focus.
  const reload = useCallback(
    (override?: { issuerSecurityId?: string; clientId?: string }) => {
      const next = override?.issuerSecurityId?.trim() || driver || NI_ISSUER_SECURITY;
      setDriver(next);
      void load(true, next, override?.clientId?.trim() || NI_CLIENT);
    },
    [load, driver],
  );

  useEffect(() => {
    if (didAutoRun.current || story !== null) return;
    didAutoRun.current = true;
    void load(false, NI_ISSUER_SECURITY, NI_CLIENT);
  }, [story, load]);

  // Reactive live push: once the storyboard is on screen, subscribe to the New Issue
  // event stream. A News Desk inject that touches the issuer, either tranche, the
  // sector, or the focus client re-synthesizes the storyboard and surfaces a liveAlert.
  const hasStory = story !== null;
  useEffect(() => {
    if (!hasStory) return;
    const unsubscribe = subscribeToEvents<TdNewIssueStoryboard>(
      'td-new-issue',
      {
        onUpdate: (update) => {
          setStory(update.briefing);
          setLiveAlert(update.alert);
        },
      },
      { persona: driver },
    );
    return unsubscribe;
  }, [hasStory, setStory, driver]);

  const dismissAlert = useCallback(() => setLiveAlert(null), []);

  return { story, loading, error, reload, liveAlert, dismissAlert, activeIssuerSecurity: driver };
}
