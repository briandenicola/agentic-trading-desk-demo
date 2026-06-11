import { useCallback, useEffect, useRef, useState } from 'react';
import { runTdBriefing, subscribeToEvents, type LiveAlert, type TdBriefing } from '../../api/client';
import { loadPersistentOnce, usePersistentState } from '../../hooks/usePersistentState';

// Demo persona + snapshot date for the Institutional Sales & Trading flow.
export const TD_SALESPERSON = 'Theo Wexler';
export const TD_DATE = '2026-05-22';

/**
 * Loads the Trading Desk morning briefing once per SPA session and persists it under
 * `key`, so navigating away and back does not re-run the agent. Both TD scenes share
 * the same persisted brief by passing the same key. Exposes a `reload` for an explicit
 * refresh and, once a brief is on screen, holds a reactive SSE subscription so a News
 * Desk inject re-ranks the call list live (the "highly visible updates from /admin"
 * the desk asked for). Each push replaces the brief in place and surfaces a `liveAlert`.
 */
export function useTdBriefing(key: string) {
  const [brief, setBrief] = usePersistentState<TdBriefing | null>(key, null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [liveAlert, setLiveAlert] = useState<LiveAlert | null>(null);
  const didAutoRun = useRef(false);

  const load = useCallback(
    async (force = false) => {
      setLoading(true);
      setError(null);
      try {
        const result = force
          ? await runTdBriefing({ payload: { salespersonId: TD_SALESPERSON, date: TD_DATE } })
          : await loadPersistentOnce(key, () =>
              runTdBriefing({ payload: { salespersonId: TD_SALESPERSON, date: TD_DATE } }),
            );
        setBrief(result);
      } catch (err) {
        setError(err instanceof Error ? err.message : 'Failed to load the trading-desk briefing');
      } finally {
        setLoading(false);
      }
    },
    [key, setBrief],
  );

  const reload = useCallback(() => {
    void load(true);
  }, [load]);

  useEffect(() => {
    if (didAutoRun.current || brief !== null) return;
    didAutoRun.current = true;
    void load(false);
  }, [brief, load]);

  // Reactive live push: once a brief is on screen, subscribe to the TD event stream. Each
  // re-synthesized DTO is applied in place and its alert surfaced so the re-rank is visible.
  const hasBrief = brief !== null;
  useEffect(() => {
    if (!hasBrief) return;
    const unsubscribe = subscribeToEvents<TdBriefing>(
      'td-briefing',
      {
        onUpdate: (update) => {
          setBrief(update.briefing);
          setLiveAlert(update.alert);
        },
      },
      { persona: TD_SALESPERSON },
    );
    return unsubscribe;
  }, [hasBrief, setBrief]);

  const dismissAlert = useCallback(() => setLiveAlert(null), []);

  return { brief, loading, error, reload, liveAlert, dismissAlert };
}
