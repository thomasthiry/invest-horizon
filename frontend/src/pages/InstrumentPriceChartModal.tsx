import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import {
  Modal, Stack, Group, Text, SegmentedControl, Loader, Alert, Badge, NumberFormatter,
} from '@mantine/core';
import { AreaChart } from '@mantine/charts';
import { instrumentsApi } from '../api/instruments';
import { transactionsApi } from '../api/transactions';
import type { Holding } from '../api/types';
import {
  ChartTooltipWithTransactions, MARKER_SERIES, markerAreaProps, withTransactionMarkers,
} from './transactionMarkers';
import { fromDate, toIsoDate, type Range } from './priceHistoryRange';

const RANGES: { label: string; value: Range }[] = [
  { label: '1M', value: '1M' },
  { label: '3M', value: '3M' },
  { label: '6M', value: '6M' },
  { label: '1Y', value: '1Y' },
];

function formatAxisDate(iso: string): string {
  return new Date(iso).toLocaleDateString(undefined, { month: 'short', day: 'numeric' });
}

/**
 * Recharts defaults a numeric y-axis to a domain starting at 0, which squashes a 125 → 130 move
 * into a flat line. Scope it to what is actually plotted instead. The avg-cost reference line is
 * folded in so it can never fall outside the domain and silently disappear, and a 5% margin keeps
 * the curve off the top and bottom edges.
 */
function priceDomain(closes: number[], avgCost: number): [number, number] {
  const values = avgCost > 0 ? [...closes, avgCost] : closes;
  const min = Math.min(...values);
  const max = Math.max(...values);
  const pad = (max - min) * 0.05 || Math.abs(max) * 0.05 || 1;
  return [min - pad, max + pad];
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

  const points = data?.map(p => ({ date: p.date, close: p.close })) ?? [];
  const ownTransactions = holding
    ? transactions?.filter(t => t.instrumentId === holding.instrumentId)
    : undefined;
  const chartData = withTransactionMarkers(points, ownTransactions, p => p.close);

  const currency = data?.[0]?.currency ?? holding?.currency;
  const chartSeries = [
    { name: 'close', label: `Price (${currency})`, color: 'teal.6' },
    ...MARKER_SERIES,
  ];
  const valueFormatter = (v: number) => `${v.toFixed(2)} ${currency ?? ''}`;

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
            series={chartSeries}
            curveType="monotone"
            withDots
            withGradient
            referenceLines={referenceLines}
            xAxisProps={{
              tickFormatter: formatAxisDate,
              minTickGap: 40,
              interval: 'preserveStartEnd',
            }}
            yAxisProps={{
              width: 70,
              domain: priceDomain(points.map(p => p.close), holding?.avgCostNative ?? 0),
            }}
            valueFormatter={valueFormatter}
            tooltipProps={{
              content: ({ label, payload }) => (
                <ChartTooltipWithTransactions
                  label={label}
                  payload={payload}
                  series={chartSeries}
                  valueFormatter={valueFormatter}
                />
              ),
            }}
            areaProps={(series) => markerAreaProps(series) ?? { dot: false }}
          />
        )}
      </Stack>
    </Modal>
  );
}
