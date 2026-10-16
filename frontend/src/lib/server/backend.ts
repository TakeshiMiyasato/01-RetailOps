import "server-only";

export const API_URL = process.env.API_URL ?? "http://localhost:5080";
export const TOKEN_COOKIE = "ro_token";

export const tokenCookieOptions = (expiresAt: string) => ({
  httpOnly: true,
  sameSite: "lax" as const,
  secure: process.env.NODE_ENV === "production" && process.env.INSECURE_COOKIES !== "true",
  path: "/",
  expires: new Date(expiresAt),
});
