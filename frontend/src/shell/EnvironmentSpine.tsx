import { useEffect, useRef, useState, type CSSProperties, type KeyboardEvent } from 'react';
import { environments, useEnvironment } from './environment';

/**
 * The console's one loud element. The current environment holds a full-height band
 * of its own colour down the edge of the window, and that band is also the switcher —
 * you cannot change a flag without the blast radius being in frame.
 */
export function EnvironmentSpine() {
  const { environment, select } = useEnvironment();
  const [open, setOpen] = useState(false);
  const rootRef = useRef<HTMLDivElement>(null);
  const buttonRef = useRef<HTMLButtonElement>(null);
  const menuRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!open) return;

    const closeOnOutsidePress = (event: PointerEvent) => {
      if (!rootRef.current?.contains(event.target as Node)) setOpen(false);
    };

    const closeOnEscape = (event: globalThis.KeyboardEvent) => {
      if (event.key !== 'Escape') return;
      setOpen(false);
      buttonRef.current?.focus();
    };

    document.addEventListener('pointerdown', closeOnOutsidePress);
    document.addEventListener('keydown', closeOnEscape);

    return () => {
      document.removeEventListener('pointerdown', closeOnOutsidePress);
      document.removeEventListener('keydown', closeOnEscape);
    };
  }, [open]);

  useEffect(() => {
    if (open) menuRef.current?.querySelector('button')?.focus();
  }, [open]);

  const moveFocus = (event: KeyboardEvent<HTMLDivElement>) => {
    if (event.key !== 'ArrowDown' && event.key !== 'ArrowUp') return;

    const options = Array.from(menuRef.current?.querySelectorAll('button') ?? []);
    const current = options.indexOf(document.activeElement as HTMLButtonElement);
    if (current === -1) return;

    event.preventDefault();
    const step = event.key === 'ArrowDown' ? 1 : -1;
    options[(current + step + options.length) % options.length].focus();
  };

  const choose = (id: (typeof environments)[number]['id']) => {
    select(id);
    setOpen(false);
    buttonRef.current?.focus();
  };

  return (
    <div className="shell__spine" ref={rootRef}>
      <button
        ref={buttonRef}
        type="button"
        className="spine"
        style={{ '--tone': environment.tone } as CSSProperties}
        aria-haspopup="menu"
        aria-expanded={open}
        aria-label={`Working in ${environment.name}. Change environment.`}
        onClick={() => setOpen((wasOpen) => !wasOpen)}
      >
        <span className="spine__mark" aria-hidden="true" />
        <span className="spine__label" aria-hidden="true">
          {environment.name}
        </span>
        <span
          className={open ? 'spine__caret spine__caret--open' : 'spine__caret'}
          aria-hidden="true"
        />
      </button>

      {open && (
        <div
          ref={menuRef}
          className="envmenu"
          role="menu"
          aria-label="Working environment"
          onKeyDown={moveFocus}
        >
          <p className="envmenu__heading">Working environment</p>
          {environments.map((option) => (
            <button
              key={option.id}
              type="button"
              role="menuitemradio"
              aria-checked={option.id === environment.id}
              className="envmenu__option"
              style={{ '--tone': option.tone } as CSSProperties}
              onClick={() => choose(option.id)}
            >
              <span className="envmenu__swatch" aria-hidden="true" />
              <span>
                <span className="envmenu__name">
                  {option.name}
                  <span className="envmenu__key">{option.key}</span>
                  {option.id === environment.id && (
                    <span className="envmenu__current">Current</span>
                  )}
                </span>
                <span className="envmenu__blurb">{option.blurb}</span>
              </span>
            </button>
          ))}
        </div>
      )}
    </div>
  );
}
