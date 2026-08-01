import { useEffect, useState } from 'react';
import { Outlet, useLocation } from 'react-router-dom';
import { ChromeRail } from './ChromeRail';
import { EnvironmentProvider } from './EnvironmentProvider';
import { EnvironmentSpine } from './EnvironmentSpine';
import { EnvironmentSwitcher } from './EnvironmentSwitcher';
import { Mark } from './Mark';

const NAV_ID = 'console-nav';

export function AppShell() {
  const location = useLocation();
  const [navOpen, setNavOpen] = useState(false);

  useEffect(() => {
    setNavOpen(false);
  }, [location.pathname]);

  return (
    <EnvironmentProvider>
      <a className="skiplink" href="#content">
        Skip to content
      </a>

      <div className="shell">
        <EnvironmentSpine />

        <div className="shell__bar">
          <div className="brand">
            <Mark size={22} />
          </div>
          <EnvironmentSwitcher compact />
          <button
            type="button"
            className="bar__toggle"
            aria-expanded={navOpen}
            aria-controls={NAV_ID}
            onClick={() => setNavOpen((wasOpen) => !wasOpen)}
          >
            {navOpen ? 'Close' : 'Menu'}
          </button>
        </div>

        {navOpen && <div className="shell__scrim" onClick={() => setNavOpen(false)} />}

        <ChromeRail id={NAV_ID} open={navOpen} />

        <main className="shell__work" id="content" tabIndex={-1}>
          {/* Keyed on the route so each screen arrives with the same small lift. */}
          <div className="workspace" key={location.pathname}>
            <Outlet />
          </div>
        </main>
      </div>
    </EnvironmentProvider>
  );
}
