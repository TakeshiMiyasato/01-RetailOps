import { defineConfig, devices } from "@playwright/test";

/**
 * E2E tests drive the real stack: Next.js (started here) → BFF → .NET API → SQL Server.
 * Start the API first (`docker compose up -d sqlserver api` or `dotnet run`), or point API_URL elsewhere.
 */
export default defineConfig({
  testDir: "./e2e",
  fullyParallel: false,
  retries: process.env.CI ? 1 : 0,
  reporter: process.env.CI ? "github" : "list",
  use: {
    baseURL: process.env.E2E_BASE_URL ?? "http://localhost:3000",
    trace: "retain-on-failure",
    locale: "es-BO",
    timezoneId: "America/La_Paz",
  },
  projects: [
    { name: "desktop", use: { ...devices["Desktop Chrome"] }, grepInvert: /@screenshots/ },
    { name: "screenshots", use: { ...devices["Desktop Chrome"], viewport: { width: 1440, height: 900 } }, grep: /@screenshots/ },
  ],
  webServer: process.env.E2E_BASE_URL
    ? undefined
    : { command: "npm run dev", url: "http://localhost:3000/login", reuseExistingServer: true, timeout: 120_000 },
});
