import { PageHeader } from '../../shell/PageHeader';
import { Unbuilt } from '../../shell/Unbuilt';

export function SettingsPage() {
  return (
    <>
      <PageHeader
        eyebrow="Organization"
        title="Settings"
        lede="Your organization's name, and the defaults every new flag inherits from it."
      />
      <Unbuilt
        title="Defaults decide what a new flag does before anyone touches it."
        body="A flag created through the API today starts however the request says. These settings will set the house rule instead."
        planned={[
          {
            title: 'Rename the organization',
            text: 'The name shown in the console and on invitations.',
          },
          {
            title: 'Choose the starting state',
            text: 'Whether a new flag arrives off in every environment, or on where it is safe.',
          },
          {
            title: 'Close the organization',
            text: 'Export the flags first. Deleting takes the segments, rules, and history with it.',
          },
        ]}
      />
    </>
  );
}
