import { test, expect } from '@playwright/test';

for (const tema of ['light', 'dark'] as const) {
  test(`galeria em tema ${tema}`, async ({ page }) => {
    await page.addInitScript((t) => localStorage.setItem('sst.modoTema', t), tema);
    await page.goto('/#/ui-galeria');
    await page.waitForSelector('section');
    // Fecha painéis/diálogos que porventura estejam abertos e espera a fonte.
    await page.evaluate(() => document.fonts.ready);
    const secoes = page.locator('main section');
    const total = await secoes.count();
    expect(total).toBeGreaterThan(10);
    for (let i = 0; i < total; i++) {
      const s = secoes.nth(i);
      const titulo = (await s.locator('h2').innerText()).replace(/[^a-z0-9]+/gi, '-').toLowerCase();
      await s.scrollIntoViewIfNeeded();
      await expect(s).toHaveScreenshot(`${tema}-${String(i).padStart(2, '0')}-${titulo}.png`);
    }
  });
}
