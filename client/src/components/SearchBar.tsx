import { useState, type KeyboardEvent } from 'react';

const EXAMPLE_QUERIES = [
  'tolkien hobbit illustrated deluxe 1937',
  'mark huckleberry',
  'austen bennet',
  'twilight meyer',
  'dickens, tale two cities',
];

interface Props {
  onSearch: (query: string) => void;
  isLoading: boolean;
}

export function SearchBar({ onSearch, isLoading }: Props) {
  const [query, setQuery] = useState('');

  const handleSearch = () => {
    if (query.trim()) onSearch(query.trim());
  };

  const handleKeyDown = (e: KeyboardEvent<HTMLInputElement>) => {
    if (e.key === 'Enter') handleSearch();
  };

  return (
    <div style={{ width: '100%', maxWidth: '672px', margin: '0 auto' }}>
      <div style={{ display: 'flex', gap: '0.5rem' }}>
        <input
          type="text"
          value={query}
          onChange={(e) => setQuery(e.target.value)}
          onKeyDown={handleKeyDown}
          placeholder="Enter a messy query: author, title, character name, or any combination..."
          disabled={isLoading}
          style={{
            flex: 1, padding: '0.75rem 1rem', borderRadius: '0.75rem',
            border: '1px solid #d1d5db', outline: 'none', fontSize: '1rem',
            color: '#1f2937', background: 'white', boxShadow: '0 1px 2px rgba(0,0,0,0.05)'
          }}
        />
        <button
          onClick={handleSearch}
          disabled={isLoading || !query.trim()}
          style={{
            padding: '0.75rem 1.5rem', borderRadius: '0.75rem', border: 'none',
            background: isLoading || !query.trim() ? '#93c5fd' : '#2563eb',
            color: 'white', fontWeight: 600, fontSize: '1rem',
            cursor: isLoading || !query.trim() ? 'not-allowed' : 'pointer'
          }}
        >
          {isLoading ? '⏳ Searching…' : 'Find'}
        </button>
      </div>
      <div style={{ marginTop: '0.75rem', display: 'flex', flexWrap: 'wrap', gap: '0.5rem', alignItems: 'center' }}>
        <span style={{ fontSize: '0.75rem', color: '#6b7280' }}>Try:</span>
        {EXAMPLE_QUERIES.map((q) => (
          <button
            key={q}
            onClick={() => { setQuery(q); onSearch(q); }}
            style={{
              fontSize: '0.75rem', padding: '0.25rem 0.5rem', background: '#f3f4f6',
              border: 'none', borderRadius: '0.5rem', color: '#4b5563', cursor: 'pointer'
            }}
          >
            {q}
          </button>
        ))}
      </div>
    </div>
  );
}
