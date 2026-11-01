/**
 * The maintained Streamer Friendly list (content and experience §2). It targets language the owner considers risky to
 * show during a broadcast; it is not an official or exhaustive list of any platform's rules, and the owner reviews it
 * as platform policies change. Entries are lower-case and written without Polish diacritics: the matcher also accepts
 * ą ć ę ł ń ó ś ź ż in their place. A trailing `*` matches every word that starts with the entry; spaces in an entry
 * match any whitespace, so multiword phrases work. Bump the version with every change.
 */
export const streamerFriendlyList = {
  version: 1,
  phrases: [
    'kurw*',
    'skurw*',
    'chuj*',
    'huj',
    'huja',
    'hujem',
    'pierdol*',
    'pierdal*',
    'spierdal*',
    'wypierdal*',
    'odpierdal*',
    'jeb*',
    'zajeb*',
    'wyjeb*',
    'pojeb*',
    'odjeb*',
    'pizd*',
    'dupa',
    'dupy',
    'dupe',
    'dupie',
    'fuck*',
    'motherfuck*',
    'shit',
  ],
} as const;
