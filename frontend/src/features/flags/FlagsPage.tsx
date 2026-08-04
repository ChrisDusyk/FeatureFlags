import { useState } from 'react';

import { PageHeader } from '../../shell/PageHeader';
import { useEnvironment } from '../../shell/environment';
import { FlagRow } from './FlagRow';
import { NewFlagDialog } from './NewFlagDialog';
import { useFlags } from './useFlags';

export function FlagsPage() {
  const { environment } = useEnvironment();
  const { state, reload, replace } = useFlags(environment.key);
  const [creating, setCreating] = useState(false);

  return (
    <>
      <PageHeader
        eyebrow="Delivery"
        title="Flags"
        lede={`Every feature your app can switch on, and whether it is on in ${environment.name}.`}
      />

      <div className="flagbar">
        <p className="flagbar__count">
          {state.status === 'ready'
            ? `${state.flags.length} ${state.flags.length === 1 ? 'flag' : 'flags'}`
            : ' '}
        </p>
        <button type="button" className="button" onClick={() => setCreating(true)}>
          New flag
        </button>
      </div>

      {state.status === 'loading' && (
        <p className="flaglist__note" role="status">
          Reading the flags in {environment.name}…
        </p>
      )}

      {state.status === 'failed' && (
        <div className="flaglist__failed" role="alert">
          <p>{state.message}</p>
          <button type="button" className="textlink" onClick={reload}>
            Try again
          </button>
        </div>
      )}

      {/*
        An empty list is not an unbuilt screen. This feature works — there is simply nothing in it
        yet, and saying so plainly beats dressing the state up as something missing.
      */}
      {state.status === 'ready' && state.flags.length === 0 && (
        <div className="flaglist__empty">
          <h2 className="flaglist__emptytitle">No flags yet.</h2>
          <p className="flaglist__emptybody">
            A flag is a key your app can ask about — <code>new-checkout</code>, say — and an answer
            that differs per environment. Make the first one and it will exist everywhere at once,
            off until you turn it on.
          </p>
        </div>
      )}

      {state.status === 'ready' && state.flags.length > 0 && (
        <ul className="flaglist">
          {state.flags.map((flag) => (
            <FlagRow key={flag.id} flag={flag} environment={environment} onReplace={replace} />
          ))}
        </ul>
      )}

      {creating && (
        <NewFlagDialog
          environment={environment}
          onClose={() => setCreating(false)}
          onCreated={() => {
            setCreating(false);
            reload();
          }}
        />
      )}
    </>
  );
}
