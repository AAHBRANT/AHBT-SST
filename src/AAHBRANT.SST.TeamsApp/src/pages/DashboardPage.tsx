import { useEffect, useMemo, useState, type KeyboardEvent, type ReactElement } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  BuildingBank24Regular,
  People24Regular,
  ShieldCheckmark24Regular,
  CheckmarkCircle24Regular,
  ClipboardTaskListLtr24Regular,
  PersonAdd24Regular,
  HatGraduation24Regular,
  Stethoscope24Regular,
  DocumentBulletList24Regular,
  Alert24Regular,
  ArrowClockwise16Regular,
  Shield24Regular,
} from '@fluentui/react-icons';
import {
  api,
  ResultadoAso,
  StatusObra,
  StatusNaoConformidade,
  StatusAlerta,
  StatusDds,
  MotivoEntregaEpi,
  StatusInspecao,
  TipoOcorrencia,
  type Acidente,
  type Alerta,
  type Aso,
  type AptidaoCurso,
  type LiberacaoTrabalho,
  type Atividade,
  type Dds,
  type EntregaEpi,
  type Inspecao,
  type NaoConformidade,
  type Obra,
  type RegistroHhtMensal,
  type Trabalhador,
  type Treinamento,
} from '../lib/api';
import { asoLiberado, asoVigentePorTrabalhador, situacaoDoVigente } from '../lib/aso';
import { Button, Card, FeedbackInline, KpiCard, Legenda, Select, designTokens, usePaletaGraficos, type Tom } from '@ui';
import { AptidaoTreinamentosCard } from '../components/dashboard/AptidaoTreinamentosCard';
import { ConformidadePorObraCard, type ConformidadeObra } from '../components/dashboard/ConformidadePorObraCard';
import { PiramideOcorrenciasCard, type NivelPiramide } from '../components/dashboard/PiramideOcorrenciasCard';
import { DiasSemAcidente } from '../components/dashboard/DiasSemAcidente';
import { AsoRoscaCard, type FatiaAso } from '../components/dashboard/AsoRoscaCard';
import { LinhaClicavel, RodapeLista, TituloGrupo, type VarianteIcone } from '../components/dashboard/LinhaClicavel';
import { useDashboardStyles } from '../components/dashboard/dashboardStyles';
import { TaxaGravidadeCard } from '../components/dashboard/TaxaGravidadeCard';
import { BloqueadosDialog } from '../components/dashboard/BloqueadosDialog';
import { useFiltroObra } from '../lib/ObraAtivaContext';

interface KpiDelta {
  texto: string;
  tom: Tom;
  pulsar?: 'rapido' | 'leve';
}

interface Kpi {
  rotulo: string;
  valor: string;
  icone: ReactElement;
  tom: Tom;
  deltas: KpiDelta[];
  destino: string;
  // Quando definido, o clique executa isto em vez de navegar para `destino`.
  aoClicar?: () => void;
  /** Indicador principal: vem primeiro, mais largo, em vidro esmeralda com brilho. */
  destaque?: boolean;
  /** Total ao lado do valor (ex.: "/ 200"). */
  total?: number;
}

interface ItemFeed {
  id: string;
  icone: ReactElement;
  variante: VarianteIcone;
  titulo: string;
  meta: string;
  dataISO: string;
  /** Rota do item de origem (ex.: /prevencao/dds/dia/:id). */
  destino: string;
}

type PeriodoDashboard = 'semana' | 'mes' | 'ano' | 'tudo';

const PERIODOS_DASHBOARD: Array<{ valor: PeriodoDashboard; rotulo: string }> = [
  { valor: 'semana', rotulo: 'Última semana' },
  { valor: 'mes', rotulo: 'Último mês' },
  { valor: 'ano', rotulo: 'Último ano' },
  { valor: 'tudo', rotulo: 'Tudo' },
];

function formatarDataRelativa(dataISO: string): string {
  const data = new Date(dataISO);
  const hoje = new Date();
  const diffDias = Math.round(
    (new Date(hoje.toDateString()).getTime() - new Date(data.toDateString()).getTime()) / 86_400_000,
  );
  if (diffDias === 0) return 'Hoje';
  if (diffDias === 1) return 'Ontem';
  return data.toLocaleDateString('pt-BR');
}

// Onda 2 Task 21 (camada ui/, conversão 8): dashboard geral — mesmo formato de AprDashboardTab.tsx/
// PgrDashboardTab.tsx (KpiCard+Card+usePaletaGraficos, grade CSS Grid simples, spec §4.4). A grade de
// "atividade recente"/"próximos vencimentos" não tem equivalente em @ui (não é Tabela/Badge/Dashboard
// padrão) — mantém a renderização própria de feed (components/dashboard/dashboardStyles.ts), só
// trocando o card/título que a envolve para `Card` e o texto de vazio para `Legenda`.
export function DashboardPage() {
  const dashEstilos = useDashboardStyles();
  const paleta = usePaletaGraficos();
  const navigate = useNavigate();

  const [obras, setObras] = useState<Obra[]>([]);
  // Ligado à obra ativa do cabeçalho: vale para o sistema todo (28/10/2026).
  const [obraSelecionadaId, setObraSelecionadaId] = useFiltroObra();
  const [periodoSelecionado, setPeriodoSelecionado] = useState<PeriodoDashboard>('tudo');
  const [atividades, setAtividades] = useState<Atividade[]>([]);
  const [trabalhadores, setTrabalhadores] = useState<Trabalhador[]>([]);
  const [asos, setAsos] = useState<Aso[]>([]);
  const [treinamentos, setTreinamentos] = useState<Treinamento[]>([]);
  const [entregasEpi, setEntregasEpi] = useState<EntregaEpi[]>([]);
  const [acidentes, setAcidentes] = useState<Acidente[]>([]);
  const [naoConformidades, setNaoConformidades] = useState<NaoConformidade[]>([]);
  const [alertasAbertos, setAlertasAbertos] = useState<Alerta[]>([]);
  const [registrosHht, setRegistrosHht] = useState<RegistroHhtMensal[]>([]);
  const [dds, setDds] = useState<Dds[]>([]);
  const [inspecoes, setInspecoes] = useState<Inspecao[]>([]);
  const [aptidaoCursos, setAptidaoCursos] = useState<AptidaoCurso[]>([]);
  const [liberacao, setLiberacao] = useState<LiberacaoTrabalho | null>(null);
  const [bloqueadosAberto, setBloqueadosAberto] = useState(false);
  const [erro, setErro] = useState<string | null>(null);
  // "Atualizado há X min" (28/10/2026): `recarga` refaz as buscas; `agora` avança a cada minuto.
  const [recarga, setRecarga] = useState(0);
  const [atualizadoEm, setAtualizadoEm] = useState(() => Date.now());
  const [agora, setAgora] = useState(() => Date.now());
  useEffect(() => {
    const id = window.setInterval(() => setAgora(Date.now()), 60_000);
    return () => window.clearInterval(id);
  }, []);

  useEffect(() => {
    // obraId filtra no servidor em vez de trazer a empresa inteira e recortar aqui (memória
    // acompanha com os useMemo de *Filtrados abaixo, que continuam existindo por segurança — filtro
    // client-side idempotente sobre dado já filtrado não muda o resultado, só protege contra
    // eventuais respostas não filtradas). obras.listar() nunca leva obraId: alimenta o próprio
    // seletor, precisa sempre da lista completa.
    const obraId = obraSelecionadaId || undefined;
    let cancelado = false;

    api.atividades
      .listar(obraId)
      .then((resp) => {
        if (!cancelado) setAtividades(resp);
      })
      .catch(() => {
        if (!cancelado) setAtividades([]);
      });

    api.trabalhadores
      .liberacao(obraId)
      .then((resp) => {
        if (!cancelado) setLiberacao(resp);
      })
      .catch(() => {
        if (!cancelado) setLiberacao(null);
      });

    api.cursosTreinamento
      .aptidao(obraId)
      .then((resp) => {
        if (!cancelado) setAptidaoCursos(resp);
      })
      .catch(() => {
        if (!cancelado) setAptidaoCursos([]);
      });

    Promise.all([
      api.obras.listar(),
      api.trabalhadores.listar(obraId),
      api.asos.listar(undefined, obraId),
      api.treinamentos.listar(undefined, obraId),
      api.entregasEpi.listar(undefined, obraId),
      api.acidentes.listar({ obraId }),
      api.naoConformidades.listar(undefined, obraId),
      api.alertas.listar({ status: StatusAlerta.Aberto, obraId }),
      api.registrosHht.listar({ obraId }),
      api.dds.listar(obraId),
      api.inspecoes.listar(obraId),
    ])
      .then(
        ([
          obrasResp,
          trabalhadoresResp,
          asosResp,
          treinamentosResp,
          entregasEpiResp,
          acidentesResp,
          naoConformidadesResp,
          alertasResp,
          registrosHhtResp,
          ddsResp,
          inspecoesResp,
        ]) => {
          if (cancelado) return;
          setObras(obrasResp);
          setTrabalhadores(trabalhadoresResp);
          setAsos(asosResp);
          setTreinamentos(treinamentosResp);
          setEntregasEpi(entregasEpiResp);
          setAcidentes(acidentesResp);
          setNaoConformidades(naoConformidadesResp);
          setAlertasAbertos(alertasResp);
          setRegistrosHht(registrosHhtResp);
          setDds(ddsResp);
          setInspecoes(inspecoesResp);
          setAtualizadoEm(Date.now());
          setAgora(Date.now());
        },
      )
      .catch((e) => {
        if (!cancelado) setErro(e instanceof Error ? e.message : 'Falha ao carregar indicadores.');
      });

    return () => {
      cancelado = true;
    };
  }, [obraSelecionadaId, recarga]);

  function nomeObra(id: string) {
    return obras.find((o) => o.id === id)?.nome ?? id;
  }

  const hojeISO = new Date().toISOString().slice(0, 10);
  // "Hoje" no fuso do usuário (DDS de hoje, EPIs entregues hoje): à noite, o toISOString já está no
  // dia seguinte em UTC e zeraria os dois indicadores.
  const hojeLocal = new Date().toLocaleDateString('sv-SE');
  const inicioPeriodoISO = useMemo(() => {
    if (periodoSelecionado === 'tudo') return null;
    const data = new Date(hojeISO);
    if (periodoSelecionado === 'semana') data.setDate(data.getDate() - 7);
    if (periodoSelecionado === 'mes') data.setMonth(data.getMonth() - 1);
    if (periodoSelecionado === 'ano') data.setFullYear(data.getFullYear() - 1);
    return data.toISOString().slice(0, 10);
  }, [hojeISO, periodoSelecionado]);
  const noPeriodoSelecionado = (dataISO?: string | null) => !!dataISO && (!inicioPeriodoISO || dataISO >= inicioPeriodoISO);
  const obraSelecionada = obras.find((obra) => obra.id === obraSelecionadaId);
  const escopoIndicadores = obraSelecionada?.nome ?? 'todas as obras';
  const atividadesDaObraIds = useMemo(
    () => new Set(atividades.filter((atividade) => atividade.obraId === obraSelecionadaId).map((atividade) => atividade.id)),
    [atividades, obraSelecionadaId],
  );
  const trabalhadoresFiltrados = useMemo(
    () => (obraSelecionadaId ? trabalhadores.filter((trabalhador) => trabalhador.obraId === obraSelecionadaId) : trabalhadores),
    [trabalhadores, obraSelecionadaId],
  );
  const trabalhadoresDaObraIds = useMemo(
    () => new Set(trabalhadoresFiltrados.map((trabalhador) => trabalhador.id)),
    [trabalhadoresFiltrados],
  );
  const obrasFiltradas = useMemo(
    () => (obraSelecionadaId ? obras.filter((obra) => obra.id === obraSelecionadaId) : obras),
    [obras, obraSelecionadaId],
  );
  const asosFiltrados = useMemo(
    () => (obraSelecionadaId ? asos.filter((aso) => trabalhadoresDaObraIds.has(aso.trabalhadorId)) : asos),
    [asos, trabalhadoresDaObraIds, obraSelecionadaId],
  );
  const entregasEpiFiltradas = useMemo(
    () => (obraSelecionadaId ? entregasEpi.filter((entrega) => trabalhadoresDaObraIds.has(entrega.trabalhadorId)) : entregasEpi),
    [entregasEpi, trabalhadoresDaObraIds, obraSelecionadaId],
  );
  const acidentesFiltrados = useMemo(
    () => (obraSelecionadaId ? acidentes.filter((acidente) => acidente.obraId === obraSelecionadaId) : acidentes),
    [acidentes, obraSelecionadaId],
  );
  const naoConformidadesFiltradas = useMemo(
    () =>
      obraSelecionadaId
        ? naoConformidades.filter((naoConformidade) =>
            naoConformidade.atividadeId ? atividadesDaObraIds.has(naoConformidade.atividadeId) : false,
          )
        : naoConformidades,
    [naoConformidades, atividadesDaObraIds, obraSelecionadaId],
  );
  const alertasAbertosFiltrados = useMemo(
    () => (obraSelecionadaId ? alertasAbertos.filter((alerta) => alerta.obraId === obraSelecionadaId) : alertasAbertos),
    [alertasAbertos, obraSelecionadaId],
  );
  const registrosHhtFiltrados = useMemo(
    () => (obraSelecionadaId ? registrosHht.filter((registro) => registro.obraId === obraSelecionadaId) : registrosHht),
    [registrosHht, obraSelecionadaId],
  );
  const ddsFiltrados = useMemo(
    () => (obraSelecionadaId ? dds.filter((registro) => registro.obraId === obraSelecionadaId) : dds),
    [dds, obraSelecionadaId],
  );
  const inspecoesFiltradas = useMemo(
    () => (obraSelecionadaId ? inspecoes.filter((inspecao) => inspecao.obraId === obraSelecionadaId) : inspecoes),
    [inspecoes, obraSelecionadaId],
  );

  // ---------- KPIs ----------

  const obrasAtivas = useMemo(
    () => obrasFiltradas.filter((o) => o.status !== StatusObra.Encerrada && o.status !== StatusObra.Concluida),
    [obrasFiltradas],
  );
  const obrasEmAndamento = obrasAtivas.filter((o) => o.status === StatusObra.EmAndamento).length;

  const trabalhadoresAtivos = useMemo(() => trabalhadoresFiltrados.filter((t) => !t.dataDemissao), [trabalhadoresFiltrados]);
  const admitidosNoPeriodo = trabalhadoresAtivos.filter((t) => noPeriodoSelecionado(t.dataAdmissao)).length;

  // EPIs entregues hoje (28/10/2026, no lugar de "Trabalhadores com EPI vencido"): soma das
  // quantidades entregues hoje. "Entrega" = motivo Inicial (ou sem motivo informado); "troca" = Dano,
  // Extravio, Vencimento ou Troca de função.
  const entregasHoje = useMemo(
    () => entregasEpiFiltradas.filter((e) => e.dataEntrega?.slice(0, 10) === hojeLocal),
    [entregasEpiFiltradas, hojeLocal],
  );
  const epiHoje = useMemo(() => {
    const ehTroca = (e: EntregaEpi) => e.motivoTipo !== null && e.motivoTipo !== undefined && e.motivoTipo !== MotivoEntregaEpi.Inicial;
    return {
      itens: entregasHoje.reduce((soma, e) => soma + (e.quantidade || 1), 0),
      trabalhadores: new Set(entregasHoje.map((e) => e.trabalhadorId)).size,
      entregas: entregasHoje.filter((e) => !ehTroca(e)).length,
      trocas: entregasHoje.filter(ehTroca).length,
    };
  }, [entregasHoje]);

  // Os 6 tipos de ocorrência, separados (decisão do usuário, 04/10). A cor de cada um é fixa para o
  // chip do resumo e a série do gráfico usarem a mesma.
  const tiposOcorrencia = useMemo(
    () => [
      { chave: 'acidente', tipo: TipoOcorrencia.Acidente, rotulo: 'Acidentes', cor: paleta.alerta },
      { chave: 'incidente', tipo: TipoOcorrencia.Incidente, rotulo: 'Incidentes', cor: paleta.atencao },
      { chave: 'quaseAcidente', tipo: TipoOcorrencia.QuaseAcidente, rotulo: 'Quase-acidentes', cor: paleta.marca },
      { chave: 'condicaoInsegura', tipo: TipoOcorrencia.CondicaoInsegura, rotulo: 'Condições inseguras', cor: paleta.info },
      { chave: 'atoInseguro', tipo: TipoOcorrencia.AtoInseguro, rotulo: 'Atos inseguros', cor: paleta.ok },
      { chave: 'doencaOcupacional', tipo: TipoOcorrencia.DoencaOcupacional, rotulo: 'Doenças ocupacionais', cor: paleta.neutro },
    ],
    [paleta],
  );

  // DDS de hoje (28/10/2026, no lugar de "Não conformidades em atraso"): quantas obras ativas já
  // registraram o DDS do dia. Clique abre a lista de DDS.
  const ddsHoje = useMemo(() => {
    const doDia = ddsFiltrados.filter((d) => d.data?.slice(0, 10) === hojeLocal);
    const obrasComDds = new Set(doDia.map((d) => d.obraId));
    const concluidas = new Set(doDia.filter((d) => d.status === StatusDds.Concluido).map((d) => d.obraId));
    const aguardando = obrasAtivas.filter((o) => !obrasComDds.has(o.id));
    return { feitas: obrasAtivas.filter((o) => obrasComDds.has(o.id)).length, emAndamento: obrasComDds.size - concluidas.size, aguardando };
  }, [ddsFiltrados, obrasAtivas, hojeLocal]);

  // Chips do "Aptos para o campo" em linguagem positiva (28/10/2026): "a renovar", "aguardando".
  const chipsLiberacao: KpiDelta[] = liberacao
    ? [
        ...(liberacao.asoVencido > 0 ? [{ texto: `ASO a renovar: ${liberacao.asoVencido}`, tom: 'neutro' as const }] : []),
        ...(liberacao.semAso > 0 ? [{ texto: `ASO a agendar: ${liberacao.semAso}`, tom: 'neutro' as const }] : []),
        ...(liberacao.asoSemResultado > 0 ? [{ texto: `ASO aguardando aptidão no G-RH: ${liberacao.asoSemResultado}`, tom: 'neutro' as const }] : []),
        ...(liberacao.asoInapto > 0 ? [{ texto: `Inaptos: ${liberacao.asoInapto}`, tom: 'neutro' as const }] : []),
        ...(liberacao.treinamentoPendente > 0 ? [{ texto: `Treinamento a regularizar: ${liberacao.treinamentoPendente}`, tom: 'neutro' as const }] : []),
        ...(liberacao.bloqueados > 0 ? [{ texto: `${liberacao.bloqueados} aguardando liberação · ver quem ›`, tom: 'neutro' as const }] : []),
      ]
    : [];

  const nomesAguardandoDds = ddsHoje.aguardando.map((o) => o.nome);
  const textoAguardandoDds =
    nomesAguardandoDds.length === 0
      ? obraSelecionadaId
        ? 'DDS do dia registrado'
        : 'todas as obras com DDS hoje'
      : nomesAguardandoDds.length === 1
        ? `aguardando: ${nomesAguardandoDds[0]}`
        : `aguardando ${nomesAguardandoDds.length} obras`;

  // Ordem da faixa: o destaque primeiro (4 colunas), depois os outros 4 (2 colunas cada).
  const kpis: Kpi[] = [
    {
      rotulo: 'Aptos para o campo',
      valor: liberacao ? String(liberacao.liberados) : '—',
      total: liberacao?.ativos,
      icone: <CheckmarkCircle24Regular />,
      tom: 'ok',
      deltas: chipsLiberacao,
      destino: '/pessoas?aba=trabalhadores',
      aoClicar: liberacao && liberacao.bloqueados > 0 ? () => setBloqueadosAberto(true) : undefined,
      destaque: true,
    },
    {
      rotulo: 'Obras ativas',
      valor: String(obrasAtivas.length),
      icone: <BuildingBank24Regular />,
      tom: 'info',
      deltas: [{ texto: `${obrasEmAndamento} em andamento`, tom: 'neutro' }],
      destino: '/administracao?aba=obras',
    },
    {
      rotulo: 'Funcionários ativos',
      valor: String(trabalhadoresAtivos.length),
      icone: <People24Regular />,
      tom: 'info',
      deltas: admitidosNoPeriodo > 0 ? [{ texto: `+${admitidosNoPeriodo} no período`, tom: 'neutro' }] : [],
      destino: '/pessoas?aba=trabalhadores',
    },
    {
      rotulo: 'EPIs entregues hoje',
      valor: String(epiHoje.itens),
      icone: <Shield24Regular />,
      tom: 'ok',
      deltas:
        entregasHoje.length > 0
          ? [
              { texto: `${epiHoje.trabalhadores} trabalhadores atendidos`, tom: 'info' },
              ...(epiHoje.entregas > 0 ? [{ texto: `${epiHoje.entregas} entregas`, tom: 'ok' as const }] : []),
              ...(epiHoje.trocas > 0 ? [{ texto: `${epiHoje.trocas} trocas`, tom: 'neutro' as const }] : []),
            ]
          : [{ texto: 'nenhuma entrega registrada hoje', tom: 'neutro' }],
      destino: '/operacao?secao=epi&aba=entregas',
    },
    {
      rotulo: 'DDS de hoje',
      valor: String(ddsHoje.feitas),
      total: obrasAtivas.length,
      icone: <ClipboardTaskListLtr24Regular />,
      tom: ddsHoje.aguardando.length === 0 ? 'ok' : 'atencao',
      deltas: [
        { texto: textoAguardandoDds, tom: 'neutro' },
        ...(ddsHoje.emAndamento > 0 ? [{ texto: `${ddsHoje.emAndamento} em andamento`, tom: 'info' as const }] : []),
      ],
      destino: '/operacao?secao=dds',
    },
  ];

  // ---------- Status de aptidão ocupacional (ASO) ----------
  // Regra única do ASO vigente (lib/aso.ts, a mesma do backend): o ASO do exame mais recente de cada trabalhador decide;
  // os anteriores nunca contam como vencidos nem pendentes.

  const asoMaisRecentePorTrabalhador = useMemo(() => asoVigentePorTrabalhador(asosFiltrados), [asosFiltrados]);

  // Já considera a validade: um ASO "apto" que venceu deixa de contar como apto (antes aparecia como apto).
  const statusAsoGeral = useMemo(() => {
    const contagem = { aptos: 0, vencem: 0, vencidos: 0, inaptos: 0, restricao: 0, pendentes: 0 };
    const hojeMs = new Date(hojeISO).getTime();
    for (const trabalhador of trabalhadoresAtivos) {
      const aso = asoMaisRecentePorTrabalhador.get(trabalhador.id);
      const situacao = situacaoDoVigente(aso, hojeISO);
      if (situacao === 'SemAso' || situacao === 'SemParecer') contagem.pendentes += 1;
      else if (situacao === 'Inapto') contagem.inaptos += 1;
      else if (situacao === 'Vencido') contagem.vencidos += 1;
      else if (aso!.resultadoStatus === ResultadoAso.AptoComRestricao) contagem.restricao += 1;
      else if ((new Date(aso!.dataValidade.slice(0, 10)).getTime() - hojeMs) / 86_400_000 <= 30) contagem.vencem += 1;
      else contagem.aptos += 1;
    }
    return contagem;
  }, [trabalhadoresAtivos, asoMaisRecentePorTrabalhador, hojeISO]);

  // Rosca do ASO (28/10/2026): lista do mais urgente para o menos, em linguagem positiva.
  const fatiasAso: FatiaAso[] = [
    { chave: 'vencidos', rotulo: 'Renovação pendente', valor: statusAsoGeral.vencidos, cor: paleta.alerta },
    { chave: 'inaptos', rotulo: 'Inaptos', valor: statusAsoGeral.inaptos, cor: paleta.marca },
    { chave: 'vencem', rotulo: 'Renovar em até 30 dias', valor: statusAsoGeral.vencem, cor: paleta.atencao },
    { chave: 'pendentes', rotulo: 'Sem ASO ou pendente', valor: statusAsoGeral.pendentes, cor: paleta.neutro },
    { chave: 'restricao', rotulo: 'Restrição temporária', valor: statusAsoGeral.restricao, cor: paleta.info },
    { chave: 'aptos', rotulo: 'Aptos e válidos', valor: statusAsoGeral.aptos, cor: paleta.ok },
  ];
  const ordemAsoNaRosca = ['aptos', 'restricao', 'vencem', 'pendentes', 'inaptos', 'vencidos'];

  // ---------- Conformidade por obra ----------
  // Só faz sentido comparando obras, então some quando uma obra está filtrada. Usa as mesmas fórmulas
  // provisórias dos KPIs (EPI e treinamentos não vencidos) e, no ASO, conta o trabalhador ativo cujo
  // ASO mais recente é apto (com ou sem restrição) e está dentro da validade.
  const conformidadePorObra: ConformidadeObra[] = useMemo(() => {
    if (obraSelecionadaId) return [];
    const pct = (ok: number, total: number) => (total > 0 ? Math.round((ok / total) * 100) : null);
    const asoMaisRecente = asoVigentePorTrabalhador(asos);
    const linhas: ConformidadeObra[] = [];
    for (const obra of obrasAtivas) {
      const ativos = trabalhadores.filter((t) => t.obraId === obra.id && !t.dataDemissao);
      const ids = new Set(ativos.map((t) => t.id));
      const entregas = entregasEpi.filter((e) => ids.has(e.trabalhadorId) && !e.dataDevolucao);
      const epi = pct(entregas.filter((e) => !(e.dataValidade && e.dataValidade < hojeISO)).length, entregas.length);
      const trein = treinamentos.filter((t) => ids.has(t.trabalhadorId));
      const treinamentosPct = pct(trein.filter((t) => t.dataValidade >= hojeISO).length, trein.length);
      const asosEmDia = ativos.filter((t) => asoLiberado(asoMaisRecente.get(t.id), hojeISO)).length;
      const asoPct = pct(asosEmDia, ativos.length);
      const disponiveis = [epi, treinamentosPct, asoPct].filter((v): v is number => v !== null);
      if (disponiveis.length === 0) continue;
      linhas.push({
        id: obra.id,
        nome: obra.nome,
        geral: Math.round(disponiveis.reduce((soma, v) => soma + v, 0) / disponiveis.length),
        epi,
        treinamentos: treinamentosPct,
        aso: asoPct,
      });
    }
    // Da obra mais em dia para a menos ("Obras em dia", decisão do usuário em 28/10/2026).
    return linhas.sort((a, b) => b.geral - a.geral);
  }, [obraSelecionadaId, obrasAtivas, trabalhadores, entregasEpi, treinamentos, asos, hojeISO]);

  // ---------- Ocorrências: totais do período por tipo e evolução em 6 meses ----------

  // Pirâmide de segurança (28/10/2026): Acidentes no topo, Condições inseguras na base. Doença
  // ocupacional não é degrau da pirâmide e vai na nota de rodapé do card.
  const totalNoPeriodo = (tipo: number) => acidentesFiltrados.filter((a) => a.tipo === tipo && noPeriodoSelecionado(a.data)).length;
  const nivel = (tipo: number): NivelPiramide => {
    const t = tiposOcorrencia.find((x) => x.tipo === tipo)!;
    return { tipo, rotulo: t.rotulo, cor: t.cor, total: totalNoPeriodo(tipo) };
  };
  const niveisPiramide: NivelPiramide[] = [
    TipoOcorrencia.Acidente,
    TipoOcorrencia.Incidente,
    TipoOcorrencia.QuaseAcidente,
    TipoOcorrencia.AtoInseguro,
    TipoOcorrencia.CondicaoInsegura,
  ].map(nivel);
  const foraDaPiramide: NivelPiramide[] = [nivel(TipoOcorrencia.DoencaOcupacional)];

  // ---------- Agenda de renovações (alertas em aberto, antes "Próximos vencimentos") ----------

  const proximosVencimentos = useMemo(
    () =>
      [...alertasAbertosFiltrados]
        .sort((a, b) => (a.dataLimiteTratamento ?? '').localeCompare(b.dataLimiteTratamento ?? ''))
        .slice(0, 6),
    [alertasAbertosFiltrados],
  );

  // Cada alerta abre a página de onde ele veio (28/10/2026): a pessoa, quando o alerta é de alguém
  // (ASO, treinamento, EPI); senão o documento de origem; sem mapeamento, a lista de alertas.
  function destinoDoAlerta(alerta: Alerta): string {
    const id = alerta.entidadeOrigemId;
    switch (alerta.entidadeOrigemTipo) {
      case 'NaoConformidade':
        return `/nao-conformidades/${id}`;
      case 'Pcmso':
        return `/operacao/saude-ocupacional/pcmso/${id}`;
      case 'PgrRevisao':
      case 'PgrTermino':
        return `/prevencao/pgr/${id}`;
      case 'EntregaEpi':
        return alerta.trabalhadorId ? `/pessoas/${alerta.trabalhadorId}` : '/operacao?secao=epi&aba=entregas';
      default:
        return alerta.trabalhadorId ? `/pessoas/${alerta.trabalhadorId}` : '/alertas?aba=lista';
    }
  }

  function iconeDoAlerta(alerta: Alerta): { icone: ReactElement; variante: VarianteIcone } {
    switch (alerta.entidadeOrigemTipo) {
      case 'Aso':
      case 'Pcmso':
        return { icone: <Stethoscope24Regular />, variante: 'info' };
      case 'Treinamento':
        return { icone: <HatGraduation24Regular />, variante: 'marca' };
      case 'EntregaEpi':
        return { icone: <Shield24Regular />, variante: 'bom' };
      case 'PgrRevisao':
      case 'PgrTermino':
      case 'Contrato':
        return { icone: <DocumentBulletList24Regular />, variante: 'info' };
      default:
        return { icone: <Alert24Regular />, variante: 'atencao' };
    }
  }

  /** Dias até o prazo (negativo = já passou); null quando o alerta não tem prazo. */
  function diasAtePrazo(alerta: Alerta): number | null {
    if (!alerta.dataLimiteTratamento) return null;
    return Math.round((Date.parse(alerta.dataLimiteTratamento.slice(0, 10)) - Date.parse(hojeLocal)) / 86_400_000);
  }

  // ---------- Atividade recente (montada a partir dos módulos existentes) ----------

  const atividadeRecente: ItemFeed[] = useMemo(() => {
    const itens: ItemFeed[] = [];

    for (const registro of ddsFiltrados) {
      itens.push({
        id: `dds-${registro.id}`,
        icone: <ClipboardTaskListLtr24Regular />,
        variante: 'info',
        titulo: `DDS registrado — ${registro.atividadesNomes.join(', ') || 'DDS do dia'}`,
        meta: registro.obraNome,
        dataISO: registro.data,
        destino: `/prevencao/dds/dia/${registro.id}`,
      });
    }
    for (const inspecao of inspecoesFiltradas) {
      if (inspecao.status !== StatusInspecao.Concluida) continue;
      itens.push({
        id: `inspecao-${inspecao.id}`,
        icone: <ShieldCheckmark24Regular />,
        variante: 'bom',
        titulo: `Inspeção concluída — ${inspecao.checklistModeloNome}`,
        meta: inspecao.obraNome,
        dataISO: inspecao.data,
        destino: `/prevencao/inspecoes/${inspecao.id}`,
      });
    }
    for (const naoConformidade of naoConformidadesFiltradas) {
      if (naoConformidade.status !== StatusNaoConformidade.Encerrada || !naoConformidade.dataConclusao) continue;
      itens.push({
        id: `nc-${naoConformidade.id}`,
        icone: <CheckmarkCircle24Regular />,
        variante: 'bom',
        titulo: 'Não conformidade encerrada',
        meta: naoConformidade.descricao,
        dataISO: naoConformidade.dataConclusao,
        destino: `/nao-conformidades/${naoConformidade.id}`,
      });
    }
    for (const trabalhador of trabalhadoresAtivos) {
      itens.push({
        id: `trabalhador-${trabalhador.id}`,
        icone: <PersonAdd24Regular />,
        variante: 'info',
        titulo: `Novo funcionário admitido: ${trabalhador.nome}`,
        meta: nomeObra(trabalhador.obraId),
        dataISO: trabalhador.dataAdmissao,
        destino: `/pessoas/${trabalhador.id}`,
      });
    }

    return itens
      .filter((item) => noPeriodoSelecionado(item.dataISO))
      .sort((a, b) => b.dataISO.localeCompare(a.dataISO))
      .slice(0, 6);
  }, [ddsFiltrados, inspecoesFiltradas, naoConformidadesFiltradas, trabalhadoresAtivos, obras, inicioPeriodoISO]);


  // Prazo em linguagem simples e positiva: "hoje", "em 5 dias", "renovar já" (sem "vencido").
  function chipPrazo(alerta: Alerta) {
    const dias = diasAtePrazo(alerta);
    if (dias === null) return null;
    const [texto, cor, fundo] =
      dias < 0
        ? ['renovar já', designTokens.colorAlert, designTokens.colorAlertWash]
        : dias === 0
          ? ['hoje', designTokens.colorWarning, designTokens.colorWarningWash]
          : [`em ${dias} ${dias === 1 ? 'dia' : 'dias'}`, dias <= 7 ? designTokens.colorWarning : designTokens.colorInfo, dias <= 7 ? designTokens.colorWarningWash : designTokens.colorInfoWash];
    return (
      <span className={dashEstilos.chipPrazo} style={{ color: cor, backgroundColor: fundo }}>
        {texto}
      </span>
    );
  }

  const agendaSemana = proximosVencimentos.filter((a) => (diasAtePrazo(a) ?? 99) <= 7);
  const agendaDepois = proximosVencimentos.filter((a) => (diasAtePrazo(a) ?? 99) > 7);
  const minutosDesdeAtualizacao = Math.max(0, Math.floor((agora - atualizadoEm) / 60_000));

  function abrirComTeclado(evento: KeyboardEvent<HTMLDivElement>, destino: string) {
    if (evento.key === 'Enter' || evento.key === ' ') {
      evento.preventDefault();
      navigate(destino);
    }
  }

  return (
    <div>
      {erro && (
        <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>
          Não foi possível conectar à API ({erro}). Tente novamente em instantes; se persistir, avise o
          suporte.
        </FeedbackInline>
      )}

      <div className={dashEstilos.barraFiltrosDashboard}>
        <h1 className={dashEstilos.tituloPagina}>Início</h1>
        <div className={dashEstilos.filtrosDireita}>
          <span className={dashEstilos.atualizado} aria-live="polite">
            {minutosDesdeAtualizacao === 0 ? 'Atualizado agora' : `Atualizado há ${minutosDesdeAtualizacao} min`}
            <Button
              appearance="subtle"
              size="small"
              icon={<ArrowClockwise16Regular />}
              onClick={() => setRecarga((n) => n + 1)}
              aria-label="Atualizar os números da tela"
            >
              Atualizar
            </Button>
          </span>
          <div className={dashEstilos.grupoPeriodos} role="group" aria-label="Filtrar indicadores por período">
            {PERIODOS_DASHBOARD.map((periodo) => (
              <Button
                key={periodo.valor}
                appearance="subtle"
                className={
                  periodoSelecionado === periodo.valor ? dashEstilos.botaoPeriodoSelecionado : dashEstilos.botaoPeriodo
                }
                onClick={() => setPeriodoSelecionado(periodo.valor)}
              >
                {periodo.rotulo}
              </Button>
            ))}
          </div>
          <label className={dashEstilos.filtroObra}>
            <span style={{ fontWeight: 600, whiteSpace: 'nowrap' }}>Obra</span>
            <Select
              value={obraSelecionadaId}
              onChange={(_, dados) => setObraSelecionadaId(dados.value)}
              aria-label="Filtrar indicadores por obra"
            >
              <option value="">Todas as obras</option>
              {obras.map((obra) => (
                <option key={obra.id} value={obra.id}>
                  {obra.nome}
                </option>
              ))}
            </Select>
          </label>
        </div>
      </div>

      {/* Grade única de 12 colunas (proposta A, 28/10/2026). Linha 1: o destaque "Aptos para o campo"
          em 4 colunas e os outros 4 indicadores em 2 cada. */}
      <div className={dashEstilos.gradeInicio}>
        {kpis.map((kpi, indice) => (
          <div key={kpi.rotulo} className={`${dashEstilos.celula} ${kpi.destaque ? dashEstilos.coluna4Larga : dashEstilos.coluna2}`}>
            <KpiCard
              rotulo={kpi.rotulo}
              valor={
                kpi.total !== undefined ? (
                  <>
                    {kpi.valor}
                    <span className={dashEstilos.totalDestaque}> / {kpi.total}</span>
                  </>
                ) : (
                  kpi.valor
                )
              }
              tom={kpi.tom}
              icone={kpi.icone}
              deltas={kpi.deltas}
              indice={indice}
              destaque={kpi.destaque}
              extra={
                kpi.destaque && liberacao && liberacao.ativos > 0 ? (
                  <span className={dashEstilos.barraDestaque} aria-hidden="true">
                    <span
                      className={dashEstilos.barraDestaqueValor}
                      style={{ width: `${(liberacao.liberados / liberacao.ativos) * 100}%` }}
                    />
                  </span>
                ) : undefined
              }
              onClick={kpi.aoClicar ?? (() => navigate(kpi.destino))}
              ariaLabel={`Abrir ${kpi.rotulo}`}
            />
          </div>
        ))}
      </div>
      <BloqueadosDialog
        aberto={bloqueadosAberto}
        liberacao={liberacao}
        aoFechar={() => setBloqueadosAberto(false)}
        aoAbrirPessoa={(id) => navigate(`/pessoas/${id}`)}
      />

      {/* Linha 2: Ocorrências (8) + ASO (4). Linha 3: Taxa de gravidade, Treinamentos e Obras em dia
          (4 cada; 6 + 6 quando uma obra está filtrada e "Obras em dia" some). Linha 4: Agenda de
          renovações e Atividade recente (6 cada) — nessas duas, quem é clicável é cada linha. */}
      <div className={dashEstilos.gradeInicio}>
        <div className={`${dashEstilos.celula} ${dashEstilos.coluna8}`}>
          <PiramideOcorrenciasCard
            niveis={niveisPiramide}
            foraDaPiramide={foraDaPiramide}
            subtitulo={`Ocorrências no período selecionado, ${escopoIndicadores}`}
            topo={<DiasSemAcidente acidentes={acidentes} obras={obrasAtivas} obraFiltrada={!!obraSelecionadaId} />}
            aoAbrirTipo={(tipo) => navigate(`/ocorrencias?secao=acidentes&tipo=${tipo}`)}
            aoAbrirOcorrencias={() => navigate('/ocorrencias?secao=acidentes')}
          />
        </div>
        <div
          className={`${dashEstilos.cardAcionavel} ${dashEstilos.coluna4Larga}`}
          role="button"
          tabIndex={0}
          onClick={() => navigate('/operacao/saude-ocupacional?aba=aso')}
          onKeyDown={(evento) => abrirComTeclado(evento, '/operacao/saude-ocupacional?aba=aso')}
          aria-label="Abrir os ASOs"
        >
          <AsoRoscaCard fatias={fatiasAso} ordemNaRosca={ordemAsoNaRosca} />
        </div>

        <div className={`${dashEstilos.celula} ${obraSelecionadaId ? dashEstilos.coluna6 : dashEstilos.coluna4Larga}`}>
          <TaxaGravidadeCard
            className={dashEstilos.cardEsticado}
            acidentes={acidentesFiltrados}
            registrosHht={registrosHhtFiltrados}
            escopo={escopoIndicadores}
          />
        </div>
        <div className={`${dashEstilos.celula} ${obraSelecionadaId ? dashEstilos.coluna6 : dashEstilos.coluna4}`}>
          <AptidaoTreinamentosCard cursos={aptidaoCursos} escopo={escopoIndicadores} />
        </div>
        {!obraSelecionadaId && (
          <div className={`${dashEstilos.celula} ${dashEstilos.coluna4}`}>
            <ConformidadePorObraCard linhas={conformidadePorObra} aoAbrirObra={(obra) => setObraSelecionadaId(obra.id)} />
          </div>
        )}

        <div className={`${dashEstilos.celula} ${dashEstilos.coluna6}`}>
          <Card titulo="Agenda de renovações" subtitulo="O que precisa ser renovado, do prazo mais próximo ao mais distante">
            {proximosVencimentos.length === 0 ? (
              <Legenda>Tudo em dia por aqui. Nenhuma renovação pendente.</Legenda>
            ) : (
              <>
                {agendaSemana.length > 0 && <TituloGrupo>Esta semana</TituloGrupo>}
                {agendaSemana.map((alerta) => {
                  const { icone, variante } = iconeDoAlerta(alerta);
                  return (
                    <LinhaClicavel
                      key={alerta.id}
                      icone={icone}
                      variante={variante}
                      titulo={alerta.titulo}
                      meta={[alerta.trabalhadorNome, alerta.obraNome].filter(Boolean).join(' · ')}
                      direita={chipPrazo(alerta)}
                      aoAbrir={() => navigate(destinoDoAlerta(alerta))}
                    />
                  );
                })}
                {agendaDepois.length > 0 && <TituloGrupo>Próximas semanas</TituloGrupo>}
                {agendaDepois.map((alerta) => {
                  const { icone, variante } = iconeDoAlerta(alerta);
                  return (
                    <LinhaClicavel
                      key={alerta.id}
                      icone={icone}
                      variante={variante}
                      titulo={alerta.titulo}
                      meta={[alerta.trabalhadorNome, alerta.obraNome].filter(Boolean).join(' · ')}
                      direita={chipPrazo(alerta)}
                      aoAbrir={() => navigate(destinoDoAlerta(alerta))}
                    />
                  );
                })}
              </>
            )}
            <RodapeLista>
              <button type="button" className={dashEstilos.linkRodape} onClick={() => navigate('/alertas?aba=lista')}>
                Ver agenda completa →
              </button>
            </RodapeLista>
          </Card>
        </div>

        <div className={`${dashEstilos.celula} ${dashEstilos.coluna6}`}>
          <Card titulo="Atividade recente" subtitulo="Últimos registros nos módulos de campo">
            {atividadeRecente.length === 0 ? (
              <Legenda>Nenhuma atividade recente.</Legenda>
            ) : (
              atividadeRecente.map((item) => (
                <LinhaClicavel
                  key={item.id}
                  icone={item.icone}
                  variante={item.variante}
                  titulo={item.titulo}
                  meta={item.meta}
                  direita={<span className={dashEstilos.feedHora}>{formatarDataRelativa(item.dataISO)}</span>}
                  aoAbrir={() => navigate(item.destino)}
                />
              ))
            )}
            <RodapeLista>
              <button type="button" className={dashEstilos.linkRodape} onClick={() => navigate('/operacao')}>
                Ver toda a atividade →
              </button>
            </RodapeLista>
          </Card>
        </div>
      </div>
    </div>
  );
}
