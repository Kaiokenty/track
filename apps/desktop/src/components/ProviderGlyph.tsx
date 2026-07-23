type ProviderId = "cursor" | "openai" | "anthropic" | "xai";

export function ProviderGlyph({ id }: { id: string }) {
  const pid = id as ProviderId;
  return (
    <div className="glyph" aria-hidden>
      {pid === "cursor" && (
        <svg viewBox="0 0 24 24" width="16" height="16" fill="currentColor">
          <path d="M6 4h8l4 4v12H6V4zm2 2v14h10V9h-4V6H8zm2 2h2v2h-2V8z" />
        </svg>
      )}
      {pid === "openai" && (
        <svg viewBox="0 0 24 24" width="16" height="16" fill="currentColor">
          <path d="M12 3c-1.2 0-2.3.4-3.2 1.1L6.5 2.5 5 4l2.3 2.3C6.5 7.5 6 8.7 6 10c0 3.3 2.7 6 6 6 .8 0 1.5-.2 2.2-.5l2.3 2.3 1.5-1.5-2.3-2.3c.8-.8 1.3-2 1.3-3.2 0-1.2-.4-2.3-1.1-3.2L18.5 6 17 4.5l-2.3 2.3C14.3 3.4 13.2 3 12 3zm0 2c2.2 0 4 1.8 4 4s-1.8 4-4 4-4-1.8-4-4 1.8-4 4-4z" />
        </svg>
      )}
      {pid === "anthropic" && (
        <svg viewBox="0 0 24 24" width="16" height="16" fill="currentColor">
          <path d="M12 3 4 21h3.5l1.2-3h7.6l1.2 3H21L12 3zm0 5.2 2.8 7.2H9.2L12 8.2z" />
        </svg>
      )}
      {pid === "xai" && (
        <svg viewBox="0 0 24 24" width="16" height="16" fill="currentColor">
          <path d="M5 5h6l4 7-4 7H5l4-7-4-7zm8 0h6l-4 7 4 7h-6l-4-7 4-7z" />
        </svg>
      )}
      {!["cursor", "openai", "anthropic", "xai"].includes(pid) && (
        <span>{id.slice(0, 2).toUpperCase()}</span>
      )}
    </div>
  );
}
