// Rótulo de assinatura eletrônica exibido nas telas de assinatura. Mostrar só "Assinado" perde a
// informação que dá valor de prova ao documento (MP 2.200-2/2001): quem lê precisa ver quando a
// assinatura foi coletada, não só que existe. Mesmo texto usado no PDF da ficha de EPI
// (EntregaEpiPdfService.FormatarAssinaturaDigital), para a tela e o documento não divergirem.
//
// Vive aqui pelo mesmo motivo de termoCompromissoEpi.ts: texto compartilhado por mais de um diálogo
// desta pasta fica num módulo próprio, não duplicado em cada um.
export function formatarAssinaturaDigital(dataIso: string): string {
  const data = new Date(dataIso);
  return `Assinado digitalmente em ${data.toLocaleDateString('pt-BR')} às ${data.toLocaleTimeString('pt-BR', {
    hour: '2-digit',
    minute: '2-digit',
  })}`;
}
