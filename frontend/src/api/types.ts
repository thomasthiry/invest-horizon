export type InstrumentType = 'Etf' | 'Share' | 'Bond' | 'CapitalizingFund';
export type Broker = 'Keytrade' | 'Revolut';
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
