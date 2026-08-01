import { createContext, useContext } from 'react';

export type EnvironmentId = 'development' | 'staging' | 'production';

export interface Environment {
  id: EnvironmentId;
  /** What people call it. */
  name: string;
  /** What an SDK passes when it evaluates a flag. */
  key: string;
  /** One line on what changing something here actually costs. */
  blurb: string;
  /** The colour this environment claims across the whole console. */
  tone: string;
}

export const environments: Environment[] = [
  {
    id: 'development',
    name: 'Development',
    key: 'dev',
    blurb: 'Local and CI work. Nothing here reaches anyone.',
    tone: 'var(--env-development)',
  },
  {
    id: 'staging',
    name: 'Staging',
    key: 'stg',
    blurb: 'Production-shaped data, for the last check before release.',
    tone: 'var(--env-staging)',
  },
  {
    id: 'production',
    name: 'Production',
    key: 'prod',
    blurb: 'Real people. Every change lands the moment you make it.',
    tone: 'var(--env-production)',
  },
];

export interface EnvironmentSelection {
  environment: Environment;
  select: (id: EnvironmentId) => void;
}

export const EnvironmentContext = createContext<EnvironmentSelection | null>(null);

export function useEnvironment(): EnvironmentSelection {
  const selection = useContext(EnvironmentContext);

  if (!selection) {
    throw new Error('useEnvironment must be called inside an EnvironmentProvider.');
  }

  return selection;
}
