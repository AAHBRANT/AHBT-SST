// "Realizar integração em etapas" (11/11/2026): regras puras da tela e guarda do pop-up Funcionário novo.
// Rodar: node --test tests-unit/
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import {
  botaoIntegracao,
  faltasDaEtapa,
  mensagemEtapaConcluida,
  rotuloAvancar,
  rotuloEtapa,
} from '../src/pages/integracao/componentes/helpers.ts';

const participante = (nome: string, assinou: boolean) => ({
  trabalhadorId: nome,
  nome: `${nome} Silva`,
  presencaConfirmada: assinou,
  presencaEmUtc: assinou ? '2026-11-11T11:00:00Z' : null,
  metodo: assinou ? 'digital' : null,
  temDigital: true,
  temFacial: false,
});

const etapa = (parcial: Record<string, unknown> = {}) =>
  ({
    id: 'e1',
    ordem: 3,
    tipo: 'nr18',
    titulo: 'NR-18',
    status: 'emAndamento',
    cargaMinimaHoras: 4,
    cargaLancadaMinutos: 150,
    fotosRegistradas: 1,
    fotosNecessarias: 3,
    participantes: [participante('Rafael', true), participante('Diego', false), participante('Lucas', false)],
    periodos: [{ id: 'p1', data: '2026-11-11', inicio: '08:00', fim: '10:30', minutos: 150 }],
    faltas: [],
    ...parcial,
  }) as never;

test('pop-up: sem atendimento aberto o botão é "Iniciar integração"', () => {
  assert.equal(botaoIntegracao(null).rotulo, 'Iniciar integração');
  assert.equal(botaoIntegracao(undefined).situacao, 'iniciar');
  assert.equal(botaoIntegracao({ status: 'concluido', etapaAtualOrdem: null }).situacao, 'iniciar');
});

test('pop-up: com atendimento aberto o botão continua na etapa em que parou', () => {
  const b = botaoIntegracao({ status: 'emAndamento', etapaAtualOrdem: 3 });
  assert.equal(b.situacao, 'continuar');
  assert.equal(b.rotulo, 'Continuar integração — etapa 3 de 5 (NR-18)');
  assert.match(b.detalhe, /etapa 3 de 5/);
});

test('pop-up: nenhum texto de agendamento no passo da integração', () => {
  for (const b of [botaoIntegracao(null), botaoIntegracao({ status: 'emAndamento', etapaAtualOrdem: 1 })]) {
    assert.doesNotMatch(`${b.rotulo} ${b.detalhe}`, /agendad|data da turma|Turma em/i);
  }
});

test('pop-up: controles de data só com a integração em etapas desligada; botão novo só com ela ligada', () => {
  const fonte = readFileSync(new URL('../src/components/planos-integracao/FuncionarioNovoModal.tsx', import.meta.url), 'utf8');
  // Comportamento antigo preservado, atrás da chave.
  for (const antigo of ['Alterar data da turma', 'Salvar data', 'alterarDataTurma']) {
    assert.ok(fonte.includes(antigo), `o comportamento antigo "${antigo}" deve continuar existindo`);
  }
  assert.ok(fonte.includes("!integracaoEmEtapas && passo.situacao === 'agendado'"), 'ajuste de data só com a chave desligada');
  assert.ok(fonte.includes("integracaoEmEtapas && ehPassoIntegracao(passo.codigo) && passo.situacao !== 'feito' && ("), 'botão novo só com a chave ligada');
  assert.ok(fonte.includes('r.habilitada'), 'a chave vem de entrada.habilitada');
  for (const mantido of ['Assumir', 'Kit de EPI', 'Abrir perfil', 'Lembrar amanhã', 'Passo a passo']) {
    assert.ok(fonte.includes(mantido), `deveria manter "${mantido}"`);
  }
});

test('avançar: lista o que falta, em frases curtas', () => {
  assert.deepEqual(faltasDaEtapa(etapa(), 'instrutor-1'), ['carga 1 h 30', '2 fotos da etapa', 'assinaturas de Diego, Lucas']);
});

test('avançar: etapa completa libera o botão', () => {
  const completa = etapa({
    cargaLancadaMinutos: 240,
    fotosRegistradas: 3,
    participantes: [participante('Rafael', true), participante('Diego', true)],
  });
  assert.deepEqual(faltasDaEtapa(completa, 'instrutor-1'), []);
});

test('avançar: sem instrutor trava; toda etapa precisa de ao menos 1 período, mesmo com mínimo 0', () => {
  const comPeriodo = etapa({ cargaMinimaHoras: 0, fotosRegistradas: 3, participantes: [participante('Rafael', true)] });
  assert.deepEqual(faltasDaEtapa(comPeriodo, null), ['instrutor da etapa']);
  const semPeriodo = etapa({ cargaMinimaHoras: 0, cargaLancadaMinutos: 0, periodos: [], fotosRegistradas: 3, participantes: [participante('Rafael', true)] });
  assert.deepEqual(faltasDaEtapa(semPeriodo, 'i1'), ['Lançar a carga horária']);
});

test('avançar: quem "não assinou" não conta nas pendências nem nos certificados', () => {
  const e = etapa({
    cargaLancadaMinutos: 240,
    fotosRegistradas: 3,
    participantes: [participante('Rafael', true), { ...participante('Diego', false), naoAssinou: true, motivoNaoAssinou: 'Saiu da obra' }],
  });
  assert.deepEqual(faltasDaEtapa(e, 'i1'), []);
  assert.match(mensagemEtapaConcluida(e), /1 certificado gerado/);
});

test('avançar: etapa pulada (ninguém participa) não tem pendência', () => {
  assert.deepEqual(faltasDaEtapa(etapa({ participantes: [], status: 'pulada' }), null), []);
});

test('rótulos do botão e da confirmação', () => {
  assert.equal(rotuloAvancar({ tipo: 'nr35' }), 'Avançar para NR-35');
  assert.equal(rotuloAvancar({ tipo: 'ordemServico' }), 'Avançar para a Ordem de Serviço');
  assert.equal(rotuloAvancar(null), 'Concluir integração');
  assert.equal(rotuloEtapa('ordemServico').nome, 'Ordem de Serviço (NR-01)');
  assert.equal(
    mensagemEtapaConcluida({ tipo: 'nr06', participantes: [participante('A', true), participante('B', true)] } as never),
    'NR-06 concluída — 2 certificados gerados, cada um com a assinatura do trabalhador',
  );
});
