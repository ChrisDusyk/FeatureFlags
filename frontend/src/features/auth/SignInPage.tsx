import { useState, type FormEvent } from 'react';
import { Link, Navigate, useLocation, useNavigate } from 'react-router-dom';

import { signIn, useSession } from '../../auth/client';
import { clearToken } from '../../auth/token';
import { AuthFrame } from './AuthFrame';

export function SignInPage() {
  const navigate = useNavigate();
  const location = useLocation();
  const { data: session, isPending: sessionPending } = useSession();

  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  // Where RequireAuth turned this person away from, so signing in returns them to it.
  const returnTo = typeof location.state?.from === 'string' ? location.state.from : '/';

  if (!sessionPending && session) {
    return <Navigate to={returnTo} replace />;
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setSubmitting(true);
    setError(null);

    const { error: failure } = await signIn.email({ email, password });

    if (failure) {
      // Deliberately not "no account with that email": which half was wrong is not
      // something an unauthenticated caller should be able to learn.
      setError('That email and password do not match an account.');
      setSubmitting(false);
      return;
    }

    // The previous session's token, if any, says nothing about this one.
    clearToken();
    await navigate(returnTo, { replace: true });
  }

  return (
    <AuthFrame
      title="Sign in"
      lede="Flags take effect the moment you change them, so the console needs to know who you are."
      footer={
        <>
          No account yet? <Link to="/sign-up">Create one</Link>.
        </>
      }
    >
      <form className="authform" onSubmit={handleSubmit} noValidate>
        <label className="field">
          <span className="field__label">Email</span>
          <input
            className="field__input"
            type="email"
            name="email"
            value={email}
            onChange={(event) => setEmail(event.target.value)}
            autoComplete="username"
            required
          />
        </label>

        <label className="field">
          <span className="field__label">Password</span>
          <input
            className="field__input"
            type="password"
            name="password"
            value={password}
            onChange={(event) => setPassword(event.target.value)}
            autoComplete="current-password"
            required
          />
        </label>

        {error && (
          <p className="field__error" role="alert">
            {error}
          </p>
        )}

        <button className="button" type="submit" disabled={submitting}>
          {submitting ? 'Signing in…' : 'Sign in'}
        </button>
      </form>
    </AuthFrame>
  );
}
