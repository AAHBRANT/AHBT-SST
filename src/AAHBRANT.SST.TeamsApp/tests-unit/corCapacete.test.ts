// Regras puras da cor do capacete (Funções) e dos textos de óculos/cor nos alertas. Rodar: node --test tests-unit/
import { test } from 'node:test';
import assert from 'node:assert/strict';
import {
  CorCapacete,
  descreverAvisoCorCapacete,
  grupoPadraoCorCapacete,
  itensParaAplicar,
  montarLinhasPadraoCores,
  normalizarCorCapacete,
  resumoPadraoCores,
  rotuloBotaoAplicar,
  selecaoInicialPadraoCores,
  sugerirCorCapacetePorNome,
} from '../src/lib/corCapacete.ts';
import { descreverIrregularidade, ehSoAvisoDeCor, linhasNaoConferido, resumoCena } from '../src/pages/cameras/rotulosOcorrencia.ts';

test('normalizar: ausente, nulo ou fora da faixa vira Sem padrão', () => {
  assert.equal(normalizarCorCapacete(undefined), 0);
  assert.equal(normalizarCorCapacete(null), 0);
  assert.equal(normalizarCorCapacete(7), 0);
  assert.equal(normalizarCorCapacete(-1), 0);
  assert.equal(normalizarCorCapacete(2.5), 0);
  assert.equal(normalizarCorCapacete('2'), 0);
  assert.equal(normalizarCorCapacete(3), 3);
});

test('sugestão por nome: grupos do padrão AAHBRANT, sem acento nem maiúsculas', () => {
  assert.equal(sugerirCorCapacetePorNome('Engenheiro Civil'), CorCapacete.Branco);
  assert.equal(sugerirCorCapacetePorNome('MESTRE DE OBRAS'), CorCapacete.Branco);
  assert.equal(sugerirCorCapacetePorNome('Encarregado de Obras'), CorCapacete.Branco);
  assert.equal(sugerirCorCapacetePorNome('Pedreiro'), CorCapacete.Amarelo);
  assert.equal(sugerirCorCapacetePorNome('Operário'), CorCapacete.Amarelo);
  assert.equal(sugerirCorCapacetePorNome('Servente de Obras'), CorCapacete.Amarelo);
  assert.equal(sugerirCorCapacetePorNome('Eletricista'), CorCapacete.Azul);
  assert.equal(sugerirCorCapacetePorNome('Operador de Retroescavadeira'), CorCapacete.Azul);
  assert.equal(sugerirCorCapacetePorNome('Técnico de Segurança do Trabalho'), CorCapacete.Verde);
  assert.equal(sugerirCorCapacetePorNome('Brigadista'), CorCapacete.Vermelho);
  assert.equal(sugerirCorCapacetePorNome('Visitante'), CorCapacete.Laranja);
  assert.equal(sugerirCorCapacetePorNome('Almoxarife'), CorCapacete.SemPadrao);
  assert.equal(sugerirCorCapacetePorNome(''), CorCapacete.SemPadrao);
  assert.equal(sugerirCorCapacetePorNome(null), CorCapacete.SemPadrao);
});

const sugestoes = [
  { funcaoId: 'a', nome: 'Almoxarife', corAtual: 0, corSugerida: 0 },
  { funcaoId: 'b', nome: 'Instalador de Drywall', corAtual: 2, corSugerida: 2 },
  { funcaoId: 'c', nome: 'Pedreiro', corAtual: 1, corSugerida: 2 },
  { funcaoId: 'd', nome: 'Eletricista', corSugerida: 3 },
  { funcaoId: 'e', nome: 'Técnico de Segurança do Trabalho', corAtual: 3, corSugerida: 4, grupo: 'Segurança' },
];

test('quadro de conferência: situação de cada linha e ordem (muda, sem sugestão, já no padrão)', () => {
  const linhas = montarLinhasPadraoCores(sugestoes);
  assert.deepEqual(
    linhas.map((l) => [l.funcaoId, l.situacao]),
    [
      ['c', 'muda'],
      ['d', 'muda'],
      ['e', 'muda'],
      ['a', 'semSugestao'],
      ['b', 'jaNoPadrao'],
    ],
  );
  // Campo ausente = Sem padrão; grupo do servidor vale, senão o grupo da cor.
  assert.equal(linhas.find((l) => l.funcaoId === 'd')?.corAtual, 0);
  assert.equal(linhas.find((l) => l.funcaoId === 'e')?.grupo, 'Segurança');
  assert.equal(linhas.find((l) => l.funcaoId === 'c')?.grupo, grupoPadraoCorCapacete(2));
});

test('quadro de conferência: só as linhas que mudam começam marcadas e só elas são aplicadas', () => {
  const linhas = montarLinhasPadraoCores(sugestoes);
  const marcadas = selecaoInicialPadraoCores(linhas);
  assert.deepEqual([...marcadas].sort(), ['c', 'd', 'e']);
  assert.deepEqual(itensParaAplicar(linhas, marcadas), [
    { funcaoId: 'c', cor: 2 },
    { funcaoId: 'd', cor: 3 },
    { funcaoId: 'e', cor: 4 },
  ]);
  // Desmarcar uma linha tira ela do envio; marcar uma linha sem sugestão não envia nada.
  assert.deepEqual(itensParaAplicar(linhas, new Set(['d', 'a', 'b'])), [{ funcaoId: 'd', cor: 3 }]);
  assert.deepEqual(itensParaAplicar(linhas, new Set()), []);
});

test('quadro de conferência: resumo e botão com singular e plural', () => {
  const linhas = montarLinhasPadraoCores(sugestoes);
  const todas = selecaoInicialPadraoCores(linhas);
  assert.equal(resumoPadraoCores(linhas, todas), '3 funções serão atualizadas. 1 já está no padrão e 1 fica como está.');
  assert.equal(resumoPadraoCores(linhas, new Set(['c'])), '1 função será atualizada. 1 já está no padrão e 1 fica como está.');
  assert.equal(rotuloBotaoAplicar(11), 'Aplicar a 11 funções');
  assert.equal(rotuloBotaoAplicar(1), 'Aplicar a 1 função');
  assert.equal(rotuloBotaoAplicar(0), 'Aplicar');
  const plurais = montarLinhasPadraoCores([
    { funcaoId: '1', nome: 'X', corSugerida: 0 },
    { funcaoId: '2', nome: 'Y', corSugerida: 0 },
    { funcaoId: '3', nome: 'Pedreiro', corAtual: 2, corSugerida: 2 },
    { funcaoId: '4', nome: 'Pintor', corAtual: 2, corSugerida: 2 },
  ]);
  assert.equal(
    resumoPadraoCores(plurais, new Set()),
    '0 funções serão atualizadas. 2 já estão no padrão e 2 ficam como estão.',
  );
});

test('aviso de cor: texto do cartão e quando faltam dados', () => {
  assert.equal(descreverAvisoCorCapacete(3, 2, 'Pedreiro'), 'Capacete azul — esperado amarelo (Pedreiro)');
  assert.equal(descreverAvisoCorCapacete(3, 2), 'Capacete azul — esperado amarelo');
  assert.equal(descreverAvisoCorCapacete(null, 2, 'Pedreiro'), 'Cor do capacete diferente do padrão — esperado amarelo (Pedreiro)');
});

test('selo de irregularidade combina capacete, colete e óculos', () => {
  assert.equal(descreverIrregularidade(0, 0), 'Cena regular');
  assert.equal(descreverIrregularidade(1, 0), 'Sem capacete');
  assert.equal(descreverIrregularidade(0, 1), 'Sem colete');
  assert.equal(descreverIrregularidade(0, 0, 2), 'Sem óculos');
  assert.equal(descreverIrregularidade(1, 1), 'Sem capacete e sem colete');
  assert.equal(descreverIrregularidade(1, 0, 1), 'Sem capacete e sem óculos');
  assert.equal(descreverIrregularidade(1, 1, 1), 'Sem capacete, sem colete e sem óculos');
  // Chamadas antigas (dois argumentos) seguem iguais.
  assert.equal(descreverIrregularidade(2, 3), 'Sem capacete e sem colete');
});

test('resumo da cena inclui óculos e não conferidos', () => {
  assert.equal(resumoCena(3, 0, 0, 0, 0, 1), '3 pessoas na cena, 1 sem óculos');
  assert.equal(resumoCena(4, 1, 1), '4 pessoas na cena, 1 sem capacete, 1 sem colete');
  assert.equal(resumoCena(3, 0, 0, 0, 0, 0, 2), '3 pessoas na cena, 2 óculos não conferidos');
  assert.equal(resumoCena(3, 0, 0, 0, 0, 0, 1), '3 pessoas na cena, 1 óculos não conferido');
});

test('linhas de não conferido: só aparecem quando há pessoas', () => {
  assert.deepEqual(linhasNaoConferido(), []);
  assert.deepEqual(linhasNaoConferido(1), ['Capacete não conferido em 1 pessoa']);
  assert.deepEqual(linhasNaoConferido(0, 2), ['Colete não conferido em 2 pessoas']);
  assert.deepEqual(linhasNaoConferido(0, 0, 2), ['Óculos não conferido em 2 pessoas (longe ou de costas)']);
  assert.equal(linhasNaoConferido(1, 1, 1).length, 3);
});

test('sugestão por nome: casos que divergiam do servidor (ordem das regras de SugestaoCorCapacete.cs)', () => {
  const casos: [string, number][] = [
    ['Engenheiro de Segurança do Trabalho', CorCapacete.Branco],
    ['Ajudante de Eletricista', CorCapacete.Amarelo],
    ['TST', CorCapacete.Verde],
    ['Tec. de Segurança', CorCapacete.Verde],
    ['Técnico em Segurança do Trabalho', CorCapacete.Verde],
    ['Técnico em Edificações', CorCapacete.Azul],
    ['Gesseiro', CorCapacete.Amarelo],
    ['Soldador', CorCapacete.Amarelo],
    ['Serralheiro', CorCapacete.Amarelo],
    ['Montador de Andaimes', CorCapacete.Amarelo],
    ['Bombeiro Civil', CorCapacete.Vermelho],
    ['Bombeiro Hidráulico', CorCapacete.Azul],
    ['Brigadista de Emergência', CorCapacete.Vermelho],
    ['Gerente de Obras', CorCapacete.Branco],
    ['Técnico Ambiental', CorCapacete.Verde],
    ['Visitante', CorCapacete.Laranja],
    ['Trabalhador Temporário', CorCapacete.Laranja],
    ['Almoxarife', CorCapacete.SemPadrao],
    ['Instalador de Drywall', CorCapacete.SemPadrao],
    // Começo da palavra: "instalador" não casa "tst"; "Estagiário" não casa "tst" no meio da palavra.
    ['Estagiário de Engenharia', CorCapacete.SemPadrao],
    ['Pedreiro-Oficial', CorCapacete.Amarelo],
  ];
  for (const [nome, cor] of casos) assert.equal(sugerirCorCapacetePorNome(nome), cor, nome);
});

test('quadro: função sem sugestão só entra no envio com cor escolhida e marcada', () => {
  const linhas = montarLinhasPadraoCores(sugestoes);
  const marcadas = selecaoInicialPadraoCores(linhas);
  // Almoxarife ('a') sem escolha: não vai.
  assert.equal(itensParaAplicar(linhas, new Set([...marcadas, 'a']), {}).some((i) => i.funcaoId === 'a'), false);
  // Com escolha e marcada: vai com a cor escolhida.
  const comEscolha = itensParaAplicar(linhas, new Set([...marcadas, 'a']), { a: 5 });
  assert.deepEqual(comEscolha.find((i) => i.funcaoId === 'a'), { funcaoId: 'a', cor: 5 });
  // Com escolha mas desmarcada: não vai. Escolha igual à cor de hoje: não vai.
  assert.equal(itensParaAplicar(linhas, marcadas, { a: 5 }).some((i) => i.funcaoId === 'a'), false);
  assert.equal(itensParaAplicar(linhas, new Set(['a']), { a: 0 }).length, 0);
  assert.equal(
    resumoPadraoCores(linhas, new Set([...marcadas, 'a']), { a: 5 }),
    '4 funções serão atualizadas. 1 já está no padrão.',
  );
});

test('aviso só de cor: ocorrência sem falta de EPI e com o aviso ligado', () => {
  assert.equal(ehSoAvisoDeCor({ situacao: 'ocorrencia', semCapacete: 0, semColete: 0, avisoCorCapacete: true }), true);
  assert.equal(ehSoAvisoDeCor({ situacao: 'ocorrencia', semCapacete: 0, semColete: 0, semOculos: 1, avisoCorCapacete: true }), false);
  assert.equal(ehSoAvisoDeCor({ situacao: 'ocorrencia', semCapacete: 1, semColete: 0, avisoCorCapacete: true }), false);
  assert.equal(ehSoAvisoDeCor({ situacao: 'ocorrencia', semCapacete: 0, semColete: 0 }), false);
  assert.equal(ehSoAvisoDeCor({ situacao: 'regular', semCapacete: 0, semColete: 0, avisoCorCapacete: true }), false);
});
