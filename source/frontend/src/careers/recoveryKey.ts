// pss1.<career ID as 32 hex digits>.<43 base64url characters> (TD-121); the server checks the rest.
const keyPattern = /pss1\.[0-9a-f]{32}\.[\w-]{43}/iu;

/** The first recovery key in pasted text or a key file, or null. */
export function findRecoveryKey(text: string): string | null {
  return keyPattern.exec(text)?.[0] ?? null;
}
