import { useEffect, useMemo, useState, type ReactNode } from 'react';
import {
  EnvironmentContext,
  environments,
  type EnvironmentId,
  type EnvironmentSelection,
} from './environment';

const STORAGE_KEY = 'featureflags.console.environment';

/** Development is the default on purpose — the safest place to land. */
const FALLBACK: EnvironmentId = 'development';

function readStoredId(): EnvironmentId {
  const stored = localStorage.getItem(STORAGE_KEY);
  return environments.some((environment) => environment.id === stored)
    ? (stored as EnvironmentId)
    : FALLBACK;
}

export function EnvironmentProvider({ children }: { children: ReactNode }) {
  const [id, setId] = useState<EnvironmentId>(readStoredId);

  useEffect(() => {
    localStorage.setItem(STORAGE_KEY, id);
  }, [id]);

  const selection = useMemo<EnvironmentSelection>(
    () => ({
      environment: environments.find((environment) => environment.id === id) ?? environments[0],
      select: setId,
    }),
    [id],
  );

  return <EnvironmentContext.Provider value={selection}>{children}</EnvironmentContext.Provider>;
}
