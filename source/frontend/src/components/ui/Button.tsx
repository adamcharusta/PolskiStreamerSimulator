import { cva, type VariantProps } from 'class-variance-authority';
import type { ComponentPropsWithRef } from 'react';

import { cn } from './cn';

const button = cva('btn', {
  variants: { variant: { primary: 'btn-primary', secondary: 'btn-secondary' } },
  defaultVariants: { variant: 'secondary' },
});

export interface ButtonProps extends ComponentPropsWithRef<'button'>, VariantProps<typeof button> {
  /** A request for this action is running: the button stays visible but cannot be activated again. */
  readonly pending?: boolean;
}

export function Button({
  className,
  variant,
  pending = false,
  type = 'button',
  disabled = false,
  ...props
}: Readonly<ButtonProps>) {
  return (
    <button
      type={type === 'submit' ? 'submit' : 'button'}
      className={cn(button({ variant }), className)}
      disabled={disabled || pending}
      aria-busy={pending || undefined}
      {...props}
    />
  );
}
