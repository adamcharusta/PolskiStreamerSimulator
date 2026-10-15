import i18n from 'i18next';
import { initReactI18next } from 'react-i18next';

import translation from './locales/pl/translation.json';

// Polish is the only M2 locale; keys stay stable for later languages (content and experience §5).
// Synchronous init: the resources are bundled, so the first render already has its copy.
void i18n.use(initReactI18next).init({
  lng: 'pl',
  fallbackLng: 'pl',
  resources: { pl: { translation } },
  initAsync: false,
  nsSeparator: false,
  interpolation: { escapeValue: false },
});

export { default } from 'i18next';
