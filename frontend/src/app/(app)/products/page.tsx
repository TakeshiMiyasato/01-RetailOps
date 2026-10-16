"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Pencil, Plus } from "lucide-react";
import { useState, type FormEvent } from "react";
import { Badge, Button, Card, EmptyState, ErrorNote, Field, Input, Modal, PageHeader, Pagination, Select, Spinner, td, th } from "@/components/ui";
import { api, ApiError, qs } from "@/lib/api";
import { fmt } from "@/lib/format";
import { can, useSession } from "@/lib/session";
import type { Paged, Product } from "@/lib/types";

export default function ProductsPage() {
  const { data: user } = useSession();
  const [search, setSearch] = useState("");
  const [category, setCategory] = useState("");
  const [page, setPage] = useState(1);
  const [editing, setEditing] = useState<Product | "new" | null>(null);

  const categories = useQuery({ queryKey: ["categories"], queryFn: () => api<string[]>("/products/categories") });
  const { data, isLoading, error } = useQuery({
    queryKey: ["products", search, category, page],
    queryFn: () => api<Paged<Product>>(`/products${qs({ search, category, page, pageSize: 20 })}`),
  });

  return (
    <>
      <PageHeader
        title="Productos"
        description="Catálogo compartido por todas las sucursales"
        action={
          can.manage(user) && (
            <Button onClick={() => setEditing("new")}>
              <Plus className="size-4" aria-hidden /> Nuevo producto
            </Button>
          )
        }
      />

      <Card>
        <div className="flex flex-wrap gap-3 border-b border-line px-5 py-4">
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
          <Select
            value={category}
            onChange={(e) => {
              setCategory(e.target.value);
              setPage(1);
            }}
            aria-label="Categoría"
          >
            <option value="">Todas las categorías</option>
            {categories.data?.map((c) => (
              <option key={c}>{c}</option>
            ))}
          </Select>
        </div>

        <ErrorNote error={error} />
        {isLoading ? (
          <Spinner />
        ) : data?.items.length === 0 ? (
          <EmptyState title="No se encontraron productos" />
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full">
              <thead className="border-b border-line">
                <tr>
                  <th className={th}>Producto</th>
                  <th className={`${th} hidden md:table-cell`}>Código de barras</th>
                  <th className={`${th} hidden sm:table-cell`}>Categoría</th>
                  <th className={`${th} text-right`}>Costo</th>
                  <th className={`${th} text-right`}>Precio</th>
                  <th className={`${th} hidden text-right sm:table-cell`}>Margen</th>
                  {can.manage(user) && (
                    <th className={th}>
                      <span className="sr-only">Editar</span>
                    </th>
                  )}
                </tr>
              </thead>
              <tbody className="divide-y divide-line">
                {data?.items.map((p) => (
                  <tr key={p.id} className={p.isActive ? "" : "opacity-60"}>
                    <td className={td}>
                      <p className="font-medium">
                        {p.name} {!p.isActive && <Badge>Inactivo</Badge>}
                      </p>
                      <p className="text-xs text-muted">{p.sku}</p>
                    </td>
                    <td className={`${td} tabular hidden text-ink-2 md:table-cell`}>{p.barcode}</td>
                    <td className={`${td} hidden text-ink-2 sm:table-cell`}>{p.category}</td>
                    <td className={`${td} tabular text-right text-ink-2`}>{fmt.money(p.cost)}</td>
                    <td className={`${td} tabular text-right font-medium`}>{fmt.money(p.price)}</td>
                    <td className={`${td} tabular hidden text-right text-ink-2 sm:table-cell`}>
                      {(((p.price - p.cost) / p.price) * 100).toFixed(0)}%
                    </td>
                    {can.manage(user) && (
                      <td className={`${td} text-right`}>
                        <Button size="sm" variant="ghost" onClick={() => setEditing(p)} aria-label={`Editar ${p.name}`}>
                          <Pencil className="size-4" />
                        </Button>
                      </td>
                    )}
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
        {data && <Pagination page={data.page} totalPages={data.totalPages} onPage={setPage} />}
      </Card>

      {editing && <ProductModal product={editing === "new" ? null : editing} categories={categories.data ?? []} onClose={() => setEditing(null)} />}
    </>
  );
}

function ProductModal({ product, categories, onClose }: { product: Product | null; categories: string[]; onClose: () => void }) {
  const queryClient = useQueryClient();
  const [form, setForm] = useState({
    sku: product?.sku ?? "",
    barcode: product?.barcode ?? "",
    name: product?.name ?? "",
    category: product?.category ?? categories[0] ?? "",
    price: product?.price.toString() ?? "",
    cost: product?.cost.toString() ?? "",
    isActive: product?.isActive ?? true,
  });

  const mutation = useMutation({
    mutationFn: () =>
      api<Product>(product ? `/products/${product.id}` : "/products", {
        method: product ? "PUT" : "POST",
        json: { ...form, price: Number(form.price), cost: Number(form.cost) },
      }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["products"] });
      queryClient.invalidateQueries({ queryKey: ["categories"] });
      queryClient.invalidateQueries({ queryKey: ["stock"] });
      onClose();
    },
  });

  const fieldError = (name: string) => (mutation.error instanceof ApiError ? mutation.error.errors?.[name]?.[0] : undefined);
  const set = (key: keyof typeof form) => (e: React.ChangeEvent<HTMLInputElement>) => setForm({ ...form, [key]: e.target.value });

  function submit(e: FormEvent) {
    e.preventDefault();
    mutation.mutate();
  }

  return (
    <Modal open onClose={onClose} title={product ? "Editar producto" : "Nuevo producto"} wide>
      <form onSubmit={submit} className="grid gap-4 sm:grid-cols-2">
        <div className="sm:col-span-2">
          <Field label="Nombre" error={fieldError("name")}>
            <Input required value={form.name} onChange={set("name")} maxLength={160} />
          </Field>
        </div>
        <Field label="SKU" error={fieldError("sku")}>
          <Input required value={form.sku} onChange={set("sku")} maxLength={32} />
        </Field>
        <Field label="Código de barras (EAN)" error={fieldError("barcode")}>
          <Input required value={form.barcode} onChange={set("barcode")} inputMode="numeric" pattern="[0-9]{8,14}" />
        </Field>
        <Field label="Categoría" error={fieldError("category")}>
          <Input required value={form.category} onChange={set("category")} list="categories" />
          <datalist id="categories">
            {categories.map((c) => (
              <option key={c} value={c} />
            ))}
          </datalist>
        </Field>
        <div className="grid grid-cols-2 gap-4">
          <Field label="Costo (Bs)" error={fieldError("cost")}>
            <Input required type="number" step="0.01" min="0" value={form.cost} onChange={set("cost")} />
          </Field>
          <Field label="Precio (Bs)" error={fieldError("price")}>
            <Input required type="number" step="0.01" min="0.01" value={form.price} onChange={set("price")} />
          </Field>
        </div>
        {product && (
          <label className="flex items-center gap-2 text-sm sm:col-span-2">
            <input type="checkbox" checked={form.isActive} onChange={(e) => setForm({ ...form, isActive: e.target.checked })} className="size-4" />
            Producto activo (visible en el punto de venta)
          </label>
        )}
        <div className="sm:col-span-2">
          <ErrorNote error={mutation.error} />
        </div>
        <div className="flex justify-end gap-2 sm:col-span-2">
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
