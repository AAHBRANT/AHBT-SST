// Agrupamento do inventário de riscos por perigo (passo 4 do gerador de PGR pela EAP).
// Rodar: node --test tests-unit/
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { agruparPorPerigo, compararComPadrao } from '../src/pages/pgr/agruparPorPerigo.ts';

const medida = (texto: string) => ({ fase: 2, texto, categoria: 'EPC', catalogoEpiId: null, decisao: 'Pendente' });
const risco = (perigo: string, p: number, s: number, medidas: string[], excluido = false) => ({
  id: perigo + p + s, tipo: 'Acidente', perigo, danos: '', probabilidade: p, severidade: s, base: [], revisado: false, excluido,
  medidas: medidas.map(medida),
});
const atividade = (codigoEap: string, riscos: unknown[]) => ({ codigoEap, nome: 'Atv ' + codigoEap, riscosGerados: true, riscos });

test('agrupa o mesmo perigo ignorando acento, caixa e espaços, e ignora risco excluído', () => {
  const gh = {
    numero: 1, nome: 'Fundações', funcoes: [], atividades: [
      atividade('4.1', [risco('Queda de altura', 3, 5, ['a'])]),
      atividade('4.2', [risco('QUEDA  DE ALTURA', 3, 5, ['a']), risco('Ruído', 2, 2, ['b'])]),
      atividade('4.3', [risco('Queda de altura', 1, 1, ['x'], true), risco('Ruido', 2, 2, ['b'])]),
    ],
  };
  const grupos = agruparPorPerigo(gh as never);
  assert.equal(grupos.length, 2);
  assert.equal(grupos[0].nome, 'Queda de altura');
  assert.deepEqual(grupos[0].itens.map((i) => i.atividade.codigoEap), ['4.1', '4.2']);
  assert.equal(grupos[1].itens.length, 2);
});

test('o padrão é o P×S mais frequente e o grupo de maior nível vem primeiro', () => {
  const gh = {
    numero: 1, nome: 'X', funcoes: [], atividades: [
      atividade('1', [risco('Choque elétrico', 2, 5, ['m'])]),
      atividade('2', [risco('Choque elétrico', 2, 3, ['m'])]),
      atividade('3', [risco('Choque elétrico', 2, 3, ['m'])]),
      atividade('4', [risco('Calor', 1, 2, ['c'])]),
    ],
  };
  const grupos = agruparPorPerigo(gh as never);
  assert.equal(grupos[0].nome, 'Choque elétrico');
  assert.equal(grupos[0].padrao.r.probabilidade * grupos[0].padrao.r.severidade, 6);
  assert.equal(grupos[1].nome, 'Calor');
});

const m = (fase: number, categoria: string, texto: string) => ({ fase, texto, categoria, catalogoEpiId: null, decisao: 'Pendente' });
const comMedidas = (p: number, s: number, medidas: ReturnType<typeof m>[]) => ({ ...risco('Soterramento', p, s, []), medidas });

test('redação diferente com a mesma ideia, fase e categoria conta como igual ao padrão', () => {
  const padrao = comMedidas(4, 5, [m(2, 'EPC', 'Mantenha escada ou rampa de saída a menos de 15 m de cada trabalhador na vala')]);
  const outro = comMedidas(4, 5, [m(2, 'EPC', 'Mantenha a escada ou rampa de saída a menos de 15 m de quem trabalha na vala e saia ao ver trincas')]);
  assert.equal(compararComPadrao(padrao as never, outro as never).igual, true);
});

test('número diferente no texto é diferença: 7 m e 15 m mudam a regra', () => {
  const padrao = comMedidas(4, 5, [m(2, 'EPC', 'Mantenha escada ou rampa de saída a menos de 15 m de cada trabalhador na vala')]);
  const outro = comMedidas(4, 5, [m(2, 'EPC', 'Mantenha escada ou rampa de saída a menos de 7 m de cada trabalhador na vala')]);
  const d = compararComPadrao(padrao as never, outro as never);
  assert.equal(d.igual, false);
  assert.equal(d.aMais.length, 1);
  assert.equal(d.aMenos.length, 1);
});

test('mesma frase em categoria ou fase diferente não é a mesma medida', () => {
  const padrao = comMedidas(3, 3, [m(2, 'EPC', 'Use capacete de segurança com jugular durante todo o tempo na vala')]);
  const outro = comMedidas(3, 3, [m(2, 'EPI', 'Use capacete de segurança com jugular durante todo o tempo na vala')]);
  assert.equal(compararComPadrao(padrao as never, outro as never).igual, false);
});

test('aponta nível diferente e medida a mais ou a menos', () => {
  const padrao = comMedidas(4, 5, [m(1, 'EPC', 'Isole a área e sinalize antes de começar o serviço'), m(2, 'EPI', 'Use capacete com jugular durante todo o serviço')]);
  const outro = comMedidas(3, 5, [m(1, 'EPC', 'Isole a área e sinalize antes de começar o serviço'), m(2, 'EPI', 'Use colete refletivo em toda a frente de serviço da obra')]);
  const d = compararComPadrao(padrao as never, outro as never);
  assert.deepEqual(d.nivel, { atividade: 15, padrao: 20 });
  assert.equal(d.aMais.length, 1);
  assert.equal(d.aMenos.length, 1);
  assert.match(d.aMais[0].texto, /colete/);
});
