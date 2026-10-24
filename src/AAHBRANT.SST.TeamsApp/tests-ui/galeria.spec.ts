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
    expect(total).toBe(22);
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

  test(`painel de criação inline aberto em tema ${tema}`, async ({ page }) => {
    await page.addInitScript((t) => localStorage.setItem('sst.modoTema', t), tema);
    await page.goto('/#/ui-galeria?abrir=painel-criacao-inline');
    await page.evaluate(async () => { await document.fonts.ready; });
    const secao = page.locator('section[data-secao="painel-criacao-inline"]');
    await secao.scrollIntoViewIfNeeded();
    await expect(secao).toHaveScreenshot(`${tema}-painel-criacao-inline-aberto.png`);
  });

  test(`confirm dialog aberto em tema ${tema}`, async ({ page }) => {
    await page.addInitScript((t) => localStorage.setItem('sst.modoTema', t), tema);
    await page.goto('/#/ui-galeria?abrir=dialogo');
    await page.evaluate(async () => { await document.fonts.ready; });
    const dialogo = page.locator('[role="alertdialog"], [role="dialog"]').first();
    await expect(dialogo).toBeVisible();
    await expect(dialogo).toHaveScreenshot(`${tema}-confirm-dialog-aberto.png`);
  });

  // A seção fechada só mostra o chevron; a linha expansível com 2 filhos empilhados (título + chips)
  // só entra em algum snapshot com ?abrir=expandida (mesma razão do painel/diálogo acima). Alvo é o
  // card isolado (data-testid), não a <Secao> inteira: ela é mais alta que a viewport de teste
  // (900px) e mistura outros dois exemplos de DataTable, um deles com cabeçalho sticky — capturar a
  // seção inteira dispara um bug conhecido do Playwright de stitching de elemento alto com posição
  // sticky (o header fixo da app aparece "vazando" no meio da imagem).
  test(`data-table expandida em tema ${tema}`, async ({ page }) => {
    await page.addInitScript((t) => localStorage.setItem('sst.modoTema', t), tema);
    await page.goto('/#/ui-galeria?abrir=expandida');
    await page.waitForSelector('main section[data-secao]');
    await page.evaluate(async () => { await document.fonts.ready; });
    const card = page.locator('[data-testid="data-table-expansivel"]');
    await card.scrollIntoViewIfNeeded();
    await expect(card).toHaveScreenshot(`${tema}-data-table-expandida.png`);
  });
}
