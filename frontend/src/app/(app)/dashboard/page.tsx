"use client";

import { useQuery } from "@tanstack/react-query";
import { AlertTriangle, ArrowLeftRight } from "lucide-react";
import Link from "next/link";
import { useState } from "react";
import { RevenueChart, StoreBars } from "@/components/charts";
import { Card, CardHeader, ErrorNote, PageHeader, Select, Spinner, td, th } from "@/components/ui";
import { api, qs } from "@/lib/api";
import { fmt } from "@/lib/format";
import { can, useSession, useStores } from "@/lib/session";
import type { Dashboard } from "@/lib/types";

const ranges = [7, 30, 90];

export default function DashboardPage() {
  const { data: user } = useSession();
  const { data: stores } = useStores();
  const [days, setDays] = useState(30);
  const [storeId, setStoreId] = useState("");

  const { data, isLoading, error } = useQuery({
    queryKey: ["dashboard", days, storeId],
    queryFn: () => api<Dashboard>(`/reports/dashboard${qs({ days, storeId })}`),
  });

  return (
    <>
      <PageHeader
        title="Dashboard"
        description={user?.storeName ? `Resumen de ${user.storeName}` : "Resumen de todas las sucursales"}
        action={
          <div className="flex flex-wrap gap-2">
            {can.admin(user) && (
              <Select value={storeId} onChange={(e) => setStoreId(e.target.value)} aria-label="Sucursal">
                <option value="">Todas las sucursales</option>
                {stores?.map((s) => (
                  <option key={s.id} value={s.id}>
                    {s.name}
                  </option>
                ))}
              </Select>
            )}
            <div className="flex rounded-lg border border-line bg-surface p-0.5" role="group" aria-label="Periodo">
              {ranges.map((r) => (
                <button
                  key={r}
                  onClick={() => setDays(r)}
                  aria-pressed={days === r}
                  className={`rounded-md px-3 py-1.5 text-sm ${days === r ? "bg-surface-2 font-medium text-ink" : "text-ink-2 hover:text-ink"}`}
                >
                  {r} días
                </button>
              ))}
            </div>
          </div>
        }
      />

      <ErrorNote error={error} />
      {isLoading || !data ? (
        <Spinner />
      ) : (
        <div className="flex flex-col gap-6">
          <div className="grid grid-cols-2 gap-4 lg:grid-cols-4">
            <Stat label={`Ingresos ${days} días`} value={fmt.money(data.revenuePeriod)} />
            <Stat label="Ventas" value={fmt.int(data.salesPeriod)} sub={`Ticket promedio ${fmt.money(data.averageTicket)}`} />
            <Stat label="Margen bruto" value={`${data.grossMarginPct.toFixed(1)}%`} />
            <Stat label="Ingresos de hoy" value={fmt.money(data.revenueToday)} />
          </div>

          {(data.lowStockItems > 0 || data.openTransfers > 0) && (
            <div className="flex flex-wrap gap-3">
              {data.lowStockItems > 0 && (
                <Link
                  href="/inventory?low=1"
                  className="flex items-center gap-2 rounded-lg border border-warning/40 bg-warning/10 px-3 py-2 text-sm text-ink hover:bg-warning/20"
                >
                  <AlertTriangle className="size-4 text-warning" aria-hidden />
                  <span>
                    <strong className="tabular">{data.lowStockItems}</strong> productos con stock bajo
                  </span>
                </Link>
              )}
              {data.openTransfers > 0 && can.manage(user) && (
                <Link
                  href="/transfers"
                  className="flex items-center gap-2 rounded-lg border border-line bg-surface px-3 py-2 text-sm text-ink hover:bg-surface-2"
                >
                  <ArrowLeftRight className="size-4 text-brand" aria-hidden />
                  <span>
                    <strong className="tabular">{data.openTransfers}</strong> transferencias abiertas
                  </span>
                </Link>
              )}
            </div>
          )}

          <div className="grid gap-6 lg:grid-cols-3">
            <Card className="lg:col-span-2">
              <CardHeader title="Ingresos diarios" subtitle="Días completos · Bs (hoy se muestra arriba)" />
              <div className="px-2 py-4 sm:px-4">
                {/* Today is still in progress; plotting it would read as a false drop at the end of the line. */}
                <RevenueChart data={data.dailyRevenue.slice(0, -1)} />
              </div>
            </Card>
            <Card>
              <CardHeader title="Ingresos por sucursal" subtitle={`Últimos ${days} días`} />
              <StoreBars data={data.revenueByStore} />
            </Card>
          </div>

          <Card>
            <CardHeader title="Productos más vendidos" subtitle="Por ingresos" />
            <div className="overflow-x-auto">
              <table className="w-full">
                <thead className="border-b border-line">
                  <tr>
                    <th className={th}>Producto</th>
                    <th className={`${th} text-right`}>Unidades</th>
                    <th className={`${th} text-right`}>Ingresos</th>
                    <th className={`${th} hidden w-1/3 sm:table-cell`}>
                      <span className="sr-only">Proporción</span>
                    </th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-line">
                  {data.topProducts.map((p) => (
                    <tr key={p.sku}>
                      <td className={td}>
                        <p className="font-medium">{p.name}</p>
                        <p className="text-xs text-muted">{p.sku}</p>
                      </td>
                      <td className={`${td} tabular text-right`}>{fmt.int(p.units)}</td>
                      <td className={`${td} tabular text-right`}>{fmt.money(p.revenue)}</td>
                      <td className={`${td} hidden sm:table-cell`}>
                        <div className="h-2 rounded-full bg-surface-2">
                          <div
                            className="h-2 rounded-full bg-series-1"
                            style={{ width: `${(p.revenue / (data.topProducts[0]?.revenue || 1)) * 100}%` }}
                          />
                        </div>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </Card>
        </div>
      )}
    </>
  );
}

function Stat({ label, value, sub }: { label: string; value: string; sub?: string }) {
  return (
    <Card className="px-5 py-4">
      <p className="text-xs font-medium text-muted">{label}</p>
      <p className="mt-1.5 text-2xl font-semibold tracking-tight text-ink">{value}</p>
      {sub && <p className="mt-1 text-xs text-ink-2">{sub}</p>}
    </Card>
  );
}
