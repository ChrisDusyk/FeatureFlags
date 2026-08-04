import { Navigate, Outlet, useLocation } from 'react-router-dom';

import { useSession } from './client';
import { CurrentUserProvider } from './CurrentUserProvider';

/**
 * Stands between the console and anyone who has not signed in.
 *
 * The deep link is carried across in navigation state rather than dropped: arriving at
 * /flags?key=checkout without a session should end at /flags?key=checkout, not at the overview.
 */
export function RequireAuth() {
  const { data: session, isPending } = useSession();
  const location = useLocation();

  if (isPending) {
    // A blank frame, not a spinner: this resolves from a cookie in a few milliseconds, and
    // flashing a loading state on every navigation would be noisier than the wait.
    return <div className="authgate" aria-busy="true" aria-live="polite" />;
  }

  if (!session) {
    return (
      <Navigate
        to="/sign-in"
        replace
        state={{ from: `${location.pathname}${location.search}${location.hash}` }}
      />
    );
  }

  return (
    <CurrentUserProvider>
      <Outlet />
    </CurrentUserProvider>
  );
}
