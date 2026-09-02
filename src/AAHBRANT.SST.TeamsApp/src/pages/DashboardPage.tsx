import { useEffect, useMemo, useState, type ReactElement } from 'react';
import { mergeClasses, Text } from '@fluentui/react-components';
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
  type Dds,
  type EntregaEpi,
  type Inspecao,
  type NaoConformidade,
  type Obra,
  type RegistroHhtMensal,
  type Trabalhador,
  type Treinamento,
} from '../lib/api';
import { usePageStyles, useKpiStyles } from './pageStyles';
import { useDashboardStyles } from '../components/dashboard/dashboardStyles';
import { TaxaGravidadeCard } from '../components/dashboard/TaxaGravidadeCard';
import { StatusDonutChart, type FatiaDonut } from '../components/dashboard/charts/StatusDonutChart';
import { TrendBarChart, type PontoTendencia } from '../components/dashboard/charts/TrendBarChart';
import { MiniCalendarCard, type DiaComPrazo } from '../components/dashboard/MiniCalendarCard';
import { designTokens } from '../theme';

interface KpiDelta {
  texto: string;
  cor: 'neutra' | 'boa' | 'atencao' | 'alerta';
}

interface Kpi {
  rotulo: string;
  valor: string;
  icone: ReactElement;
  corIcone: 'info' | 'sucesso' | 'atencao' | 'alerta';
  deltas: KpiDelta[];
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

export function DashboardPage() {
  const estilos = usePageStyles();
  const kpiEstilos = useKpiStyles();
  const dashEstilos = useDashboardStyles();

  const [obras, setObras] = useState<Obra[]>([]);
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
    Promise.all([
      api.obras.listar(),
      api.trabalhadores.listar(),
      api.asos.listar(),
      api.treinamentos.listar(),
      api.entregasEpi.listar(),
      api.acidentes.listar(),
      api.naoConformidades.listar(),
      api.alertas.listar({ status: StatusAlerta.Aberto }),
      api.registrosHht.listar(),
      api.dds.listar(),
      api.inspecoes.listar(),
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
      .catch((e) => setErro(e instanceof Error ? e.message : 'Falha ao carregar indicadores.'));
  }, []);

  function nomeObra(id: string) {
    return obras.find((o) => o.id === id)?.nome ?? id;
  }

  const hojeISO = new Date().toISOString().slice(0, 10);
  const mesAtualISO = hojeISO.slice(0, 7);

  // ---------- KPIs ----------

  const obrasAtivas = useMemo(
    () => obras.filter((o) => o.status !== StatusObra.Encerrada && o.status !== StatusObra.Concluida),
    [obras],
  );
  const obrasEmAndamento = obrasAtivas.filter((o) => o.status === StatusObra.EmAndamento).length;

  const trabalhadoresAtivos = useMemo(() => trabalhadores.filter((t) => !t.dataDemissao), [trabalhadores]);
  const admitidosEsteMes = trabalhadoresAtivos.filter((t) => t.dataAdmissao?.slice(0, 7) === mesAtualISO).length;

  // Conformidade de EPI: fórmula provisória (sem indicador oficial ainda no sistema) — % de entregas
  // ativas (sem devolução registrada) que estão dentro da validade.
  const entregasEpiAtivas = useMemo(() => entregasEpi.filter((e) => !e.dataDevolucao), [entregasEpi]);
  const entregasEpiVencidas = entregasEpiAtivas.filter((e) => e.dataValidade && e.dataValidade < hojeISO);
  const conformidadeEpiPct =
    entregasEpiAtivas.length > 0
      ? Math.round(((entregasEpiAtivas.length - entregasEpiVencidas.length) / entregasEpiAtivas.length) * 100)
      : null;

  // Treinamentos em dia: fórmula provisória — % dos registros de treinamento com validade não vencida.
  const treinamentosVencidos = treinamentos.filter((t) => t.dataValidade < hojeISO);
  // Mesmo limiar de 30 dias usado em TreinamentosTab.tsx para considerar um treinamento "a vencer".
  const treinamentosAVencer = treinamentos.filter((t) => {
    if (t.dataValidade < hojeISO) return false;
    const diasRestantes = (new Date(t.dataValidade).getTime() - new Date(hojeISO).getTime()) / 86_400_000;
    return diasRestantes <= 30;
  });
  const treinamentosEmDiaPct =
    treinamentos.length > 0
      ? Math.round(((treinamentos.length - treinamentosVencidos.length) / treinamentos.length) * 100)
      : null;

  const quaseAcidentes = useMemo(
    () => acidentes.filter((a) => a.tipo === TipoOcorrencia.QuaseAcidente),
    [acidentes],
  );
  const quaseAcidentesMes = quaseAcidentes.filter((a) => a.data.slice(0, 7) === mesAtualISO);

  // "Abertas" = qualquer não conformidade que ainda não foi encerrada (mesmo critério usado no
  // dashboard do módulo Não Conformidades).
  const naoConformidadesAbertas = naoConformidades.filter((nc) => nc.status !== StatusNaoConformidade.Encerrada);
  const naoConformidadesEmTratamento = naoConformidadesAbertas.filter(
    (nc) => nc.status === StatusNaoConformidade.EmAndamento,
  ).length;

  const kpis: Kpi[] = [
    {
      rotulo: 'Obras ativas',
      valor: String(obrasAtivas.length),
      icone: <BuildingBank24Regular />,
      corIcone: 'info',
      deltas: [{ texto: `${obrasEmAndamento} em andamento`, cor: 'neutra' }],
    },
    {
      rotulo: 'Trabalhadores ativos',
      valor: String(trabalhadoresAtivos.length),
      icone: <People24Regular />,
      corIcone: 'info',
      deltas: admitidosEsteMes > 0 ? [{ texto: `+${admitidosEsteMes} este mês`, cor: 'neutra' }] : [],
    },
    {
      rotulo: 'Conformidade de EPI',
      valor: conformidadeEpiPct !== null ? `${conformidadeEpiPct}%` : '—',
      icone: <ShieldCheckmark24Regular />,
      corIcone: 'sucesso',
      deltas: entregasEpiAtivas.length > 0 ? [{ texto: `${entregasEpiAtivas.length} entregas ativas`, cor: 'neutra' }] : [],
    },
    {
      rotulo: 'Treinamentos em dia',
      valor: treinamentosEmDiaPct !== null ? `${treinamentosEmDiaPct}%` : '—',
      icone: <DocumentCheckmark24Regular />,
      corIcone: 'atencao',
      deltas: [
        ...(treinamentosAVencer.length > 0
          ? [{ texto: `${treinamentosAVencer.length} a vencer`, cor: 'atencao' as const }]
          : []),
        ...(treinamentosVencidos.length > 0
          ? [{ texto: `${treinamentosVencidos.length} vencidos`, cor: 'alerta' as const }]
          : []),
      ],
    },
    {
      rotulo: 'Quase-acidentes (mês)',
      valor: String(quaseAcidentesMes.length),
      icone: <Warning24Regular />,
      corIcone: 'atencao',
      deltas: quaseAcidentesMes.length > 0 ? [{ texto: 'Acompanhar', cor: 'atencao' }] : [],
    },
    {
      rotulo: 'Não conformidades abertas',
      valor: String(naoConformidadesAbertas.length),
      icone: <DocumentError24Regular />,
      corIcone: 'alerta',
      deltas: naoConformidadesEmTratamento > 0 ? [{ texto: `${naoConformidadesEmTratamento} em tratamento`, cor: 'alerta' }] : [],
    },
  ];

  // ---------- Status de aptidão ocupacional (ASO) ----------
  // Mesmo critério usado no dashboard de Pessoas: resultado do ASO mais recente de cada trabalhador.

  const asoMaisRecentePorTrabalhador = useMemo(() => {
    const mapa = new Map<string, Aso>();
    for (const trabalhador of trabalhadoresAtivos) {
      const aso = asos
        .filter((a) => a.trabalhadorId === trabalhador.id)
        .sort((a, b) => b.dataValidade.localeCompare(a.dataValidade))[0];
      if (aso) mapa.set(trabalhador.id, aso);
    }
    return mapa;
  }, [trabalhadoresAtivos, asos]);

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
    { rotulo: 'Aptos', valor: statusAsoGeral.aptos, cor: designTokens.colorSuccess },
    { rotulo: 'Restrição temporária', valor: statusAsoGeral.restricao, cor: designTokens.colorWarning },
    { rotulo: 'Inaptos', valor: statusAsoGeral.inaptos, cor: designTokens.colorAlert },
    { rotulo: 'Documentação pendente', valor: statusAsoGeral.pendentes, cor: designTokens.colorInfo },
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
      [...alertasAbertos]
        .sort((a, b) => (a.dataLimiteTratamento ?? '').localeCompare(b.dataLimiteTratamento ?? ''))
        .slice(0, 6),
    [alertasAbertos],
  );

  const prazosDoCalendario: DiaComPrazo[] = useMemo(
    () =>
      alertasAbertos
        .filter((alerta): alerta is Alerta & { dataLimiteTratamento: string } => !!alerta.dataLimiteTratamento)
        .map((alerta) => ({
          dataISO: alerta.dataLimiteTratamento.slice(0, 10),
          vencido: alerta.dataLimiteTratamento < hojeISO,
        })),
    [alertasAbertos, hojeISO],
  );

  // ---------- Atividade recente (montada a partir dos módulos existentes) ----------

  const atividadeRecente: ItemFeed[] = useMemo(() => {
    const itens: ItemFeed[] = [];

    for (const registro of dds) {
      itens.push({
        id: `dds-${registro.id}`,
        icone: <ClipboardTaskListLtr24Regular />,
        variante: 'info',
        titulo: `DDS registrado — ${registro.atividadesNomes.join(', ') || 'DDS do dia'}`,
        meta: registro.obraNome,
        dataISO: registro.data,
      });
    }
    for (const inspecao of inspecoes) {
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
    for (const naoConformidade of naoConformidades) {
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
        titulo: `Novo trabalhador admitido: ${trabalhador.nome}`,
        meta: nomeObra(trabalhador.obraId),
        dataISO: trabalhador.dataAdmissao,
      });
    }

    return itens.sort((a, b) => b.dataISO.localeCompare(a.dataISO)).slice(0, 6);
  }, [dds, inspecoes, naoConformidades, trabalhadoresAtivos, obras]);

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
      return <span style={{ color: designTokens.colorAlert, fontWeight: 700, fontSize: 11 }}>Vencido</span>;
    }
    const dias = Math.round(
      (new Date(alerta.dataLimiteTratamento).getTime() - new Date(hojeISO).getTime()) / 86_400_000,
    );
    return <span style={{ color: '#9A6B04', fontWeight: 700, fontSize: 11 }}>{dias} dia(s)</span>;
  }

  return (
    <div>
      {erro && (
        <Text className={estilos.erro}>
          Não foi possível conectar à API ({erro}). Verifique se o backend está rodando localmente.
        </Text>
      )}

      <div className={kpiEstilos.linha}>
        {kpis.map((kpi) => (
          <div key={kpi.rotulo} className={mergeClasses(estilos.card, kpiEstilos.cartao)}>
            <div className={kpiEstilos.textos}>
              <div className={kpiEstilos.valor}>{kpi.valor}</div>
              <Text className={kpiEstilos.rotulo}>{kpi.rotulo}</Text>
              {kpi.deltas.length > 0 && (
                <div className={kpiEstilos.deltasGrupo}>
                  {kpi.deltas.map((delta) => (
                    <span
                      key={delta.texto}
                      className={mergeClasses(
                        kpiEstilos.variacao,
                        delta.cor === 'boa' && kpiEstilos.variacaoBoa,
                        delta.cor === 'atencao' && kpiEstilos.variacaoAtencao,
                        delta.cor === 'alerta' && kpiEstilos.variacaoAlerta,
                        delta.cor === 'neutra' && kpiEstilos.variacaoNeutra,
                      )}
                    >
                      {delta.texto}
                    </span>
                  ))}
                </div>
              )}
            </div>
            <div
              className={mergeClasses(
                kpiEstilos.icone,
                kpi.corIcone === 'info' && kpiEstilos.iconeInfo,
                kpi.corIcone === 'sucesso' && kpiEstilos.iconeSucesso,
                kpi.corIcone === 'atencao' && kpiEstilos.iconeAtencao,
                kpi.corIcone === 'alerta' && kpiEstilos.iconeAlerta,
              )}
            >
              {kpi.icone}
            </div>
          </div>
        ))}
      </div>

      <div style={{ marginBottom: 16 }}>
        <TaxaGravidadeCard acidentes={acidentes} registrosHht={registrosHht} />
      </div>

      <div className={dashEstilos.chartRow}>
        <div className={dashEstilos.chartCard}>
          <Text className={dashEstilos.chartTitulo}>Status de aptidão ocupacional (ASO)</Text>
          <div className={dashEstilos.chartSubtitulo}>Situação clínica do ASO mais recente de cada trabalhador</div>
          <StatusDonutChart dados={statusAsoDados} legendaCentral="trabalhadores" />
        </div>
        <div className={dashEstilos.chartCard}>
          <Text className={dashEstilos.chartTitulo}>Quase-acidentes — últimos 6 meses</Text>
          <div className={dashEstilos.chartSubtitulo}>Registros classificados como quase-acidente, todas as obras</div>
          <TrendBarChart dados={tendenciaQuaseAcidentes} />
        </div>
        <MiniCalendarCard prazos={prazosDoCalendario} />
      </div>

      <div className={dashEstilos.chartRow}>
        <div className={dashEstilos.chartCard}>
          <Text className={dashEstilos.chartTitulo}>Próximos vencimentos</Text>
          <div className={dashEstilos.chartSubtitulo}>Alertas em aberto, ordenados por prazo</div>
          <div className={dashEstilos.feed}>
            {proximosVencimentos.map((alerta) => (
              <div key={alerta.id} className={dashEstilos.feedItem}>
                <div
                  className={mergeClasses(
                    dashEstilos.feedIcone,
                    alerta.dataLimiteTratamento && alerta.dataLimiteTratamento < hojeISO
                      ? dashEstilos.feedIconeAlerta
                      : dashEstilos.feedIconeAtencao,
                  )}
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
            {proximosVencimentos.length === 0 && (
              <Text style={{ color: designTokens.colorNeutralMedium }}>Nenhum alerta em aberto.</Text>
            )}
          </div>
        </div>

        <div className={dashEstilos.chartCard}>
          <Text className={dashEstilos.chartTitulo}>Atividade recente</Text>
          <div className={dashEstilos.chartSubtitulo}>Últimos registros nos módulos de campo</div>
          <div className={dashEstilos.feed}>
            {atividadeRecente.map((item) => (
              <div key={item.id} className={dashEstilos.feedItem}>
                <div className={mergeClasses(dashEstilos.feedIcone, classeIconeFeed[item.variante])}>
                  {item.icone}
                </div>
                <div className={dashEstilos.feedCorpo}>
                  <div className={dashEstilos.feedTitulo}>{item.titulo}</div>
                  <div className={dashEstilos.feedMeta}>{item.meta}</div>
                </div>
                <span className={dashEstilos.feedHora}>{formatarDataRelativa(item.dataISO)}</span>
              </div>
            ))}
            {atividadeRecente.length === 0 && (
              <Text style={{ color: designTokens.colorNeutralMedium }}>Nenhuma atividade recente.</Text>
            )}
          </div>
        </div>
      </div>
    </div>
  );
}
