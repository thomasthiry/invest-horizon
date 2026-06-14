import api from './client';
import type { Broker, CostPreview, Holding, RealizedGainsReport, Transaction, TransactionSide, ValuationPoint } from './types';

export const transactionsApi = {
  getAll: (portfolioId: string) =>
    api.get<Transaction[]>(`/portfolios/${portfolioId}/transactions`).then(r => r.data),

  create: (portfolioId: string, data: {
    instrumentId: string;
    broker: Broker;
    side: TransactionSide;
    date: string;
    unitPrice: number;
    quantity: number;
    currency: string;
    fxRate: number;
    custodyFee?: number;
    manualBrokerFee?: number;
  }) => api.post<Transaction>(`/portfolios/${portfolioId}/transactions`, data).then(r => r.data),

  preview: (data: {
    instrumentId: string;
    broker: Broker;
    side: TransactionSide;
    unitPrice: number;
    quantity: number;
    fxRate: number;
    manualBrokerFee?: number;
  }) => api.post<CostPreview>('/transactions/preview', data).then(r => r.data),

  getHoldings: (portfolioId: string) =>
    api.get<Holding[]>(`/portfolios/${portfolioId}/holdings`).then(r => r.data),

  refreshPrices: (portfolioId: string) =>
    api.post<Holding[]>(`/portfolios/${portfolioId}/holdings/refresh-prices`).then(r => r.data),

  getRealized: (portfolioId: string, year: number) =>
    api.get<RealizedGainsReport>(`/portfolios/${portfolioId}/realized`, { params: { year } }).then(r => r.data),

  getValuationHistory: (portfolioId: string) =>
    api.get<ValuationPoint[]>(`/portfolios/${portfolioId}/valuation-history`).then(r => r.data),
};
