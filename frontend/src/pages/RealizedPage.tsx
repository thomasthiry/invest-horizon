import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import {
  Title, Stack, Alert, Loader, NumberInput, Button, Group, Paper, Text,
  Table, NumberFormatter, Divider, Badge
} from '@mantine/core';
import { transactionsApi } from '../api/transactions';

interface Props { portfolioId: string; }

export function RealizedPage({ portfolioId }: Props) {
  const [year, setYear] = useState<number>(new Date().getFullYear());

  const { data, isLoading, error, refetch } = useQuery({
    queryKey: ['realized', portfolioId, year],
    queryFn: () => transactionsApi.getRealized(portfolioId, year),
    enabled: !!portfolioId,
  });

  return (
    <Stack>
      <Title order={3}>Realized Gains & Tax</Title>
      <Group>
        <NumberInput
          label="Year"
          value={year}
          onChange={v => setYear(Number(v))}
          min={2000}
          max={2100}
          w={120}
        />
        <Button mt="xl" onClick={() => refetch()}>Load</Button>
      </Group>

      {isLoading && <Loader />}
      {error && <Alert color="red">Failed to load realized gains.</Alert>}

      {data && (
        <>
          <Paper withBorder p="md" data-testid="tax-report">
            <Title order={4} mb="sm">Annual Tax Summary — {data.year}</Title>
            <Stack gap="xs">
              <Group justify="space-between">
                <Text size="sm">Gross gains</Text>
                <Text size="sm" c="green">€<NumberFormatter value={data.taxReport.grossGainEur} decimalScale={2} thousandSeparator /></Text>
              </Group>
              <Group justify="space-between">
                <Text size="sm">Gross losses</Text>
                <Text size="sm" c="red">€<NumberFormatter value={data.taxReport.grossLossEur} decimalScale={2} thousandSeparator /></Text>
              </Group>
              <Group justify="space-between">
                <Text size="sm" fw={600}>Net gain</Text>
                <Text size="sm" fw={600}>€<NumberFormatter value={data.taxReport.netGainEur} decimalScale={2} thousandSeparator /></Text>
              </Group>
              <Divider />
              <Group justify="space-between">
                <Text size="sm">Annual exemption</Text>
                <Text size="sm" c="dimmed">- €<NumberFormatter value={data.taxReport.exemptionEur} decimalScale={2} thousandSeparator /></Text>
              </Group>
              <Group justify="space-between">
                <Text size="sm" fw={600}>Taxable base</Text>
                <Text size="sm" fw={600}>€<NumberFormatter value={data.taxReport.taxableBaseEur} decimalScale={2} thousandSeparator /></Text>
              </Group>
              <Group justify="space-between">
                <Text size="sm" fw={700}>Tax due (10%)</Text>
                <Text size="sm" fw={700} c="orange">€<NumberFormatter value={data.taxReport.taxDueEur} decimalScale={2} thousandSeparator /></Text>
              </Group>
            </Stack>
          </Paper>

          {data.perSale.length > 0 && (
            <Stack>
              <Title order={4}>Per Sale Detail</Title>
              <Table.ScrollContainer minWidth={500}>
              <Table striped withTableBorder fz="sm">
                <Table.Thead>
                  <Table.Tr>
                    <Table.Th>Sale Transaction ID</Table.Th>
                    <Table.Th ta="right">Realized Gain (€)</Table.Th>
                    <Table.Th>Result</Table.Th>
                  </Table.Tr>
                </Table.Thead>
                <Table.Tbody>
                  {data.perSale.map(s => (
                    <Table.Tr key={s.sellTransactionId}>
                      <Table.Td style={{ fontFamily: 'monospace', fontSize: 11 }}>{s.sellTransactionId}</Table.Td>
                      <Table.Td ta="right">
                        <NumberFormatter value={s.realizedGainEur} decimalScale={2} thousandSeparator />
                      </Table.Td>
                      <Table.Td>
                        <Badge color={s.realizedGainEur >= 0 ? 'green' : 'red'}>
                          {s.realizedGainEur >= 0 ? 'Gain' : 'Loss'}
                        </Badge>
                      </Table.Td>
                    </Table.Tr>
                  ))}
                </Table.Tbody>
              </Table>
              </Table.ScrollContainer>
            </Stack>
          )}
        </>
      )}
    </Stack>
  );
}
