import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import {
  Modal, Stack, Group, Text, SegmentedControl, Loader, Alert, Badge, NumberFormatter,
} from '@mantine/core';
import { AreaChart } from '@mantine/charts';
import { instrumentsApi } from '../api/instruments';
import { transactionsApi } from '../api/transactions';
import type { Holding, PriceHistoryPoint } from '../api/types';

type Range = '1M' | '3M' | '6M' | '1Y';

const RANGES: { label: string; value: Range }[] = [
  { label: '1M', value: '1M' },
  { label: '3M', value: '3M' },
  { label: '6M', value: '6M' },
  { label: '1Y', value: '1Y' },
];

function toIsoDate(d: Date): string {
  return d.toISOString().slice(0, 10);
}

function fromDate(range: Range): string {
  const d = new Date();
  switch (range) {
    case '1M': d.setMonth(d.getMonth() - 1); break;
    case '3M': d.setMonth(d.getMonth() - 3); break;
    case '6M': d.setMonth(d.getMonth() - 6); break;
    case '1Y': d.setFullYear(d.getFullYear() - 1); break;
  }
  return toIsoDate(d);
}

function formatAxisDate(iso: string): string {
  return new Date(iso).toLocaleDateString(undefined, { month: 'short', day: 'numeric' });
}

function findNearestPoint(points: PriceHistoryPoint[], date: string): PriceHistoryPoint | undefined {
  if (points.length === 0) return undefined;
  let nearest = points[0];
  let minDiff = Math.abs(new Date(nearest.date).getTime() - new Date(date).getTime());
  for (const p of points) {
    const diff = Math.abs(new Date(p.date).getTime() - new Date(date).getTime());
    if (diff < minDiff) { minDiff = diff; nearest = p; }
  }
  return nearest;
}

interface Props {
  portfolioId: string;
  holding: Holding | null;
  onClose: () => void;
}

export function InstrumentPriceChartModal({ portfolioId, holding, onClose }: Props) {
  const [range, setRange] = useState<Range>('1Y');

  const today = toIsoDate(new Date());
  const from = fromDate(range);

  const { data, isLoading, error } = useQuery({
    queryKey: ['price-history', holding?.instrumentId, range],
    queryFn: () => instrumentsApi.getPriceHistory(holding!.instrumentId, from, today),
    enabled: !!holding,
  });

  const { data: transactions } = useQuery({
    queryKey: ['transactions', portfolioId],
    queryFn: () => transactionsApi.getAll(portfolioId),
    enabled: !!holding,
  });

  const referenceLines = holding
    ? [{ y: holding.avgCostNative, label: `Avg cost`, color: 'orange.6' }]
    : undefined;

  const chartData = data?.map(point => {
    const row: Record<string, string | number | null> = { date: point.date, close: point.close };
    row.buyMarker = null;
    row.sellMarker = null;
    return row;
  }) ?? [];

  if (data && data.length > 0 && transactions && holding) {
    const byDate = new Map(data.map(p => [p.date, p]));
    const relevant = transactions.filter(
      t => t.instrumentId === holding.instrumentId && t.date >= from && t.date <= today,
    );
    for (const tx of relevant) {
      const point = byDate.get(tx.date) ?? findNearestPoint(data, tx.date);
      if (!point) continue;
      const row = chartData.find(r => r.date === point.date);
      if (!row) continue;
      if (tx.side === 'Buy') row.buyMarker = point.close;
      else row.sellMarker = point.close;
    }
  }

  return (
    <Modal
      opened={!!holding}
      onClose={onClose}
      title={
        <Stack gap={2}>
          <Text fw={600}>{holding?.name}</Text>
          <Group gap="xs">
            <Text size="xs" c="dimmed">{holding?.isin}</Text>
            <Badge size="xs" variant="light">{holding?.currency}</Badge>
          </Group>
        </Stack>
      }
      size="xl"
    >
      <Stack gap="sm">
        <Group justify="space-between" align="center">
          <Group gap="xs">
            <Text size="sm" c="dimmed">Avg cost:</Text>
            <Text size="sm" fw={500}>
              <NumberFormatter value={holding?.avgCostNative ?? 0} decimalScale={2} thousandSeparator />
              {' '}{holding?.currency}
            </Text>
          </Group>
          <SegmentedControl
            size="xs"
            value={range}
            onChange={v => setRange(v as Range)}
            data={RANGES}
          />
        </Group>

        {isLoading ? (
          <Group justify="center" py="xl"><Loader /></Group>
        ) : error ? (
          <Alert color="red">Failed to load price history.</Alert>
        ) : !data || data.length === 0 ? (
          <Text c="dimmed" ta="center" py="xl">No price data available for this period.</Text>
        ) : (
          <AreaChart
            h={320}
            data={chartData}
            dataKey="date"
            series={[
              { name: 'close', label: `Price (${data[0]?.currency ?? holding?.currency})`, color: 'teal.6' },
              { name: 'buyMarker', label: 'Buy', color: 'green.6' },
              { name: 'sellMarker', label: 'Sell', color: 'red.6' },
            ]}
            curveType="monotone"
            withDots
            withGradient
            referenceLines={referenceLines}
            xAxisProps={{
              tickFormatter: formatAxisDate,
              minTickGap: 40,
              interval: 'preserveStartEnd',
            }}
            yAxisProps={{ width: 70 }}
            valueFormatter={(v) => `${v.toFixed(2)} ${data[0]?.currency ?? ''}`}
            areaProps={(series) => {
              if (series.name === 'close') return { dot: false, activeDot: false };
              if (series.name === 'buyMarker' || series.name === 'sellMarker')
                return { fill: 'none', stroke: 'none', activeDot: false, connectNulls: false };
              return {};
            }}
          />
        )}
      </Stack>
    </Modal>
  );
}
