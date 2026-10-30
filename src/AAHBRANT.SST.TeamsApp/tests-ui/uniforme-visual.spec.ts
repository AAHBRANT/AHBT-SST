import { test, expect, type Page } from '@playwright/test';

async function preparar(page: Page) {
  const catalogo = [
    { id: 'camisa', nome: 'CAMISA BRIM MANGA LONGA', temFoto: true, referenciaVisual: 'camisa-brim-laranja', funcoesAtribuidas: ['Carpinteiro'] },
    { id: 'calca', nome: 'CALÇA BRIM', temFoto: true, referenciaVisual: 'calca-brim-laranja', funcoesAtribuidas: ['Carpinteiro'] },
  ];
  const envios: { dados: Record<string, unknown>; chave: string | undefined }[] = [];
  let falhar = false;
  let falharEstoque = false;
  let falharTamanhos = false;
  await page.route('**/api/**', async route => {
    const url = new URL(route.request().url()), path = url.pathname;
    if (path === '/api/usuarios/eu') return route.fulfill({ json: { nome: 'Responsável', ehAdministrador: true } });
    if (path === '/api/novidades/pendente') return route.fulfill({ status: 204 });
    if (path === '/api/catalogosuniforme' || path === '/api/funcoes/f1/uniformes') return route.fulfill({ json: catalogo });
    if (path === '/api/trabalhadores/t1/uniformes' && falharTamanhos) return route.fulfill({ status: 503, json: { erro: 'Perfil indisponível' } });
    if (path === '/api/trabalhadores') return route.fulfill({ json: [
      { id: 't1', nome: 'Pessoa de teste', funcaoId: 'f1', obraId: 'o1', matricula: '01' },
      { id: 't2', nome: 'Outra pessoa', funcaoId: 'f2', obraId: 'o2', matricula: '02' },
    ] });
    if (path === '/api/obras') return route.fulfill({ json: [{ id: 'o1', nome: 'Obra teste' }, { id: 'o2', nome: 'Outra obra' }] });
    if (path.startsWith('/api/estoquesuniforme/obra/')) return falharEstoque ? route.fulfill({ status: 503, json: { erro: 'Consulta de saldo indisponível' } }) : route.fulfill({ json: path.endsWith('o1') ? [
      { catalogoUniformeId: 'camisa', tamanho: 'M', saldo: 12 }, { catalogoUniformeId: 'camisa', tamanho: 'G', saldo: 2 },
      { catalogoUniformeId: 'calca', tamanho: '42', saldo: 8 }, { catalogoUniformeId: 'calca', tamanho: '46', saldo: 3 },
    ] : [] });
    if (path === '/api/entregasuniforme/lote') {
      envios.push({ dados: route.request().postDataJSON(), chave: route.request().headers()['idempotency-key'] });
      return falhar ? route.fulfill({ status: 503, json: { erro: 'Falha temporária no kit' } }) : route.fulfill({ json: { ids: ['nova-camisa', 'nova-calca'] } });
    }
    if (path === '/api/entregasuniforme') return route.fulfill({ json: [{ id: 'legada', trabalhadorId: 't1', catalogoUniformeId: 'camisa', tamanho: 'P', quantidade: 1, dataEntrega: '2026-09-01', motivoTipo: 0 }] });
    if (path === '/api/documentos') return route.fulfill({ json: route.request().method() === 'POST' ? { id: 'doc' } : { id: 'doc', signatarios: [] } });
    return route.fulfill({ json: [] });
  });
  await page.goto('/#/operacao?secao=uniforme&aba=entrega');
  return { envios, falhar: (v: boolean) => { falhar = v; }, falharEstoque: () => { falharEstoque = true; }, falharTamanhos: () => { falharTamanhos = true; } };
}
async function escolher(page: Page, nome = 'Pessoa de teste') {
  await page.getByRole('combobox', { name: 'Funcionário', exact: true }).click();
  await page.getByRole('option', { name: new RegExp(nome) }).click();
}

test('mantém entregas legadas e mostra saldo real por tamanho, sem atribuir funções pela cor', async ({ page }) => {
  await preparar(page);
  await expect(page.getByRole('table', { name: 'Entregas de uniforme registradas' }).getByText('P', { exact: true })).toBeVisible();
  await escolher(page);
  const camisa = page.locator('.uniforme-peca').filter({ has: page.getByText('CAMISA BRIM MANGA LONGA', { exact: true }) });
  await expect(camisa.getByText('12 unidade(s) disponíveis')).toBeVisible();
  await camisa.getByRole('combobox').selectOption('G');
  await expect(camisa.getByText('2 unidade(s) disponíveis')).toBeVisible();
  await camisa.getByRole('spinbutton').fill('3');
  await expect(camisa.getByRole('button', { name: 'Adicionar', exact: true })).toBeDisabled();
  await camisa.getByRole('combobox').selectOption('GG');
  await expect(camisa.getByText('Indisponível neste tamanho')).toBeVisible();
  await expect(camisa.getByText('Carpinteiro', { exact: true })).toBeVisible();
  await expect(page.locator('body')).not.toContainText('${');
  await page.screenshot({ path: '.codex-artifacts/uniforme-integrado-desktop.png', fullPage: true });
  await page.setViewportSize({ width: 768, height: 1024 });
  // Confere a região alterada; sem incluir a navegação global, que não foi alterada.
  await expect.poll(() => page.locator('.uniforme-entrega-layout').evaluate(el => el.scrollWidth <= el.clientWidth && el.getBoundingClientRect().right <= innerWidth)).toBe(true);
  await page.locator('.uniforme-entrega-layout').screenshot({ path: '.codex-artifacts/uniforme-integrado-tablet.png' });
});

test('envia kit inteiro com tamanhos escolhidos, reutiliza chave após falha e abre assinatura existente', async ({ page }) => {
  const app = await preparar(page); await escolher(page);
  const camisa = page.locator('.uniforme-peca').first(), calca = page.locator('.uniforme-peca').nth(1);
  await camisa.getByRole('combobox').selectOption('G'); await camisa.getByRole('button', { name: 'Adicionar', exact: true }).click();
  await calca.getByRole('combobox').selectOption('46'); await calca.getByRole('button', { name: 'Adicionar', exact: true }).click();
  app.falhar(true); await page.getByRole('button', { name: 'Confirmar e assinar entrega' }).click();
  await expect(page.getByText('Falha temporária no kit', { exact: false })).toBeVisible();
  expect(app.envios).toHaveLength(1);
  app.falhar(false); await page.getByRole('button', { name: 'Confirmar e assinar entrega' }).click();
  await expect(page.getByRole('dialog').getByText('Assinatura da entrega de uniforme')).toBeVisible();
  expect(app.envios).toHaveLength(2); expect(app.envios[0].chave).toBeTruthy();
  expect(app.envios[0].chave).toBe(app.envios[1].chave);
  expect(app.envios[1].dados.itens).toEqual([{ catalogoUniformeId: 'camisa', tamanho: 'G', quantidade: 1 }, { catalogoUniformeId: 'calca', tamanho: '46', quantidade: 1 }]);
});

test('falha ao consultar saldo bloqueia entrega e informa indisponibilidade', async ({ page }) => {
  const app = await preparar(page); await escolher(page); app.falharEstoque();
  await escolher(page, 'Outra pessoa');
  await expect(page.getByText('Consulta de saldo indisponível', { exact: false })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Confirmar e assinar entrega' })).toBeDisabled();
});

test('falha na sugestão de tamanho do perfil mantém seleção manual e matriz válidas', async ({ page }) => {
  const app = await preparar(page); app.falharTamanhos(); await escolher(page);
  const camisa = page.locator('.uniforme-peca').first();
  await expect(camisa.getByText('12 unidade(s) disponíveis')).toBeVisible();
  await expect(camisa.getByRole('button', { name: 'Adicionar', exact: true })).toBeEnabled();
});
