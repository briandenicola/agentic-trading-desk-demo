import { useEffect, useState } from 'react';
import {
  Alert,
  Box,
  Button,
  Chip,
  Collapse,
  Divider,
  LinearProgress,
  Paper,
  Stack,
  TextField,
  Typography,
} from '@mui/material';
import CampaignOutlinedIcon from '@mui/icons-material/CampaignOutlined';
import FormatListBulletedRoundedIcon from '@mui/icons-material/FormatListBulletedRounded';
import EditNoteRoundedIcon from '@mui/icons-material/EditNoteRounded';
import InsightsOutlinedIcon from '@mui/icons-material/InsightsOutlined';
import type { OutreachItem, RankingRationale } from '../../api/client';
import { mint } from '../../theme/theme';

type ApprovalState = 'draft' | 'approved';

interface CallPlanItem {
  cid: string;
  name: string;
  suggestedTopic: string;
  talkingPoints: string[];
  rationale: RankingRationale;
  note: string;
}

interface CallPlanProps {
  outreach: OutreachItem[];
  asOf: string;
}

function toPlanItems(outreach: OutreachItem[]): CallPlanItem[] {
  return [...outreach]
    .sort((left, right) => left.rank - right.rank)
    .map((item) => ({
      cid: item.cid,
      name: item.name,
      suggestedTopic: item.suggestedTopic,
      talkingPoints: [...item.talkingPoints],
      rationale: item.rationale,
      note: '',
    }));
}

function formatScore(score: number): string {
  return `${Math.round(score * 100)}%`;
}

function formatPlanDate(asOf: string): string {
  const parsed = new Date(asOf);
  if (Number.isNaN(parsed.getTime())) {
    return 'today';
  }

  return new Intl.DateTimeFormat('en-US', {
    weekday: 'short',
    month: 'short',
    day: 'numeric',
    timeZone: 'UTC',
  })
    .format(parsed)
    .replace(',', '');
}

function rationaleScores(rationale: RankingRationale) {
  return [
    { label: 'Wallet', value: rationale.walletScore },
    { label: 'Engagement', value: rationale.engagementScore },
    { label: 'Event relevance', value: rationale.eventRelevanceScore },
    { label: 'Composite', value: rationale.compositeScore },
  ];
}

export default function CallPlan({ outreach, asOf }: CallPlanProps) {
  const [items, setItems] = useState<CallPlanItem[]>(() => toPlanItems(outreach));
  const [edited, setEdited] = useState(false);
  const [approvalState, setApprovalState] = useState<ApprovalState>('draft');
  const [sent] = useState(false);
  const [expandedCid, setExpandedCid] = useState<string | null>(null);

  useEffect(() => {
    setItems(toPlanItems(outreach));
    setEdited(false);
    setApprovalState('draft');
    setExpandedCid(null);
  }, [outreach]);

  const markEdited = () => {
    setEdited(true);
    setApprovalState((current) => (current === 'approved' ? 'draft' : current));
  };

  const moveItem = (index: number, direction: -1 | 1) => {
    const nextIndex = index + direction;
    if (nextIndex < 0 || nextIndex >= items.length) {
      return;
    }

    setItems((current) => {
      const next = [...current];
      [next[index], next[nextIndex]] = [next[nextIndex], next[index]];
      return next;
    });
    markEdited();
  };

  const removeClient = (cid: string) => {
    setItems((current) => current.filter((item) => item.cid !== cid));
    markEdited();
  };

  const updateTalkingPoint = (cid: string, pointIndex: number, value: string) => {
    setItems((current) =>
      current.map((item) =>
        item.cid === cid
          ? {
              ...item,
              talkingPoints: item.talkingPoints.map((point, index) =>
                index === pointIndex ? value : point,
              ),
            }
          : item,
      ),
    );
    markEdited();
  };

  const updateNote = (cid: string, value: string) => {
    setItems((current) =>
      current.map((item) => (item.cid === cid ? { ...item, note: value } : item)),
    );
    markEdited();
  };

  const approvePlan = () => {
    setApprovalState('approved');
  };

  return (
    <Paper sx={{ p: 0, overflow: 'hidden', borderTop: `3px solid ${mint.green}` }}>
      {/* Header */}
      <Box sx={{ p: 2.5, borderBottom: `1px solid ${mint.border}` }}>
        <Box sx={{ display: 'flex', justifyContent: 'space-between', gap: 2, flexWrap: 'wrap', alignItems: 'flex-start' }}>
          <Box sx={{ display: 'flex', alignItems: 'center', gap: 1.25 }}>
            <Box
              sx={{
                width: 34,
                height: 34,
                borderRadius: 2,
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
                background: `linear-gradient(135deg, ${mint.green}, ${mint.cyan})`,
                color: '#04101c',
              }}
            >
              <CampaignOutlinedIcon sx={{ fontSize: 19 }} />
            </Box>
            <Box>
              <Typography variant="h6" sx={{ fontWeight: 700, fontSize: 17 }}>
                Your outreach plan
              </Typography>
              <Typography variant="body2" color="text.secondary">
                Call plan for <strong>{formatPlanDate(asOf)}</strong> · {items.length} priority{' '}
                {items.length === 1 ? 'client' : 'clients'}
              </Typography>
            </Box>
          </Box>
          <Stack direction="row" spacing={1} sx={{ alignItems: 'center', flexWrap: 'wrap' }}>
            <Chip
              label={approvalState === 'approved' ? 'approved' : 'editable draft'}
              size="small"
              color={approvalState === 'approved' ? 'success' : 'primary'}
              variant={approvalState === 'approved' ? 'filled' : 'outlined'}
            />
            {edited && <Chip label="edited" size="small" color="warning" variant="outlined" />}
          </Stack>
        </Box>

        <Typography
          variant="caption"
          color="text.secondary"
          data-testid="call-plan-status"
          sx={{ display: 'block', mt: 1 }}
        >
          approvalState={approvalState} · edited={String(edited)} · sent={String(sent)}
        </Typography>
      </Box>

      <Box sx={{ p: 2.5 }}>
        <Stack spacing={2}>
          {approvalState === 'approved' && (
            <Alert severity="success" sx={{ fontSize: '13px' }}>
              Plan approved — ready to dial. Demo-only confirmation: no message or call task was sent.
            </Alert>
          )}

          {items.length === 0 ? (
            <Alert severity="info">No clients remain in the editable call plan.</Alert>
          ) : (
            <Stack spacing={2}>
              {items.map((item, index) => {
                const composite = item.rationale.compositeScore;
                const expanded = expandedCid === item.cid;
                return (
                  <Box
                    key={item.cid}
                    data-testid={`call-plan-item-${item.cid}`}
                    sx={{
                      borderRadius: 2,
                      border: `1px solid ${mint.border}`,
                      bgcolor: 'rgba(255,255,255,0.02)',
                      overflow: 'hidden',
                    }}
                  >
                    {/* Card header */}
                    <Box
                      sx={{
                        p: 2,
                        display: 'flex',
                        justifyContent: 'space-between',
                        gap: 1.5,
                        alignItems: 'flex-start',
                        background: `linear-gradient(135deg, ${mint.violet}10, transparent 70%)`,
                      }}
                    >
                      <Box sx={{ display: 'flex', gap: 1.5, minWidth: 0 }}>
                        <Box
                          sx={{
                            width: 26,
                            height: 26,
                            flexShrink: 0,
                            borderRadius: '50%',
                            display: 'flex',
                            alignItems: 'center',
                            justifyContent: 'center',
                            fontSize: 13,
                            fontWeight: 800,
                            color: '#fff',
                            background: `linear-gradient(135deg, ${mint.violet}, ${mint.magenta})`,
                          }}
                        >
                          {index + 1}
                        </Box>
                        <Box sx={{ minWidth: 0 }}>
                          <Typography variant="body1" sx={{ fontWeight: 700, lineHeight: 1.2 }}>
                            {item.name}
                          </Typography>
                          <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.75, mt: 0.5 }}>
                            <CampaignOutlinedIcon sx={{ fontSize: 14, color: mint.violetBright }} />
                            <Typography variant="caption" sx={{ color: mint.text }}>
                              {item.suggestedTopic}
                            </Typography>
                          </Box>
                        </Box>
                      </Box>
                      <Stack direction="row" spacing={0.75} sx={{ alignItems: 'center', flexShrink: 0 }}>
                        <Chip
                          label={formatScore(composite)}
                          size="small"
                          sx={{
                            height: 22,
                            fontWeight: 800,
                            fontSize: 11,
                            color: mint.green,
                            bgcolor: `${mint.green}1f`,
                            border: `1px solid ${mint.green}55`,
                          }}
                        />
                        <Button
                          type="button"
                          size="small"
                          variant="outlined"
                          disabled={index === 0}
                          onClick={() => moveItem(index, -1)}
                          aria-label={`Move ${item.name} up`}
                          sx={{ minWidth: 32, px: 0 }}
                        >
                          ↑
                        </Button>
                        <Button
                          type="button"
                          size="small"
                          variant="outlined"
                          disabled={index === items.length - 1}
                          onClick={() => moveItem(index, 1)}
                          aria-label={`Move ${item.name} down`}
                          sx={{ minWidth: 32, px: 0 }}
                        >
                          ↓
                        </Button>
                        <Button
                          type="button"
                          size="small"
                          color="error"
                          variant="outlined"
                          onClick={() => removeClient(item.cid)}
                          aria-label={`Remove ${item.name} from plan`}
                          sx={{ textTransform: 'none' }}
                        >
                          Remove
                        </Button>
                      </Stack>
                    </Box>

                    <Divider sx={{ borderColor: mint.borderSoft }} />

                    {/* Talking points */}
                    <Box sx={{ p: 2 }}>
                      <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.75, mb: 1.25 }}>
                        <FormatListBulletedRoundedIcon sx={{ fontSize: 15, color: mint.violetBright }} />
                        <Typography
                          sx={{ fontSize: 11, fontWeight: 800, letterSpacing: '0.6px', color: mint.text }}
                        >
                          TALKING POINTS
                        </Typography>
                        <Chip
                          label={item.talkingPoints.length}
                          size="small"
                          sx={{ height: 17, fontSize: 10, fontWeight: 700, color: mint.textDim, bgcolor: mint.borderSoft }}
                        />
                      </Box>
                      <Stack spacing={1.25}>
                        {item.talkingPoints.map((point, pointIndex) => (
                          <Box key={`${item.cid}-${pointIndex}`} sx={{ display: 'flex', gap: 1, alignItems: 'flex-start' }}>
                            <Box
                              sx={{
                                mt: '10px',
                                width: 18,
                                height: 18,
                                flexShrink: 0,
                                borderRadius: '50%',
                                display: 'flex',
                                alignItems: 'center',
                                justifyContent: 'center',
                                fontSize: 10,
                                fontWeight: 800,
                                color: mint.violetBright,
                                border: `1px solid ${mint.border}`,
                                bgcolor: `${mint.violet}14`,
                              }}
                            >
                              {pointIndex + 1}
                            </Box>
                            <TextField
                              label={`Talking point ${pointIndex + 1} for ${item.name}`}
                              value={point}
                              onChange={(event) => updateTalkingPoint(item.cid, pointIndex, event.target.value)}
                              size="small"
                              fullWidth
                              multiline
                              minRows={2}
                            />
                          </Box>
                        ))}
                      </Stack>

                      <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.75, mt: 2, mb: 1 }}>
                        <EditNoteRoundedIcon sx={{ fontSize: 16, color: mint.textDim }} />
                        <Typography sx={{ fontSize: 11, fontWeight: 800, letterSpacing: '0.6px', color: mint.textDim }}>
                          PERSONAL NOTE
                        </Typography>
                      </Box>
                      <TextField
                        label={`Personal note for ${item.name}`}
                        value={item.note}
                        onChange={(event) => updateNote(item.cid, event.target.value)}
                        placeholder="Add a personal note before calling."
                        size="small"
                        fullWidth
                      />

                      <Button
                        type="button"
                        size="small"
                        onClick={() => setExpandedCid((current) => (current === item.cid ? null : item.cid))}
                        aria-expanded={expanded}
                        aria-controls={`rationale-${item.cid}`}
                        startIcon={<InsightsOutlinedIcon sx={{ fontSize: 16 }} />}
                        sx={{ textTransform: 'none', mt: 1.5, color: mint.textDim }}
                      >
                        {expanded ? 'Hide ranking rationale' : 'Why this ranking?'}
                      </Button>
                      <Collapse in={expanded} unmountOnExit>
                        <Box
                          id={`rationale-${item.cid}`}
                          sx={{
                            mt: 1,
                            p: 2,
                            borderRadius: 2,
                            border: `1px solid ${mint.border}`,
                            bgcolor: `${mint.violet}10`,
                          }}
                        >
                          <Stack spacing={1} sx={{ mb: 1.5 }}>
                            {rationaleScores(item.rationale).map((score) => (
                              <Box key={score.label}>
                                <Box sx={{ display: 'flex', justifyContent: 'space-between', mb: 0.5 }}>
                                  <Typography variant="caption" color="text.secondary">
                                    {score.label}
                                  </Typography>
                                  <Typography variant="caption" sx={{ fontWeight: 700 }}>
                                    {formatScore(score.value)}
                                  </Typography>
                                </Box>
                                <LinearProgress
                                  variant="determinate"
                                  value={Math.max(0, Math.min(100, score.value * 100))}
                                  color={score.label === 'Composite' ? 'success' : 'primary'}
                                  sx={{ height: 6, borderRadius: 99, bgcolor: 'rgba(255,255,255,0.1)' }}
                                />
                              </Box>
                            ))}
                          </Stack>
                          <Typography variant="caption" color="text.secondary">
                            {item.rationale.explanation}
                          </Typography>
                        </Box>
                      </Collapse>
                    </Box>
                  </Box>
                );
              })}
            </Stack>
          )}

          <Divider sx={{ borderColor: mint.borderSoft }} />

          <Stack direction={{ xs: 'column', sm: 'row' }} spacing={1.5} sx={{ alignItems: 'center' }}>
            <Button
              type="button"
              variant="contained"
              color="success"
              onClick={approvePlan}
              disabled={approvalState === 'approved'}
              sx={{ textTransform: 'none', fontWeight: 600 }}
            >
              {approvalState === 'approved' ? '✓ Plan approved' : 'Approve plan'}
            </Button>
            <Typography variant="caption" color="text.secondary" sx={{ flex: 1 }}>
              Human-in-the-loop: the VP edits, removes, or adds a personal note before any call. Nothing
              is sent automatically.
            </Typography>
          </Stack>
        </Stack>
      </Box>
    </Paper>
  );
}
