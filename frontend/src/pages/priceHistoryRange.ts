/** Ranges offered by the price-history charts. Shared so every caller derives the same window
 *  — and therefore the same React Query cache entry — for a given range. */
export type Range = '1M' | '3M' | '6M' | '1Y';

export function toIsoDate(d: Date): string {
  return d.toISOString().slice(0, 10);
}

export function fromDate(range: Range): string {
  const d = new Date();
  switch (range) {
    case '1M': d.setMonth(d.getMonth() - 1); break;
    case '3M': d.setMonth(d.getMonth() - 3); break;
    case '6M': d.setMonth(d.getMonth() - 6); break;
    case '1Y': d.setFullYear(d.getFullYear() - 1); break;
  }
  return toIsoDate(d);
}
