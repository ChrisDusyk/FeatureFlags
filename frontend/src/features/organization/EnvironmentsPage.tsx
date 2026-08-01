import { PageHeader } from '../../shell/PageHeader';
import { Unbuilt } from '../../shell/Unbuilt';

export function EnvironmentsPage() {
  return (
    <>
      <PageHeader
        eyebrow="Organization"
        title="Environments"
        lede="The places your flags are evaluated. The console keeps exactly one of them selected at all times."
      />
      <Unbuilt
        title="Three environments are hard-coded for now."
        body="Development, staging, and production are fixed until this screen exists. Each one will carry its own colour, its own SDK key, and its own set of flag states."
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
            title: 'Rotate SDK keys',
            text: 'Issue a new key and retire the old one without taking the environment down.',
          },
        ]}
      />
    </>
  );
}
