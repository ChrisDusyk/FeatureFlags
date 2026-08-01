import type { CSSProperties } from 'react';
import { useEnvironment } from './environment';

/**
 * An ambient band of the working environment's colour down the edge of the window.
 * Indicator only — the control lives in <EnvironmentSwitcher>, where a control belongs.
 * Hidden from assistive tech because the switcher already names the environment.
 */
export function EnvironmentSpine() {
  const { environment } = useEnvironment();

  return (
    <div
      className="shell__spine"
      style={{ '--tone': environment.tone } as CSSProperties}
      aria-hidden="true"
    />
  );
}
