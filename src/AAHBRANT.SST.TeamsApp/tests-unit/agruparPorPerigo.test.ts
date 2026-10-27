// Agrupamento do inventário de riscos por perigo (passo 4 do gerador de PGR pela EAP).
// Rodar: node --test tests-unit/
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { agruparPorPerigo } from '../src/pages/pgr/agruparPorPerigo.ts';

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
