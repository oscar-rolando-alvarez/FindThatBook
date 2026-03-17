export function LoadingSkeleton() {
  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: '1rem' }}>
      {[1, 2, 3].map((i) => (
        <div key={i} style={{ background: 'white', borderRadius: '0.75rem', border: '1px solid #e5e7eb', padding: '1rem', display: 'flex', gap: '1rem', opacity: 1 - i * 0.15 }}>
          <div style={{ width: '64px', height: '96px', background: '#e5e7eb', borderRadius: '0.5rem', flexShrink: 0 }} />
          <div style={{ flex: 1, display: 'flex', flexDirection: 'column', gap: '0.5rem' }}>
            <div style={{ height: '1.25rem', background: '#e5e7eb', borderRadius: '0.25rem', width: '75%' }} />
            <div style={{ height: '1rem', background: '#e5e7eb', borderRadius: '0.25rem', width: '50%' }} />
            <div style={{ height: '0.75rem', background: '#e5e7eb', borderRadius: '0.25rem', width: '25%' }} />
            <div style={{ height: '1rem', background: '#e5e7eb', borderRadius: '0.25rem', width: '100%' }} />
          </div>
        </div>
      ))}
    </div>
  );
}
