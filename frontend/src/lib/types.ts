// Mirrors the DTOs exposed by RetailOps.Api (camelCase JSON, enums as strings).

export type Role = "Admin" | "Manager" | "Cashier";
export type PaymentMethod = "Cash" | "Card" | "QrTransfer";
export type TransferStatus = "Requested" | "InTransit" | "Received" | "Cancelled";

export interface UserProfile {
  id: string;
  email: string;
  fullName: string;
  role: Role;
  tenantId: string;
  tenantName: string;
  storeId: string | null;
  storeName: string | null;
}

export interface Paged<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface Store {
  id: string;
  code: string;
  name: string;
  city: string;
  isActive: boolean;
}

export interface Product {
  id: string;
  sku: string;
  barcode: string;
  name: string;
  category: string;
  price: number;
  cost: number;
  isActive: boolean;
}

export interface StockItem {
  productId: string;
  sku: string;
  barcode: string;
  name: string;
  category: string;
  price: number;
  quantity: number;
  reorderPoint: number;
  isLow: boolean;
}

export interface StockMovement {
  id: string;
  sku: string;
  productName: string;
  quantity: number;
  type: string;
  reference: string;
  note: string | null;
  createdAt: string;
}

export interface SaleSummary {
  id: string;
  number: string;
  storeName: string;
  cashierName: string;
  paymentMethod: PaymentMethod;
  total: number;
  items: number;
  createdAt: string;
}

export interface SaleDetail {
  id: string;
  number: string;
  storeId: string;
  storeName: string;
  cashierName: string;
  paymentMethod: PaymentMethod;
  total: number;
  createdAt: string;
  lines: { productId: string; sku: string; productName: string; quantity: number; unitPrice: number; lineTotal: number }[];
}

export interface Transfer {
  id: string;
  number: string;
  fromStoreId: string;
  fromStore: string;
  toStoreId: string;
  toStore: string;
  status: TransferStatus;
  requestedAt: string;
  shippedAt: string | null;
  receivedAt: string | null;
  note: string | null;
  totalUnits: number;
  lines: { productId: string; sku: string; productName: string; quantity: number }[];
}

export interface Dashboard {
  revenueToday: number;
  revenuePeriod: number;
  salesPeriod: number;
  averageTicket: number;
  grossMarginPct: number;
  lowStockItems: number;
  openTransfers: number;
  dailyRevenue: { date: string; revenue: number; sales: number }[];
  revenueByStore: { storeId: string; storeName: string; revenue: number; sales: number }[];
  topProducts: { sku: string; name: string; units: number; revenue: number }[];
}
