import { type ReactNode, useEffect, useRef } from 'react';

/** The heading of the current game step; it takes focus when the step appears (content and experience §4). */
export function StepHeading({ children }: { readonly children: ReactNode }) {
  const ref = useRef<HTMLHeadingElement>(null);
  useEffect(() => {
    ref.current?.focus();
  }, []);
  return (
    <h2 ref={ref} tabIndex={-1} className="step-heading">
      {children}
    </h2>
  );
}
