/** The follower milestones the dashboard counts towards (gameplay §13). */
const milestones = [1000, 10_000, 100_000, 1_000_000, 10_000_000, 100_000_000];

/** The starting age of every streamer; a career lasts 20 years of 52 weeks. */
const startAge = 18;

export function ageAt(week: number): number {
  return startAge + Math.floor((week - 1) / 52);
}

export function nextMilestone(followers: number): number | null {
  return milestones.find((m) => m > followers) ?? null;
}
