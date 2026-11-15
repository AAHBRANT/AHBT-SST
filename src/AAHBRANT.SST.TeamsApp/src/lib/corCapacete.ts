// Cor do capacete por função (padrão AAHBRANT) e regras puras usadas pelas telas de Funções e de
// Alertas de EPI. Os números seguem o enum CorCapacete do servidor; ausente ou fora da faixa = 0.
// Rodar os testes: node --test tests-unit/

export const CorCapacete = {
  SemPadrao: 0,
  Branco: 1,
  Amarelo: 2,
  Azul: 3,
  Verde: 4,
  Vermelho: 5,
  Laranja: 6,
} as const;

export type CorCapaceteValor = (typeof CorCapacete)[keyof typeof CorCapacete];

export const corCapaceteNome: Record<number, string> = {
  0: 'Sem padrão',
  1: 'Branco',
  2: 'Amarelo',
  3: 'Azul',
  4: 'Verde',
  5: 'Vermelho',
  6: 'Laranja',
};

// Cor real do capacete: usada só na amostra desenhada (nunca em texto ou fundo de tela).
export const corCapaceteHex: Record<number, string> = {
  1: '#ffffff',
  2: '#f5c400',
  3: '#1d5fd1',
  4: '#1f9d55',
  5: '#d62828',
  6: '#f2780c',
};

// Ordem das amostras no formulário (igual ao mockup): Sem padrão primeiro.
export const CORES_CAPACETE_OPCOES: readonly number[] = [0, 1, 2, 3, 4, 5, 6];

// Grupo do padrão AAHBRANT por cor (mesmos textos de SugestaoCorCapacete.Grupo no servidor).
// O servidor pode mandar o grupo pronto em cada sugestão; este texto é o reserva.
const grupoPorCor: Record<number, string> = {
  1: 'Engenheiros, mestres, encarregados, gestores',
  2: 'Operários, serventes, ajudantes, execução',
  3: 'Eletricistas, encanadores, operadores, técnicos especializados',
  4: 'Técnico de segurança, meio ambiente',
  5: 'Brigadistas e emergência',
  6: 'Visitantes e temporários',
};

export function normalizarCorCapacete(valor: unknown): number {
  return typeof valor === 'number' && Number.isInteger(valor) && valor >= 0 && valor <= 6 ? valor : 0;
}

export function nomeCorCapacete(valor: unknown): string {
  return corCapaceteNome[normalizarCorCapacete(valor)];
}

export function grupoPadraoCorCapacete(cor: number): string {
  return grupoPorCor[normalizarCorCapacete(cor)] ?? '';
}

// ---- Sugestão por nome ----
// Cópia fiel de Application/Funcoes/SugestaoCorCapacete.cs (regras e ordem), usada só no campo do formulário
// "Nova função". Comparação sem acento, maiúsculas nem pontuação, pelo INÍCIO de cada palavra
// ("engenheir" pega engenheiro/engenheira). Vale a primeira regra que casar. Se mudar lá, mude aqui e nos testes.

const REGRAS_SUGESTAO: { cor: number; termos: string[] }[] = [
  { cor: CorCapacete.Vermelho, termos: ['brigad', 'bombeiro civil', 'emergencia'] },
  { cor: CorCapacete.Laranja, termos: ['visitante', 'temporari'] },
  {
    cor: CorCapacete.Branco,
    termos: ['engenheir', 'arquitet', 'mestre', 'encarregad', 'gestor', 'gerente', 'coordenador', 'supervisor'],
  },
  {
    cor: CorCapacete.Verde,
    termos: [
      'tecnico de seguranca',
      'tecnico em seguranca',
      'tecnico seguranca',
      'tec de seguranca',
      'tec seguranca',
      'tst',
      'seguranca do trabalho',
      'meio ambiente',
      'ambiental',
    ],
  },
  { cor: CorCapacete.Amarelo, termos: ['ajudante', 'servente'] },
  { cor: CorCapacete.Azul, termos: ['eletricista', 'encanador', 'bombeiro hidraulico', 'operador', 'tecnico'] },
  {
    cor: CorCapacete.Amarelo,
    termos: [
      'operario',
      'pedreiro',
      'carpinteiro',
      'armador',
      'pintor',
      'gesseiro',
      'azulejista',
      'ladrilhista',
      'soldador',
      'serralheiro',
      'marceneiro',
      'montador',
      'impermeabilizador',
      'vidraceiro',
    ],
  },
];

// Minúsculas, sem acento, só letras e dígitos separados por um espaço, com espaço antes e depois.
function normalizarNome(texto: string | null | undefined): string {
  if (!texto || !texto.trim()) return '';
  let r = ' ';
  for (const c of texto.normalize('NFD')) {
    if (/\p{Mn}/u.test(c)) continue;
    if (/[\p{L}\p{N}]/u.test(c)) r += c.toLowerCase();
    else if (!r.endsWith(' ')) r += ' ';
  }
  return r.endsWith(' ') ? r : r + ' ';
}

export function sugerirCorCapacetePorNome(nome: string | null | undefined): number {
  const n = normalizarNome(nome);
  if (!n) return CorCapacete.SemPadrao;
  for (const regra of REGRAS_SUGESTAO) {
    if (regra.termos.some((t) => n.includes(' ' + t))) return regra.cor;
  }
  return CorCapacete.SemPadrao;
}

// ---- Quadro "Aplicar padrão de cores" ----

export interface SugestaoCorCapacete {
  funcaoId: string;
  nome: string;
  corAtual?: number;
  corSugerida?: number;
  // Opcional: texto do grupo vindo do servidor. Sem ele, vale o grupo da cor.
  grupo?: string | null;
}

export type SituacaoLinhaPadrao = 'muda' | 'jaNoPadrao' | 'semSugestao';

export interface LinhaPadraoCores {
  funcaoId: string;
  nome: string;
  corAtual: number;
  corSugerida: number;
  grupo: string;
  situacao: SituacaoLinhaPadrao;
}

// Linhas a atualizar vêm primeiro (como no mockup); depois "o padrão não cobre" e "já está no padrão".
const ORDEM_SITUACAO: Record<SituacaoLinhaPadrao, number> = { muda: 0, semSugestao: 1, jaNoPadrao: 2 };

export function montarLinhasPadraoCores(sugestoes: SugestaoCorCapacete[]): LinhaPadraoCores[] {
  const linhas = sugestoes.map<LinhaPadraoCores>((s) => {
    const corAtual = normalizarCorCapacete(s.corAtual);
    const corSugerida = normalizarCorCapacete(s.corSugerida);
    const situacao: SituacaoLinhaPadrao =
      corSugerida === CorCapacete.SemPadrao ? 'semSugestao' : corSugerida === corAtual ? 'jaNoPadrao' : 'muda';
    return {
      funcaoId: s.funcaoId,
      nome: s.nome,
      corAtual,
      corSugerida,
      grupo: s.grupo?.trim() || grupoPadraoCorCapacete(corSugerida),
      situacao,
    };
  });
  return linhas
    .map((l, i) => ({ l, i }))
    .sort((a, b) => ORDEM_SITUACAO[a.l.situacao] - ORDEM_SITUACAO[b.l.situacao] || a.i - b.i)
    .map(({ l }) => l);
}

// Só as linhas que mudam começam marcadas; as demais não têm o que aplicar até o usuário escolher uma cor.
export function selecaoInicialPadraoCores(linhas: LinhaPadraoCores[]): Set<string> {
  return new Set(linhas.filter((l) => l.situacao === 'muda').map((l) => l.funcaoId));
}

// escolhas: cor escolhida à mão para funções que o padrão não cobre (funcaoId -> cor).
export function itensParaAplicar(
  linhas: LinhaPadraoCores[],
  selecionadas: ReadonlySet<string>,
  escolhas: Readonly<Record<string, number>> = {},
): { funcaoId: string; cor: number }[] {
  const itens: { funcaoId: string; cor: number }[] = [];
  for (const l of linhas) {
    if (!selecionadas.has(l.funcaoId)) continue;
    if (l.situacao === 'muda') {
      itens.push({ funcaoId: l.funcaoId, cor: l.corSugerida });
    } else if (l.situacao === 'semSugestao') {
      const cor = normalizarCorCapacete(escolhas[l.funcaoId]);
      if (cor > 0 && cor !== l.corAtual) itens.push({ funcaoId: l.funcaoId, cor });
    }
  }
  return itens;
}

const funcoesTexto = (n: number) => `${n} ${n === 1 ? 'função' : 'funções'}`;

export function resumoPadraoCores(
  linhas: LinhaPadraoCores[],
  selecionadas: ReadonlySet<string>,
  escolhas: Readonly<Record<string, number>> = {},
): string {
  const itens = itensParaAplicar(linhas, selecionadas, escolhas);
  const atualizar = itens.length;
  const ids = new Set(itens.map((i) => i.funcaoId));
  const jaNoPadrao = linhas.filter((l) => l.situacao === 'jaNoPadrao').length;
  const semSugestao = linhas.filter((l) => l.situacao === 'semSugestao' && !ids.has(l.funcaoId)).length;
  const partes = [`${funcoesTexto(atualizar)} ${atualizar === 1 ? 'será atualizada' : 'serão atualizadas'}.`];
  const resto: string[] = [];
  if (jaNoPadrao > 0) resto.push(`${jaNoPadrao} já ${jaNoPadrao === 1 ? 'está' : 'estão'} no padrão`);
  if (semSugestao > 0) resto.push(`${semSugestao} ${semSugestao === 1 ? 'fica como está' : 'ficam como estão'}`);
  if (resto.length > 0) partes.push(`${resto.join(' e ')}.`);
  return partes.join(' ');
}

export function rotuloBotaoAplicar(quantidade: number): string {
  return quantidade === 0 ? 'Aplicar' : `Aplicar a ${funcoesTexto(quantidade)}`;
}

// ---- Alertas de EPI ----

export type TipoOcorrenciaEpi = 'capacete' | 'colete' | 'oculos' | 'cor';

export const TIPOS_OCORRENCIA_EPI: readonly { valor: TipoOcorrenciaEpi; rotulo: string }[] = [
  { valor: 'capacete', rotulo: 'Capacete' },
  { valor: 'colete', rotulo: 'Colete' },
  { valor: 'oculos', rotulo: 'Óculos' },
  { valor: 'cor', rotulo: 'Cor do capacete' },
];

const artigoCor = (nome: string) => nome.toLowerCase();

// "Capacete azul — esperado amarelo (Pedreiro)". Sem a cor esperada ou sem função, o texto encurta.
export function descreverAvisoCorCapacete(
  vista: number | null | undefined,
  esperada: number | null | undefined,
  funcaoNome?: string | null,
): string {
  const v = normalizarCorCapacete(vista);
  const e = normalizarCorCapacete(esperada);
  const partes: string[] = [];
  partes.push(v > 0 ? `Capacete ${artigoCor(corCapaceteNome[v])}` : 'Cor do capacete diferente do padrão');
  if (e > 0) partes.push(`esperado ${artigoCor(corCapaceteNome[e])}`);
  const base = partes.join(' — ');
  return funcaoNome ? `${base} (${funcaoNome})` : base;
}
