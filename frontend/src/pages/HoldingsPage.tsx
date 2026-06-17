import { useMutation, useQuery, useQueryClient, type UseMutationResult } from '@tanstack/react-query';
import {
  Table, Title, Text, Stack, Alert, Loader, NumberFormatter, Button, Group, Tooltip, Paper, Skeleton,
} from '@mantine/core';
import { useMediaQuery } from '@mantine/hooks';
import { AreaChart } from '@mantine/charts';
import { transactionsApi } from '../api/transactions';
import type { Holding, ValuationPoint } from '../api/types';

interface Props { portfolioId: string; }

const eurFormatter = new Intl.NumberFormat(undefined, {
  style: 'currency', currency: 'EUR', maximumFractionDigits: 0,
});

function formatAxisDate(iso: string): string {
  return new Date(iso).toLocaleDateString(undefined, { month: 'short', year: '2-digit' });
}

function ValuationChart({ portfolioId }: Props) {
  const isMobile = useMediaQuery('(max-width: 48em)');
  const { data, isLoading, error } = useQuery({
    queryKey: ['valuation-history', portfolioId],
    queryFn: () => transactionsApi.getValuationHistory(portfolioId),
    enabled: !!portfolioId,
  });

  const chartHeight = isMobile ? 220 : 300;
  if (isLoading) return <Skeleton height={chartHeight} radius="md" />;
  if (error) return <Alert color="red">Failed to load valuation history.</Alert>;
  if (!data || data.length === 0) return null;

  return (
    <Paper withBorder p="md" radius="md">
      <Title order={4} mb="sm">Portfolio value over time</Title>
      <AreaChart
        h={chartHeight}
        data={data as ValuationPoint[]}
        dataKey="date"
        series={[
          { name: 'valueEur', label: 'Market value', color: 'teal.6' },
          { name: 'investedEur', label: 'Invested', color: 'gray.5' },
        ]}
        curveType="monotone"
        withDots={false}
        withGradient
        valueFormatter={(value) => eurFormatter.format(value)}
        xAxisProps={{ tickFormatter: formatAxisDate, minTickGap: isMobile ? 20 : 40 }}
        yAxisProps={{ width: isMobile ? 50 : 70 }}
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
        : <HoldingsSection data={data} refresh={refresh} />}
    </Stack>
  );
}

function HoldingsSection({ data, refresh }: {
  data: Holding[];
  refresh: UseMutationResult<Holding[], unknown, void, unknown>;
}) {
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
            </Table.Tr>
          </Table.Thead>
          <Table.Tbody>
            {data.map(h => (
              <HoldingRow key={h.instrumentId} h={h} />
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
            </Table.Tr>
          </Table.Tfoot>
        </Table>
      </Table.ScrollContainer>
    </Stack>
  );
}

function HoldingRow({ h }: { h: Holding }) {
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
