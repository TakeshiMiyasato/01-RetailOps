"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import clsx from "clsx";
import { Banknote, CheckCircle2, CreditCard, Minus, Plus, QrCode, ScanBarcode, Trash2 } from "lucide-react";
import { useRef, useState, type FormEvent } from "react";
import { useActiveStore } from "@/components/store-picker";
import { Button, Card, CardHeader, EmptyState, ErrorNote, Input, Modal, PageHeader } from "@/components/ui";
import { api, ApiError, qs } from "@/lib/api";
import { fmt, paymentLabel } from "@/lib/format";
import type { Paged, PaymentMethod, SaleDetail, StockItem } from "@/lib/types";

interface CartLine {
  item: StockItem;
  quantity: number;
}

const payments: { value: PaymentMethod; icon: typeof Banknote }[] = [
  { value: "Cash", icon: Banknote },
  { value: "QrTransfer", icon: QrCode },
  { value: "Card", icon: CreditCard },
];

export default function PosPage() {
  const { storeId, store, picker } = useActiveStore();
  const queryClient = useQueryClient();
  const searchRef = useRef<HTMLInputElement>(null);
  const [search, setSearch] = useState("");
  const [cart, setCart] = useState<CartLine[]>([]);
  const [payment, setPayment] = useState<PaymentMethod>("Cash");
  const [receipt, setReceipt] = useState<SaleDetail | null>(null);
  const [scanError, setScanError] = useState<string | null>(null);

  const results = useQuery({
    queryKey: ["pos-search", storeId, search],
    queryFn: () => api<Paged<StockItem>>(`/stores/${storeId}/stock${qs({ search, pageSize: 8 })}`),
    enabled: !!storeId && search.trim().length >= 2,
  });

  const checkout = useMutation({
    mutationFn: () =>
      api<SaleDetail>("/sales", {
        method: "POST",
        json: { storeId, paymentMethod: payment, lines: cart.map((l) => ({ productId: l.item.productId, quantity: l.quantity })) },
      }),
    onSuccess: (sale) => {
      setReceipt(sale);
      setCart([]);
      queryClient.invalidateQueries({ queryKey: ["stock"] });
      queryClient.invalidateQueries({ queryKey: ["pos-search"] });
      queryClient.invalidateQueries({ queryKey: ["dashboard"] });
      queryClient.invalidateQueries({ queryKey: ["sales"] });
    },
  });

  function add(item: StockItem) {
    setCart((current) => {
      const existing = current.find((l) => l.item.productId === item.productId);
      if (existing) return current.map((l) => (l === existing ? { ...l, quantity: Math.min(l.quantity + 1, item.quantity) } : l));
      return item.quantity > 0 ? [...current, { item, quantity: 1 }] : current;
    });
    setSearch("");
    searchRef.current?.focus();
  }

  function setQuantity(productId: string, quantity: number) {
    setCart((current) =>
      current.flatMap((l) => (l.item.productId !== productId ? [l] : quantity <= 0 ? [] : [{ ...l, quantity: Math.min(quantity, l.item.quantity) }])),
    );
  }

  // A barcode scanner types digits and presses Enter: resolve it straight to the product.
  async function onScan(e: FormEvent) {
    e.preventDefault();
    setScanError(null);
    const code = search.trim();
    if (!/^\d{8,14}$/.test(code)) {
      if (results.data?.items.length === 1) add(results.data.items[0]);
      return;
    }
    try {
      add(await api<StockItem>(`/stores/${storeId}/stock/barcode/${code}`));
    } catch (err) {
      setScanError(err instanceof ApiError && err.status === 404 ? `No existe un producto con código ${code}` : "No se pudo leer el código");
    }
  }

  const total = cart.reduce((sum, l) => sum + l.item.price * l.quantity, 0);
  const units = cart.reduce((sum, l) => sum + l.quantity, 0);

  return (
    <>
      <PageHeader title="Punto de venta" description={store ? `Sucursal ${store.name}` : undefined} action={picker} />

      <div className="grid gap-6 lg:grid-cols-5">
        <Card className="lg:col-span-3">
          <CardHeader title="Agregar productos" subtitle="Busca por nombre o SKU, o escanea el código de barras" />
          <div className="p-5">
            <form onSubmit={onScan} className="relative">
              <ScanBarcode className="pointer-events-none absolute left-3 top-1/2 size-4 -translate-y-1/2 text-muted" aria-hidden />
              <Input
                ref={searchRef}
                autoFocus
                value={search}
                onChange={(e) => setSearch(e.target.value)}
                placeholder="Ej. leche, ABA-001 o 7770001000016"
                className="pl-9"
                aria-label="Buscar producto"
              />
            </form>
            {scanError && <p className="mt-2 text-sm text-critical">{scanError}</p>}

            <ul className="mt-4 divide-y divide-line">
              {results.data?.items.map((item) => (
                <li key={item.productId}>
                  <button
                    onClick={() => add(item)}
                    disabled={item.quantity === 0}
                    className="flex w-full items-center justify-between gap-4 rounded-lg px-2 py-3 text-left hover:bg-surface-2 disabled:opacity-50"
                  >
                    <span className="min-w-0">
                      <span className="block truncate text-sm font-medium">{item.name}</span>
                      <span className="block text-xs text-muted">
                        {item.sku} · {item.quantity === 0 ? "Sin stock" : `${fmt.int(item.quantity)} disponibles`}
                      </span>
                    </span>
                    <span className="tabular shrink-0 text-sm font-medium">{fmt.money(item.price)}</span>
                  </button>
                </li>
              ))}
            </ul>
            {search.trim().length >= 2 && results.data?.items.length === 0 && <EmptyState title="Sin resultados" hint="Prueba con otro término." />}
          </div>
        </Card>

        <Card className="flex flex-col lg:col-span-2">
          <CardHeader title="Venta actual" subtitle={units ? `${units} unidades` : undefined} />
          {cart.length === 0 ? (
            <EmptyState title="El carrito está vacío" hint="Agrega productos desde el buscador." />
          ) : (
            <ul className="divide-y divide-line px-5">
              {cart.map((line) => (
                <li key={line.item.productId} className="flex items-center gap-3 py-3">
                  <div className="min-w-0 flex-1">
                    <p className="truncate text-sm font-medium">{line.item.name}</p>
                    <p className="tabular text-xs text-muted">{fmt.money(line.item.price)} c/u</p>
                  </div>
                  <div className="flex items-center gap-1">
                    <IconButton label="Quitar uno" onClick={() => setQuantity(line.item.productId, line.quantity - 1)}>
                      {line.quantity === 1 ? <Trash2 className="size-3.5" /> : <Minus className="size-3.5" />}
                    </IconButton>
                    <span className="tabular w-8 text-center text-sm">{line.quantity}</span>
                    <IconButton
                      label="Agregar uno"
                      disabled={line.quantity >= line.item.quantity}
                      onClick={() => setQuantity(line.item.productId, line.quantity + 1)}
                    >
                      <Plus className="size-3.5" />
                    </IconButton>
                  </div>
                  <p className="tabular w-24 text-right text-sm font-medium">{fmt.money(line.item.price * line.quantity)}</p>
                </li>
              ))}
            </ul>
          )}

          <div className="mt-auto flex flex-col gap-4 border-t border-line p-5">
            <div className="grid grid-cols-3 gap-2" role="radiogroup" aria-label="Método de pago">
              {payments.map(({ value, icon: Icon }) => (
                <button
                  key={value}
                  role="radio"
                  aria-checked={payment === value}
                  onClick={() => setPayment(value)}
                  className={clsx(
                    "flex flex-col items-center gap-1 rounded-lg border px-2 py-2.5 text-xs font-medium transition-colors",
                    payment === value ? "border-brand bg-brand/10 text-brand" : "border-line text-ink-2 hover:bg-surface-2",
                  )}
                >
                  <Icon className="size-4" aria-hidden />
                  {paymentLabel[value]}
                </button>
              ))}
            </div>
            <div className="flex items-baseline justify-between">
              <span className="text-sm text-ink-2">Total</span>
              <span className="tabular text-2xl font-semibold">{fmt.money(total)}</span>
            </div>
            <ErrorNote error={checkout.error} />
            <Button onClick={() => checkout.mutate()} disabled={cart.length === 0 || !storeId} loading={checkout.isPending} className="h-12 text-base">
              Cobrar {cart.length > 0 && fmt.money(total)}
            </Button>
          </div>
        </Card>
      </div>

      <Modal open={!!receipt} onClose={() => setReceipt(null)} title="Venta registrada">
        {receipt && (
          <div className="flex flex-col gap-4">
            <div className="flex items-center gap-3">
              <CheckCircle2 className="size-8 text-good" aria-hidden />
              <div>
                <p className="font-medium">{receipt.number}</p>
                <p className="text-sm text-muted">
                  {fmt.dateTime(receipt.createdAt)} · {paymentLabel[receipt.paymentMethod]}
                </p>
              </div>
            </div>
            <ul className="divide-y divide-line rounded-lg border border-line text-sm">
              {receipt.lines.map((l) => (
                <li key={l.productId} className="flex justify-between gap-3 px-3 py-2">
                  <span>
                    {l.quantity} × {l.productName}
                  </span>
                  <span className="tabular">{fmt.money(l.lineTotal)}</span>
                </li>
              ))}
            </ul>
            <div className="flex justify-between text-base font-semibold">
              <span>Total</span>
              <span className="tabular">{fmt.money(receipt.total)}</span>
            </div>
            <Button onClick={() => setReceipt(null)}>Nueva venta</Button>
          </div>
        )}
      </Modal>
    </>
  );
}

function IconButton({ label, children, ...props }: React.ButtonHTMLAttributes<HTMLButtonElement> & { label: string }) {
  return (
    <button
      aria-label={label}
      className="grid size-7 place-items-center rounded-md border border-line text-ink-2 hover:bg-surface-2 disabled:opacity-40"
      {...props}
    >
      {children}
    </button>
  );
}
