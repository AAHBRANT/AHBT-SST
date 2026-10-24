import { test, expect, type Page } from '@playwright/test';

async function preparar(page: Page) {
  let offline = false;
  let falharFoto = false;
  let loginExpirado = false;
  let versao = 1;
  const envios: { path: string; chave: string; corpo: string }[] = [];
  const aplicadas = new Map<string, string>();
  const detalhe = {
    inspecao: { id: 'insp-a', obraId: 'obra-a', obraNome: 'Obra A', tipoInspecao: 1,
      checklistModeloNome: 'Checklist campo', checklistModeloVersao: 1, data: '2026-10-24',
      responsavelUsuarioNome: 'Técnico', status: 1, totalItens: 1, itensRespondidos: 0, itensNaoConformes: 0 },
    respostas: [{ id: 'resp-a', inspecaoId: 'insp-a', ordem: 1, descricao: 'Proteção coletiva',
      exigeFotografia: true, exigeResponsavel: false, exigePrazo: false, statusItem: null as number | null,
      observacao: '', temFoto: false, temFotoDepois: false, dadosFoto: undefined as unknown,
      versaoResposta: 'v1' }],
  };
  await page.addInitScript(() => {
    Object.defineProperty(navigator, 'onLine', { configurable: true, get: () => localStorage.getItem('teste.offline') !== '1' });
  });
  await page.route('**/api/**', async route => {
    if (offline) return route.abort('internetdisconnected');
    const path = new URL(route.request().url()).pathname;
    const method = route.request().method();
    if (method === 'GET' && path === '/api/inspecoes/insp-a') return route.fulfill({ json: detalhe });
    if (path === '/api/novidades/pendente') return route.fulfill({ status: 204 });
    if (path === '/api/usuarios/eu') return route.fulfill({ json: { nome: 'Técnico', ehAdministrador: false } });
    if (method === 'POST' && path.startsWith('/api/inspecoes/')) {
      const chave = route.request().headers()['idempotency-key'];
      const corpo = route.request().postData() ?? '';
      envios.push({ path, chave, corpo });
      if (loginExpirado) return route.fulfill({ status: 401, json: { erro: 'Login necessário' } });
      if (falharFoto && path.endsWith('/foto')) return route.fulfill({ status: 503, json: { erro: 'Temporário' } });
      if (aplicadas.has(chave)) return route.fulfill({ json: { versaoResposta: aplicadas.get(chave) } });
      const resposta = detalhe.respostas[0];
      if (path.endsWith('/encerrar')) {
        if (!resposta.statusItem || !resposta.temFoto) return route.fulfill({ status: 400, json: { erro: 'Faltam resposta e foto' } });
        detalhe.inspecao.status = 2;
        aplicadas.set(chave, resposta.versaoResposta);
        return route.fulfill({ status: 204 });
      }
      const json = path.endsWith('/resp-a') ? route.request().postDataJSON() : undefined;
      const esperada = json?.versaoEsperada ?? /name="versaoEsperada"\r\n\r\n([^\r]+)/.exec(corpo)?.[1];
      if (esperada && esperada !== resposta.versaoResposta) return route.fulfill({ status: 409, json: { mensagem: 'Mudou no servidor' } });
      if (json) {
        resposta.statusItem = json.statusItem; resposta.observacao = json.observacao;
        detalhe.inspecao.itensRespondidos = 1;
      } else {
        resposta.temFoto = true;
        resposta.dadosFoto = JSON.parse(/name="metadados"\r\n\r\n([^\r]+)/.exec(corpo)?.[1] ?? '{}');
      }
      resposta.versaoResposta = `v${++versao}`;
      aplicadas.set(chave, resposta.versaoResposta);
      return route.fulfill({ json: { versaoResposta: resposta.versaoResposta } });
    }
    if (method === 'GET' && path.endsWith('/foto')) return route.fulfill({ contentType: 'image/png', body: Buffer.from('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+a1u8AAAAASUVORK5CYII=', 'base64') });
    return route.fulfill({ json: [] });
  });
  await page.goto('/#/prevencao/inspecoes/insp-a');
  await expect(page.getByLabel('Descrição do ponto verificado')).toHaveValue('Proteção coletiva');
  return {
    envios, detalhe,
    falharFoto: (valor: boolean) => { falharFoto = valor; },
    loginExpirado: (valor: boolean) => { loginExpirado = valor; },
    desligar: async () => {
      offline = true;
      await page.evaluate(() => { localStorage.setItem('teste.offline', '1'); window.dispatchEvent(new Event('offline')); });
    },
    ligar: async () => {
      offline = false;
      await page.evaluate(() => { localStorage.removeItem('teste.offline'); window.dispatchEvent(new Event('online')); });
    },
  };
}

async function salvarFoto(page: Page, invalida = false) {
  // Exercita o mesmo callback usado pela câmera, preservando Blob e metadados reais no IndexedDB.
  await page.evaluate(async (invalida) => {
    const caminho = '/src/lib/offline/inspecoes.ts';
    const fotos = '/src/lib/dadosFoto.ts';
    const { salvarFotoInspecao } = await import(/* @vite-ignore */ caminho);
    const { vincularDadosFoto } = await import(/* @vite-ignore */ fotos);
    const bytes = Uint8Array.from(atob('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+a1u8AAAAASUVORK5CYII='), c => c.charCodeAt(0));
    const file = new File([bytes], 'campo.png', { type: 'image/png' });
    vincularDadosFoto(file, { origem: 'camera', obraId: 'obra-a', obraNome: 'Obra A', capturadaEm: '2026-10-24T12:00:00Z',
      fusoMinutos: 180, latitude: invalida ? null : -7.1, longitude: -34.8, precisaoMetros: 8, localizacaoObtidaEm: '2026-10-24T12:00:00Z' });
    await salvarFotoInspecao('insp-a', 'resp-a', file);
  }, invalida);
}

async function sincronizar(page: Page) {
  await page.evaluate(async () => {
    const caminho = '/src/lib/offline/syncEngine.ts';
    const { sincronizarFilaSaida } = await import(/* @vite-ignore */ caminho);
    await sincronizarFilaSaida();
  });
}

test('preenche offline, reabre rascunho, salva item/foto e finaliza somente após sincronizar', async ({ page }) => {
  const rede = await preparar(page);
  await rede.desligar();
  await page.getByLabel('Status do ponto verificado').selectOption('1');
  await page.getByLabel('Observação', { exact: true }).fill('Verificado no campo sem sinal');
  await expect(page.getByText('Rascunho salvo no tablet.', { exact: false })).toBeVisible();
  await page.reload();
  await expect(page.getByLabel('Observação', { exact: true })).toHaveValue('Verificado no campo sem sinal');
  await page.getByRole('button', { name: 'Salvar ponto verificado', exact: false }).or(page.getByRole('button', { name: 'Salvar item', exact: true })).click();
  await expect(page.getByLabel('Pontos verificados respondidos')).toHaveValue('1/1');
  await expect(page.getByText('Rascunho salvo no tablet.', { exact: false })).toHaveCount(0);
  await salvarFoto(page);
  await expect(page.getByLabel('Pontos verificados respondidos')).toHaveValue('1/1');
  await page.reload();
  await expect(page.getByLabel('Pontos verificados respondidos')).toHaveValue('1/1');
  await expect(page.getByRole('img', { name: 'Evidência anterior', exact: true })).toBeVisible();
  await page.getByRole('button', { name: 'Finalizar inspeção', exact: true }).click();
  const confirmar = page.getByRole('button', { name: /Confirmar|Finalizar inspeção/ });
  if (await page.getByRole('dialog').count()) await confirmar.last().click();
  await expect(page.getByText('Finalização pendente', { exact: true })).toBeVisible();
  expect(rede.envios).toHaveLength(0);
  await rede.ligar();
  await expect.poll(() => rede.detalhe.inspecao.status).toBe(2);
  await expect(page.getByText('Concluída', { exact: true })).toBeVisible();
  expect(rede.envios.map(e => e.path)).toEqual(['/api/inspecoes/respostas/resp-a', '/api/inspecoes/respostas/resp-a/foto', '/api/inspecoes/insp-a/encerrar']);
  expect(rede.envios[1].corpo).toContain('2026-10-24T12:00:00Z');
  expect(rede.envios[1].corpo).toContain('-7.1');
});

test('foto com erro temporário bloqueia finalização e conserva a chave de reenvio', async ({ page }) => {
  const rede = await preparar(page);
  await rede.desligar();
  await page.getByLabel('Status do ponto verificado').selectOption('1');
  await page.getByRole('button', { name: 'Salvar ponto verificado', exact: false }).or(page.getByRole('button', { name: 'Salvar item', exact: true })).click();
  await expect(page.getByLabel('Pontos verificados respondidos')).toHaveValue('1/1');
  await expect(page.getByText('Rascunho salvo no tablet.', { exact: false })).toHaveCount(0);
  await salvarFoto(page);
  await page.evaluate(async () => { const caminho = '/src/lib/offline/inspecoes.ts'; await (await import(/* @vite-ignore */ caminho)).finalizarInspecaoLocal('insp-a'); });
  rede.falharFoto(true);
  await rede.ligar();
  await expect.poll(() => rede.envios.filter(e => e.path.endsWith('/foto')).length).toBeGreaterThan(0);
  expect(rede.envios.some(e => e.path.endsWith('/encerrar'))).toBe(false);
  rede.falharFoto(false);
  await sincronizar(page);
  await expect.poll(() => rede.detalhe.inspecao.status).toBe(2);
  const fotos = rede.envios.filter(e => e.path.endsWith('/foto'));
  expect(new Set(fotos.map(e => e.chave)).size).toBe(1);
});

test('401 conserva respostas e foto, aguardando login para finalizar', async ({ page }) => {
  const rede = await preparar(page);
  await rede.desligar();
  await page.getByLabel('Status do ponto verificado').selectOption('1');
  await page.getByRole('button', { name: 'Salvar ponto verificado', exact: false }).or(page.getByRole('button', { name: 'Salvar item', exact: true })).click();
  await expect(page.getByLabel('Pontos verificados respondidos')).toHaveValue('1/1');
  await expect(page.getByText('Rascunho salvo no tablet.', { exact: false })).toHaveCount(0);
  await salvarFoto(page);
  rede.loginExpirado(true);
  await rede.ligar();
  await expect.poll(() => rede.envios.length).toBeGreaterThan(0);
  expect(rede.detalhe.inspecao.itensRespondidos).toBe(0);
  rede.loginExpirado(false);
  await sincronizar(page);
  await expect.poll(() => rede.detalhe.inspecao.itensRespondidos).toBe(1);
  await expect.poll(() => rede.detalhe.respostas[0].temFoto).toBe(true);
});

test('mudança concorrente mantém a versão do servidor e guarda a ação recusada', async ({ page }) => {
  const rede = await preparar(page);
  await rede.desligar();
  await page.getByLabel('Status do ponto verificado').selectOption('1');
  await page.getByLabel('Observação', { exact: true }).fill('Resposta do tablet');
  await page.getByRole('button', { name: 'Salvar ponto verificado', exact: false }).or(page.getByRole('button', { name: 'Salvar item', exact: true })).click();
  await expect(page.getByLabel('Pontos verificados respondidos')).toHaveValue('1/1');
  await expect(page.getByText('Rascunho salvo no tablet.', { exact: false })).toHaveCount(0);
  rede.detalhe.respostas[0].observacao = 'Alterado no escritório';
  rede.detalhe.respostas[0].versaoResposta = 'v99';
  await rede.ligar();
  await expect(page.getByText('Há envios recusados desta inspeção.', { exact: false })).toBeVisible();
  expect(rede.detalhe.respostas[0].observacao).toBe('Alterado no escritório');
  const primeiraChave = rede.envios[0].chave;
  await page.evaluate(async () => {
    const banco = '/src/lib/offline/db.ts';
    const motor = '/src/lib/offline/syncEngine.ts';
    const { offlineDb } = await import(/* @vite-ignore */ banco);
    const conflito = await offlineDb.conflitos.toCollection().first();
    await (await import(/* @vite-ignore */ motor)).reenviarConflito(conflito.id);
  });
  await expect.poll(() => rede.detalhe.respostas[0].observacao).toBe('Resposta do tablet');
  expect(rede.envios.at(-1)?.chave).not.toBe(primeiraChave);
});

test('foto com localização pendente é preservada, mas não enviada nem usada para finalizar', async ({ page }) => {
  const rede = await preparar(page);
  await rede.desligar();
  await page.getByLabel('Status do ponto verificado').selectOption('1');
  await page.getByRole('button', { name: 'Salvar ponto verificado', exact: false }).or(page.getByRole('button', { name: 'Salvar item', exact: true })).click();
  await expect(page.getByLabel('Pontos verificados respondidos')).toHaveValue('1/1');
  await expect(page.getByText('Rascunho salvo no tablet.', { exact: false })).toHaveCount(0);
  await salvarFoto(page, true);
  await page.reload();
  await expect(page.getByRole('img', { name: 'Evidência anterior', exact: true })).toBeVisible();
  const erro = await page.evaluate(async () => {
    const caminho = '/src/lib/offline/inspecoes.ts';
    try { await (await import(/* @vite-ignore */ caminho)).finalizarInspecaoLocal('insp-a'); return ''; }
    catch (e) { return (e as Error).message; }
  });
  expect(erro).toContain('fotos com dados de captura pendentes');
  await rede.ligar();
  await expect.poll(() => rede.detalhe.inspecao.itensRespondidos).toBe(1);
  expect(rede.envios.some(e => e.path.endsWith('/foto') || e.path.endsWith('/encerrar'))).toBe(false);
});

test('rascunho iniciado offline conserva sua versão mesmo depois de uma atualização online', async ({ page }) => {
  const rede = await preparar(page);
  await rede.desligar();
  await page.getByLabel('Status do ponto verificado').selectOption('1');
  await page.getByLabel('Observação', { exact: true }).fill('Rascunho do tablet');
  await expect(page.getByText('Rascunho salvo no tablet.', { exact: false })).toBeVisible();
  rede.detalhe.respostas[0].observacao = 'Alterado no escritório';
  rede.detalhe.respostas[0].versaoResposta = 'v99';
  await rede.ligar();
  await page.evaluate(async () => { const caminho = '/src/lib/offline/inspecoes.ts'; await (await import(/* @vite-ignore */ caminho)).obterInspecaoLocal('insp-a'); });
  await page.getByRole('button', { name: 'Salvar ponto verificado', exact: false }).or(page.getByRole('button', { name: 'Salvar item', exact: true })).click();
  await expect(page.getByText('Há envios recusados desta inspeção.', { exact: false })).toBeVisible();
  expect(rede.detalhe.respostas[0].observacao).toBe('Alterado no escritório');
});

test('trocar de conta não envia a inspeção em nome de outra pessoa', async ({ page }) => {
  // Simula apenas a emissão dos tokens; o motor usa os mesmos claims de conta do app real.
  await page.route('**/src/lib/authHeaders.ts*', route => route.fulfill({ contentType: 'application/javascript', body: `
    import { lembrarConta } from '/src/lib/offline/conta.ts';
    export async function montarHeadersAuth() {
      if (!navigator.onLine) return {};
      const oid = localStorage.getItem('teste.conta') || 'dono';
      const headers = { Authorization: 'Bearer x.' + btoa(JSON.stringify({ tid: 'tenant', oid })) + '.x' };
      lembrarConta(headers); return headers;
    }` }));
  const rede = await preparar(page);
  await rede.desligar();
  await page.getByLabel('Status do ponto verificado').selectOption('1');
  await page.getByRole('button', { name: 'Salvar ponto verificado', exact: false }).or(page.getByRole('button', { name: 'Salvar item', exact: true })).click();
  await expect(page.getByLabel('Pontos verificados respondidos')).toHaveValue('1/1');
  await expect(page.getByText('Rascunho salvo no tablet.', { exact: false })).toHaveCount(0);
  await page.evaluate(() => localStorage.setItem('teste.conta', 'outra'));
  await rede.ligar();
  await sincronizar(page);
  expect(rede.envios).toHaveLength(0);
  await page.evaluate(() => localStorage.setItem('teste.conta', 'dono'));
  await sincronizar(page);
  await expect.poll(() => rede.detalhe.inspecao.itensRespondidos).toBe(1);
});
