import { expect, test } from "@playwright/test";

// Regenerates the README screenshots: `npx playwright test --project screenshots`
const out = (name: string) => `../docs/screenshots/${name}.png`;

test("capture README screenshots @screenshots", async ({ page }) => {
  await page.goto("/login");
  await page.screenshot({ path: out("login") });

  await page.getByRole("button", { name: /^Administrador/ }).click();
  await expect(page.getByText("Ingresos diarios")).toBeVisible({ timeout: 30_000 });
  await page.waitForTimeout(2000); // let the chart finish its entry animation
  await page.screenshot({ path: out("dashboard"), fullPage: true });

  await page.getByRole("link", { name: "Punto de venta" }).click();
  await page.getByLabel("Buscar producto").fill("leche");
  await page.getByRole("button", { name: /Leche entera/ }).click();
  await page.getByLabel("Buscar producto").fill("pan");
  await page.getByRole("button", { name: /Pan de molde/ }).click();
  await page.getByLabel("Buscar producto").fill("caf");
  await page.screenshot({ path: out("pos") });

  await page.getByRole("link", { name: "Inventario" }).click();
  await expect(page.getByRole("table")).toBeVisible();
  await page.screenshot({ path: out("inventory") });

  await page.getByRole("link", { name: "Transferencias" }).click();
  await expect(page.getByText(/T-/).first()).toBeVisible();
  await page.screenshot({ path: out("transfers") });

  await page.emulateMedia({ colorScheme: "dark" });
  await page.getByRole("link", { name: "Dashboard" }).click();
  await expect(page.getByText("Ingresos diarios")).toBeVisible();
  await page.waitForTimeout(2000);
  await page.screenshot({ path: out("dashboard-dark") });
});
