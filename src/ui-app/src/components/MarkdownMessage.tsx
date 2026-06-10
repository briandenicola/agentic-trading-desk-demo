import ReactMarkdown from 'react-markdown';
import remarkGfm from 'remark-gfm';
import { Box } from '@mui/material';
import { mint } from '../theme/theme';

interface MarkdownMessageProps {
  content: string;
  /** Base body font size in px (chat bubbles ~13, the /chat scene ~14). */
  fontSize?: number;
}

/**
 * Renders assistant chat content as GitHub-flavoured Markdown (bold, ordered &
 * unordered lists, headings, code, links, tables) styled to the M.INT theme.
 * Raw HTML is not rendered, so model output is safe to display.
 */
export default function MarkdownMessage({ content, fontSize = 13 }: MarkdownMessageProps) {
  return (
    <Box
      sx={{
        fontSize,
        lineHeight: 1.55,
        color: 'inherit',
        wordBreak: 'break-word',
        '& > :first-of-type': { mt: 0 },
        '& > :last-child': { mb: 0 },
        '& p': { my: 0.75 },
        '& ul, & ol': { my: 0.75, pl: 2.5 },
        '& li': { mb: 0.25 },
        '& li::marker': { color: mint.textDim },
        '& strong': { fontWeight: 700, color: mint.text },
        '& em': { fontStyle: 'italic' },
        '& a': { color: mint.cyan, textDecoration: 'underline' },
        '& code': {
          fontFamily: 'ui-monospace, SFMono-Regular, Menlo, monospace',
          fontSize: '0.86em',
          bgcolor: mint.bgAlt,
          px: 0.5,
          py: 0.1,
          borderRadius: 0.75,
          border: `1px solid ${mint.borderSoft}`,
        },
        '& pre': {
          my: 0.75,
          p: 1.25,
          bgcolor: mint.bgAlt,
          borderRadius: 1.5,
          border: `1px solid ${mint.borderSoft}`,
          overflowX: 'auto',
        },
        '& pre code': { bgcolor: 'transparent', border: 'none', p: 0 },
        '& h1, & h2, & h3, & h4': { fontWeight: 700, color: mint.text, lineHeight: 1.3, mt: 1, mb: 0.5 },
        '& h1': { fontSize: '1.3em' },
        '& h2': { fontSize: '1.18em' },
        '& h3': { fontSize: '1.06em' },
        '& h4': { fontSize: '1em' },
        '& blockquote': {
          my: 0.75,
          pl: 1.5,
          borderLeft: `3px solid ${mint.border}`,
          color: mint.textDim,
        },
        '& hr': { border: 'none', borderTop: `1px solid ${mint.borderSoft}`, my: 1 },
        '& table': { borderCollapse: 'collapse', width: '100%', my: 0.75, fontSize: '0.95em' },
        '& th, & td': { border: `1px solid ${mint.borderSoft}`, px: 1, py: 0.5, textAlign: 'left' },
        '& th': { bgcolor: mint.bgAlt, fontWeight: 700 },
        '& img': { maxWidth: '100%' },
      }}
    >
      <ReactMarkdown remarkPlugins={[remarkGfm]}>{content}</ReactMarkdown>
    </Box>
  );
}
