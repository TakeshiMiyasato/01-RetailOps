// Browser-side API client. Every call goes through the Next.js BFF (/api/backend/*),
// which attaches the JWT from an httpOnly cookie — the token never reaches client JS.

export class ApiError extends Error {
  constructor(
    public status: number,
    message: string,
    public errors?: Record<string, string[]>,
  ) {
    super(message);
  }
}

interface ProblemDetails {
  title?: string;
  detail?: string;
  errors?: Record<string, string[]>;
}

export async function api<T>(path: string, init?: RequestInit & { json?: unknown }): Promise<T> {
  const { json, ...rest } = init ?? {};
  const response = await fetch(`/api/backend${path}`, {
    ...rest,
    headers: { ...(json !== undefined ? { "Content-Type": "application/json" } : {}), ...rest.headers },
    body: json !== undefined ? JSON.stringify(json) : rest.body,
  });

  if (response.status === 401) {
    // Outside React (no router here): a hard navigation also drops any in-memory state of the expired session.
    // eslint-disable-next-line @next/next/no-location-assign-relative-destination
    window.location.href = "/login";
    throw new ApiError(401, "Sesión expirada");
  }

  if (!response.ok) {
    const problem: ProblemDetails = await response.json().catch(() => ({}));
    const fieldErrors = problem.errors ? Object.values(problem.errors).flat().join(" ") : "";
    throw new ApiError(response.status, fieldErrors || problem.detail || problem.title || "Error inesperado", problem.errors);
  }

  return response.status === 204 ? (undefined as T) : response.json();
}

export function qs(params: Record<string, string | number | boolean | null | undefined>) {
  const search = new URLSearchParams();
  for (const [key, value] of Object.entries(params)) {
    if (value !== undefined && value !== null && value !== "") search.set(key, String(value));
  }
  const s = search.toString();
  return s ? `?${s}` : "";
}
