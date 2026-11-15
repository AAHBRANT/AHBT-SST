// Regras de alerta da câmera (Etapa 3) e textos de confirmação. Rodar: node --test tests-unit/
import { test } from 'node:test';
import assert from 'node:assert/strict';
import {
  ajudaNaoRepetir,
  ajudaPersistencia,
  ehNaoConfirmada,
  ehNaoConfirmadaSemSombra,
  equivalenteEmMinutos,
  estadoDoAviso,
  fraseResultado,
  LIMITE_NAO_REPETIR,
  LIMITE_PERSISTENCIA,
  lerSegundos,
  linhaConfirmacao,
  minimoDaOcorrencia,
  resumoSombra,
} from '../src/pages/cameras/regrasAlerta.ts';

test('lerSegundos respeita os limites de cada campo', () => {
  assert.equal(lerSegundos('3', LIMITE_PERSISTENCIA), 3);
  assert.equal(lerSegundos('1', LIMITE_PERSISTENCIA), 1);
  assert.equal(lerSegundos('30', LIMITE_PERSISTENCIA), 30);
  assert.equal(lerSegundos('0', LIMITE_PERSISTENCIA), null);
  assert.equal(lerSegundos('31', LIMITE_PERSISTENCIA), null);
  assert.equal(lerSegundos('', LIMITE_PERSISTENCIA), null);
  assert.equal(lerSegundos('2,5', LIMITE_PERSISTENCIA), null);
  assert.equal(lerSegundos('-1', LIMITE_NAO_REPETIR), null);
  assert.equal(lerSegundos('0', LIMITE_NAO_REPETIR), 0);
  assert.equal(lerSegundos('3600', LIMITE_NAO_REPETIR), 3600);
  assert.equal(lerSegundos('3601', LIMITE_NAO_REPETIR), null);
});

test('ajudas dos campos acompanham o número', () => {
  assert.equal(ajudaPersistencia('capacete', 3), 'A pessoa precisa aparecer sem capacete por pelo menos 3 segundos para gerar alerta.');
  assert.equal(ajudaPersistencia('oculos', 1), 'A pessoa precisa aparecer sem óculos por pelo menos 1 segundo para gerar alerta.');
  assert.match(ajudaNaoRepetir(300), /300 segundos \(5 minutos\)/);
  assert.match(ajudaNaoRepetir(90), /\(1,5 minutos\)/);
  assert.match(ajudaNaoRepetir(0), /Desligado/);
  assert.equal(equivalenteEmMinutos(60), '1 minuto');
});

test('o modo sombra manda sobre o Telegram e a escolha é preservada', () => {
  assert.equal(estadoDoAviso(true, true), 'sombra');
  assert.equal(estadoDoAviso(true, false), 'sombra');
  assert.equal(estadoDoAviso(false, true), 'avisa');
  assert.equal(estadoDoAviso(false, false), 'mudo');
});

test('frase de resultado conforme a combinação', () => {
  const iguais = { capacete: 3, colete: 3, oculos: 3 };
  const semAviso = 'Esta câmera vai registrar as ocorrências no sistema, sem enviar aviso no Telegram.';
  assert.equal(fraseResultado('sombra', iguais), semAviso);
  assert.equal(fraseResultado('mudo', iguais), semAviso);
  assert.equal(
    fraseResultado('avisa', iguais),
    'Esta câmera vai avisar no Telegram depois de 3 segundos sem capacete, sem colete ou sem óculos.',
  );
  assert.equal(
    fraseResultado('avisa', { capacete: 3, colete: 5, oculos: 4 }),
    'Esta câmera vai avisar no Telegram depois de 3 segundos sem capacete, 5 segundos sem colete ou 4 segundos sem óculos.',
  );
  assert.equal(
    fraseResultado('avisa', { capacete: 1, colete: 1, oculos: 1 }),
    'Esta câmera vai avisar no Telegram depois de 1 segundo sem capacete, sem colete ou sem óculos.',
  );
});

test('confirmação mostra tempo observado e mínimo da câmera', () => {
  const camera = { persistenciaCapaceteSegundos: 3, persistenciaColeteSegundos: 10 };
  const sim = { semCapacete: 1, semColete: 0, confirmada: true, sombra: true, segundosObservados: 5 };
  assert.deepEqual(linhaConfirmacao(sim, camera, 'sem capacete'), {
    confirmada: true,
    rotulo: 'Confirmada',
    detalhe: 'sem capacete por 5 s (mínimo 3 s)',
  });
  const nao = { semCapacete: 1, semColete: 1, confirmada: false, sombra: true, segundosObservados: 1 };
  assert.equal(minimoDaOcorrencia(nao, camera), 10);
  assert.deepEqual(linhaConfirmacao(nao, camera, 'sem capacete e sem colete'), {
    confirmada: false,
    rotulo: 'Não confirmada',
    detalhe: 'sem capacete e sem colete por 1 s (mínimo 10 s)',
  });
});

test('confirmação sem dados do servidor ou da câmera não inventa número', () => {
  const o = { semCapacete: 1, semColete: 0 };
  assert.deepEqual(linhaConfirmacao(o, undefined, 'sem capacete'), { confirmada: true, rotulo: 'Confirmada', detalhe: '' });
  assert.equal(linhaConfirmacao({ ...o, segundosObservados: 4 }, undefined, 'sem capacete').detalhe, 'sem capacete por 4 s');
  assert.equal(minimoDaOcorrencia({ semCapacete: 0, semColete: 0 }, {}), null);
});

test('não confirmada de câmera com aviso é reconhecida; resumo de modo sombra', () => {
  assert.equal(ehNaoConfirmadaSemSombra({ semCapacete: 1, semColete: 0, confirmada: false }), true);
  assert.equal(ehNaoConfirmadaSemSombra({ semCapacete: 1, semColete: 0, confirmada: false, sombra: true }), false);
  assert.equal(ehNaoConfirmadaSemSombra({ semCapacete: 1, semColete: 0 }), false);
  assert.equal(resumoSombra(5), '5 em modo sombra');
});

test('aviso de cor do capacete nunca aparece como não confirmado', () => {
  const cor = { semCapacete: 0, semColete: 0, avisoCorCapacete: true, confirmada: false, sombra: true, segundosObservados: 1 };
  assert.equal(ehNaoConfirmada(cor), false);
  assert.equal(linhaConfirmacao(cor, undefined, 'cor do capacete').rotulo, 'Confirmada');
  assert.equal(ehNaoConfirmada({ ...cor, semCapacete: 1 }), true);
});

test('ajuda do não repetir fala só de quem não foi identificado', () => {
  assert.equal(
    ajudaNaoRepetir(300),
    'Quem não foi identificado não gera outro alerta por 300 segundos (5 minutos) depois de um alerta. Para trabalhador identificado vale sempre 1 alerta por hora.',
  );
  assert.equal(
    ajudaNaoRepetir(0),
    'Desligado: quem não foi identificado pode gerar alerta a cada análise, respeitando o intervalo entre alertas da câmera. Para trabalhador identificado vale sempre 1 alerta por hora.',
  );
});
