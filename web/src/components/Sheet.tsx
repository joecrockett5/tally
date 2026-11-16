import { useEffect, useId, type ReactNode } from 'react';

interface SheetProps {
  title: string;
  onClose: () => void;
  children: ReactNode;
  footer?: ReactNode;
  /** Fill the screen's height from the start, so content sits at the top (above a phone's keyboard) and the sheet doesn't resize as results load. */
  tall?: boolean;
}

/** Bottom sheet on phones, centred dialog on wider screens. */
export function Sheet({ title, onClose, children, footer, tall }: SheetProps) {
  const titleId = useId();

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => e.key === 'Escape' && onClose();
    document.addEventListener('keydown', onKey);
    document.body.classList.add('no-scroll');
    return () => {
      document.removeEventListener('keydown', onKey);
      document.body.classList.remove('no-scroll');
    };
  }, [onClose]);

  return (
    <div className="sheet-backdrop" onMouseDown={(e) => e.target === e.currentTarget && onClose()}>
      <div className={tall ? 'sheet sheet-tall' : 'sheet'} role="dialog" aria-modal="true" aria-labelledby={titleId}>
        <header className="sheet-header">
          <h2 id={titleId}>{title}</h2>
          <button type="button" className="icon-button" onClick={onClose} aria-label="Close">
            ✕
          </button>
        </header>
        <div className="sheet-body">{children}</div>
        {footer && <footer className="sheet-footer">{footer}</footer>}
      </div>
    </div>
  );
}
