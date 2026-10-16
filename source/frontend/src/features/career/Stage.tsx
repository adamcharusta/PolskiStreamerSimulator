import { useState } from 'react';
import { useTranslation } from 'react-i18next';

import {
  type BatchOutcome,
  type CareerView,
  NextStepKind,
  type RollView,
  type WeekReportView,
} from '@/api/generated/game.schemas';

import { BatchLauncher } from './batch/BatchLauncher';
import { BatchResult } from './batch/BatchResult';
import { CommandNotice } from './CommandNotice';
import { EndedView } from './stage/EndedView';
import { LiveEvent } from './stage/LiveEvent';
import { PlanWeek } from './stage/PlanWeek';
import { RollResult } from './stage/RollResult';
import { SettleWeek } from './stage/SettleWeek';
import { WeekReport } from './stage/WeekReport';

/** A result the player reads before the stage moves to the server's next step. */
export type Revealed =
  | { readonly kind: 'roll'; readonly roll: RollView }
  | { readonly kind: 'report'; readonly report: WeekReportView }
  | { readonly kind: 'batch'; readonly outcome: BatchOutcome };

interface StageViewProps {
  readonly career: CareerView;
  readonly secret: string;
  readonly revealed: Revealed | null;
  readonly onReveal: (revealed: Revealed, announcement: string) => void;
  readonly onClose: () => void;
}

/** Server-driven (TD-98): the stage renders the career's next step; only revealed results are local. */
export function Stage({
  career,
  secret,
}: {
  readonly career: CareerView;
  readonly secret: string;
}) {
  const [revealed, setRevealed] = useState<Revealed | null>(null);
  const [announcement, setAnnouncement] = useState('');
  return (
    <div className="panel grid gap-3">
      <div aria-live="polite" className="sr-only">
        {announcement}
      </div>
      <CommandNotice careerId={career.careerId} />
      <StageView
        career={career}
        secret={secret}
        revealed={revealed}
        onReveal={(next, text) => {
          setRevealed(next);
          setAnnouncement(text);
        }}
        onClose={() => {
          setRevealed(null);
        }}
      />
    </div>
  );
}

function StageView({ career, secret, revealed, onReveal, onClose }: StageViewProps) {
  const { t } = useTranslation();
  if (revealed?.kind === 'roll') {
    return <RollResult roll={revealed.roll} onNext={onClose} />;
  }

  if (revealed?.kind === 'report') {
    return <WeekReport report={revealed.report} onNext={onClose} />;
  }

  if (revealed?.kind === 'batch') {
    return <BatchResult outcome={revealed.outcome} onNext={onClose} />;
  }

  switch (career.next.kind) {
    case NextStepKind.planWeek: {
      return (
        <PlanWeek key={career.version} career={career} secret={secret}>
          <BatchLauncher
            career={career}
            secret={secret}
            onDone={(outcome) => {
              onReveal({ kind: 'batch', outcome }, t(`batch.stop.${outcome.stopReason}`));
            }}
          />
        </PlanWeek>
      );
    }
    case NextStepKind.resolveEvent: {
      return (
        <LiveEvent
          key={career.version}
          career={career}
          secret={secret}
          onRolled={(roll) => {
            onReveal(
              { kind: 'roll', roll },
              t('roll.title', { outcome: t(`roll.outcome.${roll.outcome}`) }),
            );
          }}
        />
      );
    }
    case NextStepKind.settleWeek: {
      return (
        <SettleWeek
          career={career}
          secret={secret}
          onSettled={(report) => {
            onReveal({ kind: 'report', report }, t('report.title', { week: report.week }));
          }}
        />
      );
    }
    case NextStepKind.ended: {
      return <EndedView career={career} />;
    }
  }
}
