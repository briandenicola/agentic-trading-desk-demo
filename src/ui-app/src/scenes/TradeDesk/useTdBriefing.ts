import { useCallback, useEffect, useRef, useState } from 'react';
import { runTdBriefing, type TdBriefing } from '../../api/client';
import { loadPersistentOnce, usePersistentState } from '../../hooks/usePersistentState';

// Demo persona + snapshot date for the Institutional Sales & Trading flow.
export const TD_SALESPERSON = 'Theo Wexler';
export const TD_DATE = '2026-05-22';

/**
 * Loads the Trading Desk morning briefing once per SPA session and persists it under
 * `key`, so navigating away and back does not re-run the agent. Both TD scenes share
 * the same persisted brief by passing the same key. Exposes a `reload` for an explicit
 * refresh (e.g. after a News Desk inject re-ranks the call list).
 */
export function useTdBriefing(key: string) {
  const [brief, setBrief] = usePersistentState<TdBriefing | null>(key, null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
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

  return { brief, loading, error, reload };
}
