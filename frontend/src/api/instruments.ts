import api from './client';
import type { Instrument, InstrumentType } from './types';

export const instrumentsApi = {
  getAll: () => api.get<Instrument[]>('/instruments').then(r => r.data),
  create: (data: { isin: string; name: string; type: InstrumentType; currency: string; ticker?: string }) =>
    api.post<Instrument>('/instruments', data).then(r => r.data),
};
