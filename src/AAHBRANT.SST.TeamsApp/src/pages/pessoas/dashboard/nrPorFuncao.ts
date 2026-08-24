// Regra de negócio (não é dado de trabalhador): quais NRs cada função normalmente exige.
// Chaveada pelo NOME da função e pela normaReferencia do curso — não pelos ids do banco —
// porque essa tela antes usava ids fictícios que não existem na base real.
// PENDÊNCIA: idealmente essa relação deveria vir de um cadastro real (Função ↔ NR exigida)
// no backend; até lá, é uma configuração estática de frontend e deve ser validada pela equipe de SST.
export const NR_POR_FUNCAO_NOME: Record<string, string[]> = {
  'pedreiro': ['NR-18'],
  'eletricista': ['NR-10', 'NR-18'],
  'soldador': ['NR-18', 'NR-33'],
  'montador de andaime': ['NR-35', 'NR-18'],
  'operador de guindaste': ['NR-12', 'NR-18'],
  'técnico de segurança': [],
};

export function normalizarNomeFuncao(nome: string): string {
  return nome.trim().toLowerCase();
}
