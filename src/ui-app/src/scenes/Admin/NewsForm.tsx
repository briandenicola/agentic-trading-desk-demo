import { useEffect, useState } from 'react';
import {
  Alert,
  Autocomplete,
  Box,
  Button,
  Chip,
  MenuItem,
  Stack,
  TextField,
  Typography,
} from '@mui/material';
import SendRoundedIcon from '@mui/icons-material/SendRounded';
import { mint } from '../../theme/theme';
import { listCustomers, type AdminNewsSubmission, type CustomerOption } from '../../api/client';

interface NewsFormProps {
  onSubmit: (submission: AdminNewsSubmission) => Promise<void> | void;
  submitting?: boolean;
}

const SEVERITIES: AdminNewsSubmission['severity'][] = ['low', 'medium', 'high'];
const TYPES: AdminNewsSubmission['type'][] = [
  'macro_rate',
  'sector',
  'issuer_credit',
  'client_headline',
];
const DIRECTIONS: NonNullable<AdminNewsSubmission['direction']>[] = [
  'negative',
  'neutral',
  'positive',
];

function parseList(value: string): string[] {
  return value
    .split(',')
    .map((v) => v.trim())
    .filter((v) => v.length > 0);
}

/** Resolve a selected type-ahead value (a picked customer or a free-typed string) to its ID. */
function toCustomerId(value: CustomerOption | string): string {
  return typeof value === 'string' ? value.trim() : value.customerId;
}

/**
 * Admin news composer (002 US3, FR-014/FR-015). Client-side validation mirrors the server
 * re-validation: headline + summary required, and at least one affected-entity selector. An
 * incomplete submission is rejected with a clear message and nothing is ingested.
 */
export default function NewsForm({ onSubmit, submitting = false }: NewsFormProps) {
  const [headline, setHeadline] = useState('');
  const [summary, setSummary] = useState('');
  const [source, setSource] = useState('');
  const [severity, setSeverity] = useState<AdminNewsSubmission['severity']>('high');
  const [type, setType] = useState<AdminNewsSubmission['type']>('sector');
  const [direction, setDirection] = useState<NonNullable<AdminNewsSubmission['direction']>>('negative');
  const [customerIds, setCustomerIds] = useState<(CustomerOption | string)[]>([]);
  const [customerOptions, setCustomerOptions] = useState<CustomerOption[]>([]);
  const [tickers, setTickers] = useState('');
  const [sectors, setSectors] = useState('');
  const [issuers, setIssuers] = useState('');
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let active = true;
    listCustomers()
      .then((options) => {
        if (active) setCustomerOptions(options);
      })
      .catch(() => {
        // Type-ahead is a convenience; the field still accepts free-typed IDs if the lookup fails.
      });
    return () => {
      active = false;
    };
  }, []);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);

    const affectedEntities = {
      customerIds: customerIds.map(toCustomerId).filter((v) => v.length > 0),
      tickers: parseList(tickers),
      sectors: parseList(sectors),
      issuers: parseList(issuers),
    };
    const hasSelector =
      affectedEntities.customerIds.length > 0 ||
      affectedEntities.tickers.length > 0 ||
      affectedEntities.sectors.length > 0 ||
      affectedEntities.issuers.length > 0;

    if (!headline.trim()) {
      setError('Headline is required.');
      return;
    }
    if (!summary.trim()) {
      setError('Summary is required.');
      return;
    }
    if (!hasSelector) {
      setError('Add at least one affected entity (customer, ticker, sector, or issuer).');
      return;
    }

    await onSubmit({
      headline: headline.trim(),
      summary: summary.trim(),
      source: source.trim() || undefined,
      severity,
      type,
      direction,
      affectedEntities,
    });
  };

  return (
    <Box component="form" onSubmit={handleSubmit} noValidate data-testid="news-form">
      <Stack spacing={2}>
        <TextField
          label="Headline"
          value={headline}
          onChange={(e) => setHeadline(e.target.value)}
          fullWidth
          size="small"
        />
        <TextField
          label="Summary"
          value={summary}
          onChange={(e) => setSummary(e.target.value)}
          fullWidth
          size="small"
          multiline
          minRows={2}
        />
        <TextField
          label="Source (optional)"
          value={source}
          onChange={(e) => setSource(e.target.value)}
          fullWidth
          size="small"
        />

        <Stack direction={{ xs: 'column', sm: 'row' }} spacing={2}>
          <TextField
            select
            label="Severity"
            value={severity}
            onChange={(e) => setSeverity(e.target.value as AdminNewsSubmission['severity'])}
            fullWidth
            size="small"
          >
            {SEVERITIES.map((s) => (
              <MenuItem key={s} value={s}>
                {s}
              </MenuItem>
            ))}
          </TextField>
          <TextField
            select
            label="Type"
            value={type}
            onChange={(e) => setType(e.target.value as AdminNewsSubmission['type'])}
            fullWidth
            size="small"
          >
            {TYPES.map((t) => (
              <MenuItem key={t} value={t}>
                {t}
              </MenuItem>
            ))}
          </TextField>
          <TextField
            select
            label="Direction"
            value={direction}
            onChange={(e) =>
              setDirection(e.target.value as NonNullable<AdminNewsSubmission['direction']>)
            }
            fullWidth
            size="small"
          >
            {DIRECTIONS.map((d) => (
              <MenuItem key={d} value={d}>
                {d}
              </MenuItem>
            ))}
          </TextField>
        </Stack>

        <Box>
          <Typography
            variant="caption"
            sx={{ color: mint.textDim, display: 'block', mb: 1, textTransform: 'uppercase', letterSpacing: '0.7px' }}
          >
            Affected entities — at least one (comma-separated)
          </Typography>
          <Stack direction={{ xs: 'column', sm: 'row' }} spacing={2}>
            <Autocomplete
              multiple
              freeSolo
              size="small"
              fullWidth
              options={customerOptions}
              value={customerIds}
              onChange={(_, value) => setCustomerIds(value)}
              getOptionLabel={(option) =>
                typeof option === 'string' ? option : `${option.customerId} — ${option.name}`
              }
              isOptionEqualToValue={(option, value) =>
                toCustomerId(option) === toCustomerId(value)
              }
              renderInput={(params) => (
                <TextField
                  {...params}
                  label="Customer IDs"
                  placeholder={customerIds.length === 0 ? 'Search by ID or name…' : ''}
                />
              )}
            />
            <TextField
              label="Tickers"
              placeholder="SEC-3003"
              value={tickers}
              onChange={(e) => setTickers(e.target.value)}
              fullWidth
              size="small"
            />
          </Stack>
          <Stack direction={{ xs: 'column', sm: 'row' }} spacing={2} sx={{ mt: 2 }}>
            <TextField
              label="Sectors"
              placeholder="Agriculture, Manufacturing"
              value={sectors}
              onChange={(e) => setSectors(e.target.value)}
              fullWidth
              size="small"
            />
            <TextField
              label="Issuers"
              value={issuers}
              onChange={(e) => setIssuers(e.target.value)}
              fullWidth
              size="small"
            />
          </Stack>
        </Box>

        {error && (
          <Alert severity="error" data-testid="news-form-error">
            {error}
          </Alert>
        )}

        <Box sx={{ display: 'flex', alignItems: 'center', gap: 2 }}>
          <Button
            type="submit"
            variant="contained"
            disabled={submitting}
            startIcon={<SendRoundedIcon />}
            sx={{ fontWeight: 600 }}
          >
            {submitting ? 'Injecting...' : 'Inject news item'}
          </Button>
          <Chip
            label="scope: intraday · origin: admin"
            size="small"
            variant="outlined"
            sx={{ fontSize: '11px', color: mint.textDim }}
          />
        </Box>
      </Stack>
    </Box>
  );
}
