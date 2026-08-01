import { PageHeader } from '../../shell/PageHeader';
import { Unbuilt } from '../../shell/Unbuilt';
import { useEnvironment } from '../../shell/environment';

export function FlagsPage() {
  const { environment } = useEnvironment();

  return (
    <>
      <PageHeader
        eyebrow="Delivery"
        title="Flags"
        lede={`Every feature your app can switch on, and whether it is on in ${environment.name}.`}
      />
      <Unbuilt
        title="The flag list lands here."
        body="The API can create a flag today. Reading, toggling, and editing come next — this screen will hold one row per flag, scoped to the environment you are working in."
        planned={[
          {
            title: 'Read state before you read names',
            text: 'Each row carries its on-or-off state as a coloured edge, so the shape of a few hundred flags is legible in one pass.',
          },
          {
            title: 'Toggle on the record',
            text: 'Every switch captures who threw it, when, and in which environment.',
          },
          {
            title: 'Create a flag',
            text: 'A key, a name, a description, and the state it starts in. Keys are lowercase slugs and cannot be reused.',
          },
        ]}
      />
    </>
  );
}
