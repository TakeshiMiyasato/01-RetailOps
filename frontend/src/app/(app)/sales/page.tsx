"use client";

import { useQuery } from "@tanstack/react-query";
import { useState } from "react";
import { Card, EmptyState, ErrorNote, Modal, PageHeader, Pagination, Select, Spinner, td, th } from "@/components/ui";
import { api, qs } from "@/lib/api";
import { fmt, paymentLabel } from "@/lib/format";
import { can, useSession, useStores } from "@/lib/session";
import type { Paged, SaleDetail, SaleSummary } from "@/lib/types";

export default function SalesPage() {
  const { data: user } = useSession();
  const { data: stores } = useStores();
  const [storeId, setStoreId] = useState("");
  const [page, setPage] = useState(1);
  const [selected, setSelected] = useState<string | null>(null);

  const { data, isLoading, error } = useQuery({
    queryKey: ["sales", storeId, page],
    queryFn: () => api<Paged<SaleSummary>>(`/sales${qs({ storeId, page, pageSize: 20 })}`),
  });

  return (
    <>
      <PageHeader
        title="Ventas"
        description={user?.storeName ? `Historial de ${user.storeName}` : "Historial de todas las sucursales"}
        action={
          can.admin(user) && (
            <Select
              value={storeId}
              onChange={(e) => {
                setStoreId(e.target.value);
                setPage(1);
              }}
              aria-label="Sucursal"
            >
              <option value="">Todas las sucursales</option>
              {stores?.map((s) => (
                <option key={s.id} value={s.id}>
                  {s.name}
                </option>
              ))}
            </Select>
          )
        }
      />

      <Card>
        <ErrorNote error={error} />
        {isLoading ? (
          <Spinner />
        ) : data?.items.length === 0 ? (
          <EmptyState title="Todavía no hay ventas" />
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full">
              <thead className="border-b border-line">
                <tr>
                  <th className={th}>Número</th>
                  <th className={`${th} hidden md:table-cell`}>Sucursal</th>
                  <th className={`${th} hidden lg:table-cell`}>Cajero</th>
                  <th className={`${th} hidden sm:table-cell`}>Pago</th>
                  <th className={`${th} text-right`}>Unid.</th>
                  <th className={`${th} text-right`}>Total</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-line">
                {data?.items.map((s) => (
                  <tr key={s.id} onClick={() => setSelected(s.id)} className="cursor-pointer hover:bg-surface-2">
                    <td className={td}>
                      <button className="text-left font-medium hover:text-brand" onClick={() => setSelected(s.id)}>
                        {s.number}
                      </button>
                      <p className="text-xs text-muted">{fmt.dateTime(s.createdAt)}</p>
                    </td>
                    <td className={`${td} hidden text-ink-2 md:table-cell`}>{s.storeName}</td>
                    <td className={`${td} hidden text-ink-2 lg:table-cell`}>{s.cashierName}</td>
                    <td className={`${td} hidden text-ink-2 sm:table-cell`}>{paymentLabel[s.paymentMethod]}</td>
                    <td className={`${td} tabular text-right`}>{s.items}</td>
                    <td className={`${td} tabular text-right font-medium`}>{fmt.money(s.total)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
        {data && <Pagination page={data.page} totalPages={data.totalPages} onPage={setPage} />}
      </Card>

      {selected && <SaleModal id={selected} onClose={() => setSelected(null)} />}
    </>
  );
}

function SaleModal({ id, onClose }: { id: string; onClose: () => void }) {
  const { data, isLoading } = useQuery({ queryKey: ["sale", id], queryFn: () => api<SaleDetail>(`/sales/${id}`) });
  return (
    <Modal open onClose={onClose} title={data?.number ?? "Venta"}>
      {isLoading || !data ? (
        <Spinner />
      ) : (
        <div className="flex flex-col gap-4">
          <p className="text-sm text-ink-2">
            {data.storeName} · {data.cashierName} · {fmt.dateTime(data.createdAt)} · {paymentLabel[data.paymentMethod]}
          </p>
          <ul className="divide-y divide-line rounded-lg border border-line text-sm">
            {data.lines.map((l) => (
              <li key={l.productId} className="flex justify-between gap-3 px-3 py-2">
                <span>
                  {l.quantity} × {l.productName}
                  <span className="block text-xs text-muted tabular">{fmt.money(l.unitPrice)} c/u</span>
                </span>
                <span className="tabular">{fmt.money(l.lineTotal)}</span>
              </li>
            ))}
          </ul>
          <div className="flex justify-between text-base font-semibold">
            <span>Total</span>
            <span className="tabular">{fmt.money(data.total)}</span>
          </div>
        </div>
      )}
    </Modal>
  );
}
