import { create } from 'zustand';

import type { TelemetryRequest } from '@/api/generated/game.schemas';

/** Undecided counts as off; nothing is collected or sent until `granted` (spec M4-F §2, TD-15). */
export type Consent = 'undecided' | 'granted' | 'denied';

const consentKey = 'pss.analytics.v1';
const bufferKey = 'pss.analytics.buffer.v1';
const maxBuffered = 100;
const maxBatch = 50;

const booleans = ['true', 'false'];
const allowlist: Record<string, Record<string, readonly string[] | null>> = {
  career_created: { background: null, difficulty: null, ranked: booleans },
  week_settled: { stage: ['1-4', '5-13', '14-52', '53-260', '261+'] },
  batch_run: { weeks: ['4', '13', '52'] },
  career_ended: { ending: null, ranked: booleans },
  result_published: {},
  save_restored: {},
  save_imported: {},
};

/** Enum-valued props the server checks against its own enums; the browser only requires a short identifier. */
const enumValue = /^[a-z][A-Za-z]{0,39}$/u;

export type AnalyticsEvent = keyof typeof allowlist;
export type AnalyticsProps = Readonly<Record<string, string | number | boolean>>;

interface Buffered {
  readonly name: string;
  readonly props: Record<string, string | number | boolean>;
}

function read(key: string): string | null {
  try {
    return localStorage.getItem(key);
  } catch {
    return null;
  }
}

function write(key: string, value: string | null): void {
  try {
    if (value === null) {
      localStorage.removeItem(key);
    } else {
      localStorage.setItem(key, value);
    }
  } catch {
    // Blocked storage: the choice and the buffer live for this page only.
  }
}

function storedConsent(): Consent {
  const value = read(consentKey);
  return value === 'granted' || value === 'denied' ? value : 'undecided';
}

let memory: Buffered[] | null = null;

function buffer(): Buffered[] {
  if (memory !== null) {
    return memory;
  }

  try {
    const parsed: unknown = JSON.parse(read(bufferKey) ?? '[]');
    memory = Array.isArray(parsed) ? (parsed as Buffered[]) : [];
  } catch {
    memory = [];
  }

  return memory;
}

function save(events: Buffered[]): void {
  memory = events;
  write(bufferKey, events.length === 0 ? null : JSON.stringify(events));
}

interface ConsentState {
  readonly consent: Consent;
  readonly setConsent: (consent: 'granted' | 'denied') => void;
}

/** The player's analytics choice; denying (or revoking) deletes every buffered event at once. */
export const useConsent = create<ConsentState>((set) => ({
  consent: storedConsent(),
  setConsent: (consent) => {
    write(consentKey, consent);
    if (consent === 'denied') {
      save([]);
    }

    set({ consent });
  },
}));

/** Whether the event and every prop value are allowlisted; anything else is dropped whole. */
export function allowed(name: string, props: AnalyticsProps): boolean {
  const spec = allowlist[name];
  if (spec === undefined) {
    return false;
  }

  return Object.entries(props).every(([key, value]) => {
    const values = spec[key];
    if (values === undefined) {
      return false;
    }

    return values === null
      ? typeof value === 'string' && enumValue.test(value)
      : values.includes(String(value));
  });
}

/** Buffers an allowlisted event while the player has opted in; otherwise does nothing. */
export function track(name: AnalyticsEvent, props: AnalyticsProps = {}): void {
  if (useConsent.getState().consent !== 'granted' || !allowed(name, props)) {
    return;
  }

  save([...buffer(), { name, props: { ...props } }].slice(-maxBuffered));
}

/** The events waiting to be sent, for tests and the settings note. */
export function buffered(): readonly Buffered[] {
  return buffer();
}

let flushing = false;

/** Sends up to 50 buffered events; they leave the buffer only after the server accepted them. */
export async function flush(): Promise<void> {
  if (flushing || useConsent.getState().consent !== 'granted' || buffer().length === 0) {
    return;
  }

  flushing = true;
  const batch = buffer().slice(0, maxBatch);
  try {
    // fetch with keepalive survives the page being hidden or closed; no cookie or other credential is sent.
    const response = await fetch('/v1/telemetry', {
      method: 'POST',
      keepalive: true,
      credentials: 'omit',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ events: batch } satisfies TelemetryRequest),
    });
    if (!response.ok) {
      return;
    }

    // The player may have revoked while the request was out; the buffer is then already empty.
    if (useConsent.getState().consent === 'granted') {
      save(buffer().slice(batch.length));
    }
  } catch {
    // Kept for the next flush.
  } finally {
    flushing = false;
  }
}

function flushOnHide(): void {
  void flush();
}

/** Flushes every 30 seconds and when the page is hidden; returns the cleanup. */
export function startFlushing(interval = 30_000): () => void {
  const timer = setInterval(() => void flush(), interval);
  globalThis.addEventListener('pagehide', flushOnHide);
  return () => {
    clearInterval(timer);
    globalThis.removeEventListener('pagehide', flushOnHide);
  };
}

/** The `week_settled` stage of a settled week. */
export function weekStage(week: number): string {
  if (week <= 4) {
    return '1-4';
  }

  if (week <= 13) {
    return '5-13';
  }

  if (week <= 52) {
    return '14-52';
  }

  return week <= 260 ? '53-260' : '261+';
}

/** Forgets the module state between tests. */
export function resetAnalyticsForTests(): void {
  memory = null;
  flushing = false;
  useConsent.setState({ consent: storedConsent() });
}
