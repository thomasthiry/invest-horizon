export type InstrumentType = 'Etf' | 'Share' | 'Bond' | 'CapitalizingFund';
export type Broker = 'Keytrade' | 'Revolut' | 'MeDirect';
export type TransactionSide = 'Buy' | 'Sell';

export interface Portfolio {
  id: string;
  name: string;
  baseCurrency: string;
}

export interface Instrument {
  id: string;
  isin: string;
  name: string;
  type: InstrumentType;
  currency: string;
  ticker: string | null;
}

export interface Transaction {
  id: string;
  portfolioId: string;
  instrumentId: string;
  isin: string | null;
  instrumentName: string | null;
  broker: Broker;
  side: TransactionSide;
  date: string;
  unitPrice: number;
  quantity: number;
  currency: string;
  fxRate: number;
  amountNative: number;
  amountEur: number;
  brokerFee: number;
  tobAmount: number;
  totalCost: number;
  netProceeds: number;
  custodyFee: number | null;
  manualBrokerFee: number | null;
  remainingQuantity: number;
}

export interface Holding {
  instrumentId: string;
  isin: string;
  name: string;
  currency: string;
  openQuantity: number;
  avgCostEur: number;
  totalInvestedEur: number;
  // Live valuation (null until prices have been refreshed / if a quote is unavailable).
  currentPriceNative: number | null;
  priceCurrency: string | null;
  marketValueEur: number | null;
  unrealizedGainEur: number | null;
  priceAsOf: string | null;
  priceFetchedAt: string | null;
  priceSource: string | null;
}

export interface ValuationPoint {
  date: string;        // ISO date, e.g. "2024-01-02"
  valueEur: number;    // total portfolio market value in EUR
  investedEur: number; // net invested cost basis in EUR (buys − sell proceeds)
}

export interface CostPreview {
  amountNative: number;
  amountEur: number;
  brokerFee: number;
  tobAmount: number;
  totalCost: number;
  netProceeds: number;
}

export interface SaleGainDto {
  sellTransactionId: string;
  realizedGainEur: number;
}

export interface AnnualTaxReport {
  year: number;
  grossGainEur: number;
  grossLossEur: number;
  netGainEur: number;
  exemptionEur: number;
  taxableBaseEur: number;
  taxDueEur: number;
}

export interface RealizedGainsReport {
  year: number;
  perSale: SaleGainDto[];
  taxReport: AnnualTaxReport;
}

export type RecommendationRating =
  | 'Buy'
  | 'Accumulate'
  | 'Hold'
  | 'Reduce'
  | 'Sell';

export interface RecommendationEvaluation {
  priceAtRec: number;
  currentPrice: number;
  returnSince: number;
  directionallyCorrect: boolean | null;
  performanceScore: number;
}

export interface Recommendation {
  id: string;
  instrumentId: string;
  isin: string | null;
  instrumentName: string | null;
  source: string;
  rating: RecommendationRating;
  date: string;
  comment: string | null;
  createdAt: string;
  evaluation: RecommendationEvaluation | null;
}

export interface SourceScorecard {
  source: string;
  totalCount: number;
  evaluatedCount: number;
  hitRate: number | null;
  avgReturn: number | null;
  avgScore: number | null;
}
