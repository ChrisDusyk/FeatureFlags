import { useState } from 'react';
import { useNavigate } from 'react-router-dom';

import { signOut } from '../auth/client';
import { useCurrentUser } from '../auth/currentUser';
import { clearToken } from '../auth/token';

/** Who you are signed in as, and the way back out. Sits at the foot of the rail. */
export function SignedInAs() {
  const navigate = useNavigate();
  const state = useCurrentUser();
  const [signingOut, setSigningOut] = useState(false);
  const [failed, setFailed] = useState(false);

  async function handleSignOut() {
    setSigningOut(true);
    setFailed(false);

    try {
      // The client reports a refused sign-out in the result and a failed one by throwing.
      const { error } = await signOut();

      if (error) {
        throw new Error(error.message);
      }
    } catch {
      // The session is still live on the server, so leave it said. Showing someone the
      // door to a room they have not actually left would be worse than saying it failed —
      // and re-enabling the button is what lets them try again.
      setFailed(true);
      setSigningOut(false);
      return;
    }

    clearToken();
    await navigate('/sign-in', { replace: true });
  }

  return (
    <div className="whoami">
      {state.status === 'ready' ? (
        <>
          <p className="whoami__name" title={state.user.email}>
            {state.user.name}
          </p>
          {/* Only worth saying when it is not the ordinary case. */}
          {state.user.isAdmin && <p className="whoami__meta">Admin</p>}
        </>
      ) : (
        <p className="whoami__meta">
          {state.status === 'loading' ? 'Signed in' : 'Account unavailable'}
        </p>
      )}

      <button className="whoami__out" type="button" onClick={handleSignOut} disabled={signingOut}>
        {signingOut ? 'Signing out…' : 'Sign out'}
      </button>

      {failed && (
        <p className="whoami__failed" role="alert">
          Could not sign out. Try again.
        </p>
      )}
    </div>
  );
}
