import type { LeadLeftDealUpload } from '../../api/client';

/**
 * Parse an uploaded spreadsheet (.xlsx/.xls/.csv) of possible lead-left deals into the structured
 * upload shape. Column headers are matched case-/spacing-insensitively, so a desk can hand us a
 * loosely-formatted sheet. The first worksheet is used. All parsing happens in the browser; only
 * the structured JSON is sent to the API. SheetJS is loaded lazily (dynamic import) so it is
 * code-split out of the main bundle and only fetched when an upload actually happens.
 */
export async function parseLeadLeftSpreadsheet(file: File): Promise<LeadLeftDealUpload[]> {
  const XLSX = await import('xlsx');
  const buf = await file.arrayBuffer();
  const wb = XLSX.read(buf, { type: 'array' });
  const sheetName = wb.SheetNames[0];
  if (!sheetName) return [];
  const sheet = wb.Sheets[sheetName];
  const rows = XLSX.utils.sheet_to_json<Record<string, unknown>>(sheet, { defval: '' });

  const deals: LeadLeftDealUpload[] = [];
  for (const row of rows) {
    const get = (...keys: string[]) => pick(row, keys);
    const issuer = str(get('issuer', 'issuername', 'company', 'name'));
    if (!issuer) continue; // skip blank/garbage rows

    deals.push({
      dealId: str(get('dealid', 'id')) || undefined,
      issuer,
      sector: str(get('sector')) || undefined,
      ourRole: str(get('ourrole', 'role', 'syndicaterole', 'syndicate')) || undefined,
      leadLeft: bool(get('leadleft', 'isleadleft', 'lead'), get('ourrole', 'role', 'syndicate')),
      bookStatus: str(get('bookstatus', 'status', 'book')) || undefined,
      pricingDate: str(get('pricingdate', 'pricing', 'pricedate', 'priceson')) || undefined,
      ourAllocationControlPct: pct(get('ourallocationcontrolpct', 'allocationcontrol', 'allocation', 'allocationpct', 'control')),
      coManagers: list(get('comanagers', 'comanager', 'comgr', 'syndicate members', 'syndicatemembers')),
      trancheSecurityIds: list(get('tranchesecurityids', 'tranches', 'securities', 'securityids', 'isins', 'tranche')),
      notes: str(get('notes', 'note', 'comment', 'comments')) || undefined,
    });
  }
  return deals;
}

/** Look up a cell by any of several normalized header aliases. */
function pick(row: Record<string, unknown>, keys: string[]): unknown {
  for (const rawKey of Object.keys(row)) {
    const norm = normalize(rawKey);
    if (keys.includes(norm)) return row[rawKey];
  }
  return undefined;
}

const normalize = (s: string) => s.toLowerCase().replace(/[^a-z0-9]/g, '');

function str(v: unknown): string {
  if (v === null || v === undefined) return '';
  return String(v).trim();
}

/**
 * Truthy when the explicit lead-left flag reads yes/true/1/y, OR when no explicit flag is given but
 * the syndicate role mentions "lead" + "left" / "lead-left".
 */
function bool(flag: unknown, roleHint: unknown): boolean | undefined {
  const f = str(flag).toLowerCase();
  if (f) return ['y', 'yes', 'true', '1', 'lead-left', 'leadleft', 'x'].includes(f);
  const role = str(roleHint).toLowerCase();
  if (role) return role.includes('lead') && (role.includes('left') || role.includes('-left'));
  return undefined;
}

/** Accept "45%", "0.45", 45, or 0.45 and return a 0..1 fraction. */
function pct(v: unknown): number | undefined {
  const raw = str(v).replace('%', '').trim();
  if (!raw) return undefined;
  const n = Number(raw);
  if (Number.isNaN(n)) return undefined;
  if (str(v).includes('%')) return n / 100;
  return n > 1 ? n / 100 : n;
}

/** Split a comma/semicolon/pipe-separated cell into a trimmed list. */
function list(v: unknown): string[] | undefined {
  const raw = str(v);
  if (!raw) return undefined;
  const parts = raw
    .split(/[;,|]/)
    .map((p) => p.trim())
    .filter(Boolean);
  return parts.length > 0 ? parts : undefined;
}
