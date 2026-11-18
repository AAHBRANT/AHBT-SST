// "Adicionar risco" e aviso de trabalho em altura do inventário do PGR pela EAP (17/11/2026).
// Rodar: node --test tests-unit/
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { chaveAltura, costumaTrabalharEmAltura, incluirRisco, PERIGOS_PADRAO, precisaConfirmarAltura } from '../src/pages/pgr/riscoManual.ts';

const risco = (perigo: string, excluido = false) => ({
  id: perigo, tipo: 'Acidente', perigo, danos: '', probabilidade: 2, severidade: 2, base: [], medidas: [], revisado: true, excluido,
  exposicao: { criterio: 'Qualitativo', medicoes: [] },
});
const atividade = (codigoEap: string, riscos: unknown[] = [], riscosGerados = true) => ({ codigoEap, nome: 'Atv ' + codigoEap, riscosGerados, riscos });
const ghe = (funcoes: string[], atividades: unknown[], extra: object = {}) => ({
  numero: 5, nome: 'Paisagismo', funcoes: funcoes.map((nome) => ({ nome, sugeridaPelaIa: false })), atividades, ...extra,
});

test('o catálogo do front é o mesmo de PerigosPadrao.Catalogo (C#), na mesma ordem', () => {
  const cs = readFileSync(new URL('../../AAHBRANT.SST.Application/GeracaoPgr/PerigosPadrao.cs', import.meta.url), 'utf8');
  const bloco = cs.slice(cs.indexOf('Catalogo ='), cs.indexOf('};', cs.indexOf('Catalogo =')));
  const doCs = [...bloco.matchAll(/\("([^"]+)", "([^"]+)"\)/g)].map((m) => ({ tipo: m[1], nome: m[2] }));
  assert.ok(doCs.length > 20);
  assert.deepEqual(PERIGOS_PADRAO, doCs);
});

test('reconhece os ofícios que costumam trabalhar em altura, como o servidor', () => {
  for (const f of ['Pedreiro', 'CARPINTEIRO', 'Armador', 'Pintor', 'Serralheiro', 'Ajudante de Eletricista', 'Montador de Andaime']) assert.equal(costumaTrabalharEmAltura(f), true, f);
  for (const f of ['Ajudante Geral', 'Jardineiro', 'Operador de Escavadeira Hidráulica', 'Engenheiro Civil', '', null]) assert.equal(costumaTrabalharEmAltura(f), false, String(f));
});

test('a chave ignora ordem, acento e caixa, e muda com função ou atividade nova', () => {
  const a = ghe(['Serralheiro', 'PEDREIRO'], [atividade('5.2'), atividade('5.1')]);
  assert.equal(chaveAltura(a as never), 'pedreiro|serralheiro#5.1|5.2');
  assert.equal(chaveAltura(ghe(['  pedreiro', 'Serralhéiro'], [atividade('5.1'), atividade('5.2')]) as never), chaveAltura(a as never));
  assert.notEqual(chaveAltura(ghe(['Serralheiro', 'PEDREIRO', 'Pintor'], [atividade('5.2'), atividade('5.1')]) as never), chaveAltura(a as never));
  assert.notEqual(chaveAltura(ghe(['Serralheiro', 'PEDREIRO'], [atividade('5.2'), atividade('5.1'), atividade('5.3')]) as never), chaveAltura(a as never));
});

test('o aviso aparece só com função de altura, sem Queda de altura, depois de gerar os riscos', () => {
  assert.equal(precisaConfirmarAltura(ghe(['Pedreiro'], [atividade('5.1', [risco('Ruído')])]) as never), true);
  assert.equal(precisaConfirmarAltura(ghe(['Ajudante Geral'], [atividade('5.1', [risco('Ruído')])]) as never), false);
  assert.equal(precisaConfirmarAltura(ghe(['Pedreiro'], [atividade('5.1', [risco('QUEDA  de altura')])]) as never), false);
  // Queda excluída não conta como avaliada.
  assert.equal(precisaConfirmarAltura(ghe(['Pedreiro'], [atividade('5.1', [risco('Queda de altura', true)])]) as never), true);
  // Riscos ainda não gerados: a IA pode incluir a queda, não avisa.
  assert.equal(precisaConfirmarAltura(ghe(['Pedreiro'], [atividade('5.1', [], false)]) as never), false);
});

test('"Não se aplica" some com o aviso e ele volta quando funções ou atividades mudam', () => {
  const g = ghe(['Pedreiro'], [atividade('5.1', [risco('Ruído')])]);
  (g as { alturaNaoSeAplicaChave?: string }).alturaNaoSeAplicaChave = chaveAltura(g as never);
  assert.equal(precisaConfirmarAltura(g as never), false);
  g.funcoes.push({ nome: 'Pintor', sugeridaPelaIa: false });
  assert.equal(precisaConfirmarAltura(g as never), true);
});

test('incluir risco cria um risco revisado, sem medidas, só nas atividades marcadas, sem duplicar perigo', () => {
  const atividades = [atividade('5.1', [risco('Ruído')]), atividade('5.2', [risco('Queda de altura')]), atividade('5.3')];
  const dados = { perigo: ' Queda de altura ', tipo: 'Acidente', danos: 'Fraturas', probabilidade: 3, severidade: 4 };
  const r = incluirRisco(atividades as never, ['5.1', '5.2'], dados);
  assert.deepEqual(r.jaTinham, ['5.2']);
  assert.equal(atividades[0].riscos.length, 2);
  assert.equal(atividades[1].riscos.length, 1);
  assert.equal(atividades[2].riscos.length, 0);
  const novo = atividades[0].riscos[1] as ReturnType<typeof risco> & { incluidoPeloTecnico: boolean };
  assert.equal(novo.perigo, 'Queda de altura');
  assert.equal(novo.incluidoPeloTecnico, true);
  assert.equal(novo.revisado, true);
  assert.deepEqual(novo.medidas, []);
  assert.deepEqual(novo.base, []);
  assert.match(novo.id, /^[0-9a-f]{32}$/);
});
