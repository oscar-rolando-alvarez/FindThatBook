import { useState } from 'react';
import { QueryClient, QueryClientProvider, useMutation } from '@tanstack/react-query';
import { SearchBar } from './components/SearchBar';
import { ResultCard } from './components/ResultCard';
import { ExtractionPanel } from './components/ExtractionPanel';
import { LoadingSkeleton } from './components/LoadingSkeleton';
import { searchBooks } from './api/bookApi';
import type { BookSearchResponse } from './types';

const queryClient = new QueryClient();

export default function App() {
  return (
    <QueryClientProvider client={queryClient}>
      <FindThatBook />
    </QueryClientProvider>
  );
}

function FindThatBook() {
  const [response, setResponse] = useState<BookSearchResponse | null>(null);
  const [error, setError] = useState<string | null>(null);

  const mutation = useMutation({
    mutationFn: (query: string) => searchBooks({ query, maxResults: 5 }),
    onSuccess: (data) => {
      setResponse(data);
      setError(null);
    },
    onError: (err: Error) => {
      setError(err.message || 'Search failed. Please try again.');
      setResponse(null);
    },
  });

  const handleSearch = (query: string) => {
    mutation.mutate(query);
  };

  return (
    <div style={{ minHeight: '100vh', background: 'linear-gradient(135deg, #f8fafc 0%, #eff6ff 100%)', padding: '0 1rem' }}>
      <div style={{ maxWidth: '672px', margin: '0 auto', paddingTop: '3rem', paddingBottom: '3rem' }}>
        {/* Header */}
        <div style={{ textAlign: 'center', marginBottom: '2.5rem' }}>
          <h1 style={{ fontSize: '2.25rem', fontWeight: 700, color: '#111827', marginBottom: '0.5rem' }}>
            📚 Find That Book
          </h1>
          <p style={{ color: '#6b7280', fontSize: '1.125rem' }}>
            AI-powered book discovery. Give us a messy query — we'll find the book.
          </p>
        </div>

        {/* Search */}
        <SearchBar onSearch={handleSearch} isLoading={mutation.isPending} />

        {/* Results */}
        <div style={{ marginTop: '2rem' }}>
          {mutation.isPending && <LoadingSkeleton />}

          {error && (
            <div style={{ background: '#fef2f2', border: '1px solid #fecaca', borderRadius: '0.75rem', padding: '1rem', color: '#b91c1c' }}>
              <strong>Error:</strong> {error}
            </div>
          )}

          {response && !mutation.isPending && (
            <div style={{ display: 'flex', flexDirection: 'column', gap: '1.5rem' }}>
              <ExtractionPanel
                extraction={response.extraction}
                processingTimeMs={response.processingTimeMs}
                totalCandidates={response.totalCandidates}
              />

              {response.results.length === 0 ? (
                <div style={{ textAlign: 'center', padding: '2.5rem 0', color: '#6b7280' }}>
                  <p style={{ fontSize: '1.125rem' }}>No matching books found.</p>
                  <p style={{ fontSize: '0.875rem', marginTop: '0.25rem' }}>Try a different query or add more details.</p>
                </div>
              ) : (
                <div>
                  <h2 style={{ fontWeight: 600, color: '#374151', fontSize: '0.75rem', textTransform: 'uppercase', letterSpacing: '0.1em', marginBottom: '0.75rem' }}>
                    {response.results.length} result{response.results.length > 1 ? 's' : ''}
                  </h2>
                  <div style={{ display: 'flex', flexDirection: 'column', gap: '0.75rem' }}>
                    {response.results.map((book) => (
                      <ResultCard key={book.openLibraryWorkId + book.matchRank} book={book} />
                    ))}
                  </div>
                </div>
              )}
            </div>
          )}
        </div>

        <p style={{ textAlign: 'center', fontSize: '0.75rem', color: '#9ca3af', marginTop: '3rem' }}>
          Powered by{' '}
          <a href="https://openlibrary.org" target="_blank" rel="noopener noreferrer" style={{ textDecoration: 'underline' }}>
            Open Library
          </a>{' '}
          &amp; Google Gemini AI
        </p>
      </div>
    </div>
  );
}
