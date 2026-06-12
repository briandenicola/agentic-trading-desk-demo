import { sendDeskChat } from '../../api/client';
import type { ChatDockConfig } from '../Workspace/ChatOverlay';

// Coverage salesperson persona for the Institutional Sales & Trading desk. A non-empty
// salespersonId routes /api/chat to the trading-desk-grounded assistant (/mock/td/*).
const TD_SALESPERSON = 'Theo Wexler';

/** Trading-desk dock config: grounded in the trading book, axes and live tape. */
export const tdChatConfig: ChatDockConfig = {
  storageKey: 'desk/chat',
  title: 'Trading Desk Assistant',
  subtitle: 'Grounded in your book, axes & live tape',
  emptyHint: 'Ask who to call first, about a client (e.g. CL-2006), a security (e.g. SEC-3601) or what moved overnight.',
  placeholder: 'Ask about a client, security, axe or the tape…',
  send: (turns) => sendDeskChat(turns, TD_SALESPERSON),
};
