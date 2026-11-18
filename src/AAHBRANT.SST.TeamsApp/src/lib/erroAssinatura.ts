// Erros das rotas de assinatura biométrica (instrutor e técnico da OS), 16/11/2026. Puro (testável em tests-unit/).
// ---- erros das rotas de assinatura do instrutor/técnico ----

export type TipoErroAssinatura = 'ja_assinou' | 'sem_acesso' | 'outro';

/**
 * Traduz o erro das rotas de assinatura do instrutor/técnico: 409 "já assinou" (a tela só recarrega), 404
 * (outra obra ou sem vínculo; o servidor não diz mais que isso) e as recusas 400 com texto do servidor
 * ("A digital lida não é a do instrutor...", rosto rejeitado), que seguem como vieram.
 */
export function classificarErroAssinatura(err: unknown, quem: 'integracao' | 'turma' | 'os' = 'integracao'): { tipo: TipoErroAssinatura; texto: string } {
  const bruto = err instanceof Error ? err.message : '';
  if (/já assinou/i.test(bruto)) return { tipo: 'ja_assinou', texto: bruto };
  if (/\(404\)/.test(bruto) || /^not found/i.test(bruto)) {
    const onde = quem === 'turma' ? 'a esta turma' : quem === 'os' ? 'a esta Ordem de Serviço' : 'a esta integração';
    return { tipo: 'sem_acesso', texto: `Você não tem acesso ${onde} (é de outra obra ou você não tem vínculo com ela).` };
  }
  return { tipo: 'outro', texto: bruto || 'Não consegui assinar. Tente de novo.' };
}
