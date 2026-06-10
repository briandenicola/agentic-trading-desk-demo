import '@testing-library/jest-dom/vitest';
import { afterEach } from 'vitest';
import { clearPersistentState } from './hooks/usePersistentState';

// Scene results persist in a module-level store (usePersistentState) so navigating away and back
// keeps the page populated. Vitest shares module state across files, so reset it between every
// test to stop one case's persisted brief/transcript leaking into the next.
afterEach(() => {
  clearPersistentState();
});

// jsdom has no EventSource. Provide an inert stub so components that open a reactive SSE
// subscription on mount (002 US2) don't throw in tests that don't exercise live updates.
// Tests that need to drive updates mock `subscribeToEvents` directly.
if (typeof globalThis.EventSource === 'undefined') {
  class MockEventSource {
    url: string;
    readyState = 0;
    onerror: ((this: EventSource, ev: Event) => unknown) | null = null;
    onmessage: ((this: EventSource, ev: MessageEvent) => unknown) | null = null;
    onopen: ((this: EventSource, ev: Event) => unknown) | null = null;
    constructor(url: string) {
      this.url = url;
    }
    addEventListener() {}
    removeEventListener() {}
    close() {}
  }
  // @ts-expect-error -- minimal stub for the test environment
  globalThis.EventSource = MockEventSource;
}
