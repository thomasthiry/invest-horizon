import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  Table, Title, Text, Stack, Alert, Loader, NumberFormatter, Button, Group, Tooltip,
} from '@mantine/core';
import { transactionsApi } from '../api/transactions';
import type { Holding } from '../api/types';

interface Props { portfolioId: string; }

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
    onSuccess: (holdings) => queryClient.setQueryData(['holdings', portfolioId], holdings),
  });

  if (isLoading) return <Loader />;
  if (error) return <Alert color="red">Failed to load holdings.</Alert>;
  if (!data || data.length === 0) return <Text c="dimmed">No open positions.</Text>;

  const totalInvested = data.reduce((s, h) => s + h.totalInvestedEur, 0);
  const totalMarketValue = data.reduce((s, h) => s + (h.marketValueEur ?? 0), 0);
  const totalUnrealized = data.reduce((s, h) => s + (h.unrealizedGainEur ?? 0), 0);

  const priced = data.filter(h => h.priceAsOf);
  const anyMissing = data.some(h => h.marketValueEur == null);
  const oldestAsOf = priced.length > 0
    ? priced.reduce((min, h) => (h.priceAsOf! < min ? h.priceAsOf! : min), priced[0].priceAsOf!)
    : null;
  const isStale = oldestAsOf == null || (Date.now() - new Date(oldestAsOf).getTime() > STALE_AFTER_MS);

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
        color={oldestAsOf == null ? 'gray' : isStale ? 'yellow' : 'green'}
        variant="light"
        py="xs"
      >
        {refresh.isError
          ? 'Could not refresh prices — showing last known values.'
          : oldestAsOf == null
            ? 'No live prices yet — click "Refresh prices" to value this portfolio.'
            : `Prices as of ${formatDateTime(oldestAsOf)}${isStale ? ' — may be outdated' : ''}${anyMissing ? ' · some positions have no quote' : ''}`}
      </Alert>

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
