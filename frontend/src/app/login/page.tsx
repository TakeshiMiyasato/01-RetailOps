"use client";

import { useQueryClient } from "@tanstack/react-query";
import { Store } from "lucide-react";
import { useRouter } from "next/navigation";
import { useState, type FormEvent } from "react";
import { Button, ErrorNote, Field, Input } from "@/components/ui";

const demoAccounts = [
  { email: "admin@andesretail.demo", label: "Administrador", hint: "Todas las sucursales" },
  { email: "gerente.lpz@andesretail.demo", label: "Gerente", hint: "La Paz Centro" },
  { email: "cajero.lpz@andesretail.demo", label: "Cajero", hint: "La Paz Centro" },
  { email: "admin@altiplano.demo", label: "Otro tenant", hint: "Altiplano Market" },
];
const DEMO_PASSWORD = "Demo2026!";

export default function LoginPage() {
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const router = useRouter();
  const queryClient = useQueryClient();

  async function signIn(emailValue: string, passwordValue: string) {
    setLoading(true);
    setError(null);
    const response = await fetch("/api/auth/login", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ email: emailValue, password: passwordValue }),
    });
    if (response.ok) {
      queryClient.clear(); // never show the previous user's cached data
      router.replace("/dashboard");
      return;
    }
    setLoading(false);
    setError(response.status === 429 ? "Demasiados intentos. Espera un minuto." : "Correo o contraseña incorrectos.");
  }

  function onSubmit(e: FormEvent) {
    e.preventDefault();
    signIn(email, password);
  }

  return (
    <main className="grid min-h-screen place-items-center px-4 py-10">
      <div className="w-full max-w-sm">
        <div className="mb-8 flex flex-col items-center text-center">
          <div className="mb-4 grid size-11 place-items-center rounded-xl bg-brand text-brand-ink">
            <Store className="size-5" aria-hidden />
          </div>
          <h1 className="text-xl font-semibold tracking-tight">RetailOps</h1>
          <p className="mt-1 text-sm text-ink-2">Inventario y ventas multi-sucursal</p>
        </div>

        <form onSubmit={onSubmit} className="flex flex-col gap-4 rounded-xl border border-line bg-surface p-6">
          <Field label="Correo">
            <Input type="email" autoComplete="username" required value={email} onChange={(e) => setEmail(e.target.value)} />
          </Field>
          <Field label="Contraseña">
            <Input type="password" autoComplete="current-password" required value={password} onChange={(e) => setPassword(e.target.value)} />
          </Field>
          <ErrorNote error={error} />
          <Button type="submit" loading={loading}>
            Ingresar
          </Button>
        </form>

        <div className="mt-6">
          <p className="mb-2 text-center text-xs font-medium uppercase tracking-wide text-muted">Cuentas demo</p>
          <div className="grid grid-cols-2 gap-2">
            {demoAccounts.map((account) => (
              <button
                key={account.email}
                type="button"
                disabled={loading}
                onClick={() => signIn(account.email, DEMO_PASSWORD)}
                className="rounded-lg border border-line bg-surface px-3 py-2 text-left transition-colors hover:border-brand disabled:opacity-50"
              >
                <span className="block text-sm font-medium">{account.label}</span>
                <span className="block text-xs text-muted">{account.hint}</span>
              </button>
            ))}
          </div>
        </div>
      </div>
    </main>
  );
}
