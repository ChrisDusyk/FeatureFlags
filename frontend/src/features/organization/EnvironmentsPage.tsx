import { useState } from 'react';

import { useCurrentUser } from '../../auth/currentUser';
import { PageHeader } from '../../shell/PageHeader';
import { Unbuilt } from '../../shell/Unbuilt';
import { environments, type Environment } from '../../shell/environment';
import { NewSdkKeyDialog } from './NewSdkKeyDialog';
import { SdkKeyRow } from './SdkKeyRow';
import { useSdkKeys } from './useSdkKeys';

/**
 * The environments, and the keys that read them.
 *
 * Half of this screen is real and half is not, which the page says rather than hides: SDK keys are
 * issued and revoked here for real, while the environments themselves are still the three hard-coded
 * in shell/environment.ts. The Unbuilt block at the bottom is the part that has not been built.
 */
export function EnvironmentsPage() {
  const currentUser = useCurrentUser();
  const { state, reload } = useSdkKeys();
  const [issuingFor, setIssuingFor] = useState<Environment | null>(null);

  const isAdmin = currentUser.status === 'ready' && currentUser.user.isAdmin;

  return (
    <>
      <PageHeader
        eyebrow="Organization"
        title="Environments"
        lede="The places your flags are evaluated, and the keys your applications read them with."
      />

      {state.status === 'loading' && (
        <p className="flaglist__note" role="status">
          Reading the SDK keys…
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
        Not an error. An ordinary user can see the environments and needs nothing from this list —
        saying so beats an empty section they cannot explain.
      */}
      {state.status === 'forbidden' && (
        <p className="flaglist__note">
          SDK keys are managed by an administrator. Ask one of yours for a key, or for the admin
          role.
        </p>
      )}

      {state.status === 'ready' &&
        environments.map((environment) => {
          const keys = state.keys.filter((key) => key.environment === environment.key);
          const active = keys.filter((key) => key.isActive);

          return (
            <section
              className="envkeys"
              key={environment.id}
              style={{ '--tone': environment.tone } as React.CSSProperties}
            >
              <header className="envkeys__head">
                <div>
                  <h2 className="envkeys__name">
                    {environment.name}
                    <code className="envcard__key">{environment.key}</code>
                  </h2>
                  <p className="envkeys__blurb">{environment.blurb}</p>
                </div>
                {isAdmin && (
                  <button
                    type="button"
                    className="button"
                    onClick={() => setIssuingFor(environment)}
                  >
                    New key
                  </button>
                )}
              </header>

              {keys.length === 0 ? (
                <p className="envkeys__empty">
                  No keys yet. Nothing can read {environment.name} until one is issued.
                </p>
              ) : (
                <>
                  <ul className="keylist">
                    {keys.map((key) => (
                      <SdkKeyRow key={key.id} sdkKey={key} onRevoked={reload} />
                    ))}
                  </ul>
                  <p className="envkeys__count">
                    {active.length} of {keys.length} {keys.length === 1 ? 'key' : 'keys'} still work
                    {active.length === 1 ? 's' : ''}
                  </p>
                </>
              )}
            </section>
          );
        })}

      <div className="section">
        <Unbuilt
          title="The environments themselves are still hard-coded."
          body="Development, staging, and production are fixed until this screen owns them. Keys are real and can be issued and revoked above; the set of environments they belong to is not yet yours to change."
          planned={[
            {
              title: 'Add an environment',
              text: 'Name it, give it a key your SDK can pass, and pick the colour it claims in the console.',
            },
            {
              title: 'Mark the real one',
              text: 'Environments flagged as production ask for confirmation before a flag changes.',
            },
            {
              title: 'Rotate without downtime',
              text: 'Issue the replacement, move your applications across, then revoke the old one — three steps that should be one.',
            },
          ]}
        />
      </div>

      {issuingFor && (
        <NewSdkKeyDialog
          environment={issuingFor}
          onClose={() => setIssuingFor(null)}
          onIssued={reload}
        />
      )}
    </>
  );
}
