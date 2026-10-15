import './styles/index.css';
import '@fontsource/anton/400.css';
import '@fontsource/comic-neue/400.css';
import '@fontsource/comic-neue/700.css';
import './i18n';

import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';

import { App } from './App';

const root = document.querySelector('#root');
if (!root) {
  throw new Error('The #root element is missing.');
}

createRoot(root).render(
  <StrictMode>
    <App />
  </StrictMode>,
);
