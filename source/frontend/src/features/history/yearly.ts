import type { Category, WeekSummaryView } from '@/api/generated/game.schemas';

/** One 52-week year of a career, summed from its week summaries. */
export interface YearReview {
  readonly year: number;
  readonly fromWeek: number;
  readonly toWeek: number;
  readonly weeks: number;
  readonly followersNet: number;
  readonly cashDelta: number;
  readonly topCategory: Category | null;
  readonly incidents: Readonly<Record<string, number>>;
  readonly bestWeek: number | null;
}

export const weeksPerYear = 52;

/** Groups the settled weeks by career year (weeks 1–52 are year 1) in year order. */
export function yearlyReviews(weeks: readonly WeekSummaryView[]): YearReview[] {
  const byYear = new Map<number, WeekSummaryView[]>();
  for (const week of weeks) {
    const year = Math.floor((week.week - 1) / weeksPerYear) + 1;
    byYear.set(year, [...(byYear.get(year) ?? []), week]);
  }

  return [...byYear.entries()]
    .toSorted(([a], [b]) => a - b)
    .map(([year, list]) => {
      const categories = new Map<Category, number>();
      const incidents: Record<string, number> = {};
      for (const week of list) {
        categories.set(week.plan.category, (categories.get(week.plan.category) ?? 0) + 1);
        for (const tag of week.tags) {
          incidents[tag] = (incidents[tag] ?? 0) + 1;
        }
      }

      const top = [...categories.entries()].toSorted((a, b) => b[1] - a[1])[0];
      const best = list.toSorted((a, b) => b.followersNet - a.followersNet)[0];
      return {
        year,
        fromWeek: (year - 1) * weeksPerYear + 1,
        toWeek: year * weeksPerYear,
        weeks: list.length,
        followersNet: list.reduce((sum, w) => sum + w.followersNet, 0),
        cashDelta: list.reduce((sum, w) => sum + w.cashDelta, 0),
        topCategory: top?.[0] ?? null,
        incidents,
        bestWeek: best?.week ?? null,
      };
    });
}

export interface HistoryFilter {
  readonly fromWeek: number | null;
  readonly toWeek: number | null;
  readonly tag: string | null;
  readonly text: string;
}

/** The weeks within the range, with the tag, whose described text contains the search text (case-insensitive). */
export function filterWeeks(
  weeks: readonly WeekSummaryView[],
  filter: HistoryFilter,
  describe: (week: WeekSummaryView) => string,
): WeekSummaryView[] {
  const text = filter.text.trim().toLocaleLowerCase('pl-PL');
  return weeks.filter(
    (week) =>
      (filter.fromWeek === null || week.week >= filter.fromWeek) &&
      (filter.toWeek === null || week.week <= filter.toWeek) &&
      (filter.tag === null || week.tags.includes(filter.tag)) &&
      (text === '' || describe(week).toLocaleLowerCase('pl-PL').includes(text)),
  );
}
