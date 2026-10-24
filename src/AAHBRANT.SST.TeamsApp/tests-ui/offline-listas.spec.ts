import { test, expect, type Page } from '@playwright/test';

async function preparar(page: Page) {
  let semRede = false;
  await page.route('**/api/**', async (route) => {
    if (semRede) return route.abort('internetdisconnected');
    const path = new URL(route.request().url()).pathname;
    if (path === '/api/usuarios/eu') return route.fulfill({ json: { nome: 'Técnico', ehAdministrador: false } });
    if (path === '/api/novidades/pendente') return route.fulfill({ status: 204 });
    if (path === '/api/ddssemanal') return route.fulfill({ json: [{
      id: 'semana-a', numeroDocumento: 'DDS-TESTE-001', obraId: 'obra-a', obraNome: 'Obra de teste',
      tipo: 1, dataInicioSemana: '2026-10-19', dataFimSemana: '2026-10-23',
      responsavelUsuarioNome: 'Técnico', totalDiasConcluidos: 0, totalDiasRegistrados: 1, status: 1,
    }] });
    if (path === '/api/inspecoes') return route.fulfill({ json: [{
      id: 'inspecao-a', numeroDocumento: 'INS-TESTE-001', obraNome: 'Obra de teste',
      checklistModeloNome: 'Inspeção de teste', checklistModeloVersao: 1, tipoInspecao: 1, data: '2026-10-24',
      itensRespondidos: 0, totalItens: 2, itensNaoConformes: 0,
      responsavelUsuarioNome: 'Técnico', status: 1,
    }] });
    if (path === '/api/obras') return route.fulfill({ json: [{ id: 'obra-a', nome: 'Obra de teste' }] });
    return route.fulfill({ json: [] });
  });
  return { desligarRede: () => { semRede = true; } };
}

async function limparCache(page: Page, tudo = false) {
  await page.evaluate(async (limparTudo) => {
    await new Promise<void>((resolve, reject) => {
      const abertura = indexedDB.open('aahbrant-sst-offline');
      abertura.onerror = () => reject(abertura.error);
      abertura.onsuccess = () => {
        const db = abertura.result;
        const tx = db.transaction('cacheJson', 'readwrite');
        const store = tx.objectStore('cacheJson');
        if (limparTudo) store.clear();
        else for (const path of ['/api/obras', '/api/atividades', '/api/usuarios', '/api/checklistmodelos']) store.delete(path);
        tx.oncomplete = () => { db.close(); resolve(); };
        tx.onerror = () => { db.close(); reject(tx.error); };
      };
    });
  }, tudo);
}

test('DDS: opções de obra consultadas online continuam disponíveis sem rede', async ({ page }) => {
  const rede = await preparar(page);
  await page.goto('/#/operacao?secao=dds');
  await expect(page.getByRole('table', { name: 'Semanas de DDS registradas' }).getByText('DDS-TESTE-001')).toBeVisible();
  rede.desligarRede();
  await page.reload();
  await expect(page.getByRole('table', { name: 'Semanas de DDS registradas' }).getByText('DDS-TESTE-001')).toBeVisible();
  await page.getByRole('button', { name: 'Nova semana', exact: true }).click();
  await expect(page.getByRole('combobox', { name: 'Obra', exact: true }).getByRole('option', { name: 'Obra de teste' })).toHaveCount(1);
  await expect(page.getByText('A lista de obras não está disponível neste dispositivo.', { exact: false })).toHaveCount(0);
});

for (const caso of [
  { secao: 'dds', tabela: 'Semanas de DDS registradas', registro: 'DDS-TESTE-001', vazio: 'Nenhuma semana de DDS registrada ainda.' },
  { secao: 'inspecoes', tabela: 'Inspeções', registro: 'Inspeção de teste (v1)', vazio: 'Nenhuma inspeção iniciada ainda.' },
]) {
  test(`${caso.secao}: mantém registros salvos mesmo sem cache dos dados de apoio`, async ({ page }) => {
    const rede = await preparar(page);
    await page.goto(`/#/operacao?secao=${caso.secao}`);
    await expect(page.getByRole('table', { name: caso.tabela }).getByText(caso.registro)).toBeVisible();
    await limparCache(page);
    rede.desligarRede();
    await page.reload();
    await expect(page.getByRole('table', { name: caso.tabela }).getByText(caso.registro)).toBeVisible();
    await expect(page.getByText(caso.vazio, { exact: true })).toHaveCount(0);
  });

  test(`${caso.secao}: falha de leitura não afirma que não existem registros`, async ({ page }) => {
    const rede = await preparar(page);
    await page.goto(`/#/operacao?secao=${caso.secao}`);
    await expect(page.getByRole('table', { name: caso.tabela }).getByText(caso.registro)).toBeVisible();
    await limparCache(page, true);
    rede.desligarRede();
    await page.reload();
    await expect(page.getByText('Lista indisponível neste dispositivo.', { exact: true })).toBeVisible();
    await expect(page.getByText(caso.vazio, { exact: true })).toHaveCount(0);
  });
}
