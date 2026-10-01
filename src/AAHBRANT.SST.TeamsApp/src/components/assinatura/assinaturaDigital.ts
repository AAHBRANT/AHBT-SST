// Rótulo de assinatura eletrônica exibido nas telas de assinatura. Mostrar só "Assinado" perde a
// informação que dá valor de prova ao documento (MP 2.200-2/2001): quem lê precisa ver quando a
// assinatura foi coletada, não só que existe. Mesmo texto usado no PDF da ficha de EPI
// (EntregaEpiPdfService.FormatarAssinaturaDigital), para a tela e o documento não divergirem.
//
// Vive aqui pelo mesmo motivo de termoCompromissoEpi.ts: texto compartilhado por mais de um diálogo
// desta pasta fica num módulo próprio, não duplicado em cada um.
// O backend grava e devolve o instante da assinatura em UTC, mas o horário que volta do banco chega sem o
// "Z" (ex.: 2026-09-29T15:22:16). new Date() lê texto sem fuso como hora LOCAL, e a tela mostrava a
// assinatura 3 h adiantada no Brasil. Sem indicação de fuso, o instante é UTC.
export function lerInstanteUtc(dataIso: string): Date {
  return new Date(/([zZ]|[+-]\d{2}:?\d{2})$/.test(dataIso) ? dataIso : `${dataIso}Z`);
}

export function formatarAssinaturaDigital(dataIso: string): string {
  return `Assinado digitalmente em ${lerInstanteUtc(dataIso).toLocaleDateString('pt-BR', { timeZone: FUSO_BRASILIA })} às ${formatarHoraBrasilia(dataIso)}`;
}

// Sempre no horário de Brasília, independente do fuso do aparelho.
const FUSO_BRASILIA = 'America/Sao_Paulo';

export function formatarHoraBrasilia(dataIso: string): string {
  return lerInstanteUtc(dataIso).toLocaleTimeString('pt-BR', { timeZone: FUSO_BRASILIA, hour: '2-digit', minute: '2-digit' });
}

export function formatarDataHoraBrasilia(dataIso: string): string {
  return lerInstanteUtc(dataIso).toLocaleString('pt-BR', { timeZone: FUSO_BRASILIA });
}
