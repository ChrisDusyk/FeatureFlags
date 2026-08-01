import { useId } from 'react';

export interface PlannedCapability {
  title: string;
  text: string;
}

/**
 * The honest state for a screen that has navigation but no feature behind it yet:
 * say what will live here rather than dressing up an empty page as a finished one.
 */
export function Unbuilt({
  title,
  body,
  planned,
}: {
  title: string;
  body: string;
  planned: PlannedCapability[];
}) {
  const titleId = useId();

  return (
    <section className="stub" aria-labelledby={titleId}>
      <p className="stub__tag">Not built yet</p>
      <h2 className="stub__title" id={titleId}>
        {title}
      </h2>
      <p className="stub__body">{body}</p>

      <ul className="stub__list">
        {planned.map((capability) => (
          <li key={capability.title} className="stub__item">
            <span className="stub__bullet" aria-hidden="true">
              —
            </span>
            <span>
              <span className="stub__itemtitle">{capability.title}</span>
              <span className="stub__itemtext">{capability.text}</span>
            </span>
          </li>
        ))}
      </ul>
    </section>
  );
}
