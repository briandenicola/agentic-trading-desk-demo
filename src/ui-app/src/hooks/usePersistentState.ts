import { useCallback, useState } from 'react';

/**
 * `useState` whose value survives component unmount/remount for the lifetime of the SPA session.
 *
 * Scene results (RM briefing, morning brief, chat transcript) live in component state, so React
 * discards them when the user navigates to another route and the scene unmounts — forcing a re-run
 * on return. This hook keeps the latest value in a module-level store keyed by a stable string, so
 * remounting reads the prior value instead of the initial one. State is intentionally in-memory
 * (cleared on full page reload), which is exactly the "navigate away and back" persistence requested.
 */
const store = new Map<string, unknown>();
const inflight = new Map<string, Promise<unknown>>();

export function usePersistentState<T>(
  key: string,
  initial: T | (() => T),
): [T, (next: T | ((prev: T) => T)) => void] {
  const [value, setValue] = useState<T>(() => {
    if (store.has(key)) return store.get(key) as T;
    const seed = typeof initial === 'function' ? (initial as () => T)() : initial;
    store.set(key, seed);
    return seed;
  });

  const set = useCallback(
    (next: T | ((prev: T) => T)) => {
      setValue((prev) => {
        const resolved = typeof next === 'function' ? (next as (p: T) => T)(prev) : next;
        store.set(key, resolved);
        return resolved;
      });
    },
    [key],
  );

  return [value, set];
}

/**
 * Run `loader` once for a key and write the result straight into the persistent store, deduping
 * concurrent callers. This keeps an in-flight fetch alive across navigation: if the scene unmounts
 * before the request resolves, the result still lands in the store, so remounting reads the loaded
 * value instead of kicking off a fresh run. A second call after resolution starts a new load (used
 * for explicit reloads).
 */
export function loadPersistentOnce<T>(key: string, loader: () => Promise<T>): Promise<T> {
  const existing = inflight.get(key);
  if (existing) return existing as Promise<T>;
  const p = loader()
    .then((value) => {
      store.set(key, value);
      inflight.delete(key);
      return value;
    })
    .catch((err) => {
      inflight.delete(key);
      throw err;
    });
  inflight.set(key, p);
  return p;
}

/** Test seam: drop all persisted scene state so cases don't leak across renders. */
export function clearPersistentState(): void {
  store.clear();
  inflight.clear();
}
