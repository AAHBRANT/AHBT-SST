// Quem aprova cursos obrigatórios e atividades por função (17/11/2026): Técnico, Engenheiro de Segurança OU
// Administrador, só nas obras do vínculo (ou com vínculo global; o servidor manda isso pronto em /eu). Aprovar o PGR continua só do engenheiro e
// não usa esta regra. O servidor decide de verdade (403/404); isto só evita oferecer o que será recusado.
// Lógica pura: roda em node --test tests-unit/.
export interface PerfisDecisaoUsuario {
  podeDecidirSugestoes?: boolean;
  podeDecidirSugestoesGlobal?: boolean;
  obrasPodeDecidirSugestoes?: string[];
  // Campos antigos (só engenheiro): usados quando o servidor ainda não manda os novos.
  engenheiroSegurancaGlobal?: boolean;
  obrasEngenheiroSeguranca?: string[];
}

export function podeDecidirSugestoesNaObra(usuario: PerfisDecisaoUsuario | null | undefined, obraId?: string | null): boolean {
  if (!usuario) return false;
  const servidorNovo = usuario.podeDecidirSugestoes !== undefined;
  const global = servidorNovo ? usuario.podeDecidirSugestoesGlobal === true : usuario.engenheiroSegurancaGlobal === true;
  if (global) return true;
  const obras = (servidorNovo ? usuario.obrasPodeDecidirSugestoes : usuario.obrasEngenheiroSeguranca) ?? [];
  return !!obraId && obras.includes(obraId);
}
