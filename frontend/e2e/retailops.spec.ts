import { expect, test, type Page } from "@playwright/test";

async function loginAs(page: Page, account: "Administrador" | "Gerente" | "Cajero" | "Otro tenant") {
  await page.goto("/login");
  await page.getByRole("button", { name: new RegExp(`^${account}`) }).click();
  await expect(page.getByRole("heading", { name: "Dashboard" })).toBeVisible({ timeout: 30_000 });
}

test("unauthenticated users are sent to the login page", async ({ page }) => {
  await page.goto("/inventory");
  await expect(page).toHaveURL(/\/login$/);
});

test("wrong credentials show an error and keep the user on login", async ({ page }) => {
  await page.goto("/login");
  await page.getByLabel("Correo").fill("admin@andesretail.demo");
  await page.getByLabel("Contraseña").fill("incorrecta");
  await page.getByRole("button", { name: "Ingresar" }).click();
  await expect(page.getByText("Correo o contraseña incorrectos.")).toBeVisible();
  await expect(page).toHaveURL(/\/login$/);
});

test("cashier sells a product from the POS and stock goes down", async ({ page }) => {
  await loginAs(page, "Cajero");
  await expect(page.getByRole("link", { name: "Transferencias" })).toHaveCount(0);

  await page.getByRole("link", { name: "Punto de venta" }).click();
  await page.getByLabel("Buscar producto").fill("Arroz");
  const result = page.getByRole("button", { name: /Arroz grano largo/ });
  const before = Number((await result.innerText()).match(/(\d[\d.]*) disponibles/)![1].replace(/\./g, ""));
  await result.click();

  await page.getByRole("radio", { name: "QR" }).click();
  await page.getByRole("button", { name: /^Cobrar/ }).click();

  const receipt = page.getByRole("dialog", { name: "Venta registrada" });
  await expect(receipt).toContainText("1 × Arroz grano largo 1 kg");
  await receipt.getByRole("button", { name: "Nueva venta" }).click();

  await page.getByLabel("Buscar producto").fill("Arroz");
  await expect(page.getByRole("button", { name: /Arroz grano largo/ })).toContainText(`${before - 1} disponibles`);
});

test("manager sees low stock and can open transfers", async ({ page }) => {
  await loginAs(page, "Gerente");
  await page.getByRole("link", { name: "Inventario" }).click();
  await page.getByLabel("Solo stock bajo").check();
  await expect(page.getByText(/Agotado|Bajo/).first()).toBeVisible();

  await page.getByRole("link", { name: "Transferencias" }).click();
  await expect(page.getByRole("heading", { name: "Transferencias" })).toBeVisible();
});

test("a second tenant only sees its own store", async ({ page }) => {
  await loginAs(page, "Otro tenant");
  await page.getByRole("link", { name: "Inventario" }).click();
  // Role queries skip hidden nodes: with Cache Components the previous route stays mounted inside a hidden <Activity>.
  const picker = page.getByRole("combobox", { name: "Sucursal" });
  await expect(picker.locator("option")).toHaveText(["Oruro Plaza"]);
});
