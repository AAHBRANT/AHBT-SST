import type { EquipamentoPublicoDto, SituacaoAdesivo } from '../../lib/api';

// Visual do adesivo de inspeção, copiado do mockup aprovado (docs/mockups/adesivo-inspecao-2026-11-17.html).
// O adesivo é impresso e a página do QR é sempre clara, então estas cores NÃO acompanham o tema do app:
// ficam fixas, como no mockup (verde = liberado, verde-azulado = inspecionado, vermelho = não liberado).
export const ADESIVO_MARCA = '#670000';
export const ADESIVO_GRADIENTE: Record<SituacaoAdesivo, string> = {
  Liberado: 'linear-gradient(135deg, #047857, #10b981)',
  Inspecionado: 'linear-gradient(135deg, #0f766e, #14b8a6)',
  NaoLiberado: 'linear-gradient(135deg, #991b1b, #dc2626)',
};
export const ADESIVO_TITULO: Record<SituacaoAdesivo, string> = {
  Liberado: 'LIBERADO',
  Inspecionado: 'INSPECIONADO',
  NaoLiberado: 'NÃO LIBERADO',
};
export const ADESIVO_SUBTITULO: Record<SituacaoAdesivo, string> = {
  Liberado: 'Liberado para uso',
  Inspecionado: 'Inspeção realizada',
  NaoLiberado: 'Uso bloqueado',
};

export function formatarDataAdesivo(data?: string | null): string {
  if (!data) return '';
  const [ano, mes, dia] = data.slice(0, 10).split('-');
  return dia && mes && ano ? `${dia}/${mes}/${ano}` : data;
}

// Pura: o aviso de adesivo antigo vem só do servidor (protocolo do adesivo lido, validado
// contra as inspeções Concluídas do objeto, x protocolo mais recente). O ?i= cru da URL nunca é comparado nem exibido.
export function adesivoEstaAntigo(dados: Pick<EquipamentoPublicoDto, 'protocolo' | 'protocoloAtual'>): boolean {
  return !!dados.protocolo && !!dados.protocoloAtual && dados.protocolo.trim().toUpperCase() !== dados.protocoloAtual.trim().toUpperCase();
}
