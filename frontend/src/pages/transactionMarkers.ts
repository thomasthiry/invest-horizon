import type { Transaction } from '../api/types';

export interface TransactionMarkers {
  buyMarker: number | null;
  sellMarker: number | null;
}

/** Extra series that render the buy/sell dots. Append to a chart's own series. */
export const MARKER_SERIES = [
  { name: 'buyMarker',  label: 'Buy',  color: 'green.6' },
  { name: 'sellMarker', label: 'Sell', color: 'red.6'   },
];

/**
 * Mantine only renders its dot layer when `withDots` is on, so charts with markers must enable it
 * and then strip the dots back off their real series. For the marker series we hide the line/area
 * with `fill`/`stroke: 'none'` rather than the *Opacity props — SVG children inherit opacity, which
 * would hide the dots along with the line.
 *
 * Returns null for non-marker series so callers can supply their own props.
 */
export function markerAreaProps(series: { name: string }): Record<string, unknown> | null {
  return series.name === 'buyMarker' || series.name === 'sellMarker'
    ? { fill: 'none', stroke: 'none', activeDot: false, connectNulls: false }
    : null;
}

function nearestIndex(points: { date: string }[], date: string): number {
  const target = new Date(date).getTime();
  let best = 0;
  let bestDiff = Infinity;
  points.forEach((p, i) => {
    const diff = Math.abs(new Date(p.date).getTime() - target);
    if (diff < bestDiff) { bestDiff = diff; best = i; }
  });
  return best;
}

/**
 * Puts a dot on every chart point where a transaction happened, at the value of the line the dot
 * should sit on (`valueAt`). Transactions falling on a date with no point — a weekend or holiday on
 * a price series — snap to the nearest point; those outside the rendered window are dropped so a
 * short range doesn't pile old transactions onto its left edge.
 */
export function withTransactionMarkers<T extends { date: string }>(
  points: T[],
  transactions: Transaction[] | undefined,
  valueAt: (point: T) => number,
): (T & TransactionMarkers)[] {
  const rows = points.map(p => ({ ...p, buyMarker: null as number | null, sellMarker: null as number | null }));
  if (rows.length === 0 || !transactions || transactions.length === 0) return rows;

  const indexByDate = new Map(points.map((p, i) => [p.date, i]));
  const first = points[0].date;
  const last = points[points.length - 1].date;

  for (const tx of transactions) {
    if (tx.date < first || tx.date > last) continue;
    const i = indexByDate.get(tx.date) ?? nearestIndex(points, tx.date);
    if (tx.side === 'Buy') rows[i].buyMarker = valueAt(points[i]);
    else rows[i].sellMarker = valueAt(points[i]);
  }
  return rows;
}
