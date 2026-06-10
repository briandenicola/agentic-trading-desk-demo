import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useRef,
  useState,
  type ReactNode,
} from 'react';
import { runRmBriefing, subscribeToEvents, type LiveAlert, type RmBriefing } from '../../api/client';
import { usePersistentState, loadPersistentOnce } from '../../hooks/usePersistentState';
import type { AlertPriority } from './workspaceData';

// ---------------------------------------------------------------------------
// Workspace data + live wiring. This is the single engine behind the main page:
//   1. Auto-loads the real RM Daily Briefing (RM-104) on first visit and persists
//      it, so navigating away and back does not re-run the agent.
//   2. Holds a long-lived SSE subscription to the reactive rm-briefing stream — the
//      SAME pipeline the admin News Desk pushes into — so every /admin post
//      re-ranks the page in place and lights up the shell: newsfeed rows, a live
//      banner, a toast stack, the LIVE pill, KPI pulses and the unread badge.
// ---------------------------------------------------------------------------

const RM_ID = 'RM-104';
const RM_DATE = '2026-05-14';

export interface LiveFeedItem {
  id: string;
  sequence: number;
  priority: AlertPriority;
  headline: string;
  eventCount: number;
  time: string;
  isNew: boolean;
}

export interface LiveToast {
  id: string;
  priority: AlertPriority;
  headline: string;
}

interface WorkspaceLiveValue {
  brief: RmBriefing | null;
  briefLoading: boolean;
  briefError: string | null;
  reloadBrief: () => void;
  connected: boolean;
  liveItems: LiveFeedItem[];
  alert: LiveAlert | null;
  dismissAlert: () => void;
  toasts: LiveToast[];
  dismissToast: (id: string) => void;
  unread: number;
  clearUnread: () => void;
  /** Monotonic counter bumped on every impactful update; drives transient panel pulses. */
  pulse: number;
}

const PRIORITY_MAP: Record<LiveAlert['priority'], AlertPriority> = {
  urgent: 'HIGH',
  notice: 'MEDIUM',
  info: 'LOW',
};

const WorkspaceLiveContext = createContext<WorkspaceLiveValue | null>(null);

function nowTime(): string {
  return new Date().toLocaleTimeString([], { hour: 'numeric', minute: '2-digit' });
}

function isUsableBriefing(b: unknown): b is RmBriefing {
  return !!b && Array.isArray((b as RmBriefing).priorityCallList);
}

export function WorkspaceLiveProvider({ children }: { children: ReactNode }) {
  const [brief, setBrief] = usePersistentState<RmBriefing | null>('workspace/brief', null);
  const [briefLoading, setBriefLoading] = useState(false);
  const [briefError, setBriefError] = useState<string | null>(null);

  const [liveItems, setLiveItems] = usePersistentState<LiveFeedItem[]>('workspace/live-items', []);
  const [unread, setUnread] = usePersistentState<number>('workspace/live-unread', 0);
  const [alert, setAlert] = useState<LiveAlert | null>(null);
  const [toasts, setToasts] = useState<LiveToast[]>([]);
  const [pulse, setPulse] = useState(0);
  const [connected, setConnected] = useState(false);

  const seenRef = useRef<Set<number>>(new Set());
  const baselineRef = useRef(false);
  const timersRef = useRef<number[]>([]);
  const didAutoRun = useRef(false);

  const loadBrief = useCallback(async () => {
    setBriefLoading(true);
    setBriefError(null);
    try {
      const result = await loadPersistentOnce('workspace/brief', () =>
        runRmBriefing({ payload: { rmId: RM_ID, date: RM_DATE } }),
      );
      setBrief(result);
    } catch (err) {
      setBriefError(err instanceof Error ? err.message : 'Failed to load the daily briefing');
    } finally {
      setBriefLoading(false);
    }
  }, [setBrief]);

  const reloadBrief = useCallback(() => {
    void loadBrief();
  }, [loadBrief]);

  // Auto-run on first visit so the page renders real data without a manual click.
  useEffect(() => {
    if (didAutoRun.current || brief !== null) return;
    didAutoRun.current = true;
    void loadBrief();
  }, [brief, loadBrief]);

  const dismissToast = useCallback((id: string) => {
    setToasts((t) => t.filter((x) => x.id !== id));
  }, []);
  const dismissAlert = useCallback(() => setAlert(null), []);
  const clearUnread = useCallback(() => setUnread(0), [setUnread]);

  useEffect(() => {
    const unsubscribe = subscribeToEvents<RmBriefing>(
      'rm-briefing',
      {
        onReady: () => setConnected(true),
        onUpdate: (update) => {
          setConnected(true);
          if (seenRef.current.has(update.sequence)) return;
          seenRef.current.add(update.sequence);

          // Always reconcile the page to the freshest re-synthesized briefing.
          if (isUsableBriefing(update.briefing)) {
            setBrief(update.briefing);
          }

          // The first snapshot after (re)mounting is the current baseline — sync it
          // silently so navigating into the workspace doesn't fire a stale alert.
          if (!baselineRef.current) {
            baselineRef.current = true;
            return;
          }

          setAlert(update.alert);
          if (update.alert.noImpact) return;

          const priority = PRIORITY_MAP[update.alert.priority];
          const id = `live-${update.sequence}`;
          const item: LiveFeedItem = {
            id,
            sequence: update.sequence,
            priority,
            headline: update.alert.headline,
            eventCount: update.alert.eventIds.length,
            time: nowTime(),
            isNew: true,
          };

          setLiveItems((prev) => [item, ...prev.filter((p) => p.id !== id)].slice(0, 8));
          setToasts((t) => [{ id, priority, headline: update.alert.headline }, ...t].slice(0, 4));
          setUnread((n) => n + 1);
          setPulse((p) => p + 1);

          const toastTimer = window.setTimeout(() => dismissToast(id), 6000);
          const flashTimer = window.setTimeout(() => {
            setLiveItems((prev) => prev.map((p) => (p.id === id ? { ...p, isNew: false } : p)));
          }, 4000);
          timersRef.current.push(toastTimer, flashTimer);
        },
        onError: () => setConnected(false),
      },
      { persona: RM_ID },
    );

    const timers = timersRef.current;
    return () => {
      unsubscribe();
      timers.forEach((t) => window.clearTimeout(t));
      timersRef.current = [];
    };
  }, [dismissToast, setBrief, setLiveItems, setUnread]);

  const value: WorkspaceLiveValue = {
    brief,
    briefLoading,
    briefError,
    reloadBrief,
    connected,
    liveItems,
    alert,
    dismissAlert,
    toasts,
    dismissToast,
    unread,
    clearUnread,
    pulse,
  };

  return <WorkspaceLiveContext.Provider value={value}>{children}</WorkspaceLiveContext.Provider>;
}

export function useWorkspaceLive(): WorkspaceLiveValue {
  const ctx = useContext(WorkspaceLiveContext);
  if (!ctx) {
    throw new Error('useWorkspaceLive must be used within a WorkspaceLiveProvider');
  }
  return ctx;
}
