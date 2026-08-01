/** The console mark: a pole and a pennant — the spine, and the thing it raises. */
export function Mark({ size = 22 }: { size?: number }) {
  return (
    <svg
      className="brand__glyph"
      width={size}
      height={size}
      viewBox="0 0 24 24"
      fill="none"
      aria-hidden="true"
      focusable="false"
    >
      <rect x="3" y="2" width="5" height="20" rx="2.5" fill="currentColor" />
      <path d="M10 4.4 20.5 8.6 10 12.8Z" fill="var(--live)" />
    </svg>
  );
}
