import { cookies } from "next/headers";
import { NextResponse } from "next/server";
import { API_URL, TOKEN_COOKIE, tokenCookieOptions } from "@/lib/server/backend";

export async function POST(request: Request) {
  const body = await request.text();
  const response = await fetch(`${API_URL}/api/auth/login`, {
    method: "POST",
    headers: { "Content-Type": "application/json", "X-Forwarded-For": request.headers.get("x-forwarded-for") ?? "" },
    body,
  });

  const payload = await response.json().catch(() => ({}));
  if (!response.ok) return NextResponse.json(payload, { status: response.status });

  // The access token stays server-side in an httpOnly cookie; the browser only receives the profile.
  const cookieStore = await cookies();
  cookieStore.set(TOKEN_COOKIE, payload.accessToken, tokenCookieOptions(payload.expiresAt));
  return NextResponse.json({ user: payload.user });
}
