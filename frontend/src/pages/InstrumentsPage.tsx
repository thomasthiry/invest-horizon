import { useState } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import {
  Table, Title, Stack, Alert, Loader, Button, Group, Modal, TextInput,
  Select, Text
} from '@mantine/core';
import { useForm } from '@mantine/form';
import { instrumentsApi } from '../api/instruments';
import type { InstrumentType } from '../api/types';

export function InstrumentsPage() {
  const [addOpen, setAddOpen] = useState(false);
  const qc = useQueryClient();

  const { data, isLoading, error } = useQuery({
    queryKey: ['instruments'],
    queryFn: instrumentsApi.getAll,
  });

  const form = useForm({
    initialValues: {
      isin: '',
      name: '',
      type: 'Etf' as InstrumentType,
      currency: 'EUR',
      ticker: '',
    },
  });

  const mutation = useMutation({
    mutationFn: instrumentsApi.create,
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['instruments'] });
      form.reset();
      setAddOpen(false);
    },
  });

  if (isLoading) return <Loader />;
  if (error) return <Alert color="red">Failed to load instruments.</Alert>;

  return (
    <Stack>
      <Group justify="space-between">
        <Title order={3}>Securities</Title>
        <Button size="sm" onClick={() => setAddOpen(true)} data-testid="add-instrument-btn">+ Add Security</Button>
      </Group>

      <Modal opened={addOpen} onClose={() => setAddOpen(false)} title="Add Security">
        <form onSubmit={form.onSubmit(v => mutation.mutate({ ...v, ticker: v.ticker || undefined }))}>
          <Stack>
            <TextInput label="ISIN" required data-testid="isin-input" {...form.getInputProps('isin')} />
            <TextInput label="Name" required data-testid="instrument-name-input" {...form.getInputProps('name')} />
            <Select
              label="Type"
              data={[
                { value: 'Etf', label: 'ETF' },
                { value: 'Share', label: 'Share' },
                { value: 'Bond', label: 'Bond' },
                { value: 'CapitalizingFund', label: 'Capitalizing Fund' },
              ]}
              required
              data-testid="instrument-type-select"
              {...form.getInputProps('type')}
            />
            <TextInput label="Currency" maxLength={3} required {...form.getInputProps('currency')} />
            <TextInput label="Ticker (optional)" {...form.getInputProps('ticker')} />
            {mutation.isError && <Alert color="red">Failed to save security.</Alert>}
            <Button type="submit" loading={mutation.isPending} data-testid="submit-instrument">Save</Button>
          </Stack>
        </form>
      </Modal>

      {(!data || data.length === 0) && <Text c="dimmed">No securities registered yet.</Text>}
      {data && data.length > 0 && (
        <Table.ScrollContainer minWidth={600}>
        <Table striped withTableBorder>
          <Table.Thead>
            <Table.Tr>
              <Table.Th>ISIN</Table.Th>
              <Table.Th>Name</Table.Th>
              <Table.Th>Type</Table.Th>
              <Table.Th>Currency</Table.Th>
              <Table.Th>Ticker</Table.Th>
            </Table.Tr>
          </Table.Thead>
          <Table.Tbody>
            {data.map(i => (
              <Table.Tr key={i.id}>
                <Table.Td style={{ fontFamily: 'monospace' }}>{i.isin}</Table.Td>
                <Table.Td>{i.name}</Table.Td>
                <Table.Td>{i.type}</Table.Td>
                <Table.Td>{i.currency}</Table.Td>
                <Table.Td>{i.ticker ?? '—'}</Table.Td>
              </Table.Tr>
            ))}
          </Table.Tbody>
        </Table>
        </Table.ScrollContainer>
      )}
    </Stack>
  );
}
