import { test, expect, type Page } from '@playwright/test';

const funcionarios = [
  { trabalhadorId: 'ana', nome: 'Ana Souza', matricula: '001' },
  { trabalhadorId: 'bruno', nome: 'Bruno Lima', matricula: '002' },
  { trabalhadorId: 'joao', nome: 'João Santos', matricula: '003' },
];

async function preparar(page: Page, opcoes: { confirmado?: boolean; concluido?: boolean } = {}) {
  let selecionados = new Set<string>();
  let falharSalvamento = false;
  let capturaId = 'ana';
  let rostoRecusado = false;
  const enviosFaciais: string[] = [];
  const atualizacoes: string[][] = [];
  const presencas = opcoes.confirmado ? [{ id: 'presenca-ana', trabalhadorId: 'ana', trabalhadorNome: 'Ana Souza',
    fotoTipo: 3, assinadoEm: '2026-09-29T10:00:00Z' }] : [];
  function obterDetalhe() {
    return {
        dds: { id: 'dds-a', obraId: 'obra-a', obraNome: 'Obra A', data: '2026-09-29', responsavelUsuarioId: 'responsavel',
          responsavelUsuarioNome: 'Técnico de segurança', status: opcoes.concluido ? 2 : 1,
          atividadesNomes: ['Montagem'], temasAtividades: [], totalItensChecklist: 0, itensVerificados: 0,
          totalParticipantes: presencas.length, totalFotosEvidencia: 0, semExpediente: false },
        participantes: presencas, funcionariosSelecionados: funcionarios.filter((f) => selecionados.has(f.trabalhadorId)),
        fotosEvidencia: [], itensChecklist: [],
    };
  }
  await page.addInitScript(() => localStorage.setItem('sst.modoTema', 'light'));
  await page.route('**/api/**', async (route) => {
    const url = new URL(route.request().url());
    const path = url.pathname;
    if (url.port === '5251') {
      return route.fulfill({ json: path === '/api/capturar' ? { trabalhadorId: capturaId, score: 95 }
        : { dispositivoId: 'dispositivo-teste', segredoDispositivo: 'somente-teste' } });
    }
    if (path === '/api/dds/dds-a/funcionarios-disponiveis') return route.fulfill({ json: funcionarios });
    if (path === '/api/dds/dds-a/funcionarios-selecionados') {
      const ids = route.request().postDataJSON().trabalhadoresIds as string[];
      atualizacoes.push(ids);
      if (falharSalvamento) return route.fulfill({ status: 400, json: { erro: 'Não foi possível salvar a seleção.' } });
      selecionados = new Set(ids.filter((id) => !presencas.some((p) => p.trabalhadorId === id)));
      return route.fulfill({ json: obterDetalhe() });
    }
    if (path === '/api/dds/dds-a/participantes/facial') {
      if (rostoRecusado) return route.fulfill({ status: 400, json: { erro: 'Rosto não reconhecido.', motivo: 'RostoNaoReconhecido' } });
      const corpo = route.request().postData() ?? '';
      const id = /name="TrabalhadorId"\r\n\r\n([^\r]+)/.exec(corpo)?.[1] ?? '';
      enviosFaciais.push(id);
      presencas.push({ id: `presenca-${id}`, trabalhadorId: id, trabalhadorNome: funcionarios.find((f) => f.trabalhadorId === id)!.nome,
        fotoTipo: 4, assinadoEm: '2026-09-29T10:00:00Z' });
      selecionados.delete(id);
      return route.fulfill({ json: { id: `presenca-${id}` } });
    }
    if (path === '/api/dds/dds-a/participantes') {
      const id = route.request().postDataJSON().trabalhadorId as string;
      presencas.push({ id: `presenca-${id}`, trabalhadorId: id, trabalhadorNome: funcionarios.find((f) => f.trabalhadorId === id)!.nome,
        fotoTipo: 3, assinadoEm: '2026-09-29T10:00:00Z' });
      selecionados.delete(id);
      return route.fulfill({ json: { id: `presenca-${id}` } });
    }
    if (path === '/api/dds/dds-a') {
      return route.fulfill({ json: obterDetalhe() });
    }
    if (path === '/api/usuarios/eu') return route.fulfill({ json: { nome: 'Responsável', ehAdministrador: true } });
    if (path === '/api/novidades/pendente') return route.fulfill({ json: null });
    return route.fulfill({ json: [] });
  });
  await page.goto('/#/prevencao/dds/dia/dds-a');
  await expect(page.getByText('Funcionários do DDS', { exact: true })).toBeVisible();
  if (!opcoes.concluido) await expect(page.getByRole('checkbox', { name: 'Selecionar Ana Souza', exact: true })).toBeVisible();
  return {
    atualizacoes, enviosFaciais,
    falhar: () => { falharSalvamento = true; },
    capturar: (id: string) => { capturaId = id; },
    recusarRosto: () => { rostoRecusado = true; },
  };
}

test('selecionar todos inclui toda a obra mesmo com busca, persiste e permite remover/reincluir', async ({ page }) => {
  const { atualizacoes } = await preparar(page);
  await page.getByRole('textbox', { name: 'Buscar funcionário' }).fill('joao');
  await expect(page.getByRole('checkbox', { name: 'Selecionar João Santos' })).toBeVisible();
  await expect(page.getByRole('checkbox', { name: 'Selecionar Ana Souza' })).toHaveCount(0);
  await page.getByRole('button', { name: 'Selecionar todos os funcionários', exact: true }).click();
  await expect(page.getByText('3 selecionados', { exact: true })).toBeVisible();
  await expect(page.getByText('Seleção salva.', { exact: true })).toBeVisible();
  expect(atualizacoes[0]).toEqual(['ana', 'bruno', 'joao']);
  await page.reload();
  await expect(page.getByRole('checkbox', { name: 'Selecionar Ana Souza' })).toBeChecked();
  await expect(page.getByRole('checkbox', { name: 'Selecionar Bruno Lima' })).toBeChecked();
  await page.getByRole('button', { name: 'Remover Bruno Lima da seleção' }).click();
  await expect(page.getByText('2 selecionados', { exact: true })).toBeVisible();
  await page.getByRole('checkbox', { name: 'Selecionar Bruno Lima' }).check();
  await expect(page.getByText('3 selecionados', { exact: true })).toBeVisible();
  await page.getByRole('button', { name: 'Limpar seleção', exact: true }).click();
  await expect(page.getByText('0 selecionados', { exact: true })).toBeVisible();
  await page.getByRole('checkbox', { name: 'Mostrar somente selecionados' }).check();
  await expect(page.getByText('Nenhum funcionário selecionado.', { exact: true })).toBeVisible();
});

test('limpar preserva participantes com presença e assinatura e funciona em tela estreita', async ({ page }, testInfo) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await preparar(page, { confirmado: true });
  await page.getByRole('button', { name: 'Selecionar todos os funcionários', exact: true }).click();
  await expect(page.getByText('3 selecionados', { exact: true })).toBeVisible();
  await page.getByRole('button', { name: 'Remover Bruno Lima da seleção' }).click();
  await expect(page.getByText('2 selecionados', { exact: true })).toBeVisible();
  await page.getByRole('button', { name: 'Limpar seleção', exact: true }).click();
  await expect(page.getByText('1 selecionado', { exact: true })).toBeVisible();
  await expect(page.getByRole('checkbox', { name: 'Selecionar Ana Souza' })).toBeChecked();
  await expect(page.getByRole('checkbox', { name: 'Selecionar Ana Souza' })).toBeDisabled();
  await expect(page.getByText('Assinado às', { exact: false })).toBeVisible();
  await expect(page.getByText('Seleção salva.', { exact: true })).toBeVisible();
  const tabela = page.getByRole('table', { name: 'Funcionários do DDS' });
  await expect(tabela).toBeVisible();
  const largura = await tabela.boundingBox();
  expect(largura!.width).toBeLessThanOrEqual(390);
  await tabela.screenshot({ path: testInfo.outputPath('dds-mobile-lista.png') });
  await page.screenshot({ path: testInfo.outputPath('dds-mobile.png'), fullPage: true });
});

test('digital de outro funcionário é recusada; digital correta confirma uma única presença', async ({ page }, testInfo) => {
  const controles = await preparar(page);
  await page.getByRole('checkbox', { name: 'Selecionar Ana Souza' }).check();
  await expect(page.getByText('1 selecionado', { exact: true })).toBeVisible();
  controles.capturar('bruno');
  await page.getByRole('button', { name: 'Confirmar por digital', exact: true }).click();
  await expect(page.getByText(/A digital capturada não corresponde a Ana Souza/)).toBeVisible();
  await expect(page.getByText('0 presenças confirmadas', { exact: true })).toBeVisible();
  controles.capturar('ana');
  await page.getByRole('button', { name: 'Confirmar por digital', exact: true }).click();
  await expect(page.getByText('1 presença confirmada', { exact: true })).toBeVisible();
  await expect(page.getByRole('checkbox', { name: 'Selecionar Ana Souza' })).toBeDisabled();
  await expect(page.getByRole('button', { name: 'Confirmar por digital', exact: true })).toHaveCount(0);
  await page.screenshot({ path: testInfo.outputPath('dds-desktop.png'), fullPage: true });
});

test('falha de gravação mantém a seleção anterior e mostra o erro', async ({ page }) => {
  const controles = await preparar(page);
  await page.getByRole('checkbox', { name: 'Selecionar Ana Souza' }).check();
  await expect(page.getByText('1 selecionado', { exact: true })).toBeVisible();
  await expect(page.getByText('Seleção salva.', { exact: true })).toBeVisible();
  controles.falhar();
  await page.getByRole('button', { name: 'Remover Ana Souza da seleção' }).click();
  await expect(page.getByText(/Não foi possível salvar a seleção/)).toBeVisible();
  await expect(page.getByRole('checkbox', { name: 'Selecionar Ana Souza' })).toBeChecked();
});

test('DDS concluído mostra a lista sem ações de edição', async ({ page }) => {
  await preparar(page, { confirmado: true, concluido: true });
  await expect(page.getByText('Ana Souza', { exact: true })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Selecionar todos os funcionários', exact: true })).toHaveCount(0);
  await expect(page.getByRole('checkbox', { name: 'Selecionar Ana Souza' })).toHaveCount(0);
  await expect(page.getByRole('button', { name: 'Confirmar por digital', exact: true })).toHaveCount(0);
});

test('cada linha oferece digital e facial; facial confirma a presença do funcionário da linha', async ({ page }, testInfo) => {
  const controles = await preparar(page);
  await page.getByRole('checkbox', { name: 'Selecionar Ana Souza' }).check();
  await expect(page.getByText('1 selecionado', { exact: true })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Confirmar por digital', exact: true })).toBeVisible();
  const facial = page.getByRole('button', { name: 'Confirmar por facial', exact: true });
  await expect(facial).toBeVisible();
  await expect(page.getByText('Seleção salva.', { exact: true })).toBeVisible();
  await page.screenshot({ path: testInfo.outputPath('dds-botoes-digital-facial.png') });
  await facial.click();
  await page.getByRole('button', { name: 'Capturar', exact: true }).click();
  await expect(page.getByText('1 presença confirmada', { exact: true })).toBeVisible();
  await expect(page.getByText(/Presença de Ana Souza confirmada por reconhecimento facial/)).toBeVisible();
  expect(controles.enviosFaciais).toEqual(['ana']);
  await expect(page.getByRole('button', { name: 'Confirmar por facial', exact: true })).toHaveCount(0);
});

test('rosto recusado mostra o motivo e não confirma a presença', async ({ page }) => {
  const controles = await preparar(page);
  await page.getByRole('checkbox', { name: 'Selecionar Ana Souza' }).check();
  await expect(page.getByText('1 selecionado', { exact: true })).toBeVisible();
  controles.recusarRosto();
  await page.getByRole('button', { name: 'Confirmar por facial', exact: true }).click();
  await page.getByRole('button', { name: 'Capturar', exact: true }).click();
  await expect(page.getByText('Rosto não reconhecido.')).toBeVisible();
  await expect(page.getByText('0 presenças confirmadas', { exact: true })).toBeVisible();
});
