import type { ExtractionDto } from '../types';

const confidenceColor: Record<string, { bg: string; color: string }> = {
  high: { bg: '#dcfce7', color: '#166534' },
  medium: { bg: '#fef9c3', color: '#854d0e' },
  low: { bg: '#fee2e2', color: '#991b1b' },
};

interface Props {
  extraction: ExtractionDto;
  processingTimeMs: number;
  totalCandidates: number;
}

export function ExtractionPanel({ extraction, processingTimeMs, totalCandidates }: Props) {
  const conf = confidenceColor[extraction.confidence] ?? confidenceColor.low;

  return (
    <div style={{ background: '#eff6ff', border: '1px solid #bfdbfe', borderRadius: '0.75rem', padding: '1rem', fontSize: '0.875rem' }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '0.75rem' }}>
        <h3 style={{ fontWeight: 600, color: '#1e40af', margin: 0 }}>AI Extraction</h3>
        <div style={{ display: 'flex', gap: '0.5rem' }}>
          <span style={{ padding: '0.125rem 0.5rem', borderRadius: '9999px', fontSize: '0.75rem', fontWeight: 500, background: conf.bg, color: conf.color }}>
            {extraction.confidence} confidence
          </span>
          <span style={{ padding: '0.125rem 0.5rem', borderRadius: '9999px', fontSize: '0.75rem', background: 'white', border: '1px solid #e5e7eb', color: '#374151' }}>
            via {extraction.method}
          </span>
        </div>
      </div>
      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '0.5rem', marginBottom: '0.5rem' }}>
        {extraction.title && <div><span style={{ color: '#6b7280' }}>Title: </span><strong>{extraction.title}</strong></div>}
        {extraction.author && <div><span style={{ color: '#6b7280' }}>Author: </span><strong>{extraction.author}</strong></div>}
        {extraction.year && <div><span style={{ color: '#6b7280' }}>Year: </span><strong>{extraction.year}</strong></div>}
        {extraction.keywords.length > 0 && <div><span style={{ color: '#6b7280' }}>Keywords: </span><strong>{extraction.keywords.join(', ')}</strong></div>}
      </div>
      {extraction.reasoning && <p style={{ color: '#6b7280', fontStyle: 'italic', fontSize: '0.75rem', margin: '0.25rem 0 0' }}>{extraction.reasoning}</p>}
      <div style={{ marginTop: '0.5rem', paddingTop: '0.5rem', borderTop: '1px solid #bfdbfe', display: 'flex', gap: '1rem', fontSize: '0.75rem', color: '#6b7280' }}>
        <span>{totalCandidates} candidates found</span>
        <span>{processingTimeMs}ms</span>
      </div>
    </div>
  );
}
