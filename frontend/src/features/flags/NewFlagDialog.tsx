import { useEffect, useRef, useState, type FormEvent } from 'react';

import type { Environment } from '../../shell/environment';
import { ApiError, createFlag } from './api';

/** What FlagKey.Create will make of what you typed. Kept in step with the domain's rule. */
function normalizeKey(value: string): string {
  return value.trim().toLowerCase();
}

const KEY_PATTERN = /^[a-z0-9]+(?:-[a-z0-9]+)*$/;

export function NewFlagDialog({
  environment,
  onClose,
  onCreated,
}: {
  environment: Environment;
  onClose: () => void;
  onCreated: () => void;
}) {
  const dialogRef = useRef<HTMLDialogElement>(null);

  const [key, setKey] = useState('');
  const [name, setName] = useState('');
  const [description, setDescription] = useState('');
  const [enabledHere, setEnabledHere] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  // showModal rather than the open attribute: it is what brings the focus trap, the Escape
  // handler, and the inert background with it, none of which are worth reimplementing.
  useEffect(() => {
    dialogRef.current?.showModal();
  }, []);

  const normalized = normalizeKey(key);
  const keyLooksWrong = normalized.length > 0 && !KEY_PATTERN.test(normalized);

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setSubmitting(true);
    setError(null);

    try {
      await createFlag({
        key: normalized,
        name: name.trim(),
        description: description.trim(),
        enabledIn: enabledHere ? [environment.key] : [],
      });

      onCreated();
    } catch (cause) {
      setError(cause instanceof ApiError ? cause.message : 'The console could not create this flag.');
      setSubmitting(false);
    }
  }

  // onClose alone: `close` fires however the dialog went away — Escape, the Cancel button, or a
  // successful create. Handling `cancel` as well would run this twice for Escape, since that path
  // fires `cancel` and then `close`.
  return (
    <dialog className="dialog" ref={dialogRef} aria-labelledby="newflag-title" onClose={onClose}>
      <form className="dialog__form" onSubmit={(event) => void handleSubmit(event)} noValidate>
        <h2 className="dialog__title" id="newflag-title">
          New flag
        </h2>
        <p className="dialog__lede">
          A flag exists in every environment from the moment you make it. This one starts off in all
          of them unless you say otherwise below — and off is the only safe place for a key nothing
          has been tested against yet.
        </p>

        <label className="field">
          <span className="field__label">Key</span>
          <input
            className="field__input field__input--mono"
            value={key}
            onChange={(event) => setKey(event.target.value)}
            autoComplete="off"
            spellCheck={false}
            required
          />
          <span className="field__hint">
            {keyLooksWrong
              ? 'Lowercase letters, digits, and single hyphens between them — like new-checkout.'
              : normalized && normalized !== key
                ? `Saved as ${normalized}`
                : 'What your SDK passes to evaluate this flag. It cannot be changed later.'}
          </span>
        </label>

        <label className="field">
          <span className="field__label">Name</span>
          <input
            className="field__input"
            value={name}
            onChange={(event) => setName(event.target.value)}
            autoComplete="off"
            required
          />
        </label>

        <label className="field">
          <span className="field__label">Description</span>
          <textarea
            className="field__input"
            value={description}
            onChange={(event) => setDescription(event.target.value)}
            rows={3}
          />
          <span className="field__hint">
            What turning this on actually does. Optional, and the thing everyone wants six months from
            now.
          </span>
        </label>

        <label className="checkfield">
          <input
            type="checkbox"
            checked={enabledHere}
            onChange={(event) => setEnabledHere(event.target.checked)}
          />
          <span>
            Start it on in <strong>{environment.name}</strong>
          </span>
        </label>

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
            {submitting ? 'Creating…' : 'Create flag'}
          </button>
        </div>
      </form>
    </dialog>
  );
}
