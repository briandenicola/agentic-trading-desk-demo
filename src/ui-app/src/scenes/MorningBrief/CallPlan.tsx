import { useEffect, useState } from 'react';
import {
  Alert,
  Box,
  Button,
  Chip,
  Divider,
  Paper,
  Stack,
  TextField,
  Typography,
} from '@mui/material';
import type { OutreachItem } from '../../api/client';

type ApprovalState = 'draft' | 'approved';

interface CallPlanItem {
  cid: string;
  name: string;
  suggestedTopic: string;
  talkingPoints: string[];
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
      note: '',
    }));
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

export default function CallPlan({ outreach, asOf }: CallPlanProps) {
  const [items, setItems] = useState<CallPlanItem[]>(() => toPlanItems(outreach));
  const [edited, setEdited] = useState(false);
  const [approvalState, setApprovalState] = useState<ApprovalState>('draft');
  const [sent, setSent] = useState(false);

  useEffect(() => {
    setItems(toPlanItems(outreach));
    setEdited(false);
    setApprovalState('draft');
    setSent(false);
  }, [outreach]);

  const markEdited = () => {
    setEdited(true);
    setSent(false);
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
    setSent(false);
  };

  return (
    <Paper sx={{ p: 3, borderLeft: '4px solid', borderLeftColor: 'success.main' }}>
      <Stack spacing={2}>
        <Box sx={{ display: 'flex', justifyContent: 'space-between', gap: 2, flexWrap: 'wrap' }}>
          <Box>
            <Typography variant="h6" sx={{ fontWeight: 500 }}>
              ✅ Your outreach plan{' '}
              <Typography
                component="span"
                variant="body2"
                sx={{ color: approvalState === 'approved' ? 'success.main' : 'primary.main' }}
              >
                · {approvalState === 'approved' ? 'approved' : 'editable'}
              </Typography>
            </Typography>
            <Typography variant="body2" color="text.secondary">
              Call plan for <strong>{formatPlanDate(asOf)}</strong> · {items.length} priority{' '}
              {items.length === 1 ? 'client' : 'clients'}
            </Typography>
          </Box>
          <Stack direction="row" spacing={1} sx={{ alignItems: 'center', flexWrap: 'wrap' }}>
            <Chip
              label={approvalState === 'approved' ? 'approved' : 'editable draft'}
              size="small"
              color={approvalState === 'approved' ? 'success' : 'primary'}
              variant={approvalState === 'approved' ? 'filled' : 'outlined'}
            />
            {edited && <Chip label="edited" size="small" color="warning" variant="outlined" />}
            <Chip label="sent=false" size="small" color="success" variant="outlined" />
          </Stack>
        </Box>

        <Typography variant="caption" color="text.secondary" data-testid="call-plan-status">
          approvalState={approvalState} · edited={String(edited)} · sent={String(sent)}
        </Typography>

        {approvalState === 'approved' && (
          <Alert severity="success" sx={{ fontSize: '13px' }}>
            Plan approved — ready to dial. Demo-only confirmation: no message or call task was sent.
          </Alert>
        )}

        {items.length === 0 ? (
          <Alert severity="info">No clients remain in the editable call plan.</Alert>
        ) : (
          <Stack spacing={1.5}>
            {items.map((item, index) => (
              <Box
                key={item.cid}
                data-testid={`call-plan-item-${item.cid}`}
                sx={{
                  p: 2,
                  border: '1px solid rgba(255,255,255,0.1)',
                  borderRadius: 2,
                  bgcolor: 'rgba(255,255,255,0.03)',
                }}
              >
                <Stack spacing={1.5}>
                  <Box sx={{ display: 'flex', justifyContent: 'space-between', gap: 2 }}>
                    <Box>
                      <Typography variant="body2" sx={{ fontWeight: 700 }}>
                        {index + 1}. {item.name}
                      </Typography>
                      <Typography variant="caption" color="text.secondary">
                        {item.suggestedTopic}
                      </Typography>
                    </Box>
                    <Stack direction="row" spacing={1} sx={{ alignItems: 'flex-start' }}>
                      <Button
                        type="button"
                        size="small"
                        variant="outlined"
                        disabled={index === 0}
                        onClick={() => moveItem(index, -1)}
                        aria-label={`Move ${item.name} up`}
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
                      >
                        Remove
                      </Button>
                    </Stack>
                  </Box>

                  <Stack spacing={1}>
                    {item.talkingPoints.map((point, pointIndex) => (
                      <TextField
                        key={`${item.cid}-${pointIndex}`}
                        label={`Talking point ${pointIndex + 1} for ${item.name}`}
                        value={point}
                        onChange={(event) =>
                          updateTalkingPoint(item.cid, pointIndex, event.target.value)
                        }
                        size="small"
                        fullWidth
                        multiline
                        minRows={2}
                      />
                    ))}
                    <TextField
                      label={`Personal note for ${item.name}`}
                      value={item.note}
                      onChange={(event) => updateNote(item.cid, event.target.value)}
                      placeholder="Add a personal note before calling."
                      size="small"
                      fullWidth
                    />
                  </Stack>
                </Stack>
              </Box>
            ))}
          </Stack>
        )}

        <Divider />

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
    </Paper>
  );
}
