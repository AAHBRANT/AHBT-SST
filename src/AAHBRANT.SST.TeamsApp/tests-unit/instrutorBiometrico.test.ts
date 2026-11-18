// Instrutor com biometria, carga automática, pendências clicáveis, fila única e fotos automáticas (16/11/2026).
// Só lógica pura (sem React nem API). Rodar: node --test tests-unit/
import { test } from 'node:test';
import assert from 'node:assert/strict';
import {
  CODIGOS_PENDENCIA,
  alvoDaPendencia,
  bloqueioAvancar,
  cargaDecorridaMin,
  estadoCarga,
  fimCargaAutomaticaMs,
  formatarFalta,
  instanteBrasilia,
  montarFilaAssinaturas,
  pendenciasDaEtapa,
  percentualCarga,
  resumoFila,
  rotuloMetodo,
  situacaoBiometriaInstrutor,
  textoFechamentoCarga,
  vezDaFila,
} from '../src/pages/integracao/componentes/helpersInstrutor.ts';
import {
  FOTOS_AUTOMATICAS_PADRAO,
  TOLERANCIA_ATRASO_MS,
  agendaAoAtivar,
  classificarFalhaCamera,
  configuracaoFotosSegura,
  formatarContagem,
  pausaParaVencerEm,
  proximaFotoDevida,
  proximaOrdemLivre,
  resumoIntervalos,
  situacaoAgenda,
  textoAtraso,
} from '../src/pages/integracao/componentes/agendaFotos.ts';
import { classificarErroAssinatura } from '../src/lib/erroAssinatura.ts';

const cfg = FOTOS_AUTOMATICAS_PADRAO; // 60 s, 300 s, 300 s

// 16/11/2026 em Brasília (UTC-3): 09:00 = 12:00Z.
const dia = '2026-11-16';
const em = (hhmm: string, data = dia) => instanteBrasilia(data, hhmm);

const periodoAuto = { id: 'p1', data: dia, inicio: '09:00', fim: '13:00', minutos: 240, automatico: true };

const participante = (nome: string, assinou: boolean, extra: Record<string, unknown> = {}) => ({
  trabalhadorId: nome,
  nome: `${nome} Silva`,
  presencaConfirmada: assinou,
  presencaEmUtc: assinou ? '2026-11-16T12:31:00Z' : null,
  metodo: assinou ? 'biometria' : null,
  temDigital: true,
  temFacial: true,
  ...extra,
});

const etapa = (parcial: Record<string, unknown> = {}) =>
  ({
    id: 'e1',
    ordem: 3,
    tipo: 'nr18',
    titulo: 'NR-18',
    status: 'emAndamento',
    cargaMinimaHoras: 4,
    cargaLancadaMinutos: 240,
    fotosRegistradas: 3,
    fotosNecessarias: 3,
    participantes: [participante('Cláudio', true), participante('Reginaldo', false)],
    periodos: [periodoAuto],
    faltas: [],
    instrutorAssinou: false,
    instrutorTemDigital: true,
    instrutorTemFacial: true,
    ...parcial,
  }) as never;

// ---- método ----

test('método: biometria aparece como digital e reconhecimentoFacial como facial', () => {
  assert.equal(rotuloMetodo('biometria'), 'digital');
  assert.equal(rotuloMetodo('reconhecimentoFacial'), 'facial');
  assert.equal(rotuloMetodo(null), null);
  assert.equal(rotuloMetodo(undefined), null);
  assert.equal(rotuloMetodo('qualquer'), null);
});

// ---- carga automática ----

test('carga: o fim do período automático vem do último período lançado pelo sistema', () => {
  assert.equal(fimCargaAutomaticaMs([periodoAuto]), em('13:00'));
  assert.equal(fimCargaAutomaticaMs([{ ...periodoAuto, automatico: false }]), null);
  assert.equal(fimCargaAutomaticaMs([]), null);
  assert.equal(fimCargaAutomaticaMs(undefined), null);
});

test('carga: o período que cruza a meia-noite termina às 23:59 mas conta até 00:00; o fim é o do segundo período', () => {
  const primeiro = { id: 'a', data: dia, inicio: '22:00', fim: '23:59', minutos: 120, automatico: true };
  const segundo = { id: 'b', data: '2026-11-17', inicio: '00:00', fim: '02:00', minutos: 120, automatico: true };
  assert.equal(fimCargaAutomaticaMs([primeiro]), em('00:00', '2026-11-17'));
  assert.equal(fimCargaAutomaticaMs([primeiro, segundo]), em('02:00', '2026-11-17'));
  // Às 22:30 o fim cai em outro dia: o texto traz a data.
  assert.equal(textoFechamentoCarga(em('02:00', '2026-11-17'), em('22:30')), 'Carga horária fecha às 02:00 de 17/11 — faltam 3 h 30 min');
});

test('carga: texto "fecha às HH:mm — faltam X h YY min" recalculado pelo relógio', () => {
  assert.equal(textoFechamentoCarga(em('13:00'), em('10:12')), 'Carga horária fecha às 13:00 — faltam 2 h 48 min');
  assert.equal(textoFechamentoCarga(em('13:00'), em('12:25')), 'Carga horária fecha às 13:00 — faltam 35 min');
  assert.equal(formatarFalta(0), '0 min');
  assert.equal(formatarFalta(30_001), '1 min'); // arredonda para cima: nunca promete menos do que falta
});

test('carga: estados sem período, aguardando o fim, cumprida e abaixo do mínimo', () => {
  assert.equal(estadoCarga({ periodos: [], cargaLancadaMinutos: 0, cargaMinimaHoras: 0 }, em('10:00')), 'sem_periodo');
  assert.equal(estadoCarga(etapa(), em('10:12')), 'aguardando_fim');
  assert.equal(estadoCarga(etapa(), em('13:00')), 'cumprida');
  assert.equal(estadoCarga(etapa(), em('13:05')), 'cumprida');
  const manualCurto = etapa({ periodos: [{ id: 'm', data: dia, inicio: '09:00', fim: '12:30', minutos: 210 }], cargaLancadaMinutos: 210 });
  assert.equal(estadoCarga(manualCurto, em('13:00')), 'abaixo_minimo');
  // Mínimo 0 com período lançado conta como cumprida.
  assert.equal(estadoCarga({ periodos: [{ ...periodoAuto, minutos: 60, fim: '10:00' }], cargaLancadaMinutos: 60, cargaMinimaHoras: 0 }, em('10:01')), 'cumprida');
});

test('carga: a barra mostra só o tempo que já passou do período automático', () => {
  assert.equal(cargaDecorridaMin([periodoAuto], em('08:00')), 0);
  assert.equal(cargaDecorridaMin([periodoAuto], em('10:12')), 72);
  assert.equal(cargaDecorridaMin([periodoAuto], em('15:00')), 240);
  assert.equal(percentualCarga(etapa(), em('10:12')), 30);
  assert.equal(percentualCarga(etapa(), em('14:00')), 100);
  // Manual conta inteiro, mesmo sem o relógio andar.
  const manual = { periodos: [{ id: 'm', data: dia, inicio: '09:00', fim: '11:00', minutos: 120 }], cargaLancadaMinutos: 120, cargaMinimaHoras: 4 };
  assert.equal(percentualCarga(manual, em('09:00')), 50);
});

test('pendências: cada código leva à seção certa; etapa sem participantes não tem ação', () => {
  assert.equal(alvoDaPendencia(CODIGOS_PENDENCIA.instrutorNaoDefinido), 'instrutor');
  assert.equal(alvoDaPendencia(CODIGOS_PENDENCIA.carga), 'carga');
  assert.equal(alvoDaPendencia(CODIGOS_PENDENCIA.cargaAguardandoFim), 'carga');
  assert.equal(alvoDaPendencia(CODIGOS_PENDENCIA.fotos), 'fotos');
  assert.equal(alvoDaPendencia(CODIGOS_PENDENCIA.assinaturasColaboradores), 'fila');
  assert.equal(alvoDaPendencia(CODIGOS_PENDENCIA.instrutorAssinatura), 'fila');
  assert.equal(alvoDaPendencia(CODIGOS_PENDENCIA.instrutorSemBiometria), 'fila');
  assert.equal(alvoDaPendencia(CODIGOS_PENDENCIA.semParticipantes), null);
  assert.equal(alvoDaPendencia('desconhecido'), null);
});

test('Avançar: bloqueado enquanto houver qualquer pendência, com o motivo e o tempo que falta', () => {
  const e = etapa({
    pendencias: [
      { codigo: 'instrutor_assinatura', texto: 'Assinatura biométrica do instrutor pendente.' },
      { codigo: 'carga_aguardando_fim', texto: 'Carga horária fecha às 13:00 — faltam 4 h 00 min.' },
    ],
  });
  const b = bloqueioAvancar(e, 'inst', em('10:12'));
  assert.equal(b.bloqueado, true);
  assert.equal(b.pendencias.length, 2);
  // A espera da carga vem com o tempo recalculado pelo relógio e tem prioridade como motivo.
  assert.equal(b.motivo, 'Carga horária fecha às 13:00 — faltam 2 h 48 min');
});

test('Avançar: passado o fim, a espera da carga some; segue bloqueado se sobrar outra pendência e libera quando não sobra nada', () => {
  const comOutra = etapa({
    pendencias: [
      { codigo: 'instrutor_assinatura', texto: 'Assinatura biométrica do instrutor pendente.' },
      { codigo: 'carga_aguardando_fim', texto: 'Carga horária fecha às 13:00 — faltam 5 min.' },
    ],
  });
  const depois = bloqueioAvancar(comOutra, 'inst', em('13:01'));
  assert.equal(depois.bloqueado, true);
  assert.deepEqual(depois.pendencias.map((p) => p.codigo), ['instrutor_assinatura']);
  assert.equal(depois.motivo, 'Assinatura biométrica do instrutor pendente.');

  const soEspera = etapa({ pendencias: [{ codigo: 'carga_aguardando_fim', texto: 'Carga horária fecha às 13:00 — faltam 5 min.' }] });
  assert.equal(bloqueioAvancar(soEspera, 'inst', em('12:55')).bloqueado, true);
  const liberado = bloqueioAvancar(soEspera, 'inst', em('13:00'));
  assert.equal(liberado.bloqueado, false);
  assert.equal(liberado.motivo, null);
});

test('Avançar: sem pendências do servidor a etapa pronta fica liberada; etapa pulada ou sem participantes nunca trava', () => {
  assert.equal(bloqueioAvancar(etapa({ pendencias: [], participantes: [participante('A', true)] }), 'inst', em('14:00')).bloqueado, false);
  assert.deepEqual(pendenciasDaEtapa(etapa({ status: 'pulada', pendencias: [{ codigo: 'fotos', texto: 'x' }] }), 'inst', em('14:00')), []);
  assert.deepEqual(pendenciasDaEtapa(etapa({ participantes: [], pendencias: [{ codigo: 'sem_participantes', texto: 'x' }] }), 'inst', em('14:00')), []);
});

test('Avançar: servidor antigo (sem `pendencias`) cai nas frases locais e continua bloqueando', () => {
  const antigo = etapa({ pendencias: undefined, fotosRegistradas: 1, periodos: [], cargaLancadaMinutos: 0 });
  const b = bloqueioAvancar(antigo, 'inst', em('10:00'));
  assert.equal(b.bloqueado, true);
  assert.ok(b.pendencias.every((p) => p.codigo === 'outra'));
});

// ---- fila única ----

test('fila: o instrutor aparece primeiro e o contador soma instrutor + colaboradores', () => {
  const linhas = montarFilaAssinaturas(etapa(), { trabalhadorId: 'inst', nome: 'Gilton' });
  assert.deepEqual(linhas.map((l) => l.tipo), ['instrutor', 'colaborador', 'colaborador']);
  assert.deepEqual(resumoFila(linhas), { feitos: 1, total: 3 });
});

test('fila: a vez é do instrutor; assinou, passa sozinha ao próximo colaborador; todos assinaram = fim', () => {
  const inst = { trabalhadorId: 'inst', nome: 'Gilton' };
  let vez = vezDaFila(montarFilaAssinaturas(etapa(), inst));
  assert.equal(vez.tipo, 'instrutor');

  vez = vezDaFila(montarFilaAssinaturas(etapa({ instrutorAssinou: true, instrutorMetodo: 'reconhecimentoFacial', instrutorAssinadoEmUtc: '2026-11-16T12:42:00Z' }), inst));
  assert.equal(vez.tipo, 'colaborador');
  assert.equal(vez.tipo === 'colaborador' && vez.linha.nome, 'Reginaldo Silva');

  const todos = etapa({ instrutorAssinou: true, instrutorMetodo: 'biometria', participantes: [participante('A', true), participante('B', true)] });
  const linhas = montarFilaAssinaturas(todos, inst);
  assert.equal(vezDaFila(linhas).tipo, 'todos');
  assert.deepEqual(resumoFila(linhas), { feitos: 3, total: 3 });
  assert.equal(linhas[0].metodo, 'digital'); // biometria -> digital
});

test('fila: instrutor sem biometria não trava os colaboradores; sozinho, a vez continua dele para mostrar o cadastro', () => {
  const inst = { trabalhadorId: 'inst', nome: 'Gilton' };
  const semBio = etapa({ instrutorTemDigital: false, instrutorTemFacial: false, pendencias: [{ codigo: 'instrutor_sem_biometria', texto: 'x' }] });
  const linhas = montarFilaAssinaturas(semBio, inst);
  assert.equal(linhas[0].semBiometria, true);
  assert.equal(vezDaFila(linhas).tipo, 'colaborador');

  const soInstrutor = etapa({ instrutorTemDigital: false, instrutorTemFacial: false, participantes: [participante('A', true)] });
  assert.equal(vezDaFila(montarFilaAssinaturas(soInstrutor, inst)).tipo, 'instrutor');
});

test('fila: quem foi marcado "Não assinou" sai da conta e da vez', () => {
  const marcado = etapa({ participantes: [participante('A', false, { naoAssinou: true, motivoNaoAssinou: 'Faltou' }), participante('B', true)], instrutorAssinou: true });
  const linhas = montarFilaAssinaturas(marcado, { trabalhadorId: 'inst', nome: 'Gilton' });
  assert.deepEqual(resumoFila(linhas), { feitos: 2, total: 2 });
  assert.equal(vezDaFila(linhas).tipo, 'todos');
});

test('pré-checagem: só a biometria do instrutor conta (sem aviso de ASO); servidor antigo não bloqueia', () => {
  assert.equal(situacaoBiometriaInstrutor({ temDigital: false, temFacial: false }), 'sem_biometria');
  assert.equal(situacaoBiometriaInstrutor({ temDigital: true, temFacial: false }), 'com_biometria');
  assert.equal(situacaoBiometriaInstrutor({ temDigital: false, temFacial: true }), 'com_biometria');
  assert.equal(situacaoBiometriaInstrutor({}), 'desconhecida');
  assert.equal(situacaoBiometriaInstrutor(null), 'desconhecida');
});

// ---- erros das rotas de assinatura ----

test('erros: já assinou (409), sem acesso (404) e recusa do servidor (400) seguem como vieram', () => {
  assert.equal(classificarErroAssinatura(new Error('O instrutor já assinou esta integração.')).tipo, 'ja_assinou');
  const sem = classificarErroAssinatura(new Error('Not Found (404)'), 'turma');
  assert.equal(sem.tipo, 'sem_acesso');
  assert.match(sem.texto, /a esta turma/);
  const recusa = classificarErroAssinatura(new Error('A digital lida não é a do instrutor desta integração.'));
  assert.equal(recusa.tipo, 'outro');
  assert.equal(recusa.texto, 'A digital lida não é a do instrutor desta integração.');
  assert.equal(classificarErroAssinatura('texto solto').texto, 'Não consegui assinar. Tente de novo.');
});

// ---- fotos automáticas: agenda por horário absoluto ----

test('fotos: a 1ª vence 60 s depois da ativação; a 2ª e a 3ª, 5 min depois da anterior', () => {
  const t0 = em('09:00');
  assert.deepEqual(proximaFotoDevida({ ativadoEm: t0, feitasEm: [] }, cfg), { indice: 0, devidaEm: t0 + 60_000 });
  const t1 = t0 + 61_000;
  assert.deepEqual(proximaFotoDevida({ ativadoEm: t0, feitasEm: [t1] }, cfg), { indice: 1, devidaEm: t1 + 300_000 });
  const t2 = t1 + 305_000;
  assert.deepEqual(proximaFotoDevida({ ativadoEm: t0, feitasEm: [t1, t2] }, cfg), { indice: 2, devidaEm: t2 + 300_000 });
  assert.equal(proximaFotoDevida({ ativadoEm: t0, feitasEm: [t1, t2, t2 + 1] }, cfg), null);
});

test('fotos: ao ativar com fotos já feitas, a próxima conta da ativação com o intervalo do passo (0 = 60 s, 1 = 300 s, 2 = 300 s)', () => {
  const t0 = em('09:00');
  assert.deepEqual(proximaFotoDevida(agendaAoAtivar(0, t0), cfg), { indice: 0, devidaEm: t0 + 60_000 });
  assert.deepEqual(proximaFotoDevida(agendaAoAtivar(1, t0), cfg), { indice: 1, devidaEm: t0 + 300_000 });
  assert.deepEqual(proximaFotoDevida(agendaAoAtivar(2, t0), cfg), { indice: 2, devidaEm: t0 + 300_000 });
  assert.equal(proximaFotoDevida(agendaAoAtivar(3, t0), cfg), null);
  assert.equal(agendaAoAtivar(9, t0).feitasEm.length, 3); // nunca passa do total
  assert.equal(situacaoAgenda(agendaAoAtivar(1, t0), cfg, t0 + 299_000).fase, 'aguardando');
  assert.equal(situacaoAgenda(agendaAoAtivar(1, t0), cfg, t0 + 300_000).fase, 'devida');
});

test('fotos: a situação depende só do horário (não de quantos ticks passaram)', () => {
  const t0 = em('09:00');
  const estado = { ativadoEm: t0, feitasEm: [] as number[] };
  assert.deepEqual(situacaoAgenda(estado, cfg, t0 + 18_000), { fase: 'aguardando', indice: 0, restanteSeg: 42, atrasoMs: 0 });
  assert.equal(formatarContagem(42), '00:42');
  assert.equal(situacaoAgenda(estado, cfg, t0 + 60_000).fase, 'devida');
  assert.equal(situacaoAgenda(estado, cfg, t0 + 60_000 + TOLERANCIA_ATRASO_MS).fase, 'devida');
  // Dormiu por 4 minutos: um único "tick" ao acordar já vê a foto como atrasada, sem disparar às cegas.
  const acordou = situacaoAgenda(estado, cfg, t0 + 60_000 + 240_000);
  assert.equal(acordou.fase, 'atrasada');
  assert.equal(acordou.atrasoMs, 240_000);
  assert.equal(textoAtraso(acordou.atrasoMs), '4 min');
  assert.equal(textoAtraso(20_000), '20 s');
  assert.equal(situacaoAgenda({ ativadoEm: t0, feitasEm: [1, 2, 3] }, cfg, t0).fase, 'concluida');
});

test('fotos: pausar desloca o prazo da foto pendente; retomar/tentar de novo reprograma para daqui a N segundos', () => {
  const t0 = em('09:00');
  const pausada = { ativadoEm: t0, feitasEm: [] as number[], pausaMs: 30_000 };
  assert.equal(proximaFotoDevida(pausada, cfg)?.devidaEm, t0 + 90_000);
  const agora = t0 + 600_000;
  const pausaMs = pausaParaVencerEm({ ativadoEm: t0, feitasEm: [] }, cfg, agora + 5_000);
  assert.equal(proximaFotoDevida({ ativadoEm: t0, feitasEm: [], pausaMs }, cfg)?.devidaEm, agora + 5_000);
  assert.equal(pausaParaVencerEm({ ativadoEm: t0, feitasEm: [1, 2, 3] }, cfg, agora), 0);
});

test('fotos: configuração inválida do servidor volta ao padrão e os intervalos aparecem escritos', () => {
  assert.deepEqual(configuracaoFotosSegura(null), cfg);
  assert.deepEqual(configuracaoFotosSegura({ primeiraAposSegundos: 0, segundaAposSegundos: -1, terceiraAposSegundos: 99999 }), cfg);
  assert.deepEqual(configuracaoFotosSegura({ primeiraAposSegundos: 10, segundaAposSegundos: 120, terceiraAposSegundos: 600 }), {
    primeiraAposSegundos: 10,
    segundaAposSegundos: 120,
    terceiraAposSegundos: 600,
  });
  assert.equal(resumoIntervalos(cfg), '1ª após 60 s · 2ª +5 min · 3ª +5 min');
});

test('fotos: erro de câmera (permissão x indisponível) e próximo quadro livre', () => {
  assert.equal(classificarFalhaCamera('NotAllowedError'), 'negada');
  assert.equal(classificarFalhaCamera('SecurityError'), 'negada');
  assert.equal(classificarFalhaCamera('NotReadableError'), 'indisponivel');
  assert.equal(classificarFalhaCamera('NotFoundError'), 'indisponivel');
  assert.equal(classificarFalhaCamera(null), 'indisponivel');
  assert.equal(proximaOrdemLivre([]), 1);
  assert.equal(proximaOrdemLivre([1, 3]), 2);
  assert.equal(proximaOrdemLivre([1, 2, 3]), null);
});
