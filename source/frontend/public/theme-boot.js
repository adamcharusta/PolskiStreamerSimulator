// Applies the stored theme before the app renders, so a returning player sees no flash of the default theme. A file,
// not an inline script, so the content security policy can forbid inline scripts (M5-D, #38).
try {
  var stored = localStorage.getItem('pss.theme');
  if (stored === 'win98' || stored === 'web2005') document.documentElement.dataset.theme = stored;
} catch {
  // Storage can be disabled; the overlay theme is already set on <html>.
}
