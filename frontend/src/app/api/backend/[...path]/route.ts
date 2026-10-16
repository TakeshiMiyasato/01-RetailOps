import { cookies } from "next/headers";
import { NextRequest, NextResponse } from "next/server";
import { API_URL, TOKEN_COOKIE } from "@/lib/server/backend";

/**
 * Backend-for-Frontend proxy: forwards /api/backend/<path> to the .NET API with the bearer token
 * taken from the httpOnly cookie. Keeps the JWT out of client JavaScript (XSS can't steal it)
 * and avoids CORS entirely because the browser only ever talks to this origin.
 */
async function forward(request: NextRequest, ctx: RouteContext<"/api/backend/[...path]">) {
  const { path } = await ctx.params;
  const token = (await cookies()).get(TOKEN_COOKIE)?.value;
  if (!token) return NextResponse.json({ title: "Unauthorized" }, { status: 401 });

  const target = `${API_URL}/api/${path.map(encodeURIComponent).join("/")}${request.nextUrl.search}`;
  const hasBody = request.method !== "GET" && request.method !== "HEAD";

  const upstream = await fetch(target, {
    method: request.method,
    headers: {
      Authorization: `Bearer ${token}`,
      Accept: "application/json",
      ...(hasBody ? { "Content-Type": request.headers.get("content-type") ?? "application/json" } : {}),
    },
    body: hasBody ? await request.text() : undefined,
    cache: "no-store",
  });

  const response = new NextResponse(upstream.status === 204 ? null : await upstream.arrayBuffer(), {
    status: upstream.status,
    headers: { "Content-Type": upstream.headers.get("content-type") ?? "application/json" },
  });
  if (upstream.status === 401) response.cookies.delete(TOKEN_COOKIE);
  return response;
}

export { forward as GET, forward as POST, forward as PUT, forward as DELETE, forward as PATCH };
