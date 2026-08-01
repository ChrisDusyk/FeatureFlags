export interface NavItem {
  to: string;
  label: string;
  /** Plain-language answer to "what do I do here?" — reused on the overview. */
  blurb: string;
}

export interface NavSection {
  id: string;
  label?: string;
  items: NavItem[];
}

export const navigation: NavSection[] = [
  {
    id: 'console',
    items: [{ to: '/', label: 'Overview', blurb: 'Where the console stands, and where to go next.' }],
  },
  {
    id: 'delivery',
    label: 'Delivery',
    items: [
      { to: '/flags', label: 'Flags', blurb: 'Turn a feature on or off, one key at a time.' },
      { to: '/segments', label: 'Segments', blurb: 'Group the people a feature can reach.' },
      { to: '/rules', label: 'Rules', blurb: 'Decide who gets a feature once its flag is on.' },
    ],
  },
  {
    id: 'organization',
    label: 'Organization',
    items: [
      { to: '/organization/members', label: 'Members', blurb: 'Who can change what, and where.' },
      {
        to: '/organization/environments',
        label: 'Environments',
        blurb: 'The places your flags are evaluated.',
      },
      {
        to: '/organization/settings',
        label: 'Settings',
        blurb: 'Your name, your keys, and the defaults new flags inherit.',
      },
    ],
  },
];

/** Flat order of every nav link, used to stagger the rail on first paint. */
export const navItemIndex = new Map(
  navigation.flatMap((section) => section.items).map((item, index) => [item.to, index]),
);
