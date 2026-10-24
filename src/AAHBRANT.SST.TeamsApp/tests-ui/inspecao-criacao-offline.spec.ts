import { test, expect, type Page } from '@playwright/test';
type RegistroSimulado = {
  inspecao: { id: string; status: number; itensRespondidos: number; numeroDocumento: string; [campo: string]: unknown };
  respostas: { id: string; statusItem?: number | null; observacao?: string; temFoto: boolean; versaoResposta?: string;
    dadosFoto?: unknown; [campo: string]: unknown }[];
};

const obraId = '11111111-1111-4111-8111-111111111111';
const usuarioId = '22222222-2222-4222-8222-222222222222';
const modeloId = '33333333-3333-4333-8333-333333333333';
const itemId = '44444444-4444-4444-8444-444444444444';
const png = 'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+a1u8AAAAASUVORK5CYII=';

async function preparar(page: Page, comPreparacao = true) {
  let offline = !comPreparacao;
  let falharCriacao = false;
  let perderResposta = false;
  let criadas = 0;
  const registros = new Map<string, RegistroSimulado>();
  const retornos = new Map<string, unknown>();
  const envios: { path: string; chave: string }[] = [];
  const modelo = { id: modeloId, nome: 'Modelo offline', tipoInspecao: 1, versao: 1, quantidadeItens: 1 };
  const item = { id: itemId, checklistModeloId: modeloId, ordem: 1, descricao: 'Proteção coletiva', exigeFotografia: true, exigeResponsavel: false, exigePrazo: false };
  await page.addInitScript((semRede) => {
    if (semRede) localStorage.setItem('teste.offline', '1');
    Object.defineProperty(navigator, 'onLine', { configurable: true, get: () => localStorage.getItem('teste.offline') !== '1' });
  }, !comPreparacao);
  await page.route('**/api/**', async route => {
    if (offline) return route.abort('internetdisconnected');
    const path = new URL(route.request().url()).pathname;
    const method = route.request().method();
    if (path === '/api/usuarios/eu') return route.fulfill({ json: { nome: 'Técnico', ehAdministrador: false } });
    if (path === '/api/novidades/pendente') return route.fulfill({ status: 204 });
    if (method === 'GET') {
      if (path === '/api/checklistmodelos') return route.fulfill({ json: [modelo] });
      if (path === `/api/checklistmodelos/${modeloId}`) return route.fulfill({ json: { checklistModelo: modelo, itens: [item] } });
      if (path === '/api/obras') return route.fulfill({ json: [{ id: obraId, nome: 'Obra teste' }] });
      if (path === '/api/usuarios') return route.fulfill({ json: [{ id: usuarioId, nome: 'Técnico' }] });
      if (path === '/api/inspecoes') return route.fulfill({ json: [...registros.values()].map(r => r.inspecao) });
      const id = /^\/api\/inspecoes\/([^/]+)$/.exec(path)?.[1];
      if (id) return route.fulfill({ status: registros.has(id) ? 200 : 404, json: registros.get(id) ?? { erro: 'Ainda não criada no servidor' } });
      if (path.endsWith('/foto')) return route.fulfill({ contentType: 'image/png', body: Buffer.from(png, 'base64') });
      return route.fulfill({ json: [] });
    }
    if (method === 'POST' && path.startsWith('/api/inspecoes')) {
      const chave = route.request().headers()['idempotency-key'];
      envios.push({ path, chave });
      if (retornos.has(chave)) return route.fulfill({ json: retornos.get(chave) });
      if (path === '/api/inspecoes') {
        if (falharCriacao) return route.fulfill({ status: 503, json: { erro: 'Temporário' } });
        const body = route.request().postDataJSON();
        const id = body.idCliente;
        const respostaId = body.respostasCliente[0].id;
        const detalhe: RegistroSimulado = { inspecao: { id, obraId, obraNome: 'Obra teste', tipoInspecao: 1, checklistModeloId: modeloId,
          checklistModeloNome: modelo.nome, checklistModeloVersao: 1, data: body.data, responsavelUsuarioId: usuarioId,
          responsavelUsuarioNome: 'Técnico', numeroDocumento: `INSP-TESTE-${++criadas}`, status: 1, totalItens: 1, itensRespondidos: 0, itensNaoConformes: 0 },
          respostas: [{ ...item, id: respostaId, inspecaoId: id, checklistModeloItemId: itemId, temFoto: false, temFotoDepois: false, versaoResposta: 'v1' }] };
        registros.set(id, detalhe);
        const retorno = { id, numeroDocumento: detalhe.inspecao.numeroDocumento, versoesRespostas: { [respostaId]: 'v1' } };
        retornos.set(chave, retorno);
        if (perderResposta) { perderResposta = false; return route.abort('internetdisconnected'); }
        return route.fulfill({ status: 201, json: retorno });
      }
      const encerramento = /^\/api\/inspecoes\/([^/]+)\/encerrar$/.exec(path)?.[1];
      if (encerramento) {
        const detalhe = registros.get(encerramento)!;
        if (!detalhe.respostas[0].statusItem || !detalhe.respostas[0].temFoto) return route.fulfill({ status: 400, json: { erro: 'Checklist incompleto' } });
        detalhe.inspecao.status = 2;
        return route.fulfill({ status: 204 });
      }
      const respostaId = /^\/api\/inspecoes\/respostas\/([^/]+)/.exec(path)?.[1];
      const detalhe = [...registros.values()].find(d => d.respostas.some(r => r.id === respostaId));
      if (!detalhe) return route.fulfill({ status: 404, json: { erro: 'Criação ainda pendente' } });
      const resposta = detalhe.respostas[0];
      const corpo = route.request().postData() ?? '';
      const body = path.endsWith('/foto') ? undefined : route.request().postDataJSON();
      const versao = body?.versaoEsperada ?? /name="versaoEsperada"\r\n\r\n([^\r]+)/.exec(corpo)?.[1];
      if (versao !== resposta.versaoResposta) return route.fulfill({ status: 409, json: { mensagem: 'Versão incorreta' } });
      if (body) { resposta.statusItem = body.statusItem; resposta.observacao = body.observacao; detalhe.inspecao.itensRespondidos = 1; }
      else { resposta.temFoto = true; resposta.dadosFoto = JSON.parse(/name="metadados"\r\n\r\n([^\r]+)/.exec(corpo)?.[1] ?? '{}'); }
      resposta.versaoResposta = `v${Number(resposta.versaoResposta?.slice(1)) + 1}`;
      const retorno = { versaoResposta: resposta.versaoResposta }; retornos.set(chave, retorno);
      return route.fulfill({ json: retorno });
    }
    return route.fulfill({ json: [] });
  });
  await page.goto('/#/operacao?secao=inspecoes');
  if (comPreparacao) await expect(page.getByText('1 modelo(s) preparado(s)', { exact: false })).toBeVisible();
  return {
    registros, envios, criadas: () => criadas,
    falhar: (valor: boolean) => { falharCriacao = valor; },
    perder: () => { perderResposta = true; },
    desligar: async () => { offline = true; await page.evaluate(() => { localStorage.setItem('teste.offline', '1'); window.dispatchEvent(new Event('offline')); }); },
    ligar: async () => { offline = false; await page.evaluate(() => { localStorage.removeItem('teste.offline'); window.dispatchEvent(new Event('online')); }); },
  };
}

async function criar(page: Page) {
  await page.getByRole('button', { name: 'Nova inspeção', exact: true }).first().click();
  const painel = page.locator('#painel-nova-inspecao');
  await painel.getByLabel('Checklist', { exact: true }).selectOption(modeloId);
  await painel.getByLabel('Obra', { exact: true }).selectOption(obraId);
  await painel.getByLabel('Responsável', { exact: true }).selectOption(usuarioId);
  await painel.getByRole('button', { name: 'Iniciar inspeção', exact: true }).click();
  await expect(page.getByText('Criada no tablet', { exact: true })).toBeVisible();
  return page.url().split('/').at(-1)!;
}

async function preencher(page: Page, foto = true) {
  await page.getByLabel('Status do ponto verificado').selectOption('1');
  await page.getByLabel('Observação', { exact: true }).fill('Nova inspeção sem sinal');
  await page.getByRole('button', { name: 'Salvar ponto verificado', exact: false }).or(page.getByRole('button', { name: 'Salvar item', exact: true })).click();
  await expect(page.getByLabel('Pontos verificados respondidos')).toHaveValue('1/1');
  if (foto) await page.evaluate(async ({ obraId, png }) => {
    const domain = '/src/lib/offline/inspecoes.ts'; const dados = '/src/lib/dadosFoto.ts';
    const modulo = await import(/* @vite-ignore */ domain);
    const id = location.hash.split('/').at(-1);
    const local = await modulo.obterInspecaoLocal(id);
    const bytes = Uint8Array.from(atob(png), c => c.charCodeAt(0));
    const file = new File([bytes], 'campo.png', { type: 'image/png' });
    (await import(/* @vite-ignore */ dados)).vincularDadosFoto(file, { origem: 'camera', obraId, obraNome: 'Obra teste',
      capturadaEm: '2026-10-24T12:00:00Z', fusoMinutos: 180, latitude: -7.1, longitude: -34.8, precisaoMetros: 8, localizacaoObtidaEm: '2026-10-24T12:00:00Z' });
    await modulo.salvarFotoInspecao(id, local.detalhe.respostas[0].id, file);
  }, { obraId, png });
}

async function sincronizar(page: Page) {
  await page.evaluate(async () => { const caminho = '/src/lib/offline/syncEngine.ts'; await (await import(/* @vite-ignore */ caminho)).sincronizarFilaSaida(); });
}

test('cria do zero, preenche, reabre e sincroniza criação antes de respostas, foto e finalização', async ({ page }) => {
  const rede = await preparar(page);
  await rede.desligar();
  const id = await criar(page);
  await preencher(page);
  await page.getByRole('button', { name: 'Finalizar inspeção', exact: true }).click();
  await page.getByRole('dialog').getByRole('button', { name: /Confirmar|Finalizar inspeção/ }).last().click();
  await expect(page.getByText('Finalização pendente', { exact: true })).toBeVisible();
  await page.goto('/#/operacao?secao=inspecoes');
  await expect(page.getByText('Criação pendente', { exact: true })).toBeVisible();
  await page.getByRole('table', { name: 'Inspeções' }).getByText('Modelo offline (v1)', { exact: true }).click();
  await expect(page.getByText('Finalização pendente', { exact: true })).toBeVisible();
  expect(rede.envios).toHaveLength(0);
  await rede.ligar();
  await expect.poll(() => rede.registros.get(id)?.inspecao.status).toBe(2);
  await expect(page.getByText('Concluída', { exact: true })).toBeVisible();
  await expect(page.getByText('INSP-TESTE-1', { exact: true })).toBeVisible();
  expect(rede.criadas()).toBe(1);
  expect(rede.envios[0].path).toBe('/api/inspecoes');
  expect(rede.envios.at(-1)?.path).toBe(`/api/inspecoes/${id}/encerrar`);
  expect(rede.envios).toHaveLength(4);
});

test('falha na criação bloqueia o envio dos itens até o servidor aceitar', async ({ page }) => {
  const rede = await preparar(page);
  await rede.desligar(); const id = await criar(page); await preencher(page, false);
  rede.falhar(true); await rede.ligar();
  await expect.poll(() => rede.envios.length).toBeGreaterThan(0);
  expect(rede.envios.every(e => e.path === '/api/inspecoes')).toBe(true);
  rede.falhar(false); await sincronizar(page);
  await expect.poll(() => rede.registros.get(id)?.inspecao.itensRespondidos).toBe(1);
  expect(rede.criadas()).toBe(1);
});

test('resposta perdida após criar reutiliza a chave e não duplica a inspeção', async ({ page }) => {
  const rede = await preparar(page);
  await rede.desligar(); const id = await criar(page); await preencher(page, false);
  rede.perder(); await rede.ligar();
  await expect.poll(rede.criadas).toBe(1);
  await sincronizar(page);
  await expect.poll(() => rede.registros.get(id)?.inspecao.itensRespondidos).toBe(1);
  expect(rede.criadas()).toBe(1);
  expect(new Set(rede.envios.filter(e => e.path === '/api/inspecoes').map(e => e.chave)).size).toBe(1);
});

test('sem preparação anterior orienta baixar o modelo, sem inventar um checklist', async ({ page }) => {
  await preparar(page, false);
  const mensagem = await page.evaluate(async (nova) => {
    const caminho = '/src/lib/offline/inspecoes.ts';
    try { await (await import(/* @vite-ignore */ caminho)).criarInspecaoCampo(nova); return ''; }
    catch (e) { return (e as Error).message; }
  }, { checklistModeloId: modeloId, obraId, responsavelUsuarioId: usuarioId, data: '2026-10-24' });
  expect(mensagem).toContain('Prepare o modelo');
});
