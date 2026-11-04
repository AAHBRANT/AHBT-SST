// Mapa de navegação (proposta aprovada em 03/11/2026, docs/propostas/navegacao-sst-v1-2026-11-03.html):
// uma única fonte para a trilha das telas de detalhe e para os subitens do módulo aberto no rail.
// As seções repetem as abas `?secao=` de GestaoSstPage/OperacaoPage/OcorrenciasPage — ao criar ou
// renomear uma aba lá, ajuste aqui também.

export interface SecaoNav {
  valor: string;
  rotulo: string;
}

export interface ModuloNav {
  rota: string;
  rotulo: string;
  secoes?: SecaoNav[];
}

export const modulosNav: Record<string, ModuloNav> = {
  gestao: {
    rota: '/gestao-sst',
    rotulo: 'Gestão de SST',
    secoes: [
      { valor: 'pgr', rotulo: 'PGR / GRO' },
      { valor: 'pcmso', rotulo: 'PCMSO' },
      { valor: 'treinamentos', rotulo: 'Treinamentos' },
      { valor: 'documentos', rotulo: 'Documentos & Procedimentos' },
      { valor: 'requisitos-legais', rotulo: 'Requisitos Legais' },
    ],
  },
  operacao: {
    rota: '/operacao',
    rotulo: 'Operação',
    secoes: [
      { valor: 'apr', rotulo: 'APR' },
      { valor: 'pt', rotulo: 'PT' },
      { valor: 'inspecoes', rotulo: 'Inspeções' },
      { valor: 'cipa', rotulo: 'CIPA' },
      { valor: 'epi', rotulo: 'EPI' },
      { valor: 'uniforme', rotulo: 'Uniforme' },
      { valor: 'epc', rotulo: 'EPC' },
      { valor: 'dds', rotulo: 'DDS' },
      { valor: 'identificacao', rotulo: 'Outros controles operacionais' },
    ],
  },
  ocorrencias: {
    rota: '/ocorrencias',
    rotulo: 'Ocorrências',
    secoes: [
      { valor: 'acidentes', rotulo: 'Acidentes / Incidentes' },
      { valor: 'nao-conformidades', rotulo: 'Não conformidades' },
    ],
  },
  pessoas: { rota: '/pessoas', rotulo: 'Pessoas' },
  terceirizados: { rota: '/terceirizados', rotulo: 'Terceirizado' },
  suporteIa: { rota: '/suporte-ia', rotulo: 'Suporte IA' },
};

type ChaveModulo = keyof typeof modulosNav;

// Prefixo da URL → módulo e seção a que a tela pertence. Ordem importa: o mais específico primeiro.
const prefixos: Array<{ prefixo: string; modulo: ChaveModulo; secao?: string }> = [
  { prefixo: '/operacao/saude-ocupacional', modulo: 'gestao', secao: 'pcmso' },
  { prefixo: '/operacao/apr', modulo: 'operacao', secao: 'apr' },
  { prefixo: '/operacao/pt', modulo: 'operacao', secao: 'pt' },
  { prefixo: '/operacao/cipa', modulo: 'operacao', secao: 'cipa' },
  { prefixo: '/operacao/identificacao', modulo: 'operacao', secao: 'identificacao' },
  { prefixo: '/prevencao/pgr', modulo: 'gestao', secao: 'pgr' },
  { prefixo: '/prevencao/pcmso', modulo: 'gestao', secao: 'pcmso' },
  { prefixo: '/prevencao/inspecoes', modulo: 'operacao', secao: 'inspecoes' },
  { prefixo: '/prevencao/temas-dds', modulo: 'operacao', secao: 'dds' },
  { prefixo: '/prevencao/dds', modulo: 'operacao', secao: 'dds' },
  { prefixo: '/treinamentos', modulo: 'gestao', secao: 'treinamentos' },
  { prefixo: '/epi', modulo: 'operacao', secao: 'epi' },
  { prefixo: '/nao-conformidades', modulo: 'ocorrencias', secao: 'nao-conformidades' },
  { prefixo: '/acidentes', modulo: 'ocorrencias', secao: 'acidentes' },
  { prefixo: '/pessoas', modulo: 'pessoas' },
  { prefixo: '/terceirizados', modulo: 'terceirizados' },
  { prefixo: '/suporte-ia', modulo: 'suporteIa' },
];

function casaPrefixo(caminho: string, prefixo: string) {
  return caminho === prefixo || caminho.startsWith(`${prefixo}/`);
}

// Módulo + seção ativos para a URL atual. Na página do próprio módulo a seção vem do `?secao=`.
export function localizarNaNavegacao(caminho: string, busca: string): { modulo: ModuloNav; secao?: SecaoNav } | null {
  const daPagina = Object.values(modulosNav).find((m) => m.rota === caminho);
  if (daPagina) {
    const valor = new URLSearchParams(busca).get('secao');
    const secao = daPagina.secoes?.find((s) => s.valor === valor) ?? daPagina.secoes?.[0];
    return { modulo: daPagina, secao };
  }
  const achado = prefixos.find((p) => casaPrefixo(caminho, p.prefixo));
  if (!achado) return null;
  const modulo = modulosNav[achado.modulo];
  return { modulo, secao: modulo.secoes?.find((s) => s.valor === achado.secao) };
}

export function rotaDaSecao(modulo: ModuloNav, secao: SecaoNav) {
  return `${modulo.rota}?secao=${secao.valor}`;
}

export interface ElosTrilha {
  elos: Array<{ rotulo: string; para: string }>;
  atual: string;
}

// Trilha só em telas de detalhe (URL fora da página do módulo). O último elo nomeia o tipo de tela,
// já que o título do registro fica no cabeçalho logo abaixo.
export function montarTrilha(caminho: string): ElosTrilha | null {
  // A própria lista (/epi, /treinamentos, /operacao/apr) não é detalhe: precisa de algo após o prefixo.
  if (!prefixos.some((p) => caminho.startsWith(`${p.prefixo}/`))) return null;
  const local = localizarNaNavegacao(caminho, '');
  if (!local) return null;
  const elos = [{ rotulo: local.modulo.rotulo, para: local.modulo.rota }];
  if (local.secao) elos.push({ rotulo: local.secao.rotulo, para: rotaDaSecao(local.modulo, local.secao) });
  const atual = caminho.endsWith('/assinar') ? 'Assinatura' : 'Detalhe';
  return { elos, atual };
}
