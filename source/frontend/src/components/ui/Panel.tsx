import { type ReactNode, useId } from 'react';

import { cn } from './cn';

export interface PanelProps {
  readonly title: ReactNode;
  readonly children: ReactNode;
  readonly className?: string;
}

/** A framed section with its heading; the frame is what each theme restyles. */
export function Panel({ title, children, className }: PanelProps) {
  const id = useId();
  return (
    <section className={cn('panel', className)} aria-labelledby={id}>
      <h2 id={id} className="panel-title">
        {title}
      </h2>
      <div className="panel-body">{children}</div>
    </section>
  );
}
