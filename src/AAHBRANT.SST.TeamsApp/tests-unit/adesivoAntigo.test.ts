// Aviso de "adesivo antigo" da página pública do QR. A decisão vem só do que o servidor devolve (protocolo do adesivo
// já validado x protocolo atual), nunca do texto cru de ?i= da URL. Rodar: node --test tests-unit/
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { adesivoEstaAntigo } from '../src/components/inspecoes/adesivoVisual.ts';

test('?i= falso: o servidor repete o protocolo atual, então não há aviso', () => {
  assert.equal(adesivoEstaAntigo({ protocolo: 'INSP-2026-000010', protocoloAtual: 'INSP-2026-000010' }), false);
});

test('?i= real e antigo: o servidor devolve o protocolo do adesivo, diferente do atual, então avisa', () => {
  assert.equal(adesivoEstaAntigo({ protocolo: 'INSP-2026-000001', protocoloAtual: 'INSP-2026-000010' }), true);
});

test('diferença só de caixa ou espaço não conta como adesivo antigo', () => {
  assert.equal(adesivoEstaAntigo({ protocolo: ' insp-2026-000010 ', protocoloAtual: 'INSP-2026-000010' }), false);
});

test('sem protocolo (um dos dois vazio) não avisa', () => {
  assert.equal(adesivoEstaAntigo({ protocolo: '', protocoloAtual: 'INSP-2026-000010' }), false);
  assert.equal(adesivoEstaAntigo({ protocolo: 'INSP-2026-000001', protocoloAtual: '' }), false);
});
