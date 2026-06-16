import { useEffect, useMemo, useRef, useState } from 'react';
import type { ChangeEvent } from 'react';
import {
  Alert,
  Box,
  Button,
  Chip,
  CircularProgress,
  Divider,
  Paper,
  Stack,
  Typography,
} from '@mui/material';
import ArrowBackRoundedIcon from '@mui/icons-material/ArrowBackRounded';
import ArrowForwardRoundedIcon from '@mui/icons-material/ArrowForwardRounded';
import RefreshRoundedIcon from '@mui/icons-material/RefreshRounded';
import PhoneInTalkOutlinedIcon from '@mui/icons-material/PhoneInTalkOutlined';
import ChatBubbleRoundedIcon from '@mui/icons-material/ChatBubbleRounded';
import CampaignRoundedIcon from '@mui/icons-material/CampaignRounded';
import AccountTreeRoundedIcon from '@mui/icons-material/AccountTreeRounded';
import TimelineRoundedIcon from '@mui/icons-material/TimelineRounded';
import UploadFileRoundedIcon from '@mui/icons-material/UploadFileRounded';
import WorkspacePremiumRoundedIcon from '@mui/icons-material/WorkspacePremiumRounded';
import CommandCenterShell from '../../components/CommandCenterShell';
import LiveAlertBanner from '../../components/LiveAlertBanner';
import { mint } from '../../theme/theme';
import type {
  ShellKpi,
} from '../../components/CommandCenterShell';
import type {
  LeadLeftDeal,
  StoryboardEvidence,
  StoryboardMetric,
  TdNewIssueStoryboard,
  TdStoryboardStep,
} from '../../api/client';
import { uploadLeadLeftDeals } from '../../api/client';
import { parseLeadLeftSpreadsheet } from './parseLeadLeftDeals';
import { useTdNewIssue } from './useTdNewIssue';
import { ChatDockProvider, useChatDock } from '../Workspace/ChatOverlay';
import { tdChatConfig } from '../TradeDesk/tdChatConfig';

const toneColor = (tone?: StoryboardMetric['tone']): string => {
  switch (tone) {
    case 'positive':
      return mint.green;
    case 'warning':
      return mint.red;
    case 'accent':
      return mint.cyan;
    default:
      return mint.text;
  }
};

const beatIcon = (id: string) => {
  switch (id) {
    case 'announcement':
      return <CampaignRoundedIcon fontSize="inherit" />;
    case 'holdings':
      return <AccountTreeRoundedIcon fontSize="inherit" />;
    case 'activity':
      return <TimelineRoundedIcon fontSize="inherit" />;
    case 'outreach':
      return <PhoneInTalkOutlinedIcon fontSize="inherit" />;
    default:
      return <TimelineRoundedIcon fontSize="inherit" />;
  }
};

const evidenceColor = (kind: StoryboardEvidence['kind']): string => {
  switch (kind) {
    case 'news':
      return mint.gold;
    case 'holding':
      return mint.green;
    case 'rfq':
      return mint.cyan;
    case 'trade':
      return mint.blueBright;
    case 'crm':
      return mint.purple;
    case 'axe':
      return mint.red;
    case 'syndicate':
      return mint.gold;
    default:
      return mint.textDim;
  }
};

/** Gold "LEAD-LEFT" pill used to flag deals/tranches/ideas we run the books on. */
function LeadLeftBadge({ label = 'LEAD-LEFT' }: { label?: string }) {
  return (
    <Chip
      icon={<WorkspacePremiumRoundedIcon sx={{ fontSize: 12, color: `${mint.bg} !important` }} />}
      label={label}
      size="small"
      sx={{
        height: 18,
        fontSize: 8,
        fontWeight: 800,
        letterSpacing: '0.5px',
        color: mint.bg,
        bgcolor: mint.gold,
        '& .MuiChip-label': { px: 0.75 },
      }}
    />
  );
}

function deriveKpis(story: TdNewIssueStoryboard): ShellKpi[] {
  const debt = story.issuer.tranches.find((t) => t.assetClass !== 'Equity');
  const leadLeft = story.issuer.leadLeft === true;
  return [
    { label: 'Issuer', value: story.issuer.name.split(' ')[0], valueColor: mint.cyan },
    {
      label: 'Our Book',
      value: leadLeft ? 'LEAD-LEFT' : story.issuer.syndicateRole ?? '—',
      valueColor: leadLeft ? mint.gold : mint.text,
      delta: leadLeft && story.issuer.ourAllocationControlPct != null
        ? `${Math.round(story.issuer.ourAllocationControlPct * 100)}% alloc`
        : undefined,
      deltaColor: mint.gold,
    },
    { label: 'Tranches', value: story.issuer.tranches.length, valueColor: mint.blue },
    { label: 'Focus Client', value: story.outreach.clientName.split(' ')[0], valueColor: mint.gold },
    { label: 'Priority', value: 'P1', valueColor: mint.red, delta: '⚠ Call now', deltaColor: mint.red },
    { label: 'New Note', value: debt?.detail?.split('·')[0]?.trim() ?? '—', valueColor: mint.green },
  ];
}

function deriveTicker(story: TdNewIssueStoryboard): string[] {
  const items: string[] = [];
  for (const ev of story.liveEvents ?? []) {
    items.push(`⚡ ${ev.headline}`);
  }
  if (story.issuer.leadLeft) {
    const role = story.issuer.syndicateRole ?? 'Lead-Left Bookrunner';
    const alloc = story.issuer.ourAllocationControlPct != null
      ? ` · ${Math.round(story.issuer.ourAllocationControlPct * 100)}% allocation control`
      : '';
    items.push(`🏆 WE RUN THE BOOKS · ${role} on ${story.issuer.name}${alloc}`);
  }
  items.push(`📣 ${story.issuer.headline}`);
  for (const t of story.issuer.tranches) {
    items.push(`💠 ${t.securityName} · ${t.assetClass}${t.detail ? ` · ${t.detail}` : ''}${t.leadLeft ? ' · LEAD-LEFT' : ''}`);
  }
  items.push(`🔔 ${story.outreach.headline}`);
  return items;
}

function MetricChip({ metric }: { metric: StoryboardMetric }) {
  const color = toneColor(metric.tone);
  const live = metric.live === true;
  return (
    <Box
      sx={{
        px: 1.5,
        py: 0.75,
        borderRadius: 1.5,
        bgcolor: live ? `${mint.gold}14` : mint.bgAlt,
        border: `1px solid ${live ? mint.gold : `${color}40`}`,
        minWidth: 96,
        position: 'relative',
      }}
    >
      {live && (
        <Chip
          label="LIVE"
          size="small"
          sx={{
            position: 'absolute',
            top: -9,
            right: 6,
            height: 16,
            fontSize: 8,
            fontWeight: 800,
            color: mint.bg,
            bgcolor: mint.gold,
          }}
        />
      )}
      <Typography sx={{ fontSize: 8, color: mint.textFaint, textTransform: 'uppercase', letterSpacing: '0.5px' }}>
        {metric.label}
      </Typography>
      <Typography sx={{ fontSize: 16, fontWeight: 800, color, lineHeight: 1.25 }}>{metric.value}</Typography>
      {metric.sub && <Typography sx={{ fontSize: 9, color: mint.textDim }}>{metric.sub}</Typography>}
    </Box>
  );
}

function EvidenceRow({ ev }: { ev: StoryboardEvidence }) {
  const color = evidenceColor(ev.kind);
  const live = ev.live === true;
  return (
    <Box
      sx={{
        display: 'flex',
        gap: 1.25,
        alignItems: 'flex-start',
        py: 0.75,
        px: live ? 1 : 0,
        borderRadius: live ? 1 : 0,
        bgcolor: live ? `${mint.gold}12` : 'transparent',
        borderBottom: `1px solid ${mint.borderSoft}`,
        borderLeft: live ? `3px solid ${mint.gold}` : undefined,
      }}
    >
      <Chip
        label={live ? 'LIVE' : ev.kind.toUpperCase()}
        size="small"
        sx={{
          height: 18,
          fontSize: 8,
          fontWeight: 700,
          color: live ? mint.bg : color,
          bgcolor: live ? mint.gold : `${color}22`,
          border: `1px solid ${live ? mint.gold : `${color}55`}`,
          flexShrink: 0,
          mt: 0.25,
        }}
      />
      <Box sx={{ minWidth: 0, flex: 1 }}>
        <Typography sx={{ fontSize: 12, fontWeight: 600, color: mint.text }}>{ev.label}</Typography>
        {ev.detail && <Typography sx={{ fontSize: 11, color: mint.textDim, mt: 0.25 }}>{ev.detail}</Typography>}
      </Box>
      <Box sx={{ textAlign: 'right', flexShrink: 0 }}>
        {ev.refId && (
          <Typography sx={{ fontSize: 9, color: mint.textFaint, fontVariantNumeric: 'tabular-nums' }}>
            {ev.refId}
          </Typography>
        )}
        {ev.date && <Typography sx={{ fontSize: 9, color: mint.textFaint }}>{ev.date}</Typography>}
      </Box>
    </Box>
  );
}

function OutreachCard({ story, onOpenChat }: { story: TdNewIssueStoryboard; onOpenChat?: (seed: string) => void }) {
  const { outreach } = story;
  return (
    <Paper
      sx={{
        p: 2.5,
        backgroundImage: `linear-gradient(135deg, ${mint.blue}22, ${mint.cyan}10)`,
        borderColor: `${mint.blue}59`,
      }}
      data-testid="ni-outreach"
    >
      <Box sx={{ display: 'flex', alignItems: 'flex-start', gap: 1.5 }}>
        <PhoneInTalkOutlinedIcon sx={{ color: mint.blueBright, mt: 0.25 }} />
        <Box sx={{ flex: 1, minWidth: 0 }}>
          <Typography variant="overline" sx={{ color: mint.blueBright, display: 'block', lineHeight: 1.6 }}>
            Call now · {outreach.clientName}
            {outreach.clientType ? ` · ${outreach.clientType}` : ''}
          </Typography>
          <Typography variant="h6" sx={{ fontSize: 16, mb: 1 }}>
            {outreach.headline}
          </Typography>

          <Typography variant="overline" sx={{ color: mint.textFaint, display: 'block' }}>
            Talking points
          </Typography>
          <Stack component="ul" spacing={0.75} sx={{ pl: 2, my: 1 }}>
            {outreach.talkingPoints.map((tp, i) => (
              <Typography component="li" key={i} variant="body2" sx={{ fontSize: 13 }}>
                {tp}
              </Typography>
            ))}
          </Stack>

          {outreach.tradeIdea && (
            <Box
              sx={{
                mt: 1.5,
                p: 1.5,
                borderRadius: 1.5,
                bgcolor: mint.bgAlt,
                border: `1px solid ${outreach.tradeIdea.leadLeft ? mint.gold : mint.borderHard}`,
                boxShadow: outreach.tradeIdea.leadLeft ? `0 0 0 1px ${mint.gold}55` : undefined,
              }}
            >
              <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, mb: 0.25 }}>
                <Typography variant="overline" sx={{ color: mint.textFaint, display: 'block' }}>
                  Trade idea
                </Typography>
                {outreach.tradeIdea.leadLeft && <LeadLeftBadge label="LEAD-LEFT ALLOCATION" />}
              </Box>
              <Typography variant="body2" sx={{ fontWeight: 700 }}>
                <Box component="span" sx={{ color: outreach.tradeIdea.side === 'Buy' ? mint.green : mint.red }}>
                  {outreach.tradeIdea.side}
                </Box>{' '}
                {outreach.tradeIdea.securityName}
                {outreach.tradeIdea.level ? ` · ${outreach.tradeIdea.level}` : ''}
              </Typography>
              {outreach.tradeIdea.rationale && (
                <Typography variant="body2" sx={{ fontSize: 12, color: mint.textDim, mt: 0.5 }}>
                  {outreach.tradeIdea.rationale}
                </Typography>
              )}
            </Box>
          )}

          <Divider sx={{ my: 1.5 }} />
          <Typography variant="overline" sx={{ color: mint.textFaint, display: 'block' }}>
            Suggested action
          </Typography>
          <Typography variant="body2" sx={{ fontSize: 13, mb: outreach.draftMessage ? 1.5 : 0 }}>
            {outreach.suggestedAction}
          </Typography>

          {outreach.draftMessage && (
            <Box
              sx={{
                p: 1.5,
                borderRadius: 1.5,
                bgcolor: '#060d1a',
                border: `1px dashed ${mint.borderAccent}`,
                fontStyle: 'italic',
              }}
            >
              <Typography variant="body2" sx={{ fontSize: 12, color: mint.textDim }}>
                “{outreach.draftMessage}”
              </Typography>
            </Box>
          )}

          {onOpenChat && (
            <Box
              onClick={() =>
                onOpenChat(
                  `Tell me about ${outreach.clientName} (${outreach.clientId}) — why should I call them now on this new issue, and what should I lead with?`,
                )
              }
              role="button"
              aria-label={`Open chat about ${outreach.clientName}`}
              sx={{
                mt: 1.5,
                display: 'inline-flex',
                alignItems: 'center',
                gap: 1,
                px: 1.75,
                py: 0.75,
                borderRadius: 99,
                cursor: 'pointer',
                color: '#fff',
                fontWeight: 700,
                fontSize: 12.5,
                background: `linear-gradient(135deg, ${mint.blue}, ${mint.cyan})`,
                '&:hover': { filter: 'brightness(1.08)' },
              }}
            >
              <ChatBubbleRoundedIcon sx={{ fontSize: 15 }} />
              Open Chat
            </Box>
          )}
        </Box>
      </Box>
    </Paper>
  );
}

function LeadLeftBoardPanel({ board, onUploaded }: { board: LeadLeftDeal[]; onUploaded: () => void }) {
  const fileRef = useRef<HTMLInputElement>(null);
  const [busy, setBusy] = useState(false);
  const [msg, setMsg] = useState<{ kind: 'success' | 'error'; text: string } | null>(null);

  const onPick = async (event: ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0];
    event.target.value = '';
    if (!file) return;
    setBusy(true);
    setMsg(null);
    try {
      const deals = await parseLeadLeftSpreadsheet(file);
      if (deals.length === 0) {
        setMsg({ kind: 'error', text: 'No deals found — the sheet needs at least an "issuer" column.' });
        return;
      }
      const result = await uploadLeadLeftDeals(deals);
      setMsg({
        kind: 'success',
        text: `Loaded ${deals.length} deal(s) — ${result.added} added, ${result.updated} updated. Re-running radar…`,
      });
      onUploaded();
    } catch (err) {
      setMsg({ kind: 'error', text: err instanceof Error ? err.message : 'Upload failed' });
    } finally {
      setBusy(false);
    }
  };

  return (
    <Paper sx={{ p: 2 }} data-testid="ni-lead-left-board">
      <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, flexWrap: 'wrap', mb: 1 }}>
        <WorkspacePremiumRoundedIcon sx={{ color: mint.gold, fontSize: 18 }} />
        <Typography sx={{ fontSize: 13, fontWeight: 800 }}>Lead-Left Board</Typography>
        <Typography sx={{ fontSize: 11, color: mint.textDim }}>
          Possible deals we run · {board.length}
        </Typography>
        <Box sx={{ flex: 1 }} />
        <input
          ref={fileRef}
          type="file"
          accept=".xlsx,.xls,.csv"
          hidden
          onChange={onPick}
          data-testid="ni-upload-input"
        />
        <Button
          size="small"
          variant="outlined"
          onClick={() => fileRef.current?.click()}
          disabled={busy}
          startIcon={busy ? <CircularProgress size={14} /> : <UploadFileRoundedIcon />}
        >
          {busy ? 'Uploading…' : 'Upload deals (.xlsx/.csv)'}
        </Button>
      </Box>

      {msg && (
        <Alert severity={msg.kind} sx={{ mb: 1, fontSize: 12, py: 0 }} onClose={() => setMsg(null)}>
          {msg.text}
        </Alert>
      )}

      <Stack spacing={0.5}>
        {board.length === 0 && (
          <Typography sx={{ fontSize: 12, color: mint.textDim }}>
            No deals loaded yet. Upload a spreadsheet of possible lead-left deals to factor them into the radar.
          </Typography>
        )}
        {board.map((d, i) => (
          <Box
            key={`${d.issuer}-${d.source ?? 'seed'}-${i}`}
            sx={{
              display: 'flex',
              alignItems: 'center',
              gap: 1,
              py: 0.5,
              borderBottom: `1px solid ${mint.borderSoft}`,
            }}
          >
            <Box sx={{ minWidth: 0, flex: 1 }}>
              <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.75 }}>
                <Typography sx={{ fontSize: 12, fontWeight: 700, color: mint.text }}>{d.issuer}</Typography>
                {d.leadLeft && <LeadLeftBadge />}
              </Box>
              {(d.role || d.bookStatus) && (
                <Typography sx={{ fontSize: 10, color: mint.textDim }}>
                  {d.role}
                  {d.role && d.bookStatus ? ' · ' : ''}
                  {d.bookStatus}
                </Typography>
              )}
            </Box>
            {d.pricingDate && (
              <Typography sx={{ fontSize: 10, color: mint.textFaint }}>prices {d.pricingDate}</Typography>
            )}
            <Chip
              label={(d.source ?? 'seed').toUpperCase()}
              size="small"
              sx={{
                height: 16,
                fontSize: 8,
                color: d.source === 'upload' ? mint.cyan : mint.textFaint,
                bgcolor: 'transparent',
                border: `1px solid ${d.source === 'upload' ? mint.cyan : mint.borderHard}`,
              }}
            />
          </Box>
        ))}
      </Stack>
    </Paper>
  );
}

export default function TdNewIssueScene() {
  return (
    <ChatDockProvider config={tdChatConfig}>
      <TdNewIssueInner />
    </ChatDockProvider>
  );
}

function TdNewIssueInner() {
  const { openChat } = useChatDock();
  const { story, loading, error, reload, liveAlert, dismissAlert } = useTdNewIssue('td-new-issue/story');
  const [stepIdx, setStepIdx] = useState(0);

  const steps = useMemo(() => (story ? [...story.steps].sort((a, b) => a.order - b.order) : []), [story]);

  // Clamp the active step whenever the storyboard reloads.
  useEffect(() => {
    setStepIdx(0);
  }, [story?.title, story?.mode]);

  const current: TdStoryboardStep | undefined = steps[stepIdx];
  const isLast = stepIdx >= steps.length - 1;
  const isOutreach = current?.id === 'outreach';

  return (
    <CommandCenterShell
      mode={story?.mode ?? 'DEMO'}
      asOf={story?.asOf}
      kpis={story ? deriveKpis(story) : []}
      tickerItems={story ? deriveTicker(story) : []}
    >
      <Box sx={{ px: { xs: 2, md: 3 }, py: 2.5, maxWidth: 1180, mx: 'auto' }}>
        <LiveAlertBanner alert={liveAlert} onDismiss={dismissAlert} />
        <Box sx={{ mb: 2 }}>
          <Typography variant="overline" color="text.secondary">
            New Issue Radar · Institutional Sales &amp; Trading
          </Typography>
          <Box sx={{ display: 'flex', alignItems: 'baseline', gap: 2, flexWrap: 'wrap', mt: 0.5 }}>
            <Typography variant="h3">{story ? story.title : 'New Issue Radar'}</Typography>
            {story && (
              <Chip
                label={story.mode}
                size="small"
                color={story.mode === 'LIVE' ? 'success' : 'default'}
                sx={{ fontWeight: 700 }}
              />
            )}
          </Box>
          <Typography variant="body2" color="text.secondary" sx={{ maxWidth: 880, mt: 0.75 }}>
            {story
              ? story.subtitle
              : 'A new issue lands. Who already owns the name, is trading the new paper, and wants our call first?'}
          </Typography>
        </Box>

        <Box sx={{ mb: 2.5, display: 'flex', alignItems: 'center', gap: 2, flexWrap: 'wrap' }}>
          <Button
            variant="contained"
            onClick={reload}
            disabled={loading}
            startIcon={loading ? <CircularProgress size={16} /> : <RefreshRoundedIcon />}
          >
            {loading ? 'Running…' : story ? 'Re-run radar' : 'Run new-issue radar'}
          </Button>
        </Box>

        {error && (
          <Alert severity="error" sx={{ mb: 3 }}>
            {error}
          </Alert>
        )}

        {story && current && (
          <Stack spacing={2} data-testid="ni-storyboard">
            {/* Issuer / new-issue header strip */}
            <Paper
              sx={{
                p: 2,
                border: story.issuer.leadLeft ? `1px solid ${mint.gold}` : undefined,
                boxShadow: story.issuer.leadLeft ? `0 0 0 1px ${mint.gold}40` : undefined,
              }}
            >
              <Box sx={{ display: 'flex', alignItems: 'flex-start', gap: 1.5, flexWrap: 'wrap' }}>
                <CampaignRoundedIcon sx={{ color: mint.gold, mt: 0.25 }} />
                <Box sx={{ flex: 1, minWidth: 220 }}>
                  <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, flexWrap: 'wrap', mb: 0.25 }}>
                    <Typography sx={{ fontSize: 14, fontWeight: 800 }}>{story.issuer.headline}</Typography>
                    {story.issuer.leadLeft && <LeadLeftBadge label={story.issuer.syndicateRole ?? 'LEAD-LEFT'} />}
                  </Box>
                  {story.issuer.leadLeft && (
                    <Typography sx={{ fontSize: 11, color: mint.gold, fontWeight: 700 }}>
                      We run the books on the left
                      {story.issuer.ourAllocationControlPct != null
                        ? ` · ~${Math.round(story.issuer.ourAllocationControlPct * 100)}% allocation control`
                        : ''}
                      {story.issuer.bookStatus ? ` · ${story.issuer.bookStatus}` : ''}
                      {story.issuer.pricingDate ? ` · prices ${story.issuer.pricingDate}` : ''}
                      {story.issuer.coManagers && story.issuer.coManagers.length > 0
                        ? ` · co-managers: ${story.issuer.coManagers.join(', ')}`
                        : ''}
                    </Typography>
                  )}
                  {story.issuer.summary && (
                    <Typography variant="body2" sx={{ fontSize: 12, color: mint.textDim, mt: 0.5 }}>
                      {story.issuer.summary}
                    </Typography>
                  )}
                </Box>
                <Stack direction="row" spacing={1} sx={{ flexWrap: 'wrap', gap: 1 }}>
                  {story.issuer.tranches.map((t) => (
                    <Box
                      key={t.securityId}
                      sx={{
                        px: 1.25,
                        py: 0.75,
                        borderRadius: 1.5,
                        bgcolor: mint.bgAlt,
                        border: `1px solid ${t.leadLeft ? mint.gold : mint.borderHard}`,
                      }}
                    >
                      <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.5 }}>
                        <Typography sx={{ fontSize: 8, color: mint.textFaint, textTransform: 'uppercase' }}>
                          {t.assetClass}
                        </Typography>
                        {t.leadLeft && <LeadLeftBadge />}
                      </Box>
                      <Typography sx={{ fontSize: 12, fontWeight: 700 }}>{t.securityName}</Typography>
                      {t.detail && <Typography sx={{ fontSize: 10, color: mint.textDim }}>{t.detail}</Typography>}
                    </Box>
                  ))}
                </Stack>
              </Box>
            </Paper>

            {/* Lead-left board + spreadsheet upload */}
            <LeadLeftBoardPanel board={story.leadLeftBoard ?? []} onUploaded={reload} />

            {/* Beat progress rail */}
            <Box sx={{ display: 'flex', gap: 1, flexWrap: 'wrap' }} data-testid="ni-beats">
              {steps.map((s, i) => {
                const active = i === stepIdx;
                const done = i < stepIdx;
                const color = active ? mint.blue : done ? mint.green : mint.textFaint;
                return (
                  <Box
                    key={s.id}
                    onClick={() => setStepIdx(i)}
                    sx={{
                      cursor: 'pointer',
                      flex: '1 1 160px',
                      px: 1.25,
                      py: 1,
                      borderRadius: 1.5,
                      bgcolor: active ? `${mint.blue}1a` : mint.bgAlt,
                      border: `1px solid ${active ? mint.blue : mint.borderHard}`,
                      display: 'flex',
                      alignItems: 'center',
                      gap: 1,
                      transition: 'all .15s',
                    }}
                  >
                    <Box
                      sx={{
                        width: 22,
                        height: 22,
                        borderRadius: '50%',
                        flexShrink: 0,
                        display: 'flex',
                        alignItems: 'center',
                        justifyContent: 'center',
                        fontSize: 12,
                        color,
                        border: `1px solid ${color}`,
                      }}
                    >
                      {beatIcon(s.id)}
                    </Box>
                    <Box sx={{ minWidth: 0 }}>
                      <Typography sx={{ fontSize: 8, color: mint.textFaint, textTransform: 'uppercase', letterSpacing: '0.5px' }}>
                        Step {s.order}
                      </Typography>
                      <Typography sx={{ fontSize: 11, fontWeight: active ? 800 : 600, color: active ? mint.text : mint.textDim, lineHeight: 1.2 }}>
                        {s.beat}
                      </Typography>
                    </Box>
                  </Box>
                );
              })}
            </Box>

            {/* Active beat */}
            <Paper sx={{ p: 2.5 }} data-testid={`ni-step-${current.id}`}>
              <Typography variant="overline" sx={{ color: mint.blueBright }}>
                {current.beat}
              </Typography>
              <Typography variant="h5" sx={{ fontSize: 20, mb: 1 }}>
                {current.title}
              </Typography>
              <Typography variant="body1" sx={{ fontSize: 14, color: mint.textDim, mb: 2 }}>
                {current.narration}
              </Typography>

              {current.metrics && current.metrics.length > 0 && (
                <Stack direction="row" spacing={1.5} sx={{ flexWrap: 'wrap', gap: 1.5, mb: 2 }}>
                  {current.metrics.map((m) => (
                    <MetricChip key={m.label} metric={m} />
                  ))}
                </Stack>
              )}

              {current.evidence && current.evidence.length > 0 && (
                <>
                  <Typography variant="overline" sx={{ color: mint.textFaint, display: 'block', mb: 0.5 }}>
                    Evidence · systems of record
                  </Typography>
                  <Box>
                    {current.evidence.map((ev, i) => (
                      <EvidenceRow key={`${ev.refId ?? ev.label}-${i}`} ev={ev} />
                    ))}
                  </Box>
                </>
              )}
            </Paper>

            {/* Outreach recommendation reveals on the final beat */}
            {isOutreach && <OutreachCard story={story} onOpenChat={openChat} />}

            {/* Walkthrough controls */}
            <Box sx={{ display: 'flex', alignItems: 'center', gap: 1.5 }}>
              <Button
                variant="outlined"
                disabled={stepIdx === 0}
                onClick={() => setStepIdx((i) => Math.max(0, i - 1))}
                startIcon={<ArrowBackRoundedIcon />}
              >
                Back
              </Button>
              <Typography variant="body2" sx={{ color: mint.textDim }}>
                Step {stepIdx + 1} of {steps.length}
              </Typography>
              <Box sx={{ flex: 1 }} />
              <Button
                variant="contained"
                disabled={isLast}
                onClick={() => setStepIdx((i) => Math.min(steps.length - 1, i + 1))}
                endIcon={<ArrowForwardRoundedIcon />}
              >
                {stepIdx === steps.length - 2 ? 'See the call' : 'Next'}
              </Button>
            </Box>

            {story.notes && story.notes.length > 0 && (
              <Alert severity="warning" sx={{ fontSize: 13 }}>
                {story.notes.map((note, idx) => (
                  <Typography key={idx} variant="body2">
                    {note}
                  </Typography>
                ))}
              </Alert>
            )}
          </Stack>
        )}

        {!story && loading && (
          <Box sx={{ display: 'flex', justifyContent: 'center', py: 8 }}>
            <CircularProgress />
          </Box>
        )}

        <Box sx={{ mt: 4, textAlign: 'center' }}>
          <Typography variant="caption" color="text.secondary">
            Institutional Sales &amp; Trading · New Issue Radar · fictional data.
          </Typography>
        </Box>
      </Box>
    </CommandCenterShell>
  );
}
