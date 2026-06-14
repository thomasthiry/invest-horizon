import { useState } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import {
  Table, Title, Stack, Alert, Loader, Button, Group, Modal,
  Select, Text, Textarea, Autocomplete, Badge, ActionIcon,
  Tabs, Tooltip
} from '@mantine/core';
import { DateInput } from '@mantine/dates';
import { useForm } from '@mantine/form';
import { recommendationsApi } from '../api/recommendations';
import { instrumentsApi } from '../api/instruments';
import type { Recommendation, RecommendationRating } from '../api/types';


const RATING_OPTIONS = [
  { value: 'Buy', label: 'Buy' },
  { value: 'Accumulate', label: 'Accumulate' },
  { value: 'Hold', label: 'Hold' },
  { value: 'Reduce', label: 'Reduce' },
  { value: 'Sell', label: 'Sell' },
];

const RATING_COLORS: Record<RecommendationRating, string> = {
  Buy: 'green',
  Accumulate: 'lime',
  Hold: 'gray',
  Reduce: 'orange',
  Sell: 'red',
};

function pct(v: number) {
  return `${v >= 0 ? '+' : ''}${(v * 100).toFixed(1)}%`;
}

export function RecommendationsPage() {
  const [modalOpen, setModalOpen] = useState(false);
  const [editing, setEditing] = useState<Recommendation | null>(null);
  const qc = useQueryClient();

  const { data: instruments = [] } = useQuery({
    queryKey: ['instruments'],
    queryFn: instrumentsApi.getAll,
  });

  const { data: sources = [] } = useQuery({
    queryKey: ['recommendation-sources'],
    queryFn: recommendationsApi.getSources,
  });

  const { data: recommendations, isLoading, error } = useQuery({
    queryKey: ['recommendations'],
    queryFn: () => recommendationsApi.getAll({}),
  });

  const { data: scorecard = [] } = useQuery({
    queryKey: ['recommendation-scorecard'],
    queryFn: recommendationsApi.getScorecard,
  });

  const form = useForm({
    initialValues: {
      instrumentId: '',
      source: '',
      rating: 'Buy' as RecommendationRating,
      date: new Date().toISOString().substring(0, 10) as string | Date,
      comment: '',
    },
  });

  function openAdd() {
    setEditing(null);
    form.reset();
    setModalOpen(true);
  }

  function openEdit(rec: Recommendation) {
    setEditing(rec);
    form.setValues({
      instrumentId: rec.instrumentId,
      source: rec.source,
      rating: rec.rating,
      date: rec.date,
      comment: rec.comment ?? '',
    });
    setModalOpen(true);
  }

  function closeModal() {
    setModalOpen(false);
    setEditing(null);
    form.reset();
  }

  function formatDate(v: string | Date): string {
    if (v instanceof Date) return v.toISOString().substring(0, 10);
    return v;
  }

  const saveMutation = useMutation({
    mutationFn: (values: typeof form.values) => {
      const dateStr = formatDate(values.date);
      if (editing) {
        return recommendationsApi.update(editing.id, {
          source: values.source,
          rating: values.rating,
          date: dateStr,
          comment: values.comment || undefined,
        });
      }
      return recommendationsApi.create({
        instrumentId: values.instrumentId,
        source: values.source,
        rating: values.rating,
        date: dateStr,
        comment: values.comment || undefined,
      });
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['recommendations'] });
      qc.invalidateQueries({ queryKey: ['recommendation-sources'] });
      qc.invalidateQueries({ queryKey: ['recommendation-scorecard'] });
      closeModal();
    },
  });

  const deleteMutation = useMutation({
    mutationFn: (id: string) => recommendationsApi.remove(id),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['recommendations'] });
      qc.invalidateQueries({ queryKey: ['recommendation-scorecard'] });
    },
  });

  return (
    <Stack>
      <Group justify="space-between">
        <Title order={3}>Recommendations</Title>
        <Button size="sm" onClick={openAdd}>+ Add</Button>
      </Group>

      <Tabs defaultValue="list">
        <Tabs.List>
          <Tabs.Tab value="list">Recommendations</Tabs.Tab>
          <Tabs.Tab value="scorecard">Scorecard by source</Tabs.Tab>
        </Tabs.List>

        <Tabs.Panel value="list" pt="sm">
          {isLoading && <Loader />}
          {error && <Alert color="red">Failed to load recommendations.</Alert>}
          {recommendations && recommendations.length === 0 && (
            <Text c="dimmed">No recommendations yet.</Text>
          )}
          {recommendations && recommendations.length > 0 && (
            <Table striped withTableBorder>
              <Table.Thead>
                <Table.Tr>
                  <Table.Th>Date</Table.Th>
                  <Table.Th>Security</Table.Th>
                  <Table.Th>Source</Table.Th>
                  <Table.Th>Rating</Table.Th>
                  <Table.Th>Return since</Table.Th>
                  <Table.Th>Correct?</Table.Th>
                  <Table.Th>Comment</Table.Th>
                  <Table.Th></Table.Th>
                </Table.Tr>
              </Table.Thead>
              <Table.Tbody>
                {recommendations.map(r => (
                  <Table.Tr key={r.id}>
                    <Table.Td style={{ fontFamily: 'monospace', whiteSpace: 'nowrap' }}>{r.date}</Table.Td>
                    <Table.Td>
                      <Text size="sm">{r.instrumentName ?? '—'}</Text>
                      <Text size="xs" c="dimmed" style={{ fontFamily: 'monospace' }}>{r.isin}</Text>
                    </Table.Td>
                    <Table.Td>{r.source}</Table.Td>
                    <Table.Td>
                      <Badge color={RATING_COLORS[r.rating]} variant="light">
                        {RATING_OPTIONS.find(o => o.value === r.rating)?.label ?? r.rating}
                      </Badge>
                    </Table.Td>
                    <Table.Td>
                      {r.evaluation
                        ? <Text c={r.evaluation.returnSince >= 0 ? 'green' : 'red'} size="sm">
                            {pct(r.evaluation.returnSince)}
                          </Text>
                        : <Text c="dimmed" size="sm">—</Text>}
                    </Table.Td>
                    <Table.Td>
                      {r.evaluation?.directionallyCorrect === true && (
                        <Tooltip label={`Score: ${(r.evaluation.performanceScore * 100).toFixed(1)}%`}>
                          <Badge color="green" variant="outline">✓</Badge>
                        </Tooltip>
                      )}
                      {r.evaluation?.directionallyCorrect === false && (
                        <Tooltip label={`Score: ${(r.evaluation.performanceScore * 100).toFixed(1)}%`}>
                          <Badge color="red" variant="outline">✗</Badge>
                        </Tooltip>
                      )}
                      {(r.evaluation === null || r.evaluation?.directionallyCorrect === null) && (
                        <Text c="dimmed" size="sm">—</Text>
                      )}
                    </Table.Td>
                    <Table.Td style={{ maxWidth: 200 }}>
                      <Text size="sm" lineClamp={2}>{r.comment ?? '—'}</Text>
                    </Table.Td>
                    <Table.Td>
                      <Group gap="xs" wrap="nowrap">
                        <ActionIcon size="sm" variant="subtle" onClick={() => openEdit(r)}>✏</ActionIcon>
                        <ActionIcon
                          size="sm" variant="subtle" color="red"
                          loading={deleteMutation.isPending}
                          onClick={() => {
                            if (confirm('Delete this recommendation?')) deleteMutation.mutate(r.id);
                          }}
                        >✕</ActionIcon>
                      </Group>
                    </Table.Td>
                  </Table.Tr>
                ))}
              </Table.Tbody>
            </Table>
          )}
        </Tabs.Panel>

        <Tabs.Panel value="scorecard" pt="sm">
          {scorecard.length === 0 && <Text c="dimmed">No data yet — add recommendations first.</Text>}
          {scorecard.length > 0 && (
            <Table striped withTableBorder>
              <Table.Thead>
                <Table.Tr>
                  <Table.Th>Source</Table.Th>
                  <Table.Th>Total</Table.Th>
                  <Table.Th>Evaluated</Table.Th>
                  <Table.Th>Hit rate</Table.Th>
                  <Table.Th>Avg return</Table.Th>
                  <Table.Th>Avg score</Table.Th>
                </Table.Tr>
              </Table.Thead>
              <Table.Tbody>
                {scorecard.map(s => (
                  <Table.Tr key={s.source}>
                    <Table.Td fw={500}>{s.source}</Table.Td>
                    <Table.Td>{s.totalCount}</Table.Td>
                    <Table.Td>{s.evaluatedCount}</Table.Td>
                    <Table.Td>
                      {s.hitRate != null
                        ? <Text c={s.hitRate >= 0.5 ? 'green' : 'red'} size="sm">
                            {(s.hitRate * 100).toFixed(0)}%
                          </Text>
                        : <Text c="dimmed" size="sm">—</Text>}
                    </Table.Td>
                    <Table.Td>
                      {s.avgReturn != null
                        ? <Text c={s.avgReturn >= 0 ? 'green' : 'red'} size="sm">{pct(s.avgReturn)}</Text>
                        : <Text c="dimmed" size="sm">—</Text>}
                    </Table.Td>
                    <Table.Td>
                      {s.avgScore != null
                        ? <Text c={s.avgScore >= 0 ? 'green' : 'red'} size="sm">
                            {(s.avgScore * 100).toFixed(1)}%
                          </Text>
                        : <Text c="dimmed" size="sm">—</Text>}
                    </Table.Td>
                  </Table.Tr>
                ))}
              </Table.Tbody>
            </Table>
          )}
        </Tabs.Panel>
      </Tabs>

      <Modal
        opened={modalOpen}
        onClose={closeModal}
        title={editing ? 'Edit Recommendation' : 'Add Recommendation'}
        size="lg"
      >
        <form onSubmit={form.onSubmit(v => saveMutation.mutate(v))}>
          <Stack>
            <Select
              label="Security"
              placeholder="Select security"
              data={instruments.map(i => ({ value: i.id, label: `${i.name} (${i.isin})` }))}
              searchable
              required
              disabled={!!editing}
              {...form.getInputProps('instrumentId')}
            />
            <Group grow>
              <Autocomplete
                label="Source"
                placeholder="e.g. Morningstar"
                data={sources}
                required
                {...form.getInputProps('source')}
              />
              <Select
                label="Rating"
                data={RATING_OPTIONS}
                required
                {...form.getInputProps('rating')}
              />
            </Group>
            <DateInput
              label="Date"
              required
              valueFormat="YYYY-MM-DD"
              {...form.getInputProps('date')}
            />
            <Textarea
              label="Comment / rationale"
              placeholder="What did the source say?"
              autosize
              minRows={3}
              {...form.getInputProps('comment')}
            />
            {saveMutation.isError && <Alert color="red">Failed to save.</Alert>}
            <Button type="submit" loading={saveMutation.isPending}>
              {editing ? 'Save changes' : 'Add recommendation'}
            </Button>
          </Stack>
        </form>
      </Modal>
    </Stack>
  );
}
