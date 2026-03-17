import type { BookResultDto } from '../types';

const matchTypeStyle: Record<string, { label: string; bg: string; color: string }> = {
  ExactTitlePrimaryAuthor: { label: 'Exact Match', bg: '#dcfce7', color: '#166534' },
  ExactTitleContributorAuthor: { label: 'Exact Title', bg: '#dbeafe', color: '#1e40af' },
  NearMatchTitleAuthor: { label: 'Near Match', bg: '#fef9c3', color: '#854d0e' },
  AuthorOnlyFallback: { label: 'Author Match', bg: '#f3e8ff', color: '#6b21a8' },
  KeywordCandidate: { label: 'Keyword Match', bg: '#f3f4f6', color: '#374151' },
};

interface Props {
  book: BookResultDto;
}

export function ResultCard({ book }: Props) {
  const mt = matchTypeStyle[book.matchType] ?? { label: book.matchType, bg: '#f3f4f6', color: '#374151' };

  return (
    <div style={{
      background: 'white', borderRadius: '0.75rem', boxShadow: '0 1px 3px rgba(0,0,0,0.08)',
      border: '1px solid #e5e7eb', padding: '1rem', display: 'flex', gap: '1rem'
    }}>
      {/* Cover */}
      <div style={{ flexShrink: 0 }}>
        {book.coverImageUrl ? (
          <img
            src={book.coverImageUrl}
            alt={`Cover of ${book.title}`}
            style={{ width: '64px', height: '96px', objectFit: 'cover', borderRadius: '0.5rem', boxShadow: '0 1px 3px rgba(0,0,0,0.2)' }}
            onError={(e) => { (e.target as HTMLImageElement).style.display = 'none'; }}
          />
        ) : (
          <div style={{ width: '64px', height: '96px', background: '#f9fafb', borderRadius: '0.5rem', display: 'flex', alignItems: 'center', justifyContent: 'center', color: '#9ca3af', fontSize: '0.7rem', textAlign: 'center', padding: '0.25rem' }}>
            No cover
          </div>
        )}
      </div>

      {/* Details */}
      <div style={{ flex: 1, minWidth: 0 }}>
        <div style={{ display: 'flex', alignItems: 'flex-start', justifyContent: 'space-between', gap: '0.5rem' }}>
          <div>
            <h3 style={{ fontWeight: 700, color: '#111827', margin: 0, fontSize: '1rem', lineHeight: '1.25' }}>{book.title}</h3>
            {book.author && <p style={{ color: '#4b5563', fontSize: '0.875rem', margin: '0.25rem 0 0' }}>{book.author}</p>}
          </div>
          <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', flexShrink: 0 }}>
            <span style={{ color: '#9ca3af', fontSize: '0.875rem', fontWeight: 500 }}>#{book.matchRank}</span>
            <span style={{ fontSize: '0.75rem', padding: '0.125rem 0.5rem', borderRadius: '9999px', fontWeight: 500, background: mt.bg, color: mt.color }}>
              {mt.label}
            </span>
          </div>
        </div>

        {book.firstPublishYear && (
          <p style={{ color: '#6b7280', fontSize: '0.75rem', margin: '0.25rem 0 0' }}>First published: {book.firstPublishYear}</p>
        )}

        <p style={{ color: '#374151', fontSize: '0.875rem', margin: '0.5rem 0 0', fontStyle: 'italic' }}>{book.explanation}</p>

        <div style={{ marginTop: '0.5rem' }}>
          <a href={book.openLibraryUrl} target="_blank" rel="noopener noreferrer"
            style={{ color: '#2563eb', fontSize: '0.75rem', textDecoration: 'underline' }}>
            Open Library →
          </a>
        </div>
      </div>
    </div>
  );
}
