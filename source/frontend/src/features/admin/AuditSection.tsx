import { useTranslation } from 'react-i18next';

import { useListAudit } from '@/api/generated/game';
import { date } from '@/format';

/** The newest owner changes, newest first (spec M4-D §5). */
export function AuditSection() {
  const { t } = useTranslation();
  const audit = useListAudit();
  const title = t('admin.audit.title');
  let body = <output className="block">{t('admin.loading')}</output>;
  if (audit.isError) {
    body = <p role="alert">{t('admin.failed')}</p>;
  } else if (audit.data?.length === 0) {
    body = <p className="m-0">{t('admin.audit.empty')}</p>;
  } else if (audit.data !== undefined) {
    body = (
      <ol className="grid gap-1">
        {audit.data.map((record) => (
          <li key={record.id}>
            <span className="font-bold">
              {t(`admin.audit.action.${record.action}`, record.action)}
            </span>{' '}
            {t('admin.audit.line', {
              target: record.target,
              actor: record.actor,
              at: date(record.at),
              reason: record.reason,
            })}
          </li>
        ))}
      </ol>
    );
  }

  return (
    <section className="panel grid gap-2" aria-label={title}>
      <h2 className="panel-title">{title}</h2>
      {body}
    </section>
  );
}
