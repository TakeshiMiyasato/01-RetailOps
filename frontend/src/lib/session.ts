"use client";

import { useQuery } from "@tanstack/react-query";
import { api } from "./api";
import type { Store, UserProfile } from "./types";

export function useSession() {
  return useQuery({ queryKey: ["me"], queryFn: () => api<UserProfile>("/auth/me"), staleTime: 5 * 60_000 });
}

export function useStores() {
  return useQuery({ queryKey: ["stores"], queryFn: () => api<Store[]>("/stores"), staleTime: 5 * 60_000 });
}

export const can = {
  manage: (u?: UserProfile) => u?.role === "Admin" || u?.role === "Manager",
  admin: (u?: UserProfile) => u?.role === "Admin",
  /** Admins operate any store; everyone else only their own. */
  operate: (u: UserProfile | undefined, storeId: string) => u?.role === "Admin" || u?.storeId === storeId,
};
