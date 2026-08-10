import { useEffect, useRef, useState, type FormEvent } from 'react';

import type { Environment } from '../../shell/environment';
import { ApiError, issueSdkKey, type IssuedSdkKey, type SdkKeyKind } from './api';

/**
 * Issues a key, then shows it.
 *
 * The two steps are one dialog on purpose: the token exists for exactly as long as this component
 * holds it, and closing the dialog is the moment it becomes unrecoverable. Splitting them across a
 * navigation would put that moment somewhere nobody decided on.
 */
export function NewSdkKeyDialog({
  environment,
  onClose,
  onIssued,
}: {
  environment: Environment;
  onClose: () => void;
  onIssued: () => void;
}) {
  const dialogRef = useRef<HTMLDialogElement>(null);

  const [name, setName] = useState('');
  const [kind, setKind] = useState<SdkKeyKind>('secret');
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [issued, setIssued] = useState<IssuedSdkKey | null>(null);
  const [copied, setCopied] = useState(false);

  useEffect(() => {
    dialogRef.current?.showModal();
  }, []);

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setSubmitting(true);
    setError(null);

    try {
      setIssued(await issueSdkKey(name.trim(), kind, environment.key));

      // The list behind the dialog is stale from this moment, and the dialog is going to sit open
      // while somebody copies the token. Refresh now rather than on close.
      onIssued();
    } catch (cause) {
      setError(cause instanceof ApiError ? cause.message : 'The console could not issue this key.');
      setSubmitting(false);
    }
  }

  async function copy(token: string) {
    try {
      await navigator.clipboard.writeText(token);
      setCopied(true);
    } catch {
      // Clipboard access can be refused, and the token is on screen and selectable anyway. Saying
      // "copied" when nothing was copied is the only outcome worth avoiding here.
      setCopied(false);
    }
  }

  if (issued) {
    return (
      <dialog className="dialog" ref={dialogRef} aria-labelledby="issuedkey-title" onClose={onClose}>
        <div className="dialog__form">
          <h2 className="dialog__title" id="issuedkey-title">
            Copy this key now
          </h2>
          <p className="dialog__lede">
            This is the only time it will be shown. Only a hash of it is stored, so nobody — you
            included — can read it back out. Lose it and the remedy is to revoke this key and issue
            another.
          </p>

          {issued.kind === 'secret' && (
            <p className="notice" role="note">
              This is a secret key. Keep it somewhere only your server can read it — not in a
              browser bundle, and not in a repository.
            </p>
          )}

          <output className="tokenbox">
            <code className="tokenbox__value">{issued.token}</code>
          </output>

          <div className="dialog__actions">
            <button type="button" className="button button--quiet" onClick={() => void copy(issued.token)}>
              {copied ? 'Copied' : 'Copy'}
            </button>
            <button type="button" className="button" onClick={() => dialogRef.current?.close()}>
              Done
            </button>
          </div>
        </div>
      </dialog>
    );
  }

  return (
    <dialog className="dialog" ref={dialogRef} aria-labelledby="newkey-title" onClose={onClose}>
      <form className="dialog__form" onSubmit={(event) => void handleSubmit(event)} noValidate>
        <h2 className="dialog__title" id="newkey-title">
          New SDK key
        </h2>
        <p className="dialog__lede">
          A key lets a program read the flags in <strong>{environment.name}</strong> and nothing
          else. It cannot change a flag, and it cannot see another environment.
        </p>

        <label className="field">
          <span className="field__label">Name</span>
          <input
            className="field__input"
            value={name}
            onChange={(event) => setName(event.target.value)}
            autoComplete="off"
            required
          />
          <span className="field__hint">
            What is holding it — “CI”, “web app”. This is what you will be going on when you have
            four of them and need to retire one.
          </span>
        </label>

        <fieldset className="kindfield">
          <legend className="field__label">Where will it run?</legend>

          <label className="kindfield__option">
            <input
              type="radio"
              name="kind"
              checked={kind === 'secret'}
              onChange={() => setKind('secret')}
            />
            <span>
              <span className="kindfield__title">On a server</span>
              <span className="kindfield__text">
                A secret key, for anything only you can read — a backend, a container, a CI job.
                The server refuses it if it arrives from a browser.
              </span>
            </span>
          </label>

          <label className="kindfield__option">
            <input
              type="radio"
              name="kind"
              checked={kind === 'publishable'}
              onChange={() => setKind('publishable')}
            />
            <span>
              <span className="kindfield__title">In a browser</span>
              <span className="kindfield__text">
                A publishable key, for a web or mobile app. Anything shipped to a browser can be
                read out of it, so treat this key as public.
              </span>
            </span>
          </label>
        </fieldset>

        {/*
          Said at the moment of the decision rather than in a doc nobody opens. This is the one
          trade a publishable key makes, and it is not obvious: the key is read-only, so the
          instinct is that publishing it costs nothing — but every flag key in this environment,
          and whether each is on, becomes readable by anyone who loads the app.
        */}
        {kind === 'publishable' && (
          <p className="notice" role="note">
            Every flag key in <strong>{environment.name}</strong>, and whether each one is on, will
            be readable by anyone who opens your app. Flag names travel further than people expect —
            name them accordingly.
          </p>
        )}

        {error && (
          <p className="field__error" role="alert">
            {error}
          </p>
        )}

        <div className="dialog__actions">
          <button
            type="button"
            className="button button--quiet"
            onClick={() => dialogRef.current?.close()}
            disabled={submitting}
          >
            Cancel
          </button>
          <button className="button" type="submit" disabled={submitting}>
            {submitting ? 'Issuing…' : 'Issue key'}
          </button>
        </div>
      </form>
    </dialog>
  );
}
