import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Stack, Title, Select, Tabs, Alert, Loader, Text } from '@mantine/core';
import { portfoliosApi } from '../api/portfolios';
import { HoldingsPage } from './HoldingsPage';
import { TransactionsPage } from './TransactionsPage';
import { RealizedPage } from './RealizedPage';

export function DashboardPage() {
  const [portfolioId, setPortfolioId] = useState<string | null>(null);

  const { data: portfolios, isLoading, error } = useQuery({
    queryKey: ['portfolios'],
    queryFn: portfoliosApi.getAll,
  });

  if (isLoading) return <Loader />;
  if (error) return <Alert color="red">Failed to load portfolios.</Alert>;

  const selected = portfolioId ?? (portfolios?.[0]?.id ?? null);

  return (
    <Stack p="md">
      <Title order={2}>InvestHorizon</Title>
      {portfolios && portfolios.length > 0 ? (
        <>
          <Select
            label="Portfolio"
            data={portfolios.map(p => ({ value: p.id, label: p.name }))}
            value={selected}
            onChange={setPortfolioId}
            w={300}
            data-testid="portfolio-select"
          />
          {selected && (
            <Tabs defaultValue="holdings" keepMounted={false}>
              <Tabs.List>
                <Tabs.Tab value="holdings">Holdings</Tabs.Tab>
                <Tabs.Tab value="transactions" data-testid="transactions-tab">Transactions</Tabs.Tab>
                <Tabs.Tab value="realized">Realized & Tax</Tabs.Tab>
              </Tabs.List>
              <Tabs.Panel value="holdings" pt="md">
                <HoldingsPage portfolioId={selected} />
              </Tabs.Panel>
              <Tabs.Panel value="transactions" pt="md">
                <TransactionsPage portfolioId={selected} />
              </Tabs.Panel>
              <Tabs.Panel value="realized" pt="md">
                <RealizedPage portfolioId={selected} />
              </Tabs.Panel>
            </Tabs>
          )}
        </>
      ) : (
        <Text c="dimmed">No portfolios yet. Create one below.</Text>
      )}
    </Stack>
  );
}
