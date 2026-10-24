import { test, expect } from '@playwright/test';

test('instala a PWA e mantém o menu interativo após reabrir sem rede', async ({ page, context }) => {
  await page.goto('/');
  // ready nunca resolve se algum import do SW estiver ausente ou for servido como HTML.
  await expect.poll(() => page.evaluate(async () => {
    const registration = await navigator.serviceWorker.getRegistration();
    return registration?.active?.state;
  })).toBe('activated');
  await expect.poll(() => page.evaluate(() => Boolean(navigator.serviceWorker.controller))).toBe(true);

  // Outra página no mesmo contexto simula fechar e abrir pelo ícone, sem usar o JS em memória.
  await context.setOffline(true);
  const reaberta = await context.newPage();
  await page.close();
  await reaberta.goto('/');
  await expect(reaberta.getByRole('button', { name: 'Expandir menu lateral', exact: true })).toBeVisible();
  await reaberta.getByRole('button', { name: 'Expandir menu lateral', exact: true }).tap();
  await expect(reaberta.getByRole('button', { name: 'Recolher menu lateral', exact: true })).toBeVisible();
  await reaberta.getByRole('link', { name: 'Operação', exact: true }).tap();
  await expect(reaberta).toHaveURL(/#\/operacao/);
  await expect(reaberta.getByRole('link', { name: 'Operação', exact: true })).toHaveAttribute('aria-current', 'page');
});
