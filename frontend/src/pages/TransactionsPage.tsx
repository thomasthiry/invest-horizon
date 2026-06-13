import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Table, Title, Stack, Alert, Loader, Badge, NumberFormatter, Button, Group, Modal, Text } from '@mantine/core';
import { transactionsApi } from '../api/transactions';
import { TransactionForm } from './TransactionForm';
import type { Transaction } from '../api/types';

interface Props { portfolioId: string; }

export function TransactionsPage({ portfolioId }: Props) {
  const [addOpen, setAddOpen] = useState(false);
  const { data, isLoading, error } = useQuery({
    queryKey: ['transactions', portfolioId],
    queryFn: () => transactionsApi.getAll(portfolioId),
    enabled: !!portfolioId,
  });

  if (isLoading) return <Loader />;
  if (error) return <Alert color="red">Failed to load transactions.</Alert>;

  return (
    <Stack>
      <Group justify="space-between">
        <Title order={3}>Transactions</Title>
        <Button size="sm" onClick={() => setAddOpen(true)} data-testid="add-transaction-btn">+ Add Transaction</Button>
      </Group>

      <Modal opened={addOpen} onClose={() => setAddOpen(false)} title="New Transaction" size="lg">
        <TransactionForm portfolioId={portfolioId} onSuccess={() => setAddOpen(false)} />
      </Modal>

      {(!data || data.length === 0) && <Text c="dimmed">No transactions yet.</Text>}
      {data && data.length > 0 && (
        <Table striped highlightOnHover withTableBorder fz="sm">
          <Table.Thead>
            <Table.Tr>
              <Table.Th>Date</Table.Th>
              <Table.Th>Security</Table.Th>
              <Table.Th>Side</Table.Th>
              <Table.Th>Broker</Table.Th>
              <Table.Th ta="right">Qty</Table.Th>
              <Table.Th ta="right">Unit Price</Table.Th>
              <Table.Th ta="right">Amount (€)</Table.Th>
              <Table.Th ta="right">Broker Fee</Table.Th>
              <Table.Th ta="right">TOB</Table.Th>
              <Table.Th ta="right">Total Cost / Net</Table.Th>
            </Table.Tr>
          </Table.Thead>
          <Table.Tbody>
            {data.map((tx: Transaction) => (
              <Table.Tr key={tx.id}>
                <Table.Td>{tx.date}</Table.Td>
                <Table.Td>{tx.instrumentName ?? tx.isin}</Table.Td>
                <Table.Td>
                  <Badge color={tx.side === 'Buy' ? 'blue' : 'green'}>{tx.side}</Badge>
                </Table.Td>
                <Table.Td>{tx.broker}</Table.Td>
                <Table.Td ta="right"><NumberFormatter value={tx.quantity} decimalScale={4} /></Table.Td>
                <Table.Td ta="right"><NumberFormatter value={tx.unitPrice} decimalScale={4} /> {tx.currency}</Table.Td>
                <Table.Td ta="right"><NumberFormatter value={tx.amountEur} decimalScale={2} thousandSeparator /></Table.Td>
                <Table.Td ta="right"><NumberFormatter value={tx.brokerFee} decimalScale={2} /></Table.Td>
                <Table.Td ta="right"><NumberFormatter value={tx.tobAmount} decimalScale={2} /></Table.Td>
                <Table.Td ta="right">
                  {tx.side === 'Buy'
                    ? <Text c="red" size="sm" fw={600}>€<NumberFormatter value={tx.totalCost} decimalScale={2} thousandSeparator /></Text>
                    : <Text c="green" size="sm" fw={600}>€<NumberFormatter value={tx.netProceeds} decimalScale={2} thousandSeparator /></Text>
                  }
                </Table.Td>
              </Table.Tr>
            ))}
          </Table.Tbody>
        </Table>
      )}
    </Stack>
  );
}
