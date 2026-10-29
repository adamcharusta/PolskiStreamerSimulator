/** `kariera-<nick>-tydzien-<week>.json` with the nickname reduced to safe ASCII file-name characters. */
export function saveFileName(nickname: string, week: number): string {
  const slug = nickname
    .normalize('NFD')
    .replaceAll(/\p{M}/gu, '')
    .replaceAll(/[łŁ]/gu, 'l')
    .toLowerCase()
    .replaceAll(/[^a-z0-9]+/gu, '-')
    .replaceAll(/(^-+)|(-+$)/gu, '');
  return `kariera-${slug === '' ? 'zapis' : slug}-tydzien-${String(week)}.json`;
}

/** The parsed file, or null when it is not JSON; the server validates everything else. */
export function parseSaveFile(text: string): unknown {
  try {
    return JSON.parse(text) as unknown;
  } catch {
    return null;
  }
}
