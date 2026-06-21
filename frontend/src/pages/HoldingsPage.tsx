import { useState } from 'react';
import { useMutation, useQuery, useQueryClient, type UseMutationResult } from '@tanstack/react-query';
import {
  Table, Title, Text, Stack, Alert, Loader, NumberFormatter, Button, Group, Tooltip, Paper, Skeleton,
  ActionIcon, SegmentedControl,
} from '@mantine/core';
import { useMediaQuery } from '@mantine/hooks';
import { AreaChart } from '@mantine/charts';
import { IconChartLine } from '@tabler/icons-react';
import { transactionsApi } from '../api/transactions';
import type { Holding, ValuationPoint } from '../api/types';
import { InstrumentPriceChartModal } from './InstrumentPriceChartModal';

interface Props { portfolioId: string; }

const eurFormatter = new Intl.NumberFormat(undefined, {
  style: 'currency', currency: 'EUR', maximumFractionDigits: 0,
});

function formatAxisDate(iso: string): string {
  return new Date(iso).toLocaleDateString(undefined, { month: 'short', year: '2-digit' });
}

type ChartMode  = 'value' | 'pnl' | 'return';
type ChartRange = '1M' | '3M' | '6M' | '1Y' | 'All';

function getRangeCutoff(range: ChartRange): string | null {
  if (range === 'All') return null;
  const d = new Date();
  if (range === '1M') d.setMonth(d.getMonth() - 1);
  if (range === '3M') d.setMonth(d.getMonth() - 3);
  if (range === '6M') d.setMonth(d.getMonth() - 6);
  if (range === '1Y') d.setFullYear(d.getFullYear() - 1);
  return d.toISOString().slice(0, 10);
}

const seriesConfig: Record<ChartMode, { name: string; label: string; color: string; strokeDasharray?: string }[]> = {
  value:  [
    { name: 'valueEur',             label: 'Market value',       color: 'teal.6'   },
    { name: 'investedEur',          label: 'Invested',           color: 'gray.5'   },
    { name: 'inflationBaselineEur', label: 'Inflation baseline', color: 'orange.5', strokeDasharray: '5 4' },
  ],
  pnl:    [{ name: 'pnl',       label: 'Gain / Loss', color: 'teal.6' }],
  return: [{ name: 'returnPct', label: 'Return',      color: 'teal.6' }],
};

function ValuationChart({ portfolioId }: Props) {
  const isMobile = useMediaQuery('(max-width: 48em)');
  const [mode,  setMode]  = useState<ChartMode>(
    () => (localStorage.getItem('chart-mode') as ChartMode | null) ?? 'value',
  );
  const [range, setRange] = useState<ChartRange>(
    () => (localStorage.getItem('chart-range') as ChartRange | null) ?? 'All',
  );

  const { data, isLoading, error } = useQuery({
    queryKey: ['valuation-history', portfolioId],
    queryFn: () => transactionsApi.getValuationHistory(portfolioId),
    enabled: !!portfolioId,
  });

  const chartHeight = isMobile ? 220 : 300;
  if (isLoading) return <Skeleton height={chartHeight} radius="md" />;
  if (error) return <Alert color="red">Failed to load valuation history.</Alert>;
  if (!data || data.length === 0) return null;

  const cutoff  = getRangeCutoff(range);
  const visible = cutoff ? data.filter((p: ValuationPoint) => p.date >= cutoff) : data;
  const chartData = visible.map((p: ValuationPoint) => ({
    date:                 p.date,
    valueEur:             p.valueEur,
    investedEur:          p.investedEur,
    inflationBaselineEur: p.inflationBaselineEur,
    pnl:                  p.valueEur - p.investedEur,
    returnPct:            p.investedEur > 0 ? ((p.valueEur - p.investedEur) / p.investedEur) * 100 : 0,
  }));

  const valueFormatter = mode === 'return'
    ? (v: number) => v.toFixed(1) + '%'
    : (v: number) => eurFormatter.format(v);

  const refLines = mode !== 'value'
    ? [{ y: 0, color: 'gray.4', label: '' }]
    : undefined;

  return (
    <Paper withBorder p="md" radius="md">
      <Group justify="space-between" wrap="wrap" mb="sm" gap="xs">
        <Title order={4}>Portfolio value over time</Title>
        <Group gap="xs">
          <SegmentedControl
            size="xs"
            value={mode}
            onChange={v => { setMode(v as ChartMode); localStorage.setItem('chart-mode', v); }}
            data={[
              { label: 'Value',      value: 'value'  },
              { label: 'P&L (€)',    value: 'pnl'    },
              { label: 'Return (%)', value: 'return' },
            ]}
          />
          <SegmentedControl
            size="xs"
            value={range}
            onChange={v => { setRange(v as ChartRange); localStorage.setItem('chart-range', v); }}
            data={['1M', '3M', '6M', '1Y', 'All'].map(v => ({ label: v, value: v }))}
          />
        </Group>
      </Group>
      <AreaChart
        h={chartHeight}
        data={chartData}
        dataKey="date"
        series={seriesConfig[mode]}
        curveType="monotone"
        withDots={false}
        withGradient
        referenceLines={refLines}
        valueFormatter={valueFormatter}
        xAxisProps={{ tickFormatter: formatAxisDate, minTickGap: isMobile ? 20 : 40 }}
        yAxisProps={{ width: isMobile ? 50 : 70 }}
        areaProps={(series) => series.name === 'inflationBaselineEur' ? { fillOpacity: 0 } : {}}
      />
    </Paper>
  );
}

// Prices older than this (or missing) are flagged as stale in the UI.
const STALE_AFTER_MS = 24 * 60 * 60 * 1000;

function formatDateTime(iso: string): string {
  return new Date(iso).toLocaleString(undefined, {
    day: '2-digit', month: 'short', year: 'numeric', hour: '2-digit', minute: '2-digit',
  });
}

export function HoldingsPage({ portfolioId }: Props) {
  const queryClient = useQueryClient();
  const { data, isLoading, error } = useQuery({
    queryKey: ['holdings', portfolioId],
    queryFn: () => transactionsApi.getHoldings(portfolioId),
    enabled: !!portfolioId,
  });

  // Reuses the same query key as ValuationChart — React Query dedupes the request.
  const { data: valuationData } = useQuery({
    queryKey: ['valuation-history', portfolioId],
    queryFn: () => transactionsApi.getValuationHistory(portfolioId),
    enabled: !!portfolioId,
  });
  const inflationBaseline = valuationData && valuationData.length > 0
    ? valuationData[valuationData.length - 1].inflationBaselineEur
    : undefined;

  const refresh = useMutation({
    mutationFn: () => transactionsApi.refreshPrices(portfolioId),
    onSuccess: (holdings) => {
      queryClient.setQueryData(['holdings', portfolioId], holdings);
      // Today's quote may have moved; let the value curve refetch its tail.
      queryClient.invalidateQueries({ queryKey: ['valuation-history', portfolioId] });
    },
  });

  return (
    <Stack>
      <ValuationChart portfolioId={portfolioId} />
      {isLoading ? <Loader />
        : error ? <Alert color="red">Failed to load holdings.</Alert>
        : !data || data.length === 0 ? <Text c="dimmed">No open positions.</Text>
        : <HoldingsSection portfolioId={portfolioId} data={data} refresh={refresh} inflationBaseline={inflationBaseline} />}
    </Stack>
  );
}

function HoldingsSection({ portfolioId, data, refresh, inflationBaseline }: {
  portfolioId: string;
  data: Holding[];
  refresh: UseMutationResult<Holding[], unknown, void, unknown>;
  inflationBaseline?: number;
}) {
  const [chartHolding, setChartHolding] = useState<Holding | null>(null);
  const totalInvested = data.reduce((s, h) => s + h.totalInvestedEur, 0);
  const totalMarketValue = data.reduce((s, h) => s + (h.marketValueEur ?? 0), 0);
  const totalUnrealized = data.reduce((s, h) => s + (h.unrealizedGainEur ?? 0), 0);

  const priced = data.filter(h => h.priceFetchedAt);
  const anyMissing = data.some(h => h.marketValueEur == null);
  const oldestFetchedAt = priced.length > 0
    ? priced.reduce((min, h) => (h.priceFetchedAt! < min ? h.priceFetchedAt! : min), priced[0].priceFetchedAt!)
    : null;
  const oldestAsOf = priced.length > 0
    ? priced.reduce((min, h) => (h.priceAsOf! < min ? h.priceAsOf! : min), priced[0].priceAsOf!)
    : null;
  const isStale = oldestFetchedAt == null || (Date.now() - new Date(oldestFetchedAt).getTime() > STALE_AFTER_MS);

  return (
    <Stack>
      <Group justify="space-between" align="flex-end">
        <Title order={3}>Open Positions</Title>
        <Button
          data-testid="refresh-prices-btn"
          size="xs"
          variant="light"
          loading={refresh.isPending}
          onClick={() => refresh.mutate()}
        >
          Refresh prices
        </Button>
      </Group>

      <Alert
        data-testid="prices-asof"
        color={oldestFetchedAt == null ? 'gray' : isStale ? 'yellow' : 'green'}
        variant="light"
        py="xs"
      >
        {refresh.isError
          ? 'Could not refresh prices — showing last known values.'
          : oldestFetchedAt == null
            ? 'No live prices yet — click "Refresh prices" to value this portfolio.'
            : `Last refreshed: ${formatDateTime(oldestFetchedAt)}${oldestAsOf ? ` · Market data from ${formatDateTime(oldestAsOf)}` : ''}${isStale ? ' — may be outdated' : ''}${anyMissing ? ' · some positions have no quote' : ''}`}
      </Alert>

      <InstrumentPriceChartModal portfolioId={portfolioId} holding={chartHolding} onClose={() => setChartHolding(null)} />

      <Table.ScrollContainer minWidth={800}>
        <Table striped highlightOnHover withTableBorder>
          <Table.Thead>
            <Table.Tr>
              <Table.Th>Security</Table.Th>
              <Table.Th>ISIN</Table.Th>
              <Table.Th>Currency</Table.Th>
              <Table.Th ta="right">Quantity</Table.Th>
              <Table.Th ta="right">Avg Cost (€)</Table.Th>
              <Table.Th ta="right">Invested (€)</Table.Th>
              <Table.Th ta="right">Price</Table.Th>
              <Table.Th ta="right">Market Value (€)</Table.Th>
              <Table.Th ta="right">Unrealized P/L</Table.Th>
              <Table.Th />
            </Table.Tr>
          </Table.Thead>
          <Table.Tbody>
            {data.map(h => (
              <HoldingRow key={h.instrumentId} h={h} onShowChart={() => setChartHolding(h)} />
            ))}
          </Table.Tbody>
          <Table.Tfoot>
            <Table.Tr>
              <Table.Th colSpan={5}>Totals</Table.Th>
              <Table.Th ta="right">
                <NumberFormatter value={totalInvested} decimalScale={2} thousandSeparator />
              </Table.Th>
              <Table.Th />
              <Table.Th ta="right">
                {priced.length > 0
                  ? <NumberFormatter value={totalMarketValue} decimalScale={2} thousandSeparator />
                  : <Text c="dimmed">—</Text>}
              </Table.Th>
              <Table.Th ta="right">
                {priced.length > 0 ? <PnL value={totalUnrealized} /> : <Text c="dimmed">—</Text>}
              </Table.Th>
              <Table.Th />
            </Table.Tr>
            {inflationBaseline != null && priced.length > 0 && (
              <Table.Tr>
                <Table.Th colSpan={5}>
                  <Text size="xs" c="dimmed">Real P/L (inflation-adj.)</Text>
                </Table.Th>
                <Table.Th />
                <Table.Th />
                <Table.Th />
                <Table.Th ta="right">
                  <PnL value={totalMarketValue - inflationBaseline} />
                </Table.Th>
                <Table.Th />
              </Table.Tr>
            )}
          </Table.Tfoot>
        </Table>
      </Table.ScrollContainer>
    </Stack>
  );
}

function HoldingRow({ h, onShowChart }: { h: Holding; onShowChart: () => void }) {
  const pnlPct = h.unrealizedGainEur != null && h.totalInvestedEur > 0
    ? (h.unrealizedGainEur / h.totalInvestedEur) * 100
    : null;

  return (
    <Table.Tr>
      <Table.Td>{h.name}</Table.Td>
      <Table.Td>{h.isin}</Table.Td>
      <Table.Td>{h.currency}</Table.Td>
      <Table.Td ta="right"><NumberFormatter value={h.openQuantity} decimalScale={4} /></Table.Td>
      <Table.Td ta="right"><NumberFormatter value={h.avgCostEur} decimalScale={2} thousandSeparator /></Table.Td>
      <Table.Td ta="right"><NumberFormatter value={h.totalInvestedEur} decimalScale={2} thousandSeparator /></Table.Td>
      <Table.Td ta="right">
        {h.currentPriceNative != null ? (
          <Tooltip
            label={h.priceAsOf
              ? `${h.priceSource ?? 'price'} · ${formatDateTime(h.priceAsOf)}`
              : ''}
            disabled={!h.priceAsOf}
          >
            <span>
              <NumberFormatter value={h.currentPriceNative} decimalScale={2} thousandSeparator />
              {' '}{h.priceCurrency}
            </span>
          </Tooltip>
        ) : <Text c="dimmed">—</Text>}
      </Table.Td>
      <Table.Td ta="right" data-testid="market-value">
        {h.marketValueEur != null
          ? <NumberFormatter value={h.marketValueEur} decimalScale={2} thousandSeparator />
          : <Text c="dimmed">—</Text>}
      </Table.Td>
      <Table.Td ta="right" data-testid="unrealized-pl">
        {h.unrealizedGainEur != null
          ? <PnL value={h.unrealizedGainEur} pct={pnlPct} />
          : <Text c="dimmed">—</Text>}
      </Table.Td>
      <Table.Td>
        <Tooltip label="Price history">
          <ActionIcon variant="subtle" color="gray" size="sm" onClick={onShowChart}>
            <IconChartLine size={16} />
          </ActionIcon>
        </Tooltip>
      </Table.Td>
    </Table.Tr>
  );
}

function PnL({ value, pct }: { value: number; pct?: number | null }) {
  const color = value > 0 ? 'teal' : value < 0 ? 'red' : undefined;
  const sign = value > 0 ? '+' : '';
  return (
    <Text c={color} span>
      {sign}<NumberFormatter value={value} decimalScale={2} thousandSeparator />
      {pct != null && <Text c={color} span size="xs"> ({sign}{pct.toFixed(1)}%)</Text>}
    </Text>
  );
}
