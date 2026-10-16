"use client";

import { Area, AreaChart, CartesianGrid, ResponsiveContainer, Tooltip, XAxis, YAxis } from "recharts";
import { fmt } from "@/lib/format";

/** Single-series daily revenue: no legend (the card title names the series), crosshair + tooltip on hover. */
export function RevenueChart({ data }: { data: { date: string; revenue: number; sales: number }[] }) {
  return (
    <div className="h-64 w-full">
      <ResponsiveContainer width="100%" height="100%">
        <AreaChart data={data} margin={{ top: 8, right: 8, bottom: 0, left: 0 }}>
          <defs>
            <linearGradient id="revenueFill" x1="0" y1="0" x2="0" y2="1">
              <stop offset="0%" stopColor="var(--series-1)" stopOpacity={0.18} />
              <stop offset="100%" stopColor="var(--series-1)" stopOpacity={0} />
            </linearGradient>
          </defs>
          <CartesianGrid vertical={false} stroke="var(--line)" />
          <XAxis
            dataKey="date"
            tickFormatter={fmt.day}
            tick={{ fill: "var(--muted)", fontSize: 12 }}
            axisLine={{ stroke: "var(--axis)" }}
            tickLine={false}
            minTickGap={24}
          />
          <YAxis
            tickFormatter={(v: number) => fmt.compactMoney(v)}
            tick={{ fill: "var(--muted)", fontSize: 12 }}
            axisLine={false}
            tickLine={false}
            width={64}
          />
          <Tooltip
            cursor={{ stroke: "var(--axis)", strokeWidth: 1 }}
            content={({ active, payload }) => {
              if (!active || !payload?.length) return null;
              const point = payload[0].payload as { date: string; revenue: number; sales: number };
              return (
                <div className="rounded-lg border border-line bg-surface px-3 py-2 text-xs shadow-md">
                  <p className="font-medium text-ink">{fmt.day(point.date)}</p>
                  <p className="tabular mt-1 text-ink-2">
                    {fmt.money(point.revenue)} · {fmt.int(point.sales)} ventas
                  </p>
                </div>
              );
            }}
          />
          <Area
            type="monotone"
            dataKey="revenue"
            stroke="var(--series-1)"
            strokeWidth={2}
            fill="url(#revenueFill)"
            activeDot={{ r: 4, stroke: "var(--surface)", strokeWidth: 2, fill: "var(--series-1)" }}
          />
        </AreaChart>
      </ResponsiveContainer>
    </div>
  );
}

/** Horizontal bars with direct value labels; one hue because the bars encode magnitude, not identity. */
export function StoreBars({ data }: { data: { storeId: string; storeName: string; revenue: number; sales: number }[] }) {
  const max = Math.max(1, ...data.map((d) => d.revenue));
  return (
    <ul className="flex flex-col gap-4 px-5 py-5">
      {data.map((d) => (
        <li key={d.storeId} title={`${d.storeName}: ${fmt.money(d.revenue)} en ${fmt.int(d.sales)} ventas`}>
          <div className="mb-1.5 flex items-baseline justify-between gap-3 text-sm">
            <span className="truncate text-ink">{d.storeName}</span>
            <span className="tabular shrink-0 text-ink-2">{fmt.money(d.revenue)}</span>
          </div>
          <div className="h-2 rounded-full bg-surface-2">
            <div className="h-2 rounded-full bg-series-1" style={{ width: `${(d.revenue / max) * 100}%` }} />
          </div>
        </li>
      ))}
    </ul>
  );
}
