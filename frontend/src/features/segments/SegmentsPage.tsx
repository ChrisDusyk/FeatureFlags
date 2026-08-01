import { PageHeader } from '../../shell/PageHeader';
import { Unbuilt } from '../../shell/Unbuilt';

export function SegmentsPage() {
  return (
    <>
      <PageHeader
        eyebrow="Delivery"
        title="Segments"
        lede="Named groups of people a feature can reach — beta testers, internal staff, one account you are debugging."
      />
      <Unbuilt
        title="Segments give rules something to point at."
        body="Without them, every rule has to spell out who it applies to. A segment is defined once and reused by any rule that needs the same audience."
        planned={[
          {
            title: 'Build from attributes',
            text: 'Match on the traits your app already sends when it evaluates a flag: plan, region, account age.',
          },
          {
            title: 'Reuse across flags',
            text: 'One segment, many rules. Change the definition and every rule using it follows.',
          },
          {
            title: 'See who depends on it',
            text: 'Before you edit a segment, see which flags and rules it is holding up.',
          },
        ]}
      />
    </>
  );
}
