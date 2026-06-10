import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useRef,
  useState,
  type ReactNode,
} from 'react';
import { subscribeToEvents, type LiveAlert, type RmBriefing } from '../../api/client';
import { usePersistentState } from '../../hooks/usePersistentState';
import type { AlertPriority } from './workspaceData';

// ---------------------------------------------------------------------------
// Workspace live wiring (002 US2). Holds a long-lived SSE subscription to the
// reactive rm-briefing stream — the SAME pipeline the admin News Desk pushes
// into — and turns each impactful intraday event into highlighting cues across
// the shell: newsfeed/matched-news rows, a live banner, a toast stack, the LIVE
// status pill, KPI "updated" pulses and the notifications badge.
// ---------------------------------------------------------------------------

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

export function WorkspaceLiveProvider({ children }: { children: ReactNode }) {
  const [liveItems, setLiveItems] = usePersistentState<LiveFeedItem[]>('workspace/live-items', []);
  const [unread, setUnread] = usePersistentState<number>('workspace/live-unread', 0);
  const [alert, setAlert] = useState<LiveAlert | null>(null);
  const [toasts, setToasts] = useState<LiveToast[]>([]);
  const [pulse, setPulse] = useState(0);
  const [connected, setConnected] = useState(false);

  const seenRef = useRef<Set<number>>(new Set());
  const baselineRef = useRef(false);
  const timersRef = useRef<number[]>([]);

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
      { persona: 'RM-104' },
    );

    const timers = timersRef.current;
    return () => {
      unsubscribe();
      timers.forEach((t) => window.clearTimeout(t));
      timersRef.current = [];
    };
  }, [dismissToast, setLiveItems, setUnread]);

  const value: WorkspaceLiveValue = {
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
