import { useEffect, useRef } from 'react';
import { useTranslation } from 'react-i18next';

import { type CareerView, Category, EquipmentSlot } from '@/api/generated/game.schemas';
import { stat } from '@/format';

import { useDesk } from './deskStore';

const bars = ['energy', 'burnout', 'drama', 'credibility'] as const;

export function StatsRail({ career }: { readonly career: CareerView }) {
  const { t } = useTranslation();
  const open = useDesk((state) => state.statsOpen);
  const stats = career.stats;
  const heading = useRef<HTMLHeadingElement>(null);
  const wasOpen = useRef(open);
  // Opening the drawer moves focus into it, so the player lands on the statistics on a phone.
  useEffect(() => {
    if (open && !wasOpen.current) {
      heading.current?.focus();
    }
    wasOpen.current = open;
  }, [open]);
  return (
    <aside
      id="stats-rail"
      className={open ? 'rail panel open' : 'rail panel'}
      aria-label={t('career.stats')}
    >
      <h2 ref={heading} tabIndex={-1} className="panel-title">
        {t('career.stats')}
      </h2>
      {bars.map((bar) => {
        const id = `stat-${bar}`;
        return (
          <div key={bar} className="grid grid-cols-[1fr_auto] items-center gap-x-2">
            <span id={id}>{t(`stat.${bar}`)}</span>
            <span>{stat(stats[bar])}</span>
            <meter
              className="col-span-2 w-full"
              min={0}
              max={100}
              value={stats[bar]}
              aria-labelledby={id}
            />
          </div>
        );
      })}
      <table className="stat-table mt-2 w-full">
        <caption className="text-left font-bold">{t('stat.skill')}</caption>
        <thead>
          <tr>
            <th scope="col">{t('plan.category')}</th>
            <th scope="col">{t('stat.skill')}</th>
            <th scope="col">{t('stat.boredom')}</th>
          </tr>
        </thead>
        <tbody>
          {Object.values(Category).map((category) => (
            <tr key={category}>
              <th scope="row" className="text-left">
                {t(`category.${category}`)}
              </th>
              <td>{stat(stats.skill[category])}</td>
              <td>{stat(stats.boredom[category])}</td>
            </tr>
          ))}
        </tbody>
      </table>
      <h3 className="font-bold">{t('stat.equipment')}</h3>
      <ul>
        {Object.values(EquipmentSlot).map((slot) => (
          <li key={slot}>
            {t(`slot.${slot}`)}: {t('stat.tier', { value: stats.equipment[slot] })}
          </li>
        ))}
      </ul>
    </aside>
  );
}
