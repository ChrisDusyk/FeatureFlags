import { createBrowserRouter } from 'react-router-dom';
import { AppShell } from './shell/AppShell';
import { OverviewPage } from './features/overview/OverviewPage';
import { NotFoundPage } from './features/overview/NotFoundPage';
import { FlagsPage } from './features/flags/FlagsPage';
import { SegmentsPage } from './features/segments/SegmentsPage';
import { RulesPage } from './features/rules/RulesPage';
import { MembersPage } from './features/organization/MembersPage';
import { EnvironmentsPage } from './features/organization/EnvironmentsPage';
import { SettingsPage } from './features/organization/SettingsPage';

export const router = createBrowserRouter([
  {
    element: <AppShell />,
    children: [
      { index: true, element: <OverviewPage /> },
      { path: 'flags', element: <FlagsPage /> },
      { path: 'segments', element: <SegmentsPage /> },
      { path: 'rules', element: <RulesPage /> },
      { path: 'organization/members', element: <MembersPage /> },
      { path: 'organization/environments', element: <EnvironmentsPage /> },
      { path: 'organization/settings', element: <SettingsPage /> },
      { path: '*', element: <NotFoundPage /> },
    ],
  },
]);
