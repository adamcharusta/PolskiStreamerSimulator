// The server is the authority (TD-122); this mirror only saves a round trip. It uppercases before storing.
const nicknamePattern = /^[\dA-Za-zĄĆĘŁŃÓŚŹŻąćęłńóśźż]{1,5}$/u;

/** The leaderboard nickname as the server stores it, or null when the rule rejects it. */
export function leaderboardNickname(input: string): string | null {
  const trimmed = input.normalize('NFC').trim();
  return nicknamePattern.test(trimmed) ? trimmed.toLocaleUpperCase('pl') : null;
}
