"use client";

import clsx from "clsx";
import { ArrowLeftRight, Boxes, LayoutDashboard, LogOut, Menu, Package, Receipt, ShoppingCart, Store } from "lucide-react";
import Link from "next/link";
import { useQueryClient } from "@tanstack/react-query";
import { usePathname } from "next/navigation";
import { useState, type ReactNode } from "react";
import { roleLabel } from "@/lib/format";
import { can, useSession } from "@/lib/session";

const nav = [
  { href: "/dashboard", label: "Dashboard", icon: LayoutDashboard, show: () => true },
  { href: "/pos", label: "Punto de venta", icon: ShoppingCart, show: () => true },
  { href: "/sales", label: "Ventas", icon: Receipt, show: () => true },
  { href: "/inventory", label: "Inventario", icon: Boxes, show: () => true },
  { href: "/products", label: "Productos", icon: Package, show: () => true },
  { href: "/transfers", label: "Transferencias", icon: ArrowLeftRight, show: can.manage },
];

export function AppShell({ children }: { children: ReactNode }) {
  const pathname = usePathname();
  const { data: user } = useSession();
  const [open, setOpen] = useState(false);
  const queryClient = useQueryClient();

  async function logout() {
    await fetch("/api/auth/logout", { method: "POST" });
    queryClient.clear();
    // Full navigation, not router.replace: with cacheComponents, visited routes stay mounted
    // (hidden in <Activity>) with their local state — a stale login form, the previous user's cart.
    window.location.replace("/login");
  }

  const sidebar = (
    <nav className="flex h-full flex-col gap-1 p-3" aria-label="Principal">
      <div className="mb-4 flex items-center gap-2.5 px-2 py-1">
        <div className="grid size-8 place-items-center rounded-lg bg-brand text-brand-ink">
          <Store className="size-4" aria-hidden />
        </div>
        <div className="leading-tight">
          <p className="text-sm font-semibold">RetailOps</p>
          <p className="text-xs text-muted">{user?.tenantName ?? "…"}</p>
        </div>
      </div>
      {nav
        .filter((item) => item.show(user))
        .map(({ href, label, icon: Icon }) => {
          const active = pathname.startsWith(href);
          return (
            <Link
              key={href}
              href={href}
              onClick={() => setOpen(false)}
              className={clsx(
                "flex items-center gap-3 rounded-lg px-3 py-2 text-sm transition-colors",
                active ? "bg-brand/10 font-medium text-brand" : "text-ink-2 hover:bg-surface-2 hover:text-ink",
              )}
              aria-current={active ? "page" : undefined}
            >
              <Icon className="size-4" aria-hidden />
              {label}
            </Link>
          );
        })}
      <div className="mt-auto border-t border-line pt-3">
        {user && (
          <div className="px-2 pb-3">
            <p className="truncate text-sm font-medium">{user.fullName}</p>
            <p className="truncate text-xs text-muted">
              {roleLabel[user.role]}
              {user.storeName ? ` · ${user.storeName}` : " · Todas las sucursales"}
            </p>
          </div>
        )}
        <button onClick={logout} className="flex w-full items-center gap-3 rounded-lg px-3 py-2 text-sm text-ink-2 hover:bg-surface-2 hover:text-ink">
          <LogOut className="size-4" aria-hidden />
          Cerrar sesión
        </button>
      </div>
    </nav>
  );

  return (
    <div className="flex min-h-screen">
      <aside className="sticky top-0 hidden h-screen w-60 shrink-0 border-r border-line bg-surface lg:block">{sidebar}</aside>

      {open && (
        <div className="fixed inset-0 z-40 bg-black/40 lg:hidden" onClick={() => setOpen(false)}>
          <aside className="h-full w-64 bg-surface" onClick={(e) => e.stopPropagation()}>
            {sidebar}
          </aside>
        </div>
      )}

      <div className="flex min-w-0 flex-1 flex-col">
        <header className="sticky top-0 z-30 flex h-14 items-center gap-3 border-b border-line bg-surface px-4 lg:hidden">
          <button onClick={() => setOpen(true)} className="rounded-md p-1.5 hover:bg-surface-2" aria-label="Abrir menú">
            <Menu className="size-5" />
          </button>
          <span className="text-sm font-semibold">RetailOps</span>
        </header>
        <main className="mx-auto w-full max-w-7xl flex-1 px-4 py-6 sm:px-6 lg:px-8 lg:py-8">{children}</main>
      </div>
    </div>
  );
}
