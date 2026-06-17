import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Table, Title, Stack, Alert, Loader, Badge, NumberFormatter, Button, Group, Modal, Text, ActionIcon, Tooltip } from '@mantine/core';
import { IconEdit, IconTrash } from '@tabler/icons-react';
import { transactionsApi } from '../api/transactions';
import { TransactionForm } from './TransactionForm';
import type { Transaction } from '../api/types';

interface Props { portfolioId: string; }

export function TransactionsPage({ portfolioId }: Props) {
  const qc = useQueryClient();
  const [addOpen, setAddOpen] = useState(false);
  const [editTx, setEditTx] = useState<Transaction | null>(null);
  const [deleteTx, setDeleteTx] = useState<Transaction | null>(null);
  const [deleteError, setDeleteError] = useState('');

  const { data, isLoading, error } = useQuery({
    queryKey: ['transactions', portfolioId],
    queryFn: () => transactionsApi.getAll(portfolioId),
    enabled: !!portfolioId,
  });

  const deleteMutation = useMutation({
    mutationFn: (id: string) => transactionsApi.remove(portfolioId, id),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['transactions', portfolioId] });
      qc.invalidateQueries({ queryKey: ['holdings', portfolioId] });
      qc.invalidateQueries({ queryKey: ['realized', portfolioId] });
      qc.invalidateQueries({ queryKey: ['valuation-history', portfolioId] });
      setDeleteTx(null);
      setDeleteError('');
    },
    onError: (err: { response?: { data?: { message?: string } } }) => {
      setDeleteError(err?.response?.data?.message ?? 'Failed to delete transaction.');
    },
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

      <Modal opened={!!editTx} onClose={() => setEditTx(null)} title="Edit Transaction" size="lg">
        {editTx && (
          <TransactionForm
            portfolioId={portfolioId}
            transaction={editTx}
            onSuccess={() => setEditTx(null)}
          />
        )}
      </Modal>

      <Modal
        opened={!!deleteTx}
        onClose={() => { setDeleteTx(null); setDeleteError(''); }}
        title="Delete Transaction"
        size="sm"
      >
        <Stack>
          <Text>
            Delete <strong>{deleteTx?.side}</strong> of <strong>{deleteTx?.instrumentName ?? deleteTx?.isin}</strong> on <strong>{deleteTx?.date}</strong>?
            This will recompute FIFO allocations for the affected instrument.
          </Text>
          {deleteError && <Alert color="red">{deleteError}</Alert>}
          <Group justify="flex-end">
            <Button variant="default" onClick={() => { setDeleteTx(null); setDeleteError(''); }}>Cancel</Button>
            <Button
              color="red"
              loading={deleteMutation.isPending}
              onClick={() => deleteTx && deleteMutation.mutate(deleteTx.id)}
            >
              Delete
            </Button>
          </Group>
        </Stack>
      </Modal>

      {(!data || data.length === 0) && <Text c="dimmed">No transactions yet.</Text>}
      {data && data.length > 0 && (
        <Table.ScrollContainer minWidth={900}>
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
              <Table.Th />
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
                <Table.Td ta="right">
                  <Tooltip
                    label={`${tx.amountEur > 0 ? ((tx.tobAmount / tx.amountEur) * 100).toFixed(3) : '0.000'}%`}
                    withArrow
                    position="top"
                  >
                    <span style={{ cursor: 'default', textDecoration: 'underline dotted' }}>
                      <NumberFormatter value={tx.tobAmount} decimalScale={2} />
                    </span>
                  </Tooltip>
                </Table.Td>
                <Table.Td ta="right">
                  {tx.side === 'Buy'
                    ? <Text c="red" size="sm" fw={600}>€<NumberFormatter value={tx.totalCost} decimalScale={2} thousandSeparator /></Text>
                    : <Text c="green" size="sm" fw={600}>€<NumberFormatter value={tx.netProceeds} decimalScale={2} thousandSeparator /></Text>
                  }
                </Table.Td>
                <Table.Td>
                  <Group gap="xs" justify="flex-end" wrap="nowrap">
                    <ActionIcon variant="subtle" onClick={() => setEditTx(tx)} aria-label="Edit transaction">
                      <IconEdit size={16} />
                    </ActionIcon>
                    <ActionIcon variant="subtle" color="red" onClick={() => { setDeleteTx(tx); setDeleteError(''); }} aria-label="Delete transaction">
                      <IconTrash size={16} />
                    </ActionIcon>
                  </Group>
                </Table.Td>
              </Table.Tr>
            ))}
          </Table.Tbody>
        </Table>
        </Table.ScrollContainer>
      )}
    </Stack>
  );
}
