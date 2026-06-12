import { Box, Chip, Paper, Stack, Typography } from '@mui/material';
import ChatBubbleRoundedIcon from '@mui/icons-material/ChatBubbleRounded';
import type { TdPriorityCall, WhyNowDriver } from '../../api/client';
import { mint } from '../../theme/theme';

// Left-border accent per priority band (red → green), mirroring the briefing cards.
const BAND_COLOR: Record<number, string> = {
  1: '#ff5a5a',
  2: '#ffb547',
  3: '#ffd23f',
  4: '#3ddc97',
};

const KIND_COLOR: Record<string, string> = {
  news: mint.cyan,
  research: mint.violetBright,
  rfq: mint.amber,
  inquiry: mint.green,
  axe: mint.magenta,
  holding: mint.textDim,
  crm: mint.textDim,
};

const KIND_LABEL: Record<string, string> = {
  news: 'NEWS',
  research: 'RESEARCH',
  rfq: 'OPEN RFQ',
  inquiry: 'INQUIRY',
  axe: 'OUR AXE',
  holding: 'HOLDING',
  crm: 'CRM',
};

function WhyNowRow({ driver }: { driver: WhyNowDriver }) {
  const color = KIND_COLOR[driver.kind] ?? mint.textDim;
  return (
    <Box sx={{ display: 'flex', gap: 1, alignItems: 'flex-start' }}>
      <Chip
        label={KIND_LABEL[driver.kind] ?? driver.kind.toUpperCase()}
        size="small"
        sx={{
          mt: 0.25,
          fontSize: 9,
          fontWeight: 700,
          height: 18,
          letterSpacing: '0.5px',
          color,
          bgcolor: `${color}1f`,
          border: `1px solid ${color}3a`,
          '& .MuiChip-label': { px: 0.75 },
        }}
      />
      <Box sx={{ minWidth: 0 }}>
        <Typography variant="body2" sx={{ fontWeight: 600 }}>
          {driver.label}
        </Typography>
        {driver.detail && (
          <Typography variant="caption" sx={{ color: mint.textDim, display: 'block' }}>
            {driver.detail}
          </Typography>
        )}
      </Box>
    </Box>
  );
}

interface TdCallCardProps {
  call: TdPriorityCall;
  /** Highlights the card when its rank/priority just changed from a live event. */
  flash?: boolean;
  /** When provided, renders an "Open Chat" affordance seeded with this client's context. */
  onOpenChat?: (seed: string) => void;
}

export default function TdCallCard({ call, flash, onOpenChat }: TdCallCardProps) {
  const accent = BAND_COLOR[call.priority] ?? BAND_COLOR[4];
  const meta = [call.clientType, call.region, call.preferredAssetClass, call.clientId]
    .filter(Boolean)
    .join(' · ');
  const hasEvents = !!call.drivingEvents && call.drivingEvents.length > 0;

  return (
    <Paper
      data-testid={`td-call-${call.clientId}`}
      sx={{
        p: 2,
        borderLeft: `5px solid ${accent}`,
        bgcolor: 'rgba(255,255,255,0.02)',
        ...(flash || hasEvents
          ? {
              boxShadow: `0 0 0 1px ${mint.cyan}66, 0 8px 30px ${mint.cyan}26`,
              borderColor: `${mint.cyan}66`,
            }
          : {}),
      }}
    >
      <Box sx={{ display: 'flex', alignItems: 'baseline', justifyContent: 'space-between', gap: 2 }}>
        <Typography variant="subtitle1" sx={{ fontWeight: 700 }}>
          {call.rank}. {call.clientName}
          <Box
            component="span"
            sx={{
              ml: 1,
              fontSize: 10,
              fontWeight: 800,
              letterSpacing: '0.5px',
              color: accent,
              verticalAlign: 'middle',
            }}
          >
            P{call.priority}
          </Box>
        </Typography>
        <Chip
          label={`Score ${call.rationale.compositeScore}`}
          size="small"
          sx={{ fontWeight: 700, bgcolor: 'rgba(255,255,255,0.08)' }}
        />
      </Box>

      <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mt: 0.25 }}>
        {meta}
      </Typography>

      {hasEvents && (
        <Box
          data-testid={`td-driving-events-${call.clientId}`}
          sx={{
            mt: 1,
            p: 1,
            borderRadius: 1,
            border: `1px solid ${mint.cyan}59`,
            bgcolor: `${mint.cyan}14`,
          }}
        >
          <Typography variant="caption" sx={{ fontWeight: 800, letterSpacing: '0.08em', color: mint.cyan }}>
            ⚡ RE-RANKED BY LIVE EVENTS
          </Typography>
          <Box component="ul" sx={{ pl: 2.5, my: 0.5, '& li': { mb: 0.25 } }}>
            {call.drivingEvents!.map((ev) => (
              <Typography component="li" key={ev.eventId} variant="body2" sx={{ color: mint.text }}>
                {ev.rationale}
              </Typography>
            ))}
          </Box>
        </Box>
      )}

      {call.whyNow.length > 0 && (
        <Stack spacing={0.75} sx={{ mt: 1.25 }}>
          {call.whyNow.map((d, i) => (
            <WhyNowRow key={`${d.kind}-${d.refId ?? i}`} driver={d} />
          ))}
        </Stack>
      )}

      {call.tradeIdeas.length > 0 && (
        <Box sx={{ mt: 1.25 }}>
          <Typography
            variant="caption"
            sx={{ fontWeight: 700, letterSpacing: '0.06em', color: mint.violetBright }}
          >
            TRADE IDEAS
          </Typography>
          <Stack spacing={0.5} sx={{ mt: 0.5 }}>
            {call.tradeIdeas.map((t) => (
              <Box key={`${t.securityId}-${t.side}`} sx={{ display: 'flex', gap: 1, alignItems: 'baseline' }}>
                <Chip
                  label={t.side}
                  size="small"
                  sx={{
                    fontSize: 10,
                    fontWeight: 700,
                    height: 18,
                    color: t.side.toLowerCase() === 'buy' ? mint.green : mint.red,
                    bgcolor: t.side.toLowerCase() === 'buy' ? `${mint.green}1f` : `${mint.red}1f`,
                  }}
                />
                <Typography variant="body2">
                  <strong>{t.securityName}</strong>
                  {t.level ? ` · ${t.level}` : ''}
                  {t.rationale ? (
                    <Box component="span" sx={{ color: mint.textDim }}>
                      {' '}
                      — {t.rationale}
                    </Box>
                  ) : null}
                </Typography>
              </Box>
            ))}
          </Stack>
        </Box>
      )}

      {call.talkingPoints.length > 0 && (
        <Box component="ul" sx={{ pl: 2.5, my: 1.25, '& li': { mb: 0.5 } }}>
          {call.talkingPoints.map((tp, i) => (
            <Typography component="li" key={i} variant="body2" color="text.secondary">
              {tp}
            </Typography>
          ))}
        </Box>
      )}

      {call.personalNote && (
        <Typography variant="caption" sx={{ display: 'block', mt: 0.5, color: mint.textDim, fontStyle: 'italic' }}>
          CRM: {call.personalNote}
        </Typography>
      )}

      <Typography variant="body2" sx={{ mt: 1 }}>
        <Box component="span" sx={{ fontWeight: 700, color: 'primary.main' }}>
          Suggested action:{' '}
        </Box>
        {call.suggestedAction}
      </Typography>

      {onOpenChat && (
        <Box
          onClick={() =>
            onOpenChat(
              `Tell me about ${call.clientName} (${call.clientId}) — why are they my top call this morning, and what should I lead with?`,
            )
          }
          role="button"
          aria-label={`Open chat about ${call.clientName}`}
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
    </Paper>
  );
}
