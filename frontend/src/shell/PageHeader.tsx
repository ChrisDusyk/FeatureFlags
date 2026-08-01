export function PageHeader({
  eyebrow,
  title,
  lede,
}: {
  eyebrow: string;
  title: string;
  lede: string;
}) {
  return (
    <header className="pagehead">
      <p className="pagehead__eyebrow">{eyebrow}</p>
      <h1 className="pagehead__title">{title}</h1>
      <p className="pagehead__lede">{lede}</p>
    </header>
  );
}
