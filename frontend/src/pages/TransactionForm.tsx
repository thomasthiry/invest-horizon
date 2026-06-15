import { useEffect, useState } from 'react';
import { useForm } from '@mantine/form';
import {
  TextInput, Select, NumberInput, Button, Stack, Paper, Title, Group, Divider,
  Text, NumberFormatter, Alert, Switch
} from '@mantine/core';
import { DateInput } from '@mantine/dates';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { transactionsApi } from '../api/transactions';
import { instrumentsApi } from '../api/instruments';
import type { Broker, CostPreview, Transaction, TransactionSide } from '../api/types';

interface Props {
  portfolioId: string;
  transaction?: Transaction;
  onSuccess?: () => void;
}

export function TransactionForm({ portfolioId, transaction, onSuccess }: Props) {
  const isEdit = !!transaction;
  const qc = useQueryClient();
  const [preview, setPreview] = useState<CostPreview | null>(null);
  const [previewError, setPreviewError] = useState('');

  const { data: instruments = [] } = useQuery({
    queryKey: ['instruments'],
    queryFn: instrumentsApi.getAll,
  });

  const hasManualFee = transaction
    ? transaction.manualBrokerFee != null
    : false;

  const form = useForm({
    initialValues: {
      instrumentId: transaction?.instrumentId ?? '',
      broker: (transaction?.broker ?? 'Keytrade') as Broker,
      side: (transaction?.side ?? 'Buy') as TransactionSide,
      date: transaction?.date ?? new Date().toISOString().substring(0, 10),
      unitPrice: transaction?.unitPrice ?? 0,
      quantity: transaction?.quantity ?? 1,
      currency: transaction?.currency ?? 'EUR',
      fxRate: transaction?.fxRate ?? 1,
      custodyFee: transaction?.custodyFee ?? undefined as number | undefined,
      manualBrokerFee: transaction?.manualBrokerFee ?? undefined as number | undefined,
      useManualFee: hasManualFee,
    },
  });

  const selectedInstrument = instruments.find(i => i.id === form.values.instrumentId);

  // Live cost preview
  useEffect(() => {
    if (!form.values.instrumentId || !form.values.unitPrice || !form.values.quantity) {
      setPreview(null);
      return;
    }
    const timer = setTimeout(async () => {
      try {
        const p = await transactionsApi.preview({
          instrumentId: form.values.instrumentId,
          broker: form.values.broker,
          side: form.values.side,
          unitPrice: form.values.unitPrice,
          quantity: form.values.quantity,
          fxRate: form.values.fxRate,
          manualBrokerFee: form.values.useManualFee ? form.values.manualBrokerFee : undefined,
        });
        setPreview(p);
        setPreviewError('');
      } catch {
        setPreviewError('Could not compute preview.');
      }
    }, 400);
    return () => clearTimeout(timer);
  }, [
    form.values.instrumentId, form.values.broker, form.values.side,
    form.values.unitPrice, form.values.quantity, form.values.fxRate,
    form.values.manualBrokerFee, form.values.useManualFee,
  ]);

  const mutation = useMutation({
    mutationFn: (values: typeof form.values) => {
      const payload = {
        instrumentId: values.instrumentId,
        broker: values.broker,
        side: values.side,
        date: values.date,
        unitPrice: values.unitPrice,
        quantity: values.quantity,
        currency: values.currency,
        fxRate: values.fxRate,
        custodyFee: values.custodyFee,
        manualBrokerFee: values.useManualFee ? values.manualBrokerFee : undefined,
      };
      return isEdit
        ? transactionsApi.update(portfolioId, transaction!.id, payload)
        : transactionsApi.create(portfolioId, payload);
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['transactions', portfolioId] });
      qc.invalidateQueries({ queryKey: ['holdings', portfolioId] });
      qc.invalidateQueries({ queryKey: ['realized', portfolioId] });
      qc.invalidateQueries({ queryKey: ['valuation-history', portfolioId] });
      if (!isEdit) {
        form.reset();
        setPreview(null);
      }
      onSuccess?.();
    },
  });

  return (
    <Paper shadow="xs" p="md">
      <Title order={4} mb="md">{isEdit ? 'Edit Transaction' : 'Add Transaction'}</Title>
      <form onSubmit={form.onSubmit(values => mutation.mutate(values))}>
        <Stack>
          <Group grow>
            <Select
              label="Security"
              placeholder="Select instrument"
              data={instruments.map(i => ({ value: i.id, label: `${i.name} (${i.isin})` }))}
              searchable
              required
              data-testid="instrument-select"
              {...form.getInputProps('instrumentId')}
            />
            <Select
              label="Broker"
              data={['Keytrade', 'Revolut', 'MeDirect']}
              required
              data-testid="broker-select"
              {...form.getInputProps('broker')}
            />
          </Group>
          <Group grow>
            <Select
              label="Side"
              data={[
                { value: 'Buy', label: 'Buy' },
                { value: 'Sell', label: 'Sell' },
              ]}
              required
              data-testid="side-select"
              {...form.getInputProps('side')}
            />
            <DateInput
              label="Date"
              required
              data-testid="date-input"
              valueFormat="YYYY-MM-DD"
              {...form.getInputProps('date')}
            />
          </Group>
          <Group grow>
            <NumberInput
              label={`Unit Price (${selectedInstrument?.currency ?? form.values.currency})`}
              decimalScale={4}
              min={0}
              required
              data-testid="unit-price-input"
              {...form.getInputProps('unitPrice')}
            />
            <NumberInput
              label="Quantity"
              decimalScale={8}
              min={0}
              required
              data-testid="quantity-input"
              {...form.getInputProps('quantity')}
            />
          </Group>
          <Group grow>
            <TextInput
              label="Currency"
              maxLength={3}
              required
              {...form.getInputProps('currency')}
            />
            <NumberInput
              label="FX Rate (1 EUR = x currency)"
              decimalScale={6}
              min={0.000001}
              required
              {...form.getInputProps('fxRate')}
            />
          </Group>
          <NumberInput
            label="Custody Fee / Droits de garde (€, optional)"
            decimalScale={2}
            min={0}
            {...form.getInputProps('custodyFee')}
          />
          <Switch
            label="Override broker fee manually"
            {...form.getInputProps('useManualFee', { type: 'checkbox' })}
          />
          {form.values.useManualFee && (
            <NumberInput
              label="Manual Broker Fee (€)"
              decimalScale={2}
              min={0}
              {...form.getInputProps('manualBrokerFee')}
            />
          )}

          {previewError && <Alert color="orange">{previewError}</Alert>}
          {preview && (
            <>
              <Divider label="Cost Preview" labelPosition="center" />
              <Paper withBorder p="sm" bg="gray.0" data-testid="cost-preview">
                <Stack gap="xs">
                  <Group justify="space-between">
                    <Text size="sm">Amount (native)</Text>
                    <Text size="sm" fw={500}>
                      <NumberFormatter value={preview.amountNative} decimalScale={2} thousandSeparator /> {form.values.currency}
                    </Text>
                  </Group>
                  <Group justify="space-between">
                    <Text size="sm">Amount (EUR)</Text>
                    <Text size="sm" fw={500}>€<NumberFormatter value={preview.amountEur} decimalScale={2} thousandSeparator /></Text>
                  </Group>
                  <Group justify="space-between">
                    <Text size="sm">Broker fee</Text>
                    <Text size="sm">€<NumberFormatter value={preview.brokerFee} decimalScale={2} /></Text>
                  </Group>
                  <Group justify="space-between">
                    <Text size="sm">TOB</Text>
                    <Text size="sm">€<NumberFormatter value={preview.tobAmount} decimalScale={2} /></Text>
                  </Group>
                  <Divider />
                  {form.values.side === 'Buy' ? (
                    <Group justify="space-between">
                      <Text size="sm" fw={700}>Total cost</Text>
                      <Text size="sm" fw={700} c="red">€<NumberFormatter value={preview.totalCost} decimalScale={2} thousandSeparator /></Text>
                    </Group>
                  ) : (
                    <Group justify="space-between">
                      <Text size="sm" fw={700}>Net proceeds</Text>
                      <Text size="sm" fw={700} c="green">€<NumberFormatter value={preview.netProceeds} decimalScale={2} thousandSeparator /></Text>
                    </Group>
                  )}
                </Stack>
              </Paper>
            </>
          )}

          {mutation.isError && (
            <Alert color="red">
              {(mutation.error as { response?: { data?: { message?: string } } })?.response?.data?.message ?? 'Failed to save transaction.'}
            </Alert>
          )}
          <Button type="submit" loading={mutation.isPending} data-testid="submit-transaction">
            {isEdit ? 'Update Transaction' : 'Save Transaction'}
          </Button>
        </Stack>
      </form>
    </Paper>
  );
}
