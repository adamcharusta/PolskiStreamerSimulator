const locale = 'pl-PL';
const MINUS = '−';

const plnFormat = new Intl.NumberFormat(locale, {
  style: 'currency',
  currency: 'PLN',
  maximumFractionDigits: 0,
});
const countFormat = new Intl.NumberFormat(locale, { maximumFractionDigits: 0 });
const statFormat = new Intl.NumberFormat(locale, { maximumFractionDigits: 1 });
const percentFormat = new Intl.NumberFormat(locale, { style: 'percent', maximumFractionDigits: 0 });
const multiplierFormat = new Intl.NumberFormat(locale, {
  minimumFractionDigits: 2,
  maximumFractionDigits: 2,
});
const dateFormat = new Intl.DateTimeFormat(locale, { dateStyle: 'medium' });

export const pln = (value: number): string => plnFormat.format(value);
export const count = (value: number): string => countFormat.format(value);
export const stat = (value: number): string => statFormat.format(value);
export const percent = (probability: number): string => percentFormat.format(probability);
export const multiplier = (value: number): string => multiplierFormat.format(value);
export const date = (iso: string): string => dateFormat.format(new Date(iso));

/** A change with its sign, never colour alone (content and experience §4). */
export function signed(value: number, format: (value: number) => string = count): string {
  if (value > 0) {
    return `+${format(value)}`;
  }

  return value < 0 ? `${MINUS}${format(-value)}` : format(0);
}
