import { NextRequest, NextResponse } from "next/server";

const TOKEN_COOKIE = "ro_token";

/**
 * Optimistic route guard: only checks that a session cookie exists. Real authorization
 * (roles, store scope, tenant) is enforced by the API on every request.
 */
export function proxy(request: NextRequest) {
  const hasSession = request.cookies.has(TOKEN_COOKIE);
  const isLogin = request.nextUrl.pathname === "/login";

  if (!hasSession && !isLogin) return NextResponse.redirect(new URL("/login", request.url));
  if (hasSession && isLogin) return NextResponse.redirect(new URL("/dashboard", request.url));
  return NextResponse.next();
}

export const config = {
  matcher: ["/((?!api|_next/static|_next/image|favicon.ico|.*\\..*).*)"],
};
