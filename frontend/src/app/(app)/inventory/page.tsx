"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { AlertTriangle, History, SlidersHorizontal } from "lucide-react";
import { useSearchParams } from "next/navigation";
import { Suspense, useState, type FormEvent } from "react";
import { useActiveStore } from "@/components/store-picker";
import { Badge, Button, Card, EmptyState, ErrorNote, Field, Input, Modal, PageHeader, Pagination, Select, Spinner, td, th } from "@/components/ui";
import { api, qs } from "@/lib/api";
import { fmt, movementLabel } from "@/lib/format";
import { can } from "@/lib/session";
import type { Paged, StockItem, StockMovement } from "@/lib/types";

export default function InventoryPage() {
  // useSearchParams is request data; under Cache Components it must sit inside a Suspense boundary.
  return (
    <Suspense fallback={<Spinner />}>
      <Inventory />
    </Suspense>
  );
}

function Inventory() {
  const params = useSearchParams();
  const { storeId, store, picker, user } = useActiveStore();
  const [search, setSearch] = useState("");
  const [lowOnly, setLowOnly] = useState(params.get("low") === "1");
  const [page, setPage] = useState(1);
  const [adjusting, setAdjusting] = useState<StockItem | null>(null);
  const [history, setHistory] = useState<StockItem | null>(null);

  const { data, isLoading, error } = useQuery({
    queryKey: ["stock", storeId, search, lowOnly, page],
    queryFn: () => api<Paged<StockItem>>(`/stores/${storeId}/stock${qs({ search, lowOnly, page, pageSize: 20 })}`),
    enabled: !!storeId,
  });

  return (
    <>
      <PageHeader title="Inventario" description={store ? `Stock de ${store.name}` : undefined} action={picker} />

      <Card>
        <div className="flex flex-wrap items-center gap-3 border-b border-line px-5 py-4">
          <Input
            value={search}
            onChange={(e) => {
              setSearch(e.target.value);
              setPage(1);
            }}
            placeholder="Buscar por nombre, SKU o código"
            className="max-w-xs"
            aria-label="Buscar"
          />
          <label className="flex items-center gap-2 text-sm text-ink-2">
            <input
              type="checkbox"
              checked={lowOnly}
              onChange={(e) => {
                setLowOnly(e.target.checked);
                setPage(1);
              }}
              className="size-4 accent-[var(--brand)]"
            />
            Solo stock bajo
          </label>
          {data && <span className="ml-auto text-sm text-muted tabular">{fmt.int(data.totalCount)} productos</span>}
        </div>

        <ErrorNote error={error} />
        {isLoading ? (
          <Spinner />
        ) : data?.items.length === 0 ? (
          <EmptyState title="No hay productos para mostrar" hint={lowOnly ? "Ningún producto está bajo el punto de reorden." : undefined} />
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full">
              <thead className="border-b border-line">
                <tr>
                  <th className={th}>Producto</th>
                  <th className={`${th} hidden md:table-cell`}>Categoría</th>
                  <th className={`${th} text-right`}>Precio</th>
                  <th className={`${th} text-right`}>Stock</th>
                  <th className={`${th} text-right`}>
                    <span className="sr-only">Acciones</span>
                  </th>
                </tr>
              </thead>
              <tbody className="divide-y divide-line">
                {data?.items.map((item) => (
                  <tr key={item.productId}>
                    <td className={td}>
                      <p className="font-medium">{item.name}</p>
                      <p className="text-xs text-muted">
                        {item.sku} · {item.barcode}
                      </p>
                    </td>
                    <td className={`${td} hidden text-ink-2 md:table-cell`}>{item.category}</td>
                    <td className={`${td} tabular text-right`}>{fmt.money(item.price)}</td>
                    <td className={`${td} text-right`}>
                      <div className="flex items-center justify-end gap-2">
                        {item.isLow && (
                          <Badge tone={item.quantity === 0 ? "critical" : "warning"}>
                            <AlertTriangle className="size-3" aria-hidden />
                            {item.quantity === 0 ? "Agotado" : "Bajo"}
                          </Badge>
                        )}
                        <span className="tabular font-medium">{fmt.int(item.quantity)}</span>
                      </div>
                    </td>
                    <td className={`${td} text-right`}>
                      <div className="flex justify-end gap-1">
                        <Button size="sm" variant="ghost" onClick={() => setHistory(item)} aria-label={`Movimientos de ${item.name}`}>
                          <History className="size-4" />
                        </Button>
                        {can.manage(user) && (
                          <Button size="sm" variant="ghost" onClick={() => setAdjusting(item)} aria-label={`Ajustar ${item.name}`}>
                            <SlidersHorizontal className="size-4" />
                          </Button>
                        )}
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
        {data && <Pagination page={data.page} totalPages={data.totalPages} onPage={setPage} />}
      </Card>

      {adjusting && <AdjustModal storeId={storeId} item={adjusting} onClose={() => setAdjusting(null)} />}
      {history && <HistoryModal storeId={storeId} item={history} onClose={() => setHistory(null)} />}
    </>
  );
}

function AdjustModal({ storeId, item, onClose }: { storeId: string; item: StockItem; onClose: () => void }) {
  const queryClient = useQueryClient();
  const [kind, setKind] = useState<"receipt" | "adjust">("receipt");
  const [quantity, setQuantity] = useState("");
  const [reason, setReason] = useState("");

  const mutation = useMutation({
    mutationFn: () =>
      api<StockItem>(`/stores/${storeId}/stock/adjustments`, {
        method: "POST",
        json: { productId: item.productId, quantity: Number(quantity), reason, isReceipt: kind === "receipt" },
      }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["stock"] });
      queryClient.invalidateQueries({ queryKey: ["movements"] });
      onClose();
    },
  });

  function submit(e: FormEvent) {
    e.preventDefault();
    mutation.mutate();
  }

  const preview = item.quantity + (Number(quantity) || 0);

  return (
    <Modal open onClose={onClose} title={`Ajustar stock · ${item.name}`}>
      <form onSubmit={submit} className="flex flex-col gap-4">
        <Field label="Tipo de movimiento">
          <Select value={kind} onChange={(e) => setKind(e.target.value as typeof kind)}>
            <option value="receipt">Ingreso de mercadería (proveedor)</option>
            <option value="adjust">Ajuste (merma, rotura, corrección)</option>
          </Select>
        </Field>
        <Field label={kind === "receipt" ? "Unidades recibidas" : "Unidades (+ suma, − resta)"}>
          <Input type="number" required value={quantity} onChange={(e) => setQuantity(e.target.value)} min={kind === "receipt" ? 1 : undefined} />
        </Field>
        <Field label="Motivo">
          <Input required value={reason} onChange={(e) => setReason(e.target.value)} placeholder={kind === "receipt" ? "Factura #1234" : "Producto dañado"} maxLength={200} />
        </Field>
        <p className="text-sm text-ink-2">
          Stock actual <strong className="tabular">{item.quantity}</strong> → nuevo{" "}
          <strong className={`tabular ${preview < 0 ? "text-critical" : ""}`}>{preview}</strong>
        </p>
        <ErrorNote error={mutation.error} />
        <div className="flex justify-end gap-2">
          <Button type="button" variant="secondary" onClick={onClose}>
            Cancelar
          </Button>
          <Button type="submit" loading={mutation.isPending}>
            Guardar
          </Button>
        </div>
      </form>
    </Modal>
  );
}

function HistoryModal({ storeId, item, onClose }: { storeId: string; item: StockItem; onClose: () => void }) {
  const { data, isLoading } = useQuery({
    queryKey: ["movements", storeId, item.productId],
    queryFn: () => api<Paged<StockMovement>>(`/stores/${storeId}/movements${qs({ productId: item.productId, pageSize: 15 })}`),
  });

  return (
    <Modal open onClose={onClose} title={`Movimientos · ${item.name}`} wide>
      {isLoading ? (
        <Spinner />
      ) : (
        <ul className="divide-y divide-line">
          {data?.items.map((m) => (
            <li key={m.id} className="flex items-center justify-between gap-4 py-2.5 text-sm">
              <div className="min-w-0">
                <p className="font-medium">
                  {movementLabel[m.type] ?? m.type} <span className="font-normal text-muted">· {m.reference}</span>
                </p>
                <p className="truncate text-xs text-muted">
                  {fmt.dateTime(m.createdAt)}
                  {m.note ? ` · ${m.note}` : ""}
                </p>
              </div>
              <span className={`tabular shrink-0 font-medium ${m.quantity > 0 ? "text-good-text" : "text-ink-2"}`}>
                {m.quantity > 0 ? "+" : "−"}
                {fmt.int(Math.abs(m.quantity))}
              </span>
            </li>
          ))}
        </ul>
      )}
    </Modal>
  );
}
