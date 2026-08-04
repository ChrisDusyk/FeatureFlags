import { createAuthClient } from 'better-auth/react';

/**
 * Better Auth runs in its own Node service, but the console never addresses it directly: the
 * .NET server forwards /api/auth to it, which keeps everything on one origin and the session
 * cookie first-party. So there is no base URL here — same origin is the point.
 */
export const authClient = createAuthClient({
  basePath: '/api/auth',
});

export const { useSession, signIn, signUp, signOut } = authClient;

/** How long a password has to be, matching what the auth service will accept. */
export const minPasswordLength = 12;
