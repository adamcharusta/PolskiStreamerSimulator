/**
 * The page's content security policy and security headers (M5-D, #38), one source for the Vite servers here and for
 * the production web server (M6). Everything loads from the game's own origin: scripts, styles, fonts and API calls.
 * Inline style attributes stay allowed because Radix positions dialogs with them; images may be data: or blob: URLs
 * (the PDF diploma is a blob). In development Vite injects an inline React Refresh preamble, so only the development
 * policy allows inline scripts.
 */
export function contentSecurityPolicy(mode: 'development' | 'production'): string {
  const scripts = mode === 'development' ? "'self' 'unsafe-inline'" : "'self'";
  return [
    "default-src 'self'",
    `script-src ${scripts}`,
    "style-src 'self' 'unsafe-inline'",
    "img-src 'self' data: blob:",
    "font-src 'self'",
    "connect-src 'self'",
    "object-src 'none'",
    "base-uri 'self'",
    "form-action 'self'",
    "frame-ancestors 'none'",
  ].join('; ');
}

export function securityHeaders(mode: 'development' | 'production'): Record<string, string> {
  return {
    'Content-Security-Policy': contentSecurityPolicy(mode),
    'X-Content-Type-Options': 'nosniff',
    'Referrer-Policy': 'no-referrer',
    'Permissions-Policy': 'camera=(), microphone=(), geolocation=()',
  };
}
