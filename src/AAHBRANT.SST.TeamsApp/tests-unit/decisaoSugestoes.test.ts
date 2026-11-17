// Regra pura de quem pode aprovar cursos obrigatórios e atividades por função. Rodar: node --test tests-unit/
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { podeDecidirSugestoesNaObra } from '../src/lib/decisaoSugestoes.ts';

test('sem usuário ou sem perfil: não pode', () => {
  assert.equal(podeDecidirSugestoesNaObra(null, 'a'), false);
  assert.equal(podeDecidirSugestoesNaObra(undefined, 'a'), false);
  assert.equal(podeDecidirSugestoesNaObra({ podeDecidirSugestoes: false, obrasPodeDecidirSugestoes: [] }, 'a'), false);
});

test('técnico ou engenheiro da obra (servidor novo): pode na obra dele, não na outra', () => {
  const u = { podeDecidirSugestoes: true, podeDecidirSugestoesGlobal: false, obrasPodeDecidirSugestoes: ['a'] };
  assert.equal(podeDecidirSugestoesNaObra(u, 'a'), true);
  assert.equal(podeDecidirSugestoesNaObra(u, 'b'), false);
  assert.equal(podeDecidirSugestoesNaObra(u, null), false);
});

test('vínculo global vale em qualquer obra', () => {
  const u = { podeDecidirSugestoes: true, podeDecidirSugestoesGlobal: true, obrasPodeDecidirSugestoes: [] };
  assert.equal(podeDecidirSugestoesNaObra(u, 'qualquer'), true);
});

test('servidor antigo (sem os campos novos): vale a regra do engenheiro', () => {
  assert.equal(podeDecidirSugestoesNaObra({ obrasEngenheiroSeguranca: ['a'] }, 'a'), true);
  assert.equal(podeDecidirSugestoesNaObra({ obrasEngenheiroSeguranca: ['a'] }, 'b'), false);
  assert.equal(podeDecidirSugestoesNaObra({ engenheiroSegurancaGlobal: true }, 'b'), true);
});

test('servidor novo ignora os campos antigos de engenheiro', () => {
  const u = { podeDecidirSugestoes: false, obrasEngenheiroSeguranca: ['a'] };
  assert.equal(podeDecidirSugestoesNaObra(u, 'a'), false);
});
