import { useEffect, useMemo, useState, type ReactNode } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  Avatar,
  Button,
  Card,
  PageHeader,
  DataTable,
  StatusChip,
  EstadoVazio,
  FeedbackInline,
  PainelCriacaoInline,
  FormGrid,
  FormRodape,
  FormSection,
  Campo,
  Field,
  Input,
  Select,
  CampoData,
  KpiCard,
  Legenda,
  designTokens,
  tokensUi,
  useTipografia,
  useConfirmar,
  nivelVencimento,
  type Coluna,
  type Tom,
} from '@ui';
import {
  Add24Regular,
  CheckmarkCircle24Regular,
  ChevronDown20Regular,
  ChevronUp20Regular,
  DataBarVertical24Regular,
  DismissCircle24Regular,
  People24Regular,
  Search24Regular,
  Warning24Regular,
} from '@fluentui/react-icons';
import {
  api,
  ResultadoAso,
  tipoVinculoLabel,
  TipoVinculo,
  type Aso,
  type CursoTreinamento,
  type EntregaEpi,
  type Funcao,
  type NovoTrabalhador,
  type Obra,
  type Trabalhador,
  type Treinamento,
} from '../../lib/api';
import { formatarCpf } from '../../lib/cpf';
import { CadastroDigitalDialog } from '../../components/pessoas/CadastroDigitalDialog';
import { RequisitosFuncaoDialog } from '../../components/pessoas/RequisitosFuncaoDialog';
import { CadastrosFaciaisFracosCard } from '../../components/pessoas/CadastrosFaciaisFracosCard';
import { FaixaKpis } from '../../components/dashboard/FaixaKpis';
import { useSucessoToast } from '../../hooks/useSucessoToast';
import { PessoasDashboardTab } from './dashboard/PessoasDashboardTab';

function ultimoAso(asos: Aso[], trabalhadorId: string): Aso | undefined {
  return asos
    .filter((a) => a.trabalhadorId === trabalhadorId)
    .sort((a, b) => new Date(b.dataExame).getTime() - new Date(a.dataExame).getTime())[0];
}

function dataBr(iso: string): string {
  return iso.slice(0, 10).split('-').reverse().join('/');
}

const trabalhadorVazio: NovoTrabalhador = {
  obraId: '',
  setorId: null,
  equipeId: null,
  funcaoId: '',
  nome: '',
  matricula: '',
  cpf: '',
  vinculo: TipoVinculo.Clt,
  dataAdmissao: '',
  turno: '',
};

// Situação de um item da linha (ASO, NR ou EPI): tom do ponto + texto do tooltip/leitor de tela.
// `null` quando a lista daquele item não pôde ser carregada (ex.: perfil sem acesso a EPIs).
interface Item {
  tom: Tom;
  texto: string;
}

interface Situacao {
  aso: Item;
  nr: Item | null;
  epi: Item | null;
  geral: 'apto' | 'atencao' | 'bloqueado';
}

// Bloqueado = algum item vencido/inapto/sem ASO (mesma soma "inaptos + pendentes" do dashboard de
// Pessoas, mais NR e EPI vencidos, que a ficha do trabalhador já trata como bloqueio de PT).
// Atenção = algo vence em até 30 dias (PR-SST-003) ou ASO apto com restrição.
function situacaoGeral(itens: Array<Item | null>): Situacao['geral'] {
  if (itens.some((i) => i?.tom === 'alerta')) return 'bloqueado';
  if (itens.some((i) => i?.tom === 'atencao')) return 'atencao';
  return 'apto';
}

const rotuloGeral: Record<Situacao['geral'], { texto: string; tom: Tom }> = {
  apto: { texto: 'Apto', tom: 'ok' },
  atencao: { texto: 'Atenção', tom: 'atencao' },
  bloqueado: { texto: 'Bloqueado', tom: 'alerta' },
};

function situacaoAso(aso: Aso | undefined): Item {
  if (!aso) return { tom: 'alerta', texto: 'sem ASO cadastrado' };
  if (aso.resultadoStatus === ResultadoAso.Pendente) return { tom: 'alerta', texto: 'ASO pendente' };
  if (aso.resultadoStatus === ResultadoAso.Inapto) return { tom: 'alerta', texto: 'inapto' };
  const nivel = nivelVencimento(aso.dataValidade);
  if (nivel === 'vencido') return { tom: 'alerta', texto: `vencido em ${dataBr(aso.dataValidade)}` };
  if (nivel === 'alerta') return { tom: 'atencao', texto: `vence em ${dataBr(aso.dataValidade)}` };
  if (aso.resultadoStatus === ResultadoAso.AptoComRestricao) return { tom: 'atencao', texto: 'apto com restrição' };
  return { tom: 'ok', texto: `válido até ${dataBr(aso.dataValidade)}` };
}

// Pior situação entre as validades (a mais urgente define o ponto).
function piorValidade(validades: string[], plural: string): Item {
  const vencidos = validades.filter((v) => nivelVencimento(v) === 'vencido').length;
  const vencendo = validades.filter((v) => nivelVencimento(v) === 'alerta').length;
  if (vencidos > 0) return { tom: 'alerta', texto: `${vencidos} ${plural} vencido(s)` };
  if (vencendo > 0) return { tom: 'atencao', texto: `${vencendo} ${plural} vencendo em até 30 dias` };
  return { tom: 'ok', texto: `${plural} em dia` };
}

const CONSULTA_TELA_ESTREITA = '(max-width: 760px)';
const PASSO_PAGINA = 50;

type FiltroRapido = 'todos' | 'aptos' | 'pendencia' | 'bloqueados' | 'terceirizados' | 'asoVencendo';

const FILTROS_RAPIDOS: Array<{ valor: FiltroRapido; rotulo: string }> = [
  { valor: 'todos', rotulo: 'Todos' },
  { valor: 'aptos', rotulo: 'Aptos' },
  { valor: 'pendencia', rotulo: 'Com pendência' },
  { valor: 'bloqueados', rotulo: 'Bloqueados' },
  { valor: 'terceirizados', rotulo: 'Terceirizados' },
];

// Lista de funcionários — redesenho do módulo Pessoas (pedido do usuário, 21/10, proposta aprovada em
// HTML): a busca vem primeiro e procura em TODAS as obras; a obra deixou de ser uma etapa (grade de
// obras → lista) e virou filtro numa faixa de atalhos; os 4 KPIs filtram a lista ao clicar; os gráficos
// do dashboard só montam ao abrir "Ver análise". Cada linha mostra a situação de ASO, NR e EPI,
// calculada no cliente a partir de uma lista de cada (nenhuma chamada por trabalhador) — mesmo padrão
// de agregação client-side do PessoasDashboardTab. O cadastro (PainelCriacaoInline) não mudou.
export function TrabalhadoresTab({
  acoesCabecalho,
  mostrarTitulo = true,
}: { acoesCabecalho?: ReactNode; mostrarTitulo?: boolean } = {}) {
  const navigate = useNavigate();
  const tipo = useTipografia();
  const [trabalhadores, setTrabalhadores] = useState<Trabalhador[]>([]);
  const [obras, setObras] = useState<Obra[]>([]);
  const [funcoes, setFuncoes] = useState<Funcao[]>([]);
  const [asos, setAsos] = useState<Aso[]>([]);
  const [treinamentos, setTreinamentos] = useState<Treinamento[] | null>(null);
  const [cursos, setCursos] = useState<CursoTreinamento[]>([]);
  const [entregasEpi, setEntregasEpi] = useState<EntregaEpi[] | null>(null);
  const [busca, setBusca] = useState('');
  const [filtroRapido, setFiltroRapido] = useState<FiltroRapido>('todos');
  const [funcaoFiltro, setFuncaoFiltro] = useState('');
  const [obraFiltro, setObraFiltro] = useState<string | null>(null);
  const [analiseAberta, setAnaliseAberta] = useState(false);
  const [limite, setLimite] = useState(PASSO_PAGINA);
  const [telaEstreita, setTelaEstreita] = useState(() => window.matchMedia(CONSULTA_TELA_ESTREITA).matches);
  const [novoTrabalhador, setNovoTrabalhador] = useState<NovoTrabalhador>(trabalhadorVazio);
  const [erro, setErro] = useState<string | null>(null);
  const [erroPainel, setErroPainel] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(false);
  const [carregandoLista, setCarregandoLista] = useState(true);
  const [painelAberto, setPainelAberto] = useState(false);
  const [fotoUrls, setFotoUrls] = useState<Record<string, string>>({});
  const [trabalhadorDigitalAlvo, setTrabalhadorDigitalAlvo] = useState<{ id: string; nome: string } | null>(
    null,
  );
  const [trabalhadorRequisitosAlvo, setTrabalhadorRequisitosAlvo] = useState<{
    id: string;
    nome: string;
    funcaoId: string;
  } | null>(null);
  const { dialogElement } = useConfirmar();
  const sucessoToast = useSucessoToast();

  useEffect(() => {
    const media = window.matchMedia(CONSULTA_TELA_ESTREITA);
    const atualizar = () => setTelaEstreita(media.matches);
    media.addEventListener('change', atualizar);
    return () => media.removeEventListener('change', atualizar);
  }, []);

  async function carregar() {
    try {
      setErro(null);
      const [trabs, obs, funcs, asosResp] = await Promise.all([
        api.trabalhadores.listar(),
        api.obras.listar(),
        api.funcoes.listar(),
        api.asos.listar(),
      ]);
      setTrabalhadores(trabs);
      setObras(obs);
      setFuncoes(funcs);
      setAsos(asosResp);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar funcionários.');
    } finally {
      setCarregandoLista(false);
    }
    // NR e EPI da linha são complementares: se o perfil do usuário não puder listar um deles, a lista
    // continua funcionando e só o ponto correspondente some.
    const [trein, cursosResp, entregas] = await Promise.allSettled([
      api.treinamentos.listar(),
      api.cursosTreinamento.listar(),
      api.entregasEpi.listar(),
    ]);
    setTreinamentos(trein.status === 'fulfilled' ? trein.value : null);
    setCursos(cursosResp.status === 'fulfilled' ? cursosResp.value : []);
    setEntregasEpi(entregas.status === 'fulfilled' ? entregas.value : null);
  }

  useEffect(() => {
    carregar();
  }, []);

  // Miniaturas da foto são baixadas sob demanda (só para trabalhadores com temFoto) e mantidas como
  // object URL até a página ser desmontada — mesmo padrão de ObrasPage.baixarLogo.
  useEffect(() => {
    let cancelado = false;
    (async () => {
      for (const trabalhador of trabalhadores) {
        if (!trabalhador.temFoto || fotoUrls[trabalhador.id]) continue;
        try {
          const blob = await api.trabalhadores.baixarFoto(trabalhador.id);
          if (cancelado) return;
          setFotoUrls((atual) => ({ ...atual, [trabalhador.id]: URL.createObjectURL(blob) }));
        } catch {
          // Falha ao carregar miniatura não impede o uso da página; o trabalhador fica sem foto.
        }
      }
    })();
    return () => {
      cancelado = true;
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [trabalhadores]);

  useEffect(() => {
    return () => {
      Object.values(fotoUrls).forEach((url) => URL.revokeObjectURL(url));
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  function nomeFuncao(id: string) {
    return funcoes.find((f) => f.id === id)?.nome ?? id;
  }

  const nomeObra = useMemo(() => new Map(obras.map((o) => [o.id, o.nome])), [obras]);
  const nomeCurso = useMemo(() => new Map(cursos.map((c) => [c.id, c.normaReferencia || c.nome])), [cursos]);

  // NR: validade do treinamento mais recente de cada curso do trabalhador (a reciclagem substitui a
  // anterior). Não cruza com "NRs exigidas pela função": esse vínculo ainda é uma tabela estática de
  // frontend (dashboard/nrPorFuncao.ts, pendência de backend) e marcar ausência por ela daria falso
  // bloqueio. EPI: só o que está em posse (sem devolução), a entrega mais recente de cada item.
  const situacoes = useMemo(() => {
    const treinosPorTrabalhador = new Map<string, Map<string, string>>();
    for (const t of treinamentos ?? []) {
      const porCurso = treinosPorTrabalhador.get(t.trabalhadorId) ?? new Map<string, string>();
      const atual = porCurso.get(t.cursoTreinamentoId);
      if (!atual || t.dataValidade > atual) porCurso.set(t.cursoTreinamentoId, t.dataValidade);
      treinosPorTrabalhador.set(t.trabalhadorId, porCurso);
    }
    const episPorTrabalhador = new Map<string, Map<string, EntregaEpi>>();
    for (const e of entregasEpi ?? []) {
      if (e.dataDevolucao) continue;
      const porItem = episPorTrabalhador.get(e.trabalhadorId) ?? new Map<string, EntregaEpi>();
      const atual = porItem.get(e.catalogoEpiId);
      if (!atual || e.dataEntrega > atual.dataEntrega) porItem.set(e.catalogoEpiId, e);
      episPorTrabalhador.set(e.trabalhadorId, porItem);
    }

    const mapa = new Map<string, Situacao>();
    for (const t of trabalhadores) {
      const aso = situacaoAso(ultimoAso(asos, t.id));

      let nr: Item | null = null;
      if (treinamentos) {
        const porCurso = treinosPorTrabalhador.get(t.id);
        if (!porCurso || porCurso.size === 0) {
          nr = { tom: 'neutro', texto: 'nenhum treinamento registrado' };
        } else {
          nr = piorValidade([...porCurso.values()], 'treinamento(s)');
          if (nr.tom === 'alerta') {
            const vencidos = [...porCurso.entries()]
              .filter(([, validade]) => nivelVencimento(validade) === 'vencido')
              .map(([cursoId]) => nomeCurso.get(cursoId) ?? 'curso');
            nr = { tom: 'alerta', texto: `vencido: ${vencidos.join(', ')}` };
          }
        }
      }

      let epi: Item | null = null;
      if (entregasEpi) {
        const porItem = episPorTrabalhador.get(t.id);
        const validades = porItem
          ? [...porItem.values()].map((e) => e.dataValidade).filter((v): v is string => !!v)
          : [];
        epi =
          validades.length > 0
            ? piorValidade(validades, 'EPI(s)')
            : { tom: 'neutro', texto: porItem ? 'EPIs sem validade definida' : 'nenhum EPI em posse' };
      }

      mapa.set(t.id, { aso, nr, epi, geral: situacaoGeral([aso, nr, epi]) });
    }
    return mapa;
  }, [trabalhadores, asos, treinamentos, entregasEpi, nomeCurso]);

  // Universo dos KPIs: respeita a obra escolhida, mas não a busca nem o filtro rápido — senão o número
  // do KPI mudaria ao clicar nele.
  const universo = useMemo(
    () => trabalhadores.filter((t) => !obraFiltro || t.obraId === obraFiltro),
    [trabalhadores, obraFiltro],
  );

  const kpis = useMemo(() => {
    let aptos = 0;
    let bloqueados = 0;
    let asoVencendo = 0;
    for (const t of universo) {
      const s = situacoes.get(t.id);
      if (!s) continue;
      if (s.geral === 'apto') aptos++;
      if (s.geral === 'bloqueado') bloqueados++;
      if (s.aso.tom !== 'ok') asoVencendo++;
    }
    return { total: universo.length, aptos, bloqueados, asoVencendo };
  }, [universo, situacoes]);

  // Faixa de obras (substitui a grade com os 4 contadores de vínculo, que mostrava zeros): total e
  // bloqueados por obra.
  const resumoPorObra = useMemo(() => {
    const mapa = new Map<string, { total: number; bloqueados: number }>();
    for (const t of trabalhadores) {
      const atual = mapa.get(t.obraId) ?? { total: 0, bloqueados: 0 };
      atual.total++;
      if (situacoes.get(t.id)?.geral === 'bloqueado') atual.bloqueados++;
      mapa.set(t.obraId, atual);
    }
    return mapa;
  }, [trabalhadores, situacoes]);

  const trabalhadoresFiltrados = useMemo(() => {
    const termo = busca.trim().toLowerCase();
    const nomePorFuncao = new Map(funcoes.map((f) => [f.id, f.nome.toLowerCase()]));
    return universo.filter((t) => {
      if (funcaoFiltro && t.funcaoId !== funcaoFiltro) return false;
      const s = situacoes.get(t.id);
      if (filtroRapido === 'aptos' && s?.geral !== 'apto') return false;
      if (filtroRapido === 'pendencia' && s?.geral === 'apto') return false;
      if (filtroRapido === 'bloqueados' && s?.geral !== 'bloqueado') return false;
      if (filtroRapido === 'terceirizados' && t.vinculo !== TipoVinculo.Terceirizado) return false;
      if (filtroRapido === 'asoVencendo' && s?.aso.tom === 'ok') return false;
      if (!termo) return true;
      return (
        t.nome.toLowerCase().includes(termo) ||
        (t.matricula ?? '').toLowerCase().includes(termo) ||
        (nomePorFuncao.get(t.funcaoId) ?? '').includes(termo)
      );
    });
  }, [busca, universo, funcaoFiltro, filtroRapido, situacoes, funcoes]);

  const filtrosAtivos = busca.trim() !== '' || funcaoFiltro !== '' || filtroRapido !== 'todos' || obraFiltro !== null;

  // Toda mudança de filtro volta para a primeira página da lista.
  function aplicarFiltroRapido(filtro: FiltroRapido) {
    setFiltroRapido(filtro);
    setLimite(PASSO_PAGINA);
  }

  // KPI clicado de novo desliga o filtro (volta para "Todos").
  function alternarFiltroKpi(filtro: FiltroRapido) {
    aplicarFiltroRapido(filtroRapido === filtro ? 'todos' : filtro);
  }

  function limparFiltros() {
    setBusca('');
    setFuncaoFiltro('');
    setFiltroRapido('todos');
    setObraFiltro(null);
    setLimite(PASSO_PAGINA);
  }

  function abrirPainelNovoFuncionario() {
    setNovoTrabalhador((atual) => ({ ...atual, obraId: obraFiltro ?? atual.obraId }));
    setPainelAberto(true);
  }

  // Todo caminho de fechar o painel limpa o formulário e o erro dele — senão reabrir mostra rascunho
  // e mensagem de uma tentativa anterior.
  function fecharPainel() {
    setPainelAberto(false);
    setErroPainel(null);
    setNovoTrabalhador(trabalhadorVazio);
  }

  async function criar() {
    try {
      setCarregando(true);
      setErroPainel(null);
      const { id } = await api.trabalhadores.criar(novoTrabalhador);
      setTrabalhadorRequisitosAlvo({ id, nome: novoTrabalhador.nome, funcaoId: novoTrabalhador.funcaoId });
      setNovoTrabalhador(trabalhadorVazio);
      setPainelAberto(false);
      await carregar();
      sucessoToast('Funcionário criado com sucesso.');
    } catch (e) {
      setErroPainel(e instanceof Error ? e.message : 'Falha ao criar funcionário.');
    } finally {
      setCarregando(false);
    }
  }

  function ponto(item: Item | null, rotulo: string) {
    if (!item) return null;
    const neutro = item.tom === 'neutro';
    return (
      <span
        role="img"
        aria-label={`${rotulo}: ${item.texto}`}
        title={`${rotulo}: ${item.texto}`}
        style={{
          width: 12,
          height: 12,
          borderRadius: '50%',
          flexShrink: 0,
          boxSizing: 'border-box',
          backgroundColor: neutro ? 'transparent' : tokensUi.status[item.tom].tinta,
          border: neutro ? `1.5px solid ${designTokens.colorNeutralMedium}` : 'none',
        }}
      />
    );
  }

  function pontos(s: Situacao | undefined) {
    return (
      <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
        {ponto(s?.aso ?? null, 'ASO')}
        {ponto(s?.nr ?? null, 'NR')}
        {ponto(s?.epi ?? null, 'EPI')}
      </div>
    );
  }

  const colunas: Coluna<Trabalhador>[] = [
    {
      chave: 'nome',
      rotulo: 'Funcionário',
      render: (t) => {
        const s = situacoes.get(t.id);
        const tomAnel: Tom = s ? rotuloGeral[s.geral].tom : 'neutro';
        return (
          <div style={{ display: 'flex', alignItems: 'center', gap: 12, minWidth: 0 }}>
            {/* Anel do avatar na cor da situação geral — leitura rápida antes do chip. */}
            <span
              style={{
                display: 'inline-flex',
                flexShrink: 0,
                padding: 2,
                borderRadius: '50%',
                boxShadow: `0 0 0 2px ${tokensUi.status[tomAnel].tinta}`,
              }}
            >
              {fotoUrls[t.id] ? (
                <Avatar image={{ src: fotoUrls[t.id] }} size={40} name={t.nome} />
              ) : (
                <Avatar name={t.nome} color="colorful" size={40} />
              )}
            </span>
            <div style={{ minWidth: 0 }}>
              <div style={{ fontWeight: 600 }}>{t.nome}</div>
              <Legenda>
                {t.matricula ? `Mat. ${t.matricula} · ` : ''}
                {nomeFuncao(t.funcaoId)}
              </Legenda>
              {telaEstreita && (
                <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginTop: 6, flexWrap: 'wrap' }}>
                  {pontos(s)}
                  {s && <StatusChip tom={rotuloGeral[s.geral].tom}>{rotuloGeral[s.geral].texto}</StatusChip>}
                </div>
              )}
            </div>
          </div>
        );
      },
    },
  ];
  if (!telaEstreita) {
    colunas.push(
      {
        chave: 'obra',
        rotulo: 'Obra e vínculo',
        render: (t) => (
          <div style={{ minWidth: 0 }}>
            <div>{nomeObra.get(t.obraId) ?? '—'}</div>
            <Legenda>{tipoVinculoLabel[t.vinculo] ?? '—'}</Legenda>
          </div>
        ),
      },
      {
        chave: 'situacoes',
        rotulo: 'ASO · NR · EPI',
        render: (t) => pontos(situacoes.get(t.id)),
      },
      {
        chave: 'situacao',
        rotulo: 'Situação',
        render: (t) => {
          const s = situacoes.get(t.id);
          return s ? <StatusChip tom={rotuloGeral[s.geral].tom}>{rotuloGeral[s.geral].texto}</StatusChip> : null;
        },
      },
    );
  }

  const acoes = (
    <div style={{ display: 'flex', gap: tokensUi.espaco.sm, flexWrap: 'wrap' }}>
      {acoesCabecalho}
      <Button
        appearance="primary"
        icon={<Add24Regular />}
        onClick={() => (painelAberto ? fecharPainel() : abrirPainelNovoFuncionario())}
        aria-expanded={painelAberto}
        aria-controls="painel-novo-funcionario"
      >
        {painelAberto ? 'Fechar' : 'Novo funcionário'}
      </Button>
    </div>
  );

  const linhasVisiveis = trabalhadoresFiltrados.slice(0, limite);
  const obrasFaixa: Array<{ id: string | null; nome: string; total: number; bloqueados: number }> = [
    { id: null, nome: 'Todas as obras', total: trabalhadores.length, bloqueados: 0 },
    ...obras.map((o) => ({ id: o.id, nome: o.nome, ...(resumoPorObra.get(o.id) ?? { total: 0, bloqueados: 0 }) })),
  ];

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: tokensUi.espaco.lg }}>
      {dialogElement}
      {mostrarTitulo ? (
        <PageHeader
          titulo="Funcionários"
          subtitulo="Encontre qualquer pessoa em todas as obras e veja, na própria lista, quem pode trabalhar hoje."
          acoes={acoes}
        />
      ) : (
        <div style={{ display: 'flex', justifyContent: 'flex-end' }}>{acoes}</div>
      )}

      {erro && <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>{erro}</FeedbackInline>}

      <div id="painel-novo-funcionario">
        <PainelCriacaoInline aberto={painelAberto} titulo="Novo funcionário">
          <FormSection titulo="Dados do funcionário" numero={1} primeira>
            {erroPainel && (
              <FeedbackInline tom="erro" aoFechar={() => setErroPainel(null)}>
                {erroPainel}
              </FeedbackInline>
            )}
            <FormGrid>
              <Campo span={6}>
                <Field label="Obra">
                  <Select
                    value={novoTrabalhador.obraId}
                    onChange={(_, d) => setNovoTrabalhador({ ...novoTrabalhador, obraId: d.value })}
                  >
                    <option value="">Selecione</option>
                    {obras.map((obra) => (
                      <option key={obra.id} value={obra.id}>
                        {obra.nome}
                      </option>
                    ))}
                  </Select>
                </Field>
              </Campo>
              <Campo span={6}>
                <Field label="Função">
                  <Select
                    value={novoTrabalhador.funcaoId}
                    onChange={(_, d) => setNovoTrabalhador({ ...novoTrabalhador, funcaoId: d.value })}
                  >
                    <option value="">Selecione</option>
                    {funcoes.map((funcao) => (
                      <option key={funcao.id} value={funcao.id}>
                        {funcao.nome}
                      </option>
                    ))}
                  </Select>
                </Field>
              </Campo>
              <Campo span={8}>
                <Field label="Nome">
                  <Input
                    value={novoTrabalhador.nome}
                    onChange={(_, d) => setNovoTrabalhador({ ...novoTrabalhador, nome: d.value })}
                  />
                </Field>
              </Campo>
              <Campo span={4}>
                <Field label="Matrícula">
                  <Input
                    value={novoTrabalhador.matricula ?? ''}
                    onChange={(_, d) => setNovoTrabalhador({ ...novoTrabalhador, matricula: d.value })}
                  />
                </Field>
              </Campo>
              <Campo span={6}>
                <Field label="CPF (11 dígitos)">
                  <Input
                    value={formatarCpf(novoTrabalhador.cpf)}
                    onChange={(_, d) =>
                      setNovoTrabalhador({ ...novoTrabalhador, cpf: d.value.replace(/\D/g, '').slice(0, 11) })
                    }
                  />
                </Field>
              </Campo>
              <Campo span={3}>
                <Field label="Vínculo">
                  <Select
                    value={novoTrabalhador.vinculo}
                    onChange={(_, d) => setNovoTrabalhador({ ...novoTrabalhador, vinculo: Number(d.value) })}
                  >
                    {Object.entries(tipoVinculoLabel).map(([valor, rotulo]) => (
                      <option key={valor} value={valor}>
                        {rotulo}
                      </option>
                    ))}
                  </Select>
                </Field>
              </Campo>
              <Campo span={3}>
                <Field label="Data de admissão">
                  <CampoData
                    value={novoTrabalhador.dataAdmissao}
                    onChange={(_, d) => setNovoTrabalhador({ ...novoTrabalhador, dataAdmissao: d.value })}
                  />
                </Field>
              </Campo>
            </FormGrid>
            <FormRodape>
              <Button onClick={fecharPainel}>Cancelar</Button>
              <Button appearance="primary" onClick={criar} disabled={carregando}>
                Adicionar funcionário
              </Button>
            </FormRodape>
          </FormSection>
        </PainelCriacaoInline>
      </div>

      <Card>
        <div style={{ display: 'flex', flexDirection: 'column', gap: tokensUi.espaco.md }}>
          <Input
            size="large"
            contentBefore={<Search24Regular />}
            value={busca}
            onChange={(_, d) => {
              setBusca(d.value);
              setLimite(PASSO_PAGINA);
            }}
            placeholder="Buscar por nome, matrícula ou função em todas as obras"
            aria-label="Buscar funcionário por nome, matrícula ou função"
          />
          <div style={{ display: 'flex', gap: tokensUi.espaco.sm, alignItems: 'center', flexWrap: 'wrap' }}>
            <div role="group" aria-label="Filtro rápido" style={{ display: 'flex', gap: 6, flexWrap: 'wrap' }}>
              {FILTROS_RAPIDOS.map((f) => (
                <Button
                  key={f.valor}
                  size="small"
                  appearance={filtroRapido === f.valor ? 'primary' : 'secondary'}
                  aria-pressed={filtroRapido === f.valor}
                  onClick={() => aplicarFiltroRapido(f.valor)}
                >
                  {f.rotulo}
                </Button>
              ))}
            </div>
            <Select
              value={funcaoFiltro}
              onChange={(_, d) => {
                setFuncaoFiltro(d.value);
                setLimite(PASSO_PAGINA);
              }}
              aria-label="Filtrar por função"
            >
              <option value="">Todas as funções</option>
              {funcoes.map((f) => (
                <option key={f.id} value={f.id}>
                  {f.nome}
                </option>
              ))}
            </Select>
            <div style={{ marginLeft: 'auto', display: 'flex', alignItems: 'center', gap: tokensUi.espaco.sm }}>
              <Legenda>
                {trabalhadoresFiltrados.length} {trabalhadoresFiltrados.length === 1 ? 'resultado' : 'resultados'}
              </Legenda>
              {filtrosAtivos && (
                <Button size="small" appearance="subtle" onClick={limparFiltros}>
                  Limpar filtros
                </Button>
              )}
            </div>
          </div>
        </div>
      </Card>

      <FaixaKpis>
        <KpiCard
          rotulo="Funcionários ativos"
          valor={kpis.total}
          tom="info"
          indice={0}
          icone={<People24Regular />}
          onClick={() => aplicarFiltroRapido('todos')}
          ariaLabel="Mostrar todos os funcionários"
          deltas={[{ texto: obraFiltro ? (nomeObra.get(obraFiltro) ?? 'Obra') : `em ${obras.length} obras`, tom: 'neutro' }]}
        />
        <KpiCard
          rotulo="Aptos para trabalhar"
          valor={kpis.aptos}
          tom="ok"
          indice={1}
          icone={<CheckmarkCircle24Regular />}
          onClick={() => alternarFiltroKpi('aptos')}
          ariaLabel="Filtrar funcionários aptos"
          deltas={filtroRapido === 'aptos' ? [{ texto: 'Filtro ativo', tom: 'ok' }] : undefined}
        />
        <KpiCard
          rotulo="Bloqueados"
          valor={kpis.bloqueados}
          tom="alerta"
          indice={2}
          icone={<DismissCircle24Regular />}
          onClick={() => alternarFiltroKpi('bloqueados')}
          ariaLabel="Filtrar funcionários bloqueados"
          deltas={[
            filtroRapido === 'bloqueados' ? { texto: 'Filtro ativo', tom: 'alerta' } : { texto: 'ASO, NR ou EPI', tom: 'neutro' },
          ]}
        />
        <KpiCard
          rotulo="ASOs vencendo ou vencidos"
          valor={kpis.asoVencendo}
          tom="atencao"
          indice={3}
          icone={<Warning24Regular />}
          onClick={() => alternarFiltroKpi('asoVencendo')}
          ariaLabel="Filtrar funcionários com ASO vencendo, vencido ou ausente"
          deltas={[
            filtroRapido === 'asoVencendo'
              ? { texto: 'Filtro ativo', tom: 'atencao' }
              : { texto: 'inclui sem ASO e próximos 30 dias', tom: 'neutro' },
          ]}
        />
      </FaixaKpis>

      <div style={{ display: 'flex', flexWrap: 'wrap', gap: tokensUi.espaco.sm, alignItems: 'flex-start' }}>
        <CadastrosFaciaisFracosCard compacto />
        <Button
          appearance="secondary"
          icon={<DataBarVertical24Regular />}
          onClick={() => setAnaliseAberta((v) => !v)}
          aria-expanded={analiseAberta}
          aria-controls="analise-pessoas"
        >
          {analiseAberta ? 'Ocultar análise' : 'Ver análise'}
          {analiseAberta ? <ChevronUp20Regular style={{ marginLeft: 6 }} /> : <ChevronDown20Regular style={{ marginLeft: 6 }} />}
        </Button>
      </div>

      {/* Gráficos e Motor de Elegibilidade só montam quando abertos — quem veio só buscar uma pessoa
          não paga as consultas do dashboard. */}
      {analiseAberta && (
        <div id="analise-pessoas">
          <Card titulo="Análise da força de trabalho" subtitulo="Status geral, cobertura de NR, vencimento de ASO e elegibilidade">
            <PessoasDashboardTab />
          </Card>
        </div>
      )}

      {!carregandoLista && obras.length === 0 ? (
        <Card>
          <EstadoVazio
            titulo="Nenhuma obra cadastrada ainda."
            descricao="Cadastre uma obra em Obras para depois vincular funcionários a ela."
          />
        </Card>
      ) : (
        <nav aria-label="Filtrar por obra" style={{ display: 'flex', gap: tokensUi.espaco.sm, overflowX: 'auto', paddingBottom: 4 }}>
          {obrasFaixa.map((o) => {
            const ativo = obraFiltro === o.id;
            return (
              <button
                key={o.id ?? 'todas'}
                type="button"
                aria-pressed={ativo}
                onClick={() => {
                  setObraFiltro(o.id);
                  setLimite(PASSO_PAGINA);
                }}
                style={{
                  flex: 'none',
                  minWidth: 170,
                  textAlign: 'left',
                  cursor: 'pointer',
                  padding: '10px 14px',
                  borderRadius: tokensUi.raio.md,
                  border: `1px solid ${ativo ? designTokens.colorPrimary : designTokens.colorCardBorder}`,
                  backgroundColor: ativo
                    ? 'color-mix(in srgb, var(--sst-color-surface) 88%, var(--sst-color-primary))'
                    : designTokens.colorSurface,
                  color: 'inherit',
                  font: 'inherit',
                }}
              >
                <div className={tipo.corpo} style={{ fontWeight: 600 }}>
                  {o.nome}
                </div>
                <Legenda>
                  {o.total} {o.total === 1 ? 'funcionário' : 'funcionários'}
                  {o.bloqueados > 0 && (
                    <span style={{ color: tokensUi.status.alerta.tinta, fontWeight: 600 }}>
                      {' '}
                      · {o.bloqueados} {o.bloqueados === 1 ? 'bloqueado' : 'bloqueados'}
                    </span>
                  )}
                </Legenda>
              </button>
            );
          })}
        </nav>
      )}

      <Card>
        <DataTable
          aria-label="Funcionários cadastrados"
          colunas={colunas}
          linhas={linhasVisiveis}
          chaveLinha={(t) => t.id}
          carregando={carregandoLista}
          aoClicarLinha={(t) => navigate(`/pessoas/${t.id}`)}
          vazio={
            trabalhadores.length === 0
              ? {
                  titulo: 'Nenhum funcionário cadastrado ainda.',
                  descricao: 'Cadastre o primeiro funcionário para começar.',
                  acao: { rotulo: 'Novo funcionário', aoClicar: abrirPainelNovoFuncionario },
                }
              : {
                  titulo: 'Nenhum funcionário encontrado.',
                  descricao: 'Tente outro nome ou limpe os filtros.',
                  variante: 'sem-resultado',
                  acao: { rotulo: 'Limpar filtros', aoClicar: limparFiltros },
                }
          }
        />
        {trabalhadoresFiltrados.length > 0 && (
          <div
            style={{
              display: 'flex',
              justifyContent: 'space-between',
              alignItems: 'center',
              gap: tokensUi.espaco.sm,
              paddingTop: tokensUi.espaco.md,
            }}
          >
            <Legenda>
              Mostrando {linhasVisiveis.length} de {trabalhadoresFiltrados.length}
            </Legenda>
            {trabalhadoresFiltrados.length > linhasVisiveis.length && (
              <Button size="small" onClick={() => setLimite((l) => l + PASSO_PAGINA)}>
                Carregar mais
              </Button>
            )}
          </div>
        )}
      </Card>

      <RequisitosFuncaoDialog
        funcaoId={trabalhadorRequisitosAlvo?.funcaoId ?? null}
        trabalhadorNome={trabalhadorRequisitosAlvo?.nome}
        funcaoNome={trabalhadorRequisitosAlvo ? nomeFuncao(trabalhadorRequisitosAlvo.funcaoId) : undefined}
        aoFechar={() => {
          const alvo = trabalhadorRequisitosAlvo;
          setTrabalhadorRequisitosAlvo(null);
          if (alvo) setTrabalhadorDigitalAlvo({ id: alvo.id, nome: alvo.nome });
        }}
      />

      <CadastroDigitalDialog
        trabalhadorId={trabalhadorDigitalAlvo?.id ?? null}
        trabalhadorNome={trabalhadorDigitalAlvo?.nome}
        aoFechar={() => setTrabalhadorDigitalAlvo(null)}
        aoConcluir={carregar}
      />
    </div>
  );
}
