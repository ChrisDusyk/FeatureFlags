import { useState, type FormEvent } from 'react';
import { Link, Navigate, useNavigate } from 'react-router-dom';

import { minPasswordLength, signUp, useSession } from '../../auth/client';
import { clearToken } from '../../auth/token';
import { AuthFrame } from './AuthFrame';

export function SignUpPage() {
  const navigate = useNavigate();
  const { data: session, isPending: sessionPending } = useSession();

  const [name, setName] = useState('');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  if (!sessionPending && session) {
    return <Navigate to="/" replace />;
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setSubmitting(true);
    setError(null);

    if (password.length < minPasswordLength) {
      setError(`Use at least ${minPasswordLength} characters.`);
      setSubmitting(false);
      return;
    }

    const { error: failure } = await signUp.email({ name, email, password });

    if (failure) {
      setError(failure.message ?? 'That account could not be created.');
      setSubmitting(false);
      return;
    }

    clearToken();
    await navigate('/', { replace: true });
  }

  return (
    <AuthFrame
      title="Create an account"
      lede="The first account to exist administers this install; everyone after it starts as an ordinary user."
      footer={
        <>
          Already have one? <Link to="/sign-in">Sign in</Link>.
        </>
      }
    >
      <form className="authform" onSubmit={handleSubmit} noValidate>
        <label className="field">
          <span className="field__label">Name</span>
          <input
            className="field__input"
            type="text"
            name="name"
            value={name}
            onChange={(event) => setName(event.target.value)}
            autoComplete="name"
            required
          />
        </label>

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
            autoComplete="new-password"
            minLength={minPasswordLength}
            required
          />
          <span className="field__hint">At least {minPasswordLength} characters.</span>
        </label>

        {error && (
          <p className="field__error" role="alert">
            {error}
          </p>
        )}

        <button className="button" type="submit" disabled={submitting}>
          {submitting ? 'Creating…' : 'Create account'}
        </button>
      </form>
    </AuthFrame>
  );
}
