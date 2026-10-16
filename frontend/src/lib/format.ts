const money = new Intl.NumberFormat("es-BO", { style: "currency", currency: "BOB", minimumFractionDigits: 2 });
const compact = new Intl.NumberFormat("es-BO", { notation: "compact", maximumFractionDigits: 1 });
const integer = new Intl.NumberFormat("es-BO");
const dateTime = new Intl.DateTimeFormat("es-BO", { dateStyle: "short", timeStyle: "short", timeZone: "America/La_Paz" });
const shortDate = new Intl.DateTimeFormat("es-BO", { day: "2-digit", month: "short", timeZone: "UTC" });

export const fmt = {
  money: (n: number) => money.format(n),
  compactMoney: (n: number) => `Bs ${compact.format(n)}`,
  int: (n: number) => integer.format(n),
  dateTime: (iso: string) => dateTime.format(new Date(iso)),
  // Report dates are plain calendar days ("2026-10-16"); format in UTC so they never shift.
  day: (isoDate: string) => shortDate.format(new Date(`${isoDate}T00:00:00Z`)),
};

export const paymentLabel: Record<string, string> = {
  Cash: "Efectivo",
  Card: "Tarjeta",
  QrTransfer: "QR",
};

export const roleLabel: Record<string, string> = {
  Admin: "Administrador",
  Manager: "Gerente",
  Cashier: "Cajero",
};

export const movementLabel: Record<string, string> = {
  Sale: "Venta",
  TransferOut: "Envío",
  TransferIn: "Recepción",
  Adjustment: "Ajuste",
  Receipt: "Ingreso",
};

export const transferLabel: Record<string, string> = {
  Requested: "Solicitada",
  InTransit: "En tránsito",
  Received: "Recibida",
  Cancelled: "Cancelada",
};
