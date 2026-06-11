import type { LiveAlert, TdBriefing } from '../../api/client';
import type { ShellKpi } from '../../components/CommandCenterShell';
import { mint } from '../../theme/theme';

/** KPI tiles for the command-center stats bar, derived from the briefing. */
export function deriveKpis(brief: TdBriefing): ShellKpi[] {
  const p1 = brief.priorityCallList.filter((c) => c.priority === 1).length;
  const topScore = brief.priorityCallList[0]?.score ?? 0;
  return [
    { label: 'Clients Covered', value: brief.salesperson.clientCount, valueColor: mint.blue, to: '/desk' },
    { label: 'Priority Calls', value: brief.priorityCallList.length, valueColor: mint.cyan, to: '/desk/morning-brief' },
    { label: 'P1 Now', value: p1, valueColor: mint.red, delta: p1 > 0 ? '⚠ Action' : 'Clear', deltaColor: p1 > 0 ? mint.red : mint.green },
    { label: 'Inventory Axes', value: brief.inventoryAxes.length, valueColor: mint.green, to: '/desk' },
    { label: 'Top Score', value: topScore, valueColor: mint.gold },
    { label: 'Events', value: brief.eventsConsidered?.length ?? 0, valueColor: mint.purple, to: '/admin' },
  ];
}

/** Scrolling live-feed headlines, derived from the briefing (+ any live alert). */
export function deriveTicker(brief: TdBriefing, liveAlert?: LiveAlert | null): string[] {
  const items: string[] = [];
  if (liveAlert?.headline) items.push(`⚡ ${liveAlert.headline}`);

  for (const ev of brief.eventsConsidered ?? []) {
    const dot = ev.sentiment === 'Positive' ? '🟢' : ev.sentiment === 'Negative' ? '🔴' : '📊';
    items.push(`${dot} ${ev.headline}${ev.sector ? ` · ${ev.sector}` : ''}`);
  }

  for (const call of brief.priorityCallList) {
    const lead = call.whyNow[0]?.label ?? call.suggestedAction;
    items.push(`🔔 ${call.clientName} · ${lead}`);
  }

  for (const axe of brief.inventoryAxes.slice(0, 4)) {
    items.push(`↔ ${axe.securityName} · axed to ${axe.axeSide.toUpperCase()}`);
  }

  return items.length > 0 ? items : ['Markets intelligence · fictional data'];
}
