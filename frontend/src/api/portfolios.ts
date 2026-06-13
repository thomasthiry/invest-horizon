import api from './client';
import type { Portfolio } from './types';

export const portfoliosApi = {
  getAll: () => api.get<Portfolio[]>('/portfolios').then(r => r.data),
  getById: (id: string) => api.get<Portfolio>(`/portfolios/${id}`).then(r => r.data),
  create: (name: string, baseCurrency = 'EUR') =>
    api.post<Portfolio>('/portfolios', { name, baseCurrency }).then(r => r.data),
  update: (id: string, name: string) =>
    api.put<Portfolio>(`/portfolios/${id}`, { name }).then(r => r.data),
};
