import { useEffect, useMemo, useState, type KeyboardEvent, type ReactElement } from 'react';
import { useNavigate } from 'react-router-dom';
import { Button, mergeClasses } from '@fluentui/react-components';
import {
  BuildingBank24Regular,
  People24Regular,
  ShieldCheckmark24Regular,
  DocumentCheckmark24Regular,
  Warning24Regular,
  DocumentError24Regular,
  Alert24Regular,
  CheckmarkCircle24Regular,
  ClipboardTaskListLtr24Regular,
  PersonAdd24Regular,
} from '@fluentui/react-icons';
import {
  api,
  ResultadoAso,
  StatusObra,
  StatusNaoConformidade,
  StatusAlerta,
  StatusInspecao,
  TipoOcorrencia,
  type Acidente,
  type Alerta,
  type Aso,
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
import { Card, FeedbackInline, KpiCard, Legenda, Select, StatusChip, StatusDonutChart, TrendBarChart, usePaletaGraficos, type FatiaDonut, type PontoTendencia, type Tom } from '@ui';
import { useDashboardStyles } from '../components/dashboard/dashboardStyles';
import { TaxaGravidadeCard } from '../components/dashboard/TaxaGravidadeCard';
import { MiniCalendarioCard } from '../components/dashboard/MiniCalendarioCard';

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
}

interface ItemFeed {
  id: string;
  icone: ReactElement;
  variante: 'bom' | 'info' | 'atencao' | 'alerta';
  titulo: string;
  meta: string;
  dataISO: string;
}

const NOMES_MESES_ABREVIADOS = [
  'Jan', 'Fev', 'Mar', 'Abr', 'Mai', 'Jun', 'Jul', 'Ago', 'Set', 'Out', 'Nov', 'Dez',
];

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

function ultimosSeisMeses(): Array<{ ano: number; mes: number; rotulo: string }> {
  const agora = new Date();
  const meses: Array<{ ano: number; mes: number; rotulo: string }> = [];
  for (let i = 5; i >= 0; i -= 1) {
    const data = new Date(agora.getFullYear(), agora.getMonth() - i, 1);
    meses.push({ ano: data.getFullYear(), mes: data.getMonth() + 1, rotulo: NOMES_MESES_ABREVIADOS[data.getMonth()] });
  }
  return meses;
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
  const [obraSelecionadaId, setObraSelecionadaId] = useState('');
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
  const [erro, setErro] = useState<string | null>(null);

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
        },
      )
      .catch((e) => {
        if (!cancelado) setErro(e instanceof Error ? e.message : 'Falha ao carregar indicadores.');
      });

    return () => {
      cancelado = true;
    };
  }, [obraSelecionadaId]);

  function nomeObra(id: string) {
    return obras.find((o) => o.id === id)?.nome ?? id;
  }

  const hojeISO = new Date().toISOString().slice(0, 10);
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
  const treinamentosFiltrados = useMemo(
    () =>
      obraSelecionadaId
        ? treinamentos.filter((treinamento) => trabalhadoresDaObraIds.has(treinamento.trabalhadorId))
        : treinamentos,
    [treinamentos, trabalhadoresDaObraIds, obraSelecionadaId],
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

  // Conformidade de EPI: fórmula provisória (sem indicador oficial ainda no sistema) — % de entregas
  // ativas (sem devolução registrada) que estão dentro da validade.
  const entregasEpiAtivas = useMemo(() => entregasEpiFiltradas.filter((e) => !e.dataDevolucao), [entregasEpiFiltradas]);
  const entregasEpiVencidas = entregasEpiAtivas.filter((e) => e.dataValidade && e.dataValidade < hojeISO);
  const conformidadeEpiPct =
    entregasEpiAtivas.length > 0
      ? Math.round(((entregasEpiAtivas.length - entregasEpiVencidas.length) / entregasEpiAtivas.length) * 100)
      : null;

  // Treinamentos em dia: fórmula provisória — % dos registros de treinamento com validade não vencida.
  const treinamentosVencidos = treinamentosFiltrados.filter((t) => t.dataValidade < hojeISO);
  // Mesmo limiar de 30 dias usado em TreinamentosTab.tsx para considerar um treinamento "a vencer".
  const treinamentosAVencer = treinamentosFiltrados.filter((t) => {
    if (t.dataValidade < hojeISO) return false;
    const diasRestantes = (new Date(t.dataValidade).getTime() - new Date(hojeISO).getTime()) / 86_400_000;
    return diasRestantes <= 30;
  });
  const treinamentosEmDiaPct =
    treinamentosFiltrados.length > 0
      ? Math.round(((treinamentosFiltrados.length - treinamentosVencidos.length) / treinamentosFiltrados.length) * 100)
      : null;

  const quaseAcidentes = useMemo(
    () => acidentesFiltrados.filter((a) => a.tipo === TipoOcorrencia.QuaseAcidente),
    [acidentesFiltrados],
  );
  const quaseAcidentesNoPeriodo = quaseAcidentes.filter((a) => noPeriodoSelecionado(a.data));

  // "Abertas" = qualquer não conformidade que ainda não foi encerrada (mesmo critério usado no
  // dashboard do módulo Não Conformidades).
  const naoConformidadesAbertas = naoConformidadesFiltradas.filter((nc) => nc.status !== StatusNaoConformidade.Encerrada);
  const naoConformidadesEmTratamento = naoConformidadesAbertas.filter(
    (nc) => nc.status === StatusNaoConformidade.EmAndamento,
  ).length;

  const kpis: Kpi[] = [
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
      rotulo: 'Conformidade de EPI',
      valor: conformidadeEpiPct !== null ? `${conformidadeEpiPct}%` : '—',
      icone: <ShieldCheckmark24Regular />,
      tom: 'ok',
      deltas: entregasEpiAtivas.length > 0 ? [{ texto: `${entregasEpiAtivas.length} entregas ativas`, tom: 'neutro' }] : [],
      destino: '/operacao?secao=epi&aba=entregas',
    },
    {
      rotulo: 'Treinamentos em dia',
      valor: treinamentosEmDiaPct !== null ? `${treinamentosEmDiaPct}%` : '—',
      icone: <DocumentCheckmark24Regular />,
      tom: 'atencao',
      deltas: [
        ...(treinamentosAVencer.length > 0
          ? [{ texto: `${treinamentosAVencer.length} a vencer`, tom: 'atencao' as const, pulsar: 'leve' as const }]
          : []),
        ...(treinamentosVencidos.length > 0
          ? [{ texto: `${treinamentosVencidos.length} vencidos`, tom: 'alerta' as const, pulsar: 'rapido' as const }]
          : []),
      ],
      destino: '/gestao-sst?secao=treinamentos&aba=turmas',
    },
    {
      rotulo: 'Quase-acidentes',
      valor: String(quaseAcidentesNoPeriodo.length),
      icone: <Warning24Regular />,
      tom: 'atencao',
      deltas: quaseAcidentesNoPeriodo.length > 0 ? [{ texto: 'Acompanhar', tom: 'atencao' }] : [],
      destino: '/ocorrencias?secao=acidentes',
    },
    {
      rotulo: 'Não conformidades abertas',
      valor: String(naoConformidadesAbertas.length),
      icone: <DocumentError24Regular />,
      tom: 'alerta',
      deltas: naoConformidadesEmTratamento > 0 ? [{ texto: `${naoConformidadesEmTratamento} em tratamento`, tom: 'alerta' }] : [],
      destino: '/ocorrencias?secao=nao-conformidades&aba=registros',
    },
  ];

  // ---------- Status de aptidão ocupacional (ASO) ----------
  // Mesmo critério usado no dashboard de Pessoas: resultado do ASO mais recente de cada trabalhador.

  const asoMaisRecentePorTrabalhador = useMemo(() => {
    const mapa = new Map<string, Aso>();
    for (const trabalhador of trabalhadoresAtivos) {
      const aso = asosFiltrados
        .filter((a) => a.trabalhadorId === trabalhador.id)
        .sort((a, b) => b.dataValidade.localeCompare(a.dataValidade))[0];
      if (aso) mapa.set(trabalhador.id, aso);
    }
    return mapa;
  }, [trabalhadoresAtivos, asosFiltrados]);

  const statusAsoGeral = useMemo(() => {
    let aptos = 0;
    let restricao = 0;
    let inaptos = 0;
    let pendentes = 0;
    for (const trabalhador of trabalhadoresAtivos) {
      const aso = asoMaisRecentePorTrabalhador.get(trabalhador.id);
      if (!aso || aso.resultadoStatus === ResultadoAso.Pendente) pendentes += 1;
      else if (aso.resultadoStatus === ResultadoAso.Inapto) inaptos += 1;
      else if (aso.resultadoStatus === ResultadoAso.AptoComRestricao) restricao += 1;
      else aptos += 1;
    }
    return { aptos, restricao, inaptos, pendentes };
  }, [trabalhadoresAtivos, asoMaisRecentePorTrabalhador]);

  const statusAsoDados: FatiaDonut[] = [
    { rotulo: 'Aptos', valor: statusAsoGeral.aptos, cor: paleta.ok },
    { rotulo: 'Restrição temporária', valor: statusAsoGeral.restricao, cor: paleta.atencao },
    { rotulo: 'Inaptos', valor: statusAsoGeral.inaptos, cor: paleta.alerta },
    { rotulo: 'Documentação pendente', valor: statusAsoGeral.pendentes, cor: paleta.info },
  ];

  // ---------- Quase-acidentes: tendência e distribuição por obra ----------

  const tendenciaQuaseAcidentes: PontoTendencia[] = useMemo(
    () =>
      ultimosSeisMeses().map(({ ano, mes, rotulo }) => ({
        rotulo,
        valor: quaseAcidentes.filter((a) => {
          const data = new Date(a.data);
          return data.getFullYear() === ano && data.getMonth() + 1 === mes;
        }).length,
      })),
    [quaseAcidentes],
  );

  // ---------- Próximos vencimentos (alertas em aberto) ----------

  const proximosVencimentos = useMemo(
    () =>
      [...alertasAbertosFiltrados]
        .sort((a, b) => (a.dataLimiteTratamento ?? '').localeCompare(b.dataLimiteTratamento ?? ''))
        .slice(0, 6),
    [alertasAbertosFiltrados],
  );

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
      });
    }

    return itens
      .filter((item) => noPeriodoSelecionado(item.dataISO))
      .sort((a, b) => b.dataISO.localeCompare(a.dataISO))
      .slice(0, 6);
  }, [ddsFiltrados, inspecoesFiltradas, naoConformidadesFiltradas, trabalhadoresAtivos, obras, inicioPeriodoISO]);

  const classeIconeFeed: Record<ItemFeed['variante'], string> = {
    bom: dashEstilos.feedIconeBom,
    info: dashEstilos.feedIconeInfo,
    atencao: dashEstilos.feedIconeAtencao,
    alerta: dashEstilos.feedIconeAlerta,
  };

  function statusChipAlerta(alerta: Alerta) {
    if (!alerta.dataLimiteTratamento) return null;
    const vencido = alerta.dataLimiteTratamento < hojeISO;
    if (vencido) {
      return <StatusChip tom="alerta">Vencido</StatusChip>;
    }
    const dias = Math.round(
      (new Date(alerta.dataLimiteTratamento).getTime() - new Date(hojeISO).getTime()) / 86_400_000,
    );
    return <StatusChip tom="atencao">{dias} dia(s)</StatusChip>;
  }

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
        <div className={dashEstilos.grupoPeriodos} aria-label="Filtrar dashboard por período">
          {PERIODOS_DASHBOARD.map((periodo) => (
            <Button
              key={periodo.valor}
              appearance="subtle"
              className={mergeClasses(
                dashEstilos.botaoPeriodo,
                periodoSelecionado === periodo.valor && dashEstilos.botaoPeriodoAtivo,
              )}
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

      <div className={dashEstilos.linhaKpisCalendario}>
        <div className={dashEstilos.gradeKpis}>
          {kpis.map((kpi, indice) => (
            <KpiCard
              key={kpi.rotulo}
              rotulo={kpi.rotulo}
              valor={kpi.valor}
              tom={kpi.tom}
              icone={kpi.icone}
              deltas={kpi.deltas}
              indice={indice}
              onClick={() => navigate(kpi.destino)}
              ariaLabel={`Abrir ${kpi.rotulo}`}
            />
          ))}
          {/* Taxa de Gravidade é o 7º indicador da mesma grade — em linha própria ele ficava órfão,
              com estilo de card diferente e um vazio ao lado (13/09). */}
          <div
            className={dashEstilos.cardAcionavel}
            role="button"
            tabIndex={0}
            onClick={() => navigate('/ocorrencias?secao=acidentes')}
            onKeyDown={(evento) => abrirComTeclado(evento, '/ocorrencias?secao=acidentes')}
          >
            <TaxaGravidadeCard acidentes={acidentesFiltrados} registrosHht={registrosHhtFiltrados} indice={kpis.length} />
          </div>
        </div>
        <MiniCalendarioCard />
      </div>

      <div className={dashEstilos.dashboardGrid}>
        <div
          className={dashEstilos.cardAcionavel}
          role="button"
          tabIndex={0}
          onClick={() => navigate('/operacao/saude-ocupacional?aba=aso')}
          onKeyDown={(evento) => abrirComTeclado(evento, '/operacao/saude-ocupacional?aba=aso')}
        >
          <Card titulo="Status de aptidão ocupacional (ASO)" subtitulo="Situação clínica do ASO mais recente de cada funcionário">
            <StatusDonutChart dados={statusAsoDados} legendaCentral="funcionários" />
          </Card>
        </div>
        <div
          className={dashEstilos.cardAcionavel}
          role="button"
          tabIndex={0}
          onClick={() => navigate('/ocorrencias?secao=acidentes')}
          onKeyDown={(evento) => abrirComTeclado(evento, '/ocorrencias?secao=acidentes')}
        >
          <Card titulo="Quase-acidentes — últimos 6 meses" subtitulo={`Registros classificados como quase-acidente, ${escopoIndicadores}`}>
            <TrendBarChart dados={tendenciaQuaseAcidentes} />
          </Card>
        </div>
      </div>

      <div className={dashEstilos.dashboardGrid}>
        <div
          className={dashEstilos.cardAcionavel}
          role="button"
          tabIndex={0}
          onClick={() => navigate('/alertas?aba=lista')}
          onKeyDown={(evento) => abrirComTeclado(evento, '/alertas?aba=lista')}
        >
        <Card titulo="Próximos vencimentos" subtitulo="Alertas em aberto, ordenados por prazo">
          <div className={dashEstilos.feed}>
            {proximosVencimentos.map((alerta) => (
              <div key={alerta.id} className={dashEstilos.feedItem}>
                <div
                  className={`${dashEstilos.feedIcone} ${
                    alerta.dataLimiteTratamento && alerta.dataLimiteTratamento < hojeISO
                      ? dashEstilos.feedIconeAlerta
                      : dashEstilos.feedIconeAtencao
                  }`}
                >
                  <Alert24Regular />
                </div>
                <div className={dashEstilos.feedCorpo}>
                  <div className={dashEstilos.feedTitulo}>{alerta.titulo}</div>
                  {alerta.obraNome && <div className={dashEstilos.feedMeta}>{alerta.obraNome}</div>}
                </div>
                {statusChipAlerta(alerta)}
              </div>
            ))}
            {proximosVencimentos.length === 0 && <Legenda>Nenhum alerta em aberto.</Legenda>}
          </div>
        </Card>
        </div>

        <div
          className={dashEstilos.cardAcionavel}
          role="button"
          tabIndex={0}
          onClick={() => navigate('/operacao')}
          onKeyDown={(evento) => abrirComTeclado(evento, '/operacao')}
        >
        <Card titulo="Atividade recente" subtitulo="Últimos registros nos módulos de campo">
          <div className={dashEstilos.feed}>
            {atividadeRecente.map((item) => (
              <div key={item.id} className={dashEstilos.feedItem}>
                <div className={`${dashEstilos.feedIcone} ${classeIconeFeed[item.variante]}`}>
                  {item.icone}
                </div>
                <div className={dashEstilos.feedCorpo}>
                  <div className={dashEstilos.feedTitulo}>{item.titulo}</div>
                  <div className={dashEstilos.feedMeta}>{item.meta}</div>
                </div>
                <span className={dashEstilos.feedHora}>{formatarDataRelativa(item.dataISO)}</span>
              </div>
            ))}
            {atividadeRecente.length === 0 && <Legenda>Nenhuma atividade recente.</Legenda>}
          </div>
        </Card>
        </div>
      </div>
    </div>
  );
}
