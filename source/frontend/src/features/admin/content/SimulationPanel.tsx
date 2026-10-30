import { useState } from 'react';
import { useTranslation } from 'react-i18next';

import { simulateContentDraft } from '@/api/generated/game';
import type {
  OwnerSessionView,
  SimulationRunView,
  SimulationView,
} from '@/api/generated/game.schemas';
import { Button } from '@/components/ui/Button';
import { count } from '@/format';

import { csrf } from '../csrf';

/** Replays the draft and the current content on the same careers; publication repeats this check (spec M4-E §6). */
export function SimulationPanel({
  disabled,
  session,
}: {
  readonly disabled: boolean;
  readonly session: OwnerSessionView;
}) {
  const { t } = useTranslation();
  const [result, setResult] = useState<SimulationView | null>(null);
  const [failed, setFailed] = useState(false);
  const [pending, setPending] = useState(false);

  return (
    <section className="grid gap-2" aria-label={t('admin.content.simulation.title')}>
      <h3 className="font-bold m-0">{t('admin.content.simulation.title')}</h3>
      <p className="m-0">
        <Button
          pending={pending}
          disabled={disabled}
          onClick={() => {
            setPending(true);
            setFailed(false);
            void simulateContentDraft(csrf(session))
              .then(setResult)
              .catch(() => {
                setFailed(true);
              })
              .finally(() => {
                setPending(false);
              });
          }}
        >
          {t('admin.content.simulation.run')}
        </Button>
      </p>
      {failed && <p role="alert">{t('admin.content.simulation.failed')}</p>}
      {result !== null && (
        <>
          <p className={result.passed ? 'm-0' : 'bad m-0'}>
            {t(
              result.passed ? 'admin.content.simulation.passed' : 'admin.content.simulation.broken',
            )}
          </p>
          <table className="w-full">
            <caption className="sr-only">{t('admin.content.simulation.title')}</caption>
            <thead>
              <tr>
                <th scope="col">{t('admin.content.simulation.measure')}</th>
                <th scope="col">{t('admin.content.simulation.draft')}</th>
                <th scope="col">{t('admin.content.simulation.current')}</th>
              </tr>
            </thead>
            <tbody>
              <Row
                label={t('admin.content.simulation.score')}
                draft={result.draft}
                current={result.current}
                pick={(r) => r.score}
              />
              <Row
                label={t('admin.content.simulation.weeks')}
                draft={result.draft}
                current={result.current}
                pick={(r) => r.weeks}
              />
              <tr>
                <th scope="row">{t('admin.content.simulation.endings')}</th>
                <td>{endings(result.draft)}</td>
                <td>{endings(result.current)}</td>
              </tr>
              <tr>
                <th scope="row">{t('admin.content.simulation.violations')}</th>
                <td>{result.draft.violations.length + result.draft.errors.length}</td>
                <td>{result.current.violations.length + result.current.errors.length}</td>
              </tr>
            </tbody>
          </table>
          {[...result.draft.violations, ...result.draft.errors].length > 0 && (
            <ul className="bad m-0">
              {[...result.draft.violations, ...result.draft.errors].slice(0, 10).map((line) => (
                <li key={line} className="font-mono text-sm">
                  {line}
                </li>
              ))}
            </ul>
          )}
        </>
      )}
    </section>
  );
}

function endings(run: SimulationRunView): string {
  return Object.entries(run.endings)
    .map(([ending, n]) => `${ending}: ${String(n)}`)
    .join(', ');
}

function Row({
  label,
  draft,
  current,
  pick,
}: {
  readonly label: string;
  readonly draft: SimulationRunView;
  readonly current: SimulationRunView;
  readonly pick: (run: SimulationRunView) => { p10: number; p50: number; p90: number };
}) {
  const spread = (run: SimulationRunView) => {
    const { p10, p50, p90 } = pick(run);
    return `${count(p10)} / ${count(p50)} / ${count(p90)}`;
  };
  return (
    <tr>
      <th scope="row">{label}</th>
      <td>{spread(draft)}</td>
      <td>{spread(current)}</td>
    </tr>
  );
}
