import {
  useCallback,
  useEffect,
  useId,
  useRef,
  useState,
  type CSSProperties,
  type KeyboardEvent,
} from 'react';
import { environments, useEnvironment } from './environment';

/**
 * Picks the environment everything else in the console is scoped to.
 *
 * A plain labelled dropdown on purpose: this is the most consequential control in
 * the console, so it reads as a control. The spine down the edge of the window
 * echoes the choice, but it is the indicator, not the switch.
 */
export function EnvironmentSwitcher({ compact = false }: { compact?: boolean }) {
  const { environment, select } = useEnvironment();
  const [open, setOpen] = useState(false);
  const [activeIndex, setActiveIndex] = useState(0);

  const rootRef = useRef<HTMLDivElement>(null);
  const buttonRef = useRef<HTMLButtonElement>(null);
  const optionRefs = useRef<(HTMLDivElement | null)[]>([]);
  const listId = useId();

  const close = useCallback((returnFocus: boolean) => {
    setOpen(false);
    if (returnFocus) buttonRef.current?.focus();
  }, []);

  useEffect(() => {
    if (!open) return;

    const closeOnOutsidePress = (event: PointerEvent) => {
      if (!rootRef.current?.contains(event.target as Node)) close(false);
    };

    document.addEventListener('pointerdown', closeOnOutsidePress);
    return () => document.removeEventListener('pointerdown', closeOnOutsidePress);
  }, [open, close]);

  useEffect(() => {
    if (open) optionRefs.current[activeIndex]?.focus();
  }, [open, activeIndex]);

  const openList = () => {
    setActiveIndex(environments.findIndex((option) => option.id === environment.id));
    setOpen(true);
  };

  const onListKeyDown = (event: KeyboardEvent<HTMLDivElement>) => {
    switch (event.key) {
      case 'ArrowDown':
      case 'ArrowUp': {
        event.preventDefault();
        const step = event.key === 'ArrowDown' ? 1 : -1;
        setActiveIndex((index) => (index + step + environments.length) % environments.length);
        break;
      }
      case 'Home':
        event.preventDefault();
        setActiveIndex(0);
        break;
      case 'End':
        event.preventDefault();
        setActiveIndex(environments.length - 1);
        break;
      case 'Enter':
      case ' ':
        event.preventDefault();
        choose(activeIndex);
        break;
      case 'Escape':
        event.preventDefault();
        close(true);
        break;
      case 'Tab':
        close(false);
        break;
    }
  };

  const choose = (index: number) => {
    select(environments[index].id);
    close(true);
  };

  return (
    <div className={compact ? 'envswitch envswitch--compact' : 'envswitch'} ref={rootRef}>
      {!compact && <span className="envswitch__label">Environment</span>}

      <button
        ref={buttonRef}
        type="button"
        className="envswitch__button"
        style={{ '--tone': environment.tone } as CSSProperties}
        aria-haspopup="listbox"
        aria-expanded={open}
        aria-controls={open ? listId : undefined}
        aria-label={`Environment: ${environment.name}`}
        onClick={() => (open ? close(false) : openList())}
      >
        <span className="envswitch__dot" aria-hidden="true" />
        <span className="envswitch__value">
          <span className="envswitch__name">{environment.name}</span>
          {!compact && <span className="envswitch__key">{environment.key}</span>}
        </span>
        <span className={open ? 'envswitch__caret envswitch__caret--open' : 'envswitch__caret'}>
          <svg width="10" height="10" viewBox="0 0 10 10" aria-hidden="true" focusable="false">
            <path
              d="M1.5 3.5 5 7l3.5-3.5"
              fill="none"
              stroke="currentColor"
              strokeWidth="1.5"
              strokeLinecap="round"
              strokeLinejoin="round"
            />
          </svg>
        </span>
      </button>

      {open && (
        // Focus rides the options themselves (roving tabIndex), so the listbox
        // deliberately has no aria-activedescendant — that is for the other pattern,
        // where focus stays on the container.
        <div className="envpanel" id={listId} role="listbox" aria-label="Environment" onKeyDown={onListKeyDown}>
          {environments.map((option, index) => (
            <div
              key={option.id}
              ref={(node) => {
                optionRefs.current[index] = node;
              }}
              role="option"
              aria-selected={option.id === environment.id}
              tabIndex={index === activeIndex ? 0 : -1}
              className={
                option.id === environment.id ? 'envoption envoption--selected' : 'envoption'
              }
              style={{ '--tone': option.tone } as CSSProperties}
              onClick={() => choose(index)}
              onMouseEnter={() => setActiveIndex(index)}
            >
              <span className="envoption__dot" aria-hidden="true" />
              <span className="envoption__body">
                <span className="envoption__head">
                  <span className="envoption__name">{option.name}</span>
                  <span className="envoption__key">{option.key}</span>
                </span>
                <span className="envoption__blurb">{option.blurb}</span>
              </span>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
