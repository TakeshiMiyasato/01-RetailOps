"use client";

import { useState } from "react";
import { Select } from "@/components/ui";
import { can, useSession, useStores } from "@/lib/session";

/**
 * Admins choose which store to operate; managers and cashiers are pinned to their own.
 * Returns the effective store id (empty while the session loads).
 */
export function useActiveStore() {
  const { data: user } = useSession();
  const { data: stores } = useStores();
  const [selected, setSelected] = useState<string>("");

  const storeId = can.admin(user) ? selected || stores?.[0]?.id || "" : (user?.storeId ?? "");
  const store = stores?.find((s) => s.id === storeId);

  const picker = can.admin(user) ? (
    <Select value={storeId} onChange={(e) => setSelected(e.target.value)} aria-label="Sucursal">
      {stores?.map((s) => (
        <option key={s.id} value={s.id}>
          {s.name}
        </option>
      ))}
    </Select>
  ) : null;

  return { storeId, store, picker, user };
}
