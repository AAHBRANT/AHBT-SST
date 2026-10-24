// Testes de estresse do rastreador de localização das fotos.
// Rodar: node --test tests-unit/   (Node 23.6+ executa TypeScript direto, sem build)
import { test, mock, beforeEach, afterEach } from 'node:test';
import assert from 'node:assert/strict';
import {
  criarRastreador, normalizarMomento, type FonteLocalizacao, type LeituraLocalizacao,
} from '../src/lib/rastreadorLocalizacao.ts';

const T0 = Date.UTC(2026, 9, 8, 12, 0, 0);
beforeEach(() => { mock.timers.enable({ apis: ['setTimeout', 'Date'], now: T0 }); });
afterEach(() => { mock.timers.reset(); });
const avancar = (ms: number) => mock.timers.tick(ms);

type Passo = { em: number; ler?: Partial<LeituraLocalizacao> & { timestampInformado?: number }; falhar?: string };
// Fonte simulada: executa um roteiro de leituras/falhas em instantes fixos (ms após iniciar).
function fonte(nome: string, roteiro: Passo[]) {
  const registro = { parada: false };
  const f: FonteLocalizacao = {
    nome,
    iniciar(aoLer, aoFalhar) {
      const timers = roteiro.map(p => setTimeout(() => {
        if (p.falhar) return aoFalhar(p.falhar);
        const agora = Date.now();
        aoLer({ latitude: -7.1, longitude: -34.8, precisaoMetros: 15, fonte: nome, ...p.ler,
          obtidaEm: normalizarMomento(p.ler?.timestampInformado ?? agora, agora) });
      }, p.em));
      return () => { registro.parada = true; timers.forEach(clearTimeout); };
    },
  };
  return Object.assign(f, { registro });
}
const nunca = (nome: string) => fonte(nome, []);

test('iPhone: SDK do Teams trava e GPS do WebView leva 25 s — antes estourava em 20 s, agora resolve', async () => {
  const r = criarRastreador({ fontes: [nunca('teams'), fonte('navegador', [{ em: 25_000 }])] });
  const p = r.aguardarPronta(45_000);
  avancar(20_000);
  assert.equal(r.estado().status, 'buscando');
  avancar(5_000);
  const l = await p;
  assert.equal(l?.fonte, 'navegador');
  assert.equal(r.estado().status, 'pronta');
  r.parar();
});

test('SDK do Teams lança erro na hora e o navegador responde em 3 s', async () => {
  const r = criarRastreador({ fontes: [fonte('teams', [{ em: 0, falhar: 'SDK falhou' }]), fonte('navegador', [{ em: 3_000 }])] });
  const p = r.aguardarPronta(10_000);
  avancar(3_000);
  assert.equal((await p)?.fonte, 'navegador');
  r.parar();
});

test('as duas fontes em paralelo: vence a que chegar primeiro, sem esperar a outra', async () => {
  const r = criarRastreador({ fontes: [fonte('teams', [{ em: 2_000 }]), fonte('navegador', [{ em: 9_000 }])] });
  const p = r.aguardarPronta(30_000);
  avancar(2_000);
  assert.equal((await p)?.fonte, 'teams');
  r.parar();
});

test('primeira posição imprecisa (300 m) e depois boa (20 m): fica "imprecisa" e então "pronta"', async () => {
  const r = criarRastreador({ fontes: [fonte('navegador', [{ em: 2_000, ler: { precisaoMetros: 300 } }, { em: 9_000, ler: { precisaoMetros: 20 } }])] });
  const p = r.aguardarPronta(30_000);
  avancar(2_000);
  assert.equal(r.estado().status, 'imprecisa');
  assert.equal(r.estado().leitura?.precisaoMetros, 300);
  avancar(7_000);
  assert.equal((await p)?.precisaoMetros, 20);
  r.parar();
});

test('leitura precisa não é trocada por uma imprecisa que chega depois', () => {
  const r = criarRastreador({ fontes: [fonte('teams', [{ em: 1_000, ler: { precisaoMetros: 10 } }]), fonte('navegador', [{ em: 2_000, ler: { precisaoMetros: 900 } }])] });
  avancar(2_000);
  assert.equal(r.estado().status, 'pronta');
  assert.equal(r.estado().leitura?.precisaoMetros, 10);
  r.parar();
});

test('timestamp do Teams em SEGUNDOS é normalizado (antes virava 1970 e reprovava a validação de 60 s)', () => {
  const r = criarRastreador({ fontes: [fonte('teams', [{ em: 1_000, ler: { timestampInformado: Math.floor((T0 + 1_000) / 1000) } }])] });
  avancar(1_000);
  const e = r.estado();
  assert.equal(e.status, 'pronta');
  assert.ok(Math.abs(e.leitura!.obtidaEm - (T0 + 1_000)) < 1_000);
  r.parar();
});

test('timestamp absurdo (cache antigo / relógio errado) usa o momento da chegada', () => {
  assert.equal(normalizarMomento(T0 - 3_600_000, T0), T0);
  assert.equal(normalizarMomento(T0 + 3_600_000, T0), T0);
  assert.equal(normalizarMomento(0, T0), T0);
  assert.equal(normalizarMomento(undefined, T0), T0);
  assert.equal(normalizarMomento(T0 - 4_000, T0), T0 - 4_000);
});

test('permissão negada nas duas fontes: falha na hora com motivo de permissão, sem esperar teto', async () => {
  const r = criarRastreador({ fontes: [
    fonte('teams', [{ em: 0, falhar: 'O Teams não conseguiu obter a localização.' }]),
    fonte('navegador', [{ em: 0, falhar: 'Permissão de localização bloqueada.' }]),
  ] });
  avancar(0);
  const e = r.estado();
  assert.equal(e.status, 'falhou');
  assert.match(e.motivo!, /Permiss/);
  assert.equal(await r.aguardarPronta(60_000), null);
  r.parar();
});

test('uma fonte falha e a outra ainda busca: NÃO declara falha antes da hora', () => {
  const r = criarRastreador({ fontes: [fonte('teams', [{ em: 0, falhar: 'erro' }]), nunca('navegador')] });
  avancar(10_000);
  assert.equal(r.estado().status, 'buscando');
  r.parar();
});

test('posição envelhece: depois de 30 s sem leitura nova ela não é mais usada', () => {
  const r = criarRastreador({ fontes: [fonte('navegador', [{ em: 0 }])] });
  avancar(0);
  assert.equal(r.estado().status, 'pronta');
  avancar(31_000);
  assert.equal(r.estado().status, 'buscando');
  r.parar();
});

test('rastreio contínuo: leituras periódicas mantêm a posição sempre fresca por 3 minutos', () => {
  const roteiro = Array.from({ length: 36 }, (_, i) => ({ em: i * 5_000 }));
  const r = criarRastreador({ fontes: [fonte('navegador', roteiro)] });
  for (let i = 0; i < 36; i++) { avancar(5_000); assert.equal(r.estado().status, 'pronta', `t=${(i + 1) * 5}s`); }
  r.parar();
});

test('PC sem GPS (Wi-Fi/IP, 1500 m): nunca vira "pronta" e o teto devolve null em vez de travar', async () => {
  const r = criarRastreador({ fontes: [fonte('navegador', [{ em: 1_000, ler: { precisaoMetros: 1_500 } }])] });
  const p = r.aguardarPronta(10_000);
  avancar(10_000);
  assert.equal(await p, null);
  assert.equal(r.estado().status, 'imprecisa');
  r.parar();
});

test('parar() encerra as fontes e ignora respostas atrasadas', () => {
  const nav = fonte('navegador', [{ em: 5_000 }]);
  const r = criarRastreador({ fontes: [nav] });
  const estados: string[] = [];
  r.assinar(e => estados.push(e.status));
  r.parar();
  avancar(10_000);
  assert.equal(nav.registro.parada, true);
  assert.deepEqual(estados, ['buscando']);
});

test('fonte que lança ao iniciar não derruba o rastreador', async () => {
  const quebrada: FonteLocalizacao = { nome: 'teams', iniciar() { throw new Error('boom'); } };
  const r = criarRastreador({ fontes: [quebrada, fonte('navegador', [{ em: 1_000 }])] });
  const p = r.aguardarPronta(5_000);
  avancar(1_000);
  assert.equal((await p)?.fonte, 'navegador');
  r.parar();
});

test('estresse: 200 rastreadores abrindo/fechando com fontes aleatórias não vazam nem travam', async () => {
  let resolvidos = 0, esperados = 0;
  for (let i = 0; i < 200; i++) {
    const atraso = (i * 7919) % 40_000;
    const r = criarRastreador({ fontes: [
      i % 3 === 0 ? fonte('teams', [{ em: atraso % 9_000, falhar: 'erro' }]) : nunca('teams'),
      fonte('navegador', [{ em: atraso, ler: { precisaoMetros: (i % 5) * 40 } }]),
    ] });
    if (atraso < 30_000 && (i % 5) * 40 <= 100) esperados++;
    const p = r.aguardarPronta(30_000);
    avancar(30_000);
    if (await p) resolvidos++;
    r.parar();
  }
  // Aceita exatamente as que chegaram dentro do teto com precisão <= 100 m; nenhuma trava.
  assert.equal(resolvidos, esperados);
  assert.ok(esperados > 50);
});
