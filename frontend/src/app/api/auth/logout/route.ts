import { cookies } from "next/headers";
import { NextResponse } from "next/server";
import { TOKEN_COOKIE } from "@/lib/server/backend";

export async function POST() {
  const cookieStore = await cookies();
  cookieStore.delete(TOKEN_COOKIE);
  return new NextResponse(null, { status: 204 });
}
