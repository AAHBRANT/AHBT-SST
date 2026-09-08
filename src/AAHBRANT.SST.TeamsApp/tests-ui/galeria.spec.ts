import { test, expect } from '@playwright/test';

// Snapshots visuais da galeria da camada ui/ (spec §6), nos dois temas. O nome do PNG vem do
// data-secao de cada <Secao> — id estável em kebab-case —, não do índice nem do texto do <h2>:
// reordenar ou renomear uma seção não invalida mais os arquivos de referência das outras.
for (const tema of ['light', 'dark'] as const) {
  test(`galeria em tema ${tema}`, async ({ page }) => {
    await page.addInitScript((t) => localStorage.setItem('sst.modoTema', t), tema);
    await page.goto('/#/ui-galeria');
    await page.waitForSelector('main section[data-secao]');
    await page.evaluate(async () => { await document.fonts.ready; });
    const secoes = page.locator('main section[data-secao]');
    const total = await secoes.count();
    expect(total).toBe(19);
    for (let i = 0; i < total; i++) {
      const s = secoes.nth(i);
      const dataSecao = await s.getAttribute('data-secao');
      await s.scrollIntoViewIfNeeded();
      await expect(s).toHaveScreenshot(`${tema}-${dataSecao}.png`);
    }
  });

  // Peças que só existem abertas: ?abrir= na galeria monta o PainelLateral/ConfirmDialog já aberto,
  // porque um snapshot da seção fechada só mostraria o botão que os abre.
  test(`painel lateral aberto em tema ${tema}`, async ({ page }) => {
    await page.addInitScript((t) => localStorage.setItem('sst.modoTema', t), tema);
    await page.goto('/#/ui-galeria?abrir=painel');
    await page.evaluate(async () => { await document.fonts.ready; });
    const painel = page.locator('[role="dialog"]').first();
    await expect(painel).toBeVisible();
    await expect(painel).toHaveScreenshot(`${tema}-painel-lateral-aberto.png`);
  });

  test(`confirm dialog aberto em tema ${tema}`, async ({ page }) => {
    await page.addInitScript((t) => localStorage.setItem('sst.modoTema', t), tema);
    await page.goto('/#/ui-galeria?abrir=dialogo');
    await page.evaluate(async () => { await document.fonts.ready; });
    const dialogo = page.locator('[role="alertdialog"], [role="dialog"]').first();
    await expect(dialogo).toBeVisible();
    await expect(dialogo).toHaveScreenshot(`${tema}-confirm-dialog-aberto.png`);
  });
}
