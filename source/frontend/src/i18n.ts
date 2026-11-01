import i18n, { type PostProcessorModule } from 'i18next';
import { initReactI18next } from 'react-i18next';

import translation from './locales/pl/translation.json';
import { masked, useTextFilters } from './textFilters/textFilters';

// Streamer Friendly masks every translated string after interpolation, so nicknames and numbers passed into a message
// are covered too (content and experience §2).
const streamerFriendly: PostProcessorModule = {
  type: 'postProcessor',
  name: 'streamerFriendly',
  process: (value: string) => masked(value),
};

// Polish is the only M2 locale; keys stay stable for later languages (content and experience §5).
// Synchronous init: the resources are bundled, so the first render already has its copy.
void i18n
  .use(initReactI18next)
  .use(streamerFriendly)
  .init({
    lng: 'pl',
    fallbackLng: 'pl',
    resources: { pl: { translation } },
    initAsync: false,
    nsSeparator: false,
    interpolation: { escapeValue: false },
    postProcess: ['streamerFriendly'],
  });

// Every component that translates re-renders when a text filter changes: react-i18next listens for languageChanged.
useTextFilters.subscribe(() => {
  void i18n.changeLanguage(i18n.language);
});

export { default } from 'i18next';
