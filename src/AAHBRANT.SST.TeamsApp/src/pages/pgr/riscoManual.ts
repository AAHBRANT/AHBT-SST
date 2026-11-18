import type { AtividadeGerada, GheGerado, RiscoGerado } from '../../lib/api';

// Catálogo de perigos padrão do passo 4 (17/11/2026). Espelho de PerigosPadrao.Catalogo
// (src/AAHBRANT.SST.Application/GeracaoPgr/PerigosPadrao.cs): são constantes que mudam raramente e o formulário "Adicionar
// risco" precisa delas sem chamada extra à API. tests-unit/riscoManual.test.ts compara os dois lados e falha se divergirem.
export const PERIGOS_PADRAO: ReadonlyArray<{ tipo: string; nome: string }> = [
  { tipo: 'Acidente', nome: 'Queda de altura' },
  { tipo: 'Acidente', nome: 'Queda de materiais e ferramentas sobre pessoas' },
  { tipo: 'Acidente', nome: 'Soterramento e desmoronamento em escavações e valas' },
  { tipo: 'Acidente', nome: 'Choque elétrico' },
  { tipo: 'Acidente', nome: 'Atropelamento e esmagamento por máquinas e veículos' },
  { tipo: 'Acidente', nome: 'Prensamento e esmagamento de membros' },
  { tipo: 'Acidente', nome: 'Corte e perfuração por ferramentas e materiais' },
  { tipo: 'Acidente', nome: 'Projeção de partículas nos olhos' },
  { tipo: 'Acidente', nome: 'Queda em mesmo nível e tropeço' },
  { tipo: 'Acidente', nome: 'Incêndio e explosão' },
  { tipo: 'Acidente', nome: 'Queimadura por solda, corte ou superfície quente' },
  { tipo: 'Acidente', nome: 'Movimentação e içamento de cargas' },
  { tipo: 'Acidente', nome: 'Tombamento de máquinas e equipamentos' },
  { tipo: 'Físico', nome: 'Ruído' },
  { tipo: 'Físico', nome: 'Vibração' },
  { tipo: 'Físico', nome: 'Calor e radiação solar' },
  { tipo: 'Físico', nome: 'Radiação não ionizante de solda' },
  { tipo: 'Químico', nome: 'Poeira de cimento, sílica e materiais de obra' },
  { tipo: 'Químico', nome: 'Fumos metálicos de solda' },
  { tipo: 'Químico', nome: 'Vapores de tintas, solventes e resinas' },
  { tipo: 'Químico', nome: 'Contato da pele com cimento e produtos químicos' },
  { tipo: 'Biológico', nome: 'Animais peçonhentos e insetos' },
  { tipo: 'Biológico', nome: 'Contato com água ou esgoto contaminado' },
  { tipo: 'Ergonômico', nome: 'Esforço físico e levantamento de peso' },
  { tipo: 'Ergonômico', nome: 'Postura inadequada e movimentos repetitivos' },
  { tipo: 'Psicossocial', nome: 'Pressão por prazo e jornada extensa' },
];

export const QUEDA_DE_ALTURA = 'Queda de altura';
export const DANOS_QUEDA_DE_ALTURA = 'Fraturas, traumatismo craniano, óbito';

// Mesma normalização do servidor (FuncaoSstClassifier.Normalizar): sem acento, minúsculas, espaços colapsados.
export const normalizar = (s: string) => s.normalize('NFD').replace(/\p{M}/gu, '').toLowerCase().split(' ').filter(Boolean).join(' ');

// Espelho de PerigosPadrao.CostumaTrabalharEmAltura (PerigosPadrao.cs). Alerta, não regra: o engenheiro confirma (NR-35).
const OFICIOS_EM_ALTURA = [
  'pedreiro', 'carpinteiro', 'armador', 'pintor', 'serralheiro', 'eletricista', 'montador', 'telhadista',
  'andaime', 'soldador', 'impermeabilizador', 'gesseiro', 'vidraceiro',
];
export function costumaTrabalharEmAltura(funcao?: string | null): boolean {
  if (!funcao || !funcao.trim()) return false;
  const palavras = normalizar(funcao).split(/[ /-]/);
  return OFICIOS_EM_ALTURA.some((o) => palavras.includes(o));
}

// Espelho de GheGerado.ChaveAltura (EstruturaGeracaoPgr.cs): funções e códigos da EAP do GHE, normalizados e ordenados.
// "Não se aplica" guarda esta chave; o aviso só volta quando ela muda.
export function chaveAltura(gh: Pick<GheGerado, 'funcoes' | 'atividades'>): string {
  const ordenar = (xs: string[]) => xs.filter((x) => x.length > 0).sort((a, b) => (a < b ? -1 : a > b ? 1 : 0));
  return `${ordenar(gh.funcoes.map((f) => normalizar(f.nome))).join('|')}#${ordenar(gh.atividades.map((a) => a.codigoEap.trim())).join('|')}`;
}

// Funções do GHE que costumam trabalhar em altura, sem repetir (para o texto do aviso).
export const funcoesEmAltura = (gh: GheGerado): string[] => {
  const vistos = new Set<string>();
  return gh.funcoes.map((f) => f.nome).filter((n) => costumaTrabalharEmAltura(n) && !vistos.has(normalizar(n)) && !!vistos.add(normalizar(n)));
};

// O GHE tem função que costuma subir, nenhuma atividade tem Queda de altura (risco não excluído) e o técnico ainda não
// respondeu "Não se aplica" para estas funções e atividades. Só avisa depois que todas as atividades tiveram os riscos
// gerados: antes disso a IA ainda pode incluir a queda.
export function precisaConfirmarAltura(gh: GheGerado): boolean {
  if (funcoesEmAltura(gh).length === 0 || gh.atividades.length === 0) return false;
  if (gh.atividades.some((a) => !a.riscosGerados)) return false;
  const alvo = normalizar(QUEDA_DE_ALTURA);
  if (gh.atividades.some((a) => a.riscos.some((r) => !r.excluido && normalizar(r.perigo) === alvo))) return false;
  return gh.alturaNaoSeAplicaChave !== chaveAltura(gh);
}

export interface DadosNovoRisco { tipo: string; perigo: string; danos: string; probabilidade: number; severidade: number }

// Risco criado à mão: já nasce revisado (quem decidiu foi o técnico), sem medidas e sem base legal.
export function novoRiscoManual(d: DadosNovoRisco): RiscoGerado {
  return {
    id: crypto.randomUUID().replace(/-/g, ''),
    tipo: d.tipo,
    perigo: d.perigo.trim(),
    danos: d.danos.trim() || null,
    probabilidade: d.probabilidade,
    severidade: d.severidade,
    base: [],
    medidas: [],
    revisado: true,
    excluido: false,
    incluidoPeloTecnico: true,
    exposicao: { criterio: 'Qualitativo', perfil: null, medicoes: [] },
  };
}

// Inclui o risco em cada atividade marcada. Uma atividade que já tem o mesmo perigo (não excluído) é pulada
// e devolvida em `jaTinham`, para a tela avisar em vez de duplicar.
export function incluirRisco(atividades: AtividadeGerada[], codigos: string[], d: DadosNovoRisco): { jaTinham: string[] } {
  const alvo = normalizar(d.perigo);
  const jaTinham: string[] = [];
  for (const a of atividades.filter((x) => codigos.includes(x.codigoEap))) {
    if (a.riscos.some((r) => !r.excluido && normalizar(r.perigo) === alvo)) { jaTinham.push(a.codigoEap); continue; }
    a.riscos.push(novoRiscoManual(d));
  }
  return { jaTinham };
}
