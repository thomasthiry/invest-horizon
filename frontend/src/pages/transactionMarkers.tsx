import type { ReactNode } from 'react';
import { Badge, Divider, Group, Paper, Stack, Text, useMantineTheme, getThemeColor } from '@mantine/core';
import { getFilteredChartTooltipPayload, type ChartSeries } from '@mantine/charts';
import type { Transaction } from '../api/types';

export interface TransactionMarkers {
  buyMarker: number | null;
  sellMarker: number | null;
  /** The transactions the dots on this point stand for — what the tooltip spells out. */
  markerTransactions: Transaction[];
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
  const rows = points.map(p => ({
    ...p,
    buyMarker: null as number | null,
    sellMarker: null as number | null,
    markerTransactions: [] as Transaction[],
  }));
  if (rows.length === 0 || !transactions || transactions.length === 0) return rows;

  const indexByDate = new Map(points.map((p, i) => [p.date, i]));
  const first = points[0].date;
  const last = points[points.length - 1].date;

  for (const tx of transactions) {
    if (tx.date < first || tx.date > last) continue;
    const i = indexByDate.get(tx.date) ?? nearestIndex(points, tx.date);
    if (tx.side === 'Buy') rows[i].buyMarker = valueAt(points[i]);
    else rows[i].sellMarker = valueAt(points[i]);
    rows[i].markerTransactions.push(tx);
  }
  return rows;
}

const eurTooltipFormatter = new Intl.NumberFormat(undefined, {
  style: 'currency', currency: 'EUR', minimumFractionDigits: 2, maximumFractionDigits: 2,
});
const qtyTooltipFormatter = new Intl.NumberFormat(undefined, { maximumFractionDigits: 4 });
const priceTooltipFormatter = new Intl.NumberFormat(undefined, {
  minimumFractionDigits: 2, maximumFractionDigits: 4,
});

// A hovered date can carry several orders; past this the tooltip grows taller than the chart, and
// it can't be scrolled because recharts tooltips don't take pointer events.
const MAX_TOOLTIP_TRANSACTIONS = 4;

function TransactionDetails({ tx, withInstrument }: { tx: Transaction; withInstrument: boolean }) {
  const fees = tx.brokerFee + tx.tobAmount;
  return (
    <Stack gap={2}>
      <Group gap="xs" wrap="nowrap">
        <Badge size="xs" color={tx.side === 'Buy' ? 'green' : 'red'} variant="light">{tx.side}</Badge>
        {withInstrument && (
          <Text size="xs" fw={500} lineClamp={1}>{tx.instrumentName ?? tx.isin}</Text>
        )}
        <Text size="xs" c="dimmed">{tx.date}</Text>
      </Group>
      <Text size="xs">
        {qtyTooltipFormatter.format(tx.quantity)} × {priceTooltipFormatter.format(tx.unitPrice)}{' '}
        {tx.currency} · {tx.broker}
      </Text>
      <Text size="xs" c="dimmed">
        Amount {eurTooltipFormatter.format(tx.amountEur)} · fee{' '}
        {eurTooltipFormatter.format(tx.brokerFee)} · TOB {eurTooltipFormatter.format(tx.tobAmount)}
      </Text>
      <Text size="xs">
        {tx.side === 'Buy' ? 'Total cost ' : 'Net proceeds '}
        <Text span fw={600}>
          {eurTooltipFormatter.format(tx.side === 'Buy' ? tx.totalCost : tx.netProceeds)}
        </Text>
        {fees > 0 && <Text span c="dimmed"> (incl. {eurTooltipFormatter.format(fees)} costs)</Text>}
      </Text>
    </Stack>
  );
}

interface TooltipProps {
  label: ReactNode;
  payload: readonly Record<string, any>[] | undefined;
  series: ChartSeries[];
  valueFormatter?: (value: number) => string;
  /** Portfolio-wide charts mix instruments, so their dots have to name the security. */
  withInstrument?: boolean;
}

/**
 * Mantine's own tooltip drops the marker series (it filters out `fill: 'none'` entries), so a dot
 * on the chart reads as decoration with nothing behind it. This replaces it: same series rows, plus
 * the orders the dots stand for. Pass it through `tooltipProps={{ content: ... }}`, which overrides
 * the chart's built-in content.
 */
export function ChartTooltipWithTransactions({
  label, payload, series, valueFormatter, withInstrument = false,
}: TooltipProps) {
  const theme = useMantineTheme();
  if (!payload) return null;

  const items = getFilteredChartTooltipPayload(payload) as Record<string, any>[];
  const transactions: Transaction[] = (payload[0] as any)?.payload?.markerTransactions ?? [];
  const shown = transactions.slice(0, MAX_TOOLTIP_TRANSACTIONS);

  return (
    <Paper px="md" py="sm" withBorder shadow="md" radius="md" style={{ maxWidth: 320 }}>
      {label && <Text fw={500} mb={5} size="sm">{label}</Text>}
      <Stack gap={2}>
        {items.map(item => (
          <Group key={item.name} justify="space-between" gap="lg" wrap="nowrap">
            <Group gap={6} wrap="nowrap">
              <svg width={12} height={12}>
                <circle r={6} cx={6} cy={6} fill={getThemeColor(item.color, theme)} />
              </svg>
              <Text size="xs">
                {series.find(s => s.name === item.name)?.label ?? item.name}
              </Text>
            </Group>
            <Text size="xs" fw={500}>
              {valueFormatter ? valueFormatter(item.payload[item.name]) : item.payload[item.name]}
            </Text>
          </Group>
        ))}
      </Stack>

      {shown.length > 0 && (
        <>
          <Divider my="xs" />
          <Stack gap="xs">
            {shown.map(tx => (
              <TransactionDetails key={tx.id} tx={tx} withInstrument={withInstrument} />
            ))}
            {transactions.length > shown.length && (
              <Text size="xs" c="dimmed">
                +{transactions.length - shown.length} more transaction
                {transactions.length - shown.length > 1 ? 's' : ''} on this date
              </Text>
            )}
          </Stack>
        </>
      )}
    </Paper>
  );
}
