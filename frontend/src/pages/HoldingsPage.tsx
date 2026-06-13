import { useQuery } from '@tanstack/react-query';
import { Table, Title, Text, Stack, Alert, Loader, NumberFormatter } from '@mantine/core';
import { transactionsApi } from '../api/transactions';

interface Props { portfolioId: string; }

export function HoldingsPage({ portfolioId }: Props) {
  const { data, isLoading, error } = useQuery({
    queryKey: ['holdings', portfolioId],
    queryFn: () => transactionsApi.getHoldings(portfolioId),
    enabled: !!portfolioId,
  });

  if (isLoading) return <Loader />;
  if (error) return <Alert color="red">Failed to load holdings.</Alert>;
  if (!data || data.length === 0) return <Text c="dimmed">No open positions.</Text>;

  const totalInvested = data.reduce((s, h) => s + h.totalInvestedEur, 0);

  return (
    <Stack>
      <Title order={3}>Open Positions</Title>
      <Table striped highlightOnHover withTableBorder>
        <Table.Thead>
          <Table.Tr>
            <Table.Th>Security</Table.Th>
            <Table.Th>ISIN</Table.Th>
            <Table.Th>Currency</Table.Th>
            <Table.Th ta="right">Quantity</Table.Th>
            <Table.Th ta="right">Avg Cost (€)</Table.Th>
            <Table.Th ta="right">Invested (€)</Table.Th>
          </Table.Tr>
        </Table.Thead>
        <Table.Tbody>
          {data.map(h => (
            <Table.Tr key={h.instrumentId}>
              <Table.Td>{h.name}</Table.Td>
              <Table.Td>{h.isin}</Table.Td>
              <Table.Td>{h.currency}</Table.Td>
              <Table.Td ta="right"><NumberFormatter value={h.openQuantity} decimalScale={4} /></Table.Td>
              <Table.Td ta="right"><NumberFormatter value={h.avgCostEur} decimalScale={2} thousandSeparator /></Table.Td>
              <Table.Td ta="right"><NumberFormatter value={h.totalInvestedEur} decimalScale={2} thousandSeparator /></Table.Td>
            </Table.Tr>
          ))}
        </Table.Tbody>
        <Table.Tfoot>
          <Table.Tr>
            <Table.Th colSpan={5}>Total invested</Table.Th>
            <Table.Th ta="right">
              <NumberFormatter value={totalInvested} decimalScale={2} thousandSeparator />
            </Table.Th>
          </Table.Tr>
        </Table.Tfoot>
      </Table>
    </Stack>
  );
}
