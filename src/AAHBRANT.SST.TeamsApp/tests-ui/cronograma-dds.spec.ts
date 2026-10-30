import { test, expect, type APIRequestContext, type Page } from '@playwright/test';
import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';

const api = process.env.SST_TEST_API ?? 'http://localhost:5198';
if (!['localhost', '127.0.0.1'].includes(new URL(api).hostname)) throw new Error('Use uma API local com banco isolado para testar o cronograma.');
const rota = '/#/operacao?secao=dds&aba=cronograma-mensal';
async function preparar(page: Page, request: APIRequestContext) {
  const obraResponse = await request.post(api + '/api/obras', { multipart: {
    Codigo: 'CRON-' + Date.now(), Nome: 'Obra de teste do cronograma', Cliente: 'Fiscalização de demonstração', Status: '2',
    Logo: { name: 'logo.png', mimeType: 'image/png', buffer: readFileSync(resolve('public/marca/aahbrant-positiva.png')) },
  } });
  expect(obraResponse.ok(), await obraResponse.text()).toBeTruthy();
  const obra = (await obraResponse.json()).id as string;
  const temas: { id: string; nome: string }[] = [];
  for (const nome of ['Cuidados com as mãos', 'Organização e limpeza']) {
    const nomeUnico = nome + ' ' + Date.now();
    const response = await request.post(api + '/api/catalogotemasdds', { data: { nome: nomeUnico, descricao: 'Tema cadastrado para o ensaio do calendário.' } });
    expect(response.ok()).toBeTruthy();
    temas.push({ id: (await response.json()).id, nome: nomeUnico });
  }
  await page.clock.setFixedTime(new Date('2026-10-29T14:00:00Z'));
  await page.addInitScript(id => {
    localStorage.setItem('sst.obraAtiva', JSON.stringify({ id, nome: 'Obra de teste do cronograma' }));
    localStorage.setItem('sst.modoTema', 'light');
  }, obra);
  await page.goto(rota);
  await expect(page.getByRole('button', { name: 'Emitir PDF', exact: true })).toBeVisible();
  await expect(page.locator('[data-dia="2026-10-01"]')).toBeEnabled();
  return { obra, temas };
}

test('seleciona temas, salva no SQL, reabre, cria tema e emite PDF com revisão', async ({ page, request }, info) => {
  const { obra, temas } = await preparar(page, request);
  const ddsAntes = await (await request.get(api + '/api/dds')).json();
  await page.locator('[data-dia="2026-10-01"]').click();
  for (const tema of temas) await page.getByRole('checkbox', { name: tema.nome, exact: true }).check();
  await page.getByRole('button', { name: 'Salvar dia', exact: true }).click();
  await expect(page.getByRole('dialog')).toHaveCount(0);
  await page.reload();
  await expect(page.locator('[data-dia="2026-10-01"]')).toContainText(temas[0].nome);
  await page.locator('[data-dia="2026-10-03"]').click();
  await page.getByRole('button', { name: 'Cadastrar novo tema', exact: true }).click();
  const novo = 'Tema novo pelo calendário ' + Date.now();
  await page.getByRole('textbox', { name: 'Nome do novo tema', exact: true }).fill(novo);
  await page.getByRole('button', { name: 'Criar tema e selecionar', exact: true }).click();
  await expect(page.getByRole('checkbox', { name: novo, exact: true })).toBeChecked();
  await page.getByRole('button', { name: 'Salvar dia', exact: true }).click();
  await expect(page.getByRole('checkbox', { name: 'Incluir sábado e domingo no documento' })).toBeChecked();
  await page.getByRole('button', { name: 'Visualizar documento', exact: true }).click();
  await expect(page.getByRole('dialog').locator('canvas').first()).toBeVisible();
  await page.screenshot({ path: info.outputPath('cronograma-previa.png'), fullPage: true });
  await page.getByRole('dialog').getByRole('button', { name: 'Fechar', exact: true }).last().click();
  const download = page.waitForEvent('download');
  await page.getByRole('button', { name: 'Emitir PDF', exact: true }).click();
  const arquivo = await download;
  await arquivo.saveAs(info.outputPath('cronograma-emitido.pdf'));
  await expect(page.getByRole('button', { name: /Revisão 01/ })).toBeVisible();
  const salvo = await (await request.get(api + `/api/cronogramasmensaisdds/${obra}/2026/10`)).json();
  expect(salvo.dias).toHaveLength(2);
  expect(salvo.dias[0].temas).toHaveLength(2);
  const copia = await request.get(api + `/api/cronogramasmensaisdds/${salvo.id}/revisoes/${salvo.revisoes[0].id}/pdf`);
  expect(await copia.body()).toEqual(readFileSync(info.outputPath('cronograma-emitido.pdf')));
  expect(await (await request.get(api + '/api/dds')).json()).toEqual(ddsAntes);
  await page.screenshot({ path: info.outputPath('cronograma-desktop.png'), fullPage: true });
  await page.getByRole('button', { name: 'Próximo mês', exact: true }).click();
  await expect(page.getByText('Mês ainda sem programação')).toBeVisible();
  await page.getByRole('button', { name: 'Mês anterior', exact: true }).click();
  await expect(page.locator('[data-dia="2026-10-01"]')).toContainText(temas[0].nome);
  await page.setViewportSize({ width: 390, height: 844 });
  await page.screenshot({ path: info.outputPath('cronograma-mobile.png'), fullPage: true });
});

test('conflito mantém a seleção na tela e não sobrescreve o outro usuário', async ({ page, request }) => {
  const { obra, temas } = await preparar(page, request);
  await page.locator('[data-dia="2026-10-01"]').click();
  await page.getByRole('checkbox', { name: temas[0].nome, exact: true }).check();
  const concorrente = await request.put(api + `/api/cronogramasmensaisdds/${obra}/2026/10`, { data: { versao: 0, dias: [{ data: '2026-10-02', temasIds: [temas[1].id] }] } });
  expect(concorrente.ok()).toBeTruthy();
  await page.getByRole('button', { name: 'Salvar dia', exact: true }).click();
  await expect(page.getByText(/Este cronograma foi alterado por outra pessoa/)).toBeVisible();
  await expect(page.getByRole('checkbox', { name: temas[0].nome, exact: true })).toBeChecked();
  const salvo = await (await request.get(api + `/api/cronogramasmensaisdds/${obra}/2026/10`)).json();
  expect(salvo.dias.map((d: { data: string }) => d.data)).toEqual(['2026-10-02']);
});

test('calendário mantém dia e evento noturno em São Paulo quando o Graph falha', async ({ page }) => {
  await page.clock.setFixedTime(new Date('2026-10-29T14:00:00Z'));
  await page.route('**/api/calendario?**', route => route.fulfill({ json: {
    usuarioIdentificado: true, graphDisponivel: false, mensagemErroGraph: 'O Exchange não autorizou o acesso do SST ao calendário desta conta.',
    eventosGraph: [], eventosSst: [{ alertaId: 'aviso', titulo: 'Vencimento às 23h30', data: '2026-10-29T23:30:00-03:00', severidade: 2, tipo: 1, status: 1 }],
  } }));
  await page.goto('/#/calendario');
  await expect(page.getByText('quinta-feira, 29 de outubro', { exact: true })).toBeVisible();
  await expect(page.locator('[data-dia="2026-10-29"]')).toContainText('Vencimento às 23h30');
  await expect(page.locator('[data-dia="2026-10-30"]')).not.toContainText('Vencimento às 23h30');
  await expect(page.getByText(/O Exchange não autorizou/)).toBeVisible();
  await page.locator('[data-dia="2026-10-01"]').focus();
  await page.keyboard.press('Enter');
  await expect(page.getByText('quinta-feira, 01 de outubro', { exact: true })).toBeVisible();
});
