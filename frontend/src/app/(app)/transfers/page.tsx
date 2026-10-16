"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { ArrowRight, Plus, Trash2 } from "lucide-react";
import { useState, type FormEvent } from "react";
import { Badge, Button, Card, EmptyState, ErrorNote, Field, Input, Modal, PageHeader, Pagination, Select, Spinner } from "@/components/ui";
import { api, qs } from "@/lib/api";
import { fmt, transferLabel } from "@/lib/format";
import { can, useSession, useStores } from "@/lib/session";
import type { Paged, StockItem, Transfer, TransferStatus, UserProfile } from "@/lib/types";

const statusTone: Record<TransferStatus, "brand" | "warning" | "good" | "neutral"> = {
  Requested: "warning",
  InTransit: "brand",
  Received: "good",
  Cancelled: "neutral",
};

export default function TransfersPage() {
  const { data: user } = useSession();
  const [status, setStatus] = useState("");
  const [page, setPage] = useState(1);
  const [creating, setCreating] = useState(false);

  const { data, isLoading, error } = useQuery({
    queryKey: ["transfers", status, page],
    queryFn: () => api<Paged<Transfer>>(`/transfers${qs({ status, page, pageSize: 10 })}`),
  });

  return (
    <>
      <PageHeader
        title="Transferencias"
        description="Mover stock entre sucursales: solicitar → despachar → recibir"
        action={
          <Button onClick={() => setCreating(true)}>
            <Plus className="size-4" aria-hidden /> Nueva transferencia
          </Button>
        }
      />

      <div className="mb-4 flex flex-wrap gap-2" role="group" aria-label="Filtrar por estado">
        {["", "Requested", "InTransit", "Received", "Cancelled"].map((s) => (
          <button
            key={s}
            onClick={() => {
              setStatus(s);
              setPage(1);
            }}
            aria-pressed={status === s}
            className={`rounded-full border px-3 py-1 text-sm ${status === s ? "border-brand bg-brand/10 text-brand" : "border-line text-ink-2 hover:bg-surface-2"}`}
          >
            {s ? transferLabel[s] : "Todas"}
          </button>
        ))}
      </div>

      <ErrorNote error={error} />
      {isLoading ? (
        <Spinner />
      ) : data?.items.length === 0 ? (
        <Card>
          <EmptyState title="No hay transferencias" hint="Crea una para reponer stock entre sucursales." />
        </Card>
      ) : (
        <div className="flex flex-col gap-3">
          {data?.items.map((t) => <TransferCard key={t.id} transfer={t} user={user} />)}
          {data && data.totalPages > 1 && (
            <Card>
              <Pagination page={data.page} totalPages={data.totalPages} onPage={setPage} />
            </Card>
          )}
        </div>
      )}

      {creating && <CreateTransferModal user={user} onClose={() => setCreating(false)} />}
    </>
  );
}

function TransferCard({ transfer: t, user }: { transfer: Transfer; user?: UserProfile }) {
  const queryClient = useQueryClient();
  const action = useMutation({
    mutationFn: (verb: "ship" | "receive" | "cancel") => api<Transfer>(`/transfers/${t.id}/${verb}`, { method: "POST" }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["transfers"] });
      queryClient.invalidateQueries({ queryKey: ["stock"] });
      queryClient.invalidateQueries({ queryKey: ["dashboard"] });
    },
  });

  const canShip = t.status === "Requested" && can.operate(user, t.fromStoreId);
  const canReceive = t.status === "InTransit" && can.operate(user, t.toStoreId);
  const canCancel = t.status === "Requested" && (can.operate(user, t.fromStoreId) || can.operate(user, t.toStoreId));

  return (
    <Card className="p-5">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <div className="flex flex-wrap items-center gap-2">
            <p className="font-medium">{t.number}</p>
            <Badge tone={statusTone[t.status]}>{transferLabel[t.status]}</Badge>
          </div>
          <p className="mt-1 flex items-center gap-1.5 text-sm text-ink-2">
            {t.fromStore} <ArrowRight className="size-3.5" aria-label="hacia" /> {t.toStore}
          </p>
          <p className="mt-1 text-xs text-muted">
            Solicitada {fmt.dateTime(t.requestedAt)}
            {t.shippedAt && ` · Despachada ${fmt.dateTime(t.shippedAt)}`}
            {t.receivedAt && ` · Recibida ${fmt.dateTime(t.receivedAt)}`}
          </p>
          {t.note && <p className="mt-1 text-sm text-ink-2">“{t.note}”</p>}
        </div>
        <div className="flex gap-2">
          {canCancel && (
            <Button size="sm" variant="danger" loading={action.isPending && action.variables === "cancel"} onClick={() => action.mutate("cancel")}>
              Cancelar
            </Button>
          )}
          {canShip && (
            <Button size="sm" loading={action.isPending && action.variables === "ship"} onClick={() => action.mutate("ship")}>
              Despachar
            </Button>
          )}
          {canReceive && (
            <Button size="sm" loading={action.isPending && action.variables === "receive"} onClick={() => action.mutate("receive")}>
              Confirmar recepción
            </Button>
          )}
        </div>
      </div>
      <ul className="mt-4 flex flex-wrap gap-2">
        {t.lines.map((l) => (
          <li key={l.productId} className="rounded-md bg-surface-2 px-2.5 py-1 text-xs text-ink-2">
            <span className="tabular font-medium text-ink">{l.quantity}</span> × {l.productName}
          </li>
        ))}
      </ul>
      {action.error && (
        <div className="mt-3">
          <ErrorNote error={action.error} />
        </div>
      )}
    </Card>
  );
}

function CreateTransferModal({ user, onClose }: { user?: UserProfile; onClose: () => void }) {
  const queryClient = useQueryClient();
  const { data: stores } = useStores();
  const [fromStoreId, setFrom] = useState("");
  const [toStoreId, setTo] = useState(user?.role === "Admin" ? "" : (user?.storeId ?? ""));
  const [note, setNote] = useState("");
  const [search, setSearch] = useState("");
  const [lines, setLines] = useState<{ item: StockItem; quantity: number }[]>([]);

  const origin = fromStoreId || stores?.find((s) => s.id !== toStoreId)?.id || "";
  const results = useQuery({
    queryKey: ["transfer-search", origin, search],
    queryFn: () => api<Paged<StockItem>>(`/stores/${origin}/stock${qs({ search, pageSize: 6 })}`),
    enabled: !!origin && search.trim().length >= 2 && can.admin(user),
  });
  // Non-admins can't read another store's stock, so they search the shared catalog instead.
  const catalog = useQuery({
    queryKey: ["transfer-catalog", search],
    queryFn: () => api<Paged<{ id: string; sku: string; name: string; barcode: string; category: string; price: number }>>(`/products${qs({ search, pageSize: 6 })}`),
    enabled: search.trim().length >= 2 && !can.admin(user),
  });

  const options: StockItem[] =
    results.data?.items ??
    catalog.data?.items.map((p) => ({ productId: p.id, sku: p.sku, barcode: p.barcode, name: p.name, category: p.category, price: p.price, quantity: -1, reorderPoint: 0, isLow: false })) ??
    [];

  const mutation = useMutation({
    mutationFn: () =>
      api<Transfer>("/transfers", {
        method: "POST",
        json: { fromStoreId: origin, toStoreId, note: note || null, lines: lines.map((l) => ({ productId: l.item.productId, quantity: l.quantity })) },
      }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["transfers"] });
      onClose();
    },
  });

  function submit(e: FormEvent) {
    e.preventDefault();
    mutation.mutate();
  }

  return (
    <Modal open onClose={onClose} title="Nueva transferencia" wide>
      <form onSubmit={submit} className="flex flex-col gap-4">
        <div className="grid gap-4 sm:grid-cols-2">
          <Field label="Desde">
            <Select value={origin} onChange={(e) => setFrom(e.target.value)} required>
              {stores
                ?.filter((s) => s.id !== toStoreId)
                .map((s) => (
                  <option key={s.id} value={s.id}>
                    {s.name}
                  </option>
                ))}
            </Select>
          </Field>
          <Field label="Hacia">
            <Select value={toStoreId} onChange={(e) => setTo(e.target.value)} required>
              <option value="" disabled>
                Selecciona…
              </option>
              {stores
                ?.filter((s) => s.id !== origin)
                .map((s) => (
                  <option key={s.id} value={s.id}>
                    {s.name}
                  </option>
                ))}
            </Select>
          </Field>
        </div>

        <Field label="Agregar productos">
          <Input value={search} onChange={(e) => setSearch(e.target.value)} placeholder="Buscar producto…" />
        </Field>
        {options.length > 0 && search.length >= 2 && (
          <ul className="-mt-2 divide-y divide-line rounded-lg border border-line">
            {options.map((item) => (
              <li key={item.productId}>
                <button
                  type="button"
                  className="flex w-full justify-between px-3 py-2 text-left text-sm hover:bg-surface-2"
                  onClick={() => {
                    if (!lines.some((l) => l.item.productId === item.productId)) setLines([...lines, { item, quantity: 1 }]);
                    setSearch("");
                  }}
                >
                  <span>{item.name}</span>
                  {item.quantity >= 0 && <span className="tabular text-muted">{item.quantity} en origen</span>}
                </button>
              </li>
            ))}
          </ul>
        )}

        {lines.length > 0 && (
          <ul className="divide-y divide-line rounded-lg border border-line">
            {lines.map((l) => (
              <li key={l.item.productId} className="flex items-center gap-3 px-3 py-2 text-sm">
                <span className="flex-1">{l.item.name}</span>
                <Input
                  type="number"
                  min={1}
                  value={l.quantity}
                  onChange={(e) => setLines(lines.map((x) => (x === l ? { ...x, quantity: Number(e.target.value) } : x)))}
                  className="h-8 w-20"
                  aria-label={`Cantidad de ${l.item.name}`}
                />
                <button type="button" onClick={() => setLines(lines.filter((x) => x !== l))} className="text-muted hover:text-critical" aria-label="Quitar">
                  <Trash2 className="size-4" />
                </button>
              </li>
            ))}
          </ul>
        )}

        <Field label="Nota (opcional)">
          <Input value={note} onChange={(e) => setNote(e.target.value)} maxLength={300} />
        </Field>
        <ErrorNote error={mutation.error} />
        <div className="flex justify-end gap-2">
          <Button type="button" variant="secondary" onClick={onClose}>
            Cancelar
          </Button>
          <Button type="submit" disabled={lines.length === 0 || !toStoreId} loading={mutation.isPending}>
            Solicitar transferencia
          </Button>
        </div>
      </form>
    </Modal>
  );
}
