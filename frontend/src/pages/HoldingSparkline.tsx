import { useQuery } from '@tanstack/react-query';
import { Skeleton, Text, Tooltip, UnstyledButton } from '@mantine/core';
import { Sparkline } from '@mantine/charts';
import { instrumentsApi } from '../api/instruments';
import type { Holding } from '../api/types';
import { fromDate, toIsoDate } from './priceHistoryRange';

// The sparkline only draws the last month, but it fetches a year under the same query key
// InstrumentPriceChartModal uses for its default range — so opening the modal is a cache hit.
const FETCH_RANGE = '1Y';
const DISPLAY_RANGE = '1M';

const WIDTH = 90;
const HEIGHT = 28;

// Fraction of the height kept clear above and below the curve, so the stroke isn't clipped at the
// extremes.
const PADDING = 0.1;

/**
 * Mantine's Sparkline renders an axis-less recharts chart, whose implicit y-domain starts at 0 —
 * that squashes a 125 → 130 move into a flat line. The sparkline has no axis or tooltip of its own,
 * so nothing reads these numbers as prices and we can rescale the series to fill the box: lowest
 * close at the bottom, highest at the top. The transform is linear and increasing, so `trendColors`
 * still compares the real first and last values; the true magnitude is on the tooltip.
 */
function scaleToBand(values: number[]): number[] {
  const min = Math.min(...values);
  const max = Math.max(...values);
  if (max === min) return values.map(() => 0.5);
  return values.map(v => PADDING + ((v - min) / (max - min)) * (1 - 2 * PADDING));
}

interface Props {
  holding: Holding;
  onClick: () => void;
}

export function HoldingSparkline({ holding, onClick }: Props) {
  const { data, isLoading } = useQuery({
    queryKey: ['price-history', holding.instrumentId, FETCH_RANGE],
    queryFn: () => instrumentsApi.getPriceHistory(
      holding.instrumentId,
      fromDate(FETCH_RANGE),
      toIsoDate(new Date()),
    ),
  });

  if (isLoading) return <Skeleton h={HEIGHT} w={WIDTH} radius="sm" />;

  const cutoff = fromDate(DISPLAY_RANGE);
  const values = (data ?? []).filter(p => p.date >= cutoff).map(p => p.close);

  const first = values[0];
  const last = values[values.length - 1];
  const changePct = values.length >= 2 && first !== 0
    ? ((last - first) / first) * 100
    : null;

  const label = changePct != null
    ? `${DISPLAY_RANGE}: ${changePct > 0 ? '+' : ''}${changePct.toFixed(1)}%`
    : 'Price history';

  return (
    <Tooltip label={label}>
      <UnstyledButton
        onClick={onClick}
        aria-label={`Price history for ${holding.name}`}
        style={{ cursor: 'pointer', display: 'block' }}
      >
        {values.length >= 2 ? (
          <Sparkline
            w={WIDTH}
            h={HEIGHT}
            data={scaleToBand(values)}
            curveType="linear"
            trendColors={{ positive: 'teal.6', negative: 'red.6', neutral: 'gray.5' }}
            fillOpacity={0.25}
            strokeWidth={1.5}
          />
        ) : (
          <Text c="dimmed" ta="center" w={WIDTH}>—</Text>
        )}
      </UnstyledButton>
    </Tooltip>
  );
}
