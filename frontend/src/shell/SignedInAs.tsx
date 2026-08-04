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

  async function handleSignOut() {
    setSigningOut(true);

    await signOut();
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
          <p className="whoami__meta">
            {/* Only worth saying when it is not the ordinary case. */}
            {state.user.isAdmin ? 'Admin' : 'User'}
          </p>
        </>
      ) : (
        <p className="whoami__meta">
          {state.status === 'loading' ? 'Signed in' : 'Account unavailable'}
        </p>
      )}

      <button className="whoami__out" type="button" onClick={handleSignOut} disabled={signingOut}>
        {signingOut ? 'Signing out…' : 'Sign out'}
      </button>
    </div>
  );
}
