import { PageHeader } from '../../shell/PageHeader';
import { Unbuilt } from '../../shell/Unbuilt';

export function RulesPage() {
  return (
    <>
      <PageHeader
        eyebrow="Delivery"
        title="Rules"
        lede="A flag decides whether a feature can be on. A rule decides who actually gets it."
      />
      <Unbuilt
        title="Targeting sits between the flag and the person."
        body="Rules are evaluated in order and the first match wins, so the order you put them in is the decision you are making."
        planned={[
          {
            title: 'Roll out by percentage',
            text: 'Give the feature to a stable slice of traffic and raise it when you are ready.',
          },
          {
            title: 'Target a segment',
            text: 'Point a rule at a named group instead of restating who they are.',
          },
          {
            title: 'Check before you save',
            text: 'Enter an example user and see which rule matches and what they would get.',
          },
        ]}
      />
    </>
  );
}
