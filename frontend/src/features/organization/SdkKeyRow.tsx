import { useState } from 'react';

import { changedAgo, changedExactly } from '../flags/changed';
import { ApiError, revokeSdkKey, type SdkKey } from './api';

/**
 * One SDK key. A revoked key stays on the list, greyed rather than red: retiring a key is an
 * ordinary thing to do, not a fault, and the row is what tells a key that was withdrawn from one
 * that never existed.
 */
export function SdkKeyRow({ sdkKey, onRevoked }: { sdkKey: SdkKey; onRevoked: () => void }) {
  const [confirming, setConfirming] = useState(false);
  const [pending, setPending] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function revoke() {
    setPending(true);
    setError(null);

    try {
      await revokeSdkKey(sdkKey.id);
      onRevoked();
    } catch (cause) {
      setError(cause instanceof ApiError ? cause.message : 'The console could not revoke this key.');
      setPending(false);
      setConfirming(false);
    }
  }

  return (
    <li className={`keyrow${sdkKey.isActive ? '' : ' keyrow--revoked'}`}>
      <div className="keyrow__body">
        <p className="keyrow__name">{sdkKey.name}</p>
        <code className="keyrow__hint">{sdkKey.hint}…</code>
        <p className="keyrow__meta">
          {sdkKey.revokedAt ? (
            <>
              Revoked <time dateTime={sdkKey.revokedAt}>{changedAgo(sdkKey.revokedAt)}</time>
            </>
          ) : sdkKey.lastUsedAt ? (
            <>
              Last used{' '}
              <time dateTime={sdkKey.lastUsedAt} title={changedExactly(sdkKey.lastUsedAt)}>
                {changedAgo(sdkKey.lastUsedAt)}
              </time>
            </>
          ) : (
            // Worth saying plainly: a key nothing has ever presented is the one that can be
            // revoked without wondering who is about to notice.
            <>Never used</>
          )}
          {' · issued '}
          <time dateTime={sdkKey.createdAt} title={changedExactly(sdkKey.createdAt)}>
            {changedAgo(sdkKey.createdAt)}
          </time>
        </p>
        {error && (
          <p className="keyrow__error" role="alert">
            {error}
          </p>
        )}
      </div>

      {sdkKey.isActive &&
        (confirming ? (
          <div className="keyrow__confirm">
            <p className="keyrow__confirmtext">
              Anything still using this key stops working immediately.
            </p>
            <div className="keyrow__confirmactions">
              <button
                type="button"
                className="button button--quiet"
                onClick={() => setConfirming(false)}
                disabled={pending}
              >
                Keep it
              </button>
              <button
                type="button"
                className="button button--danger"
                onClick={() => void revoke()}
                disabled={pending}
              >
                {pending ? 'Revoking…' : 'Revoke'}
              </button>
            </div>
          </div>
        ) : (
          <button type="button" className="textlink" onClick={() => setConfirming(true)}>
            Revoke
          </button>
        ))}
    </li>
  );
}
