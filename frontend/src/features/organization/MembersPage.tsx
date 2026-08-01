import { PageHeader } from '../../shell/PageHeader';
import { Unbuilt } from '../../shell/Unbuilt';

export function MembersPage() {
  return (
    <>
      <PageHeader
        eyebrow="Organization"
        title="Members"
        lede="Who belongs to this organization, and what each of them is allowed to change."
      />
      <Unbuilt
        title="Access is per environment, not per person."
        body="Being able to flip a flag in development says nothing about production. Membership and permissions land together so the two can be granted separately."
        planned={[
          {
            title: 'Invite by email',
            text: 'Send an invitation, see who has accepted, and withdraw one that is still pending.',
          },
          {
            title: 'Grant production separately',
            text: 'Read, toggle, and administer, chosen one environment at a time.',
          },
          {
            title: 'Remove access in one move',
            text: 'Revoking a member takes their keys and sessions with them.',
          },
        ]}
      />
    </>
  );
}
