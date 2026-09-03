import { useEffect, useMemo, useState, type ReactNode } from 'react';
import { Badge, ProgressBar, Text, mergeClasses } from '@fluentui/react-components';
import {
  ShieldCheckmark24Regular,
  People24Regular,
  ArrowSync24Regular,
  DocumentCheckmark24Regular,
} from '@fluentui/react-icons';
import {
  api,
  motivoEntregaEpiLabel,
  resultadoAsoLabel,
  tipoExameAsoLabel,
  type CursoTreinamento,
  type PerfilCompletoTrabalhador,
} from '../../lib/api';
import { usePageStyles, useKpiStyles } from '../pageStyles';
import { useDashboardStyles } from '../../components/dashboard/dashboardStyles';
import { StatusDonutChart, type FatiaDonut } from '../../components/dashboard/charts/StatusDonutChart';
import { designTokens } from '../../theme';

const corResultadoAso: Record<number, 'success' | 'warning' | 'danger' | 'informative'> = {
  1: 'success',
  2: 'warning',
  3: 'danger',
  4: 'informative',
};

function diasAte(data: string): number {
  return Math.round((new Date(data).getTime() - new Date(new Date().toDateString()).getTime()) / 86_400_000);
}

// Janela de "vencendo" pro donut de EPI — não veio especificada no pedido; assumido 30 dias, mesmo
// horizonte já usado pros alertas de vencimento em outras telas do sistema.
const DIAS_ALERTA_VENCIMENTO_EPI = 30;

// Dashboard do Trabalhador (pedido do usuário, 03/09, réplica de mockup) — substitui a tabela plana
// de histórico de ASO que existia antes nesta aba. "PTs Liberadas"/"Atividades Habilitadas" não tem
// motor de elegibilidade próprio no sistema hoje (não existe vínculo Atividade→CursoTreinamento) —
// interpretado aqui como "treinamentos de NR do trabalhador com validade em dia", que é o dado real
// que o sistema tem e é exatamente o que libera ou bloqueia uma PT na prática.
export function PerfilGeralTab({ perfil }: { perfil: PerfilCompletoTrabalhador }) {
  const estilos = usePageStyles();
  const kpiEstilos = useKpiStyles();
  const dashEstilos = useDashboardStyles();
  const [cursos, setCursos] = useState<CursoTreinamento[]>([]);

  useEffect(() => {
    api.cursosTreinamento
      .listar()
      .then(setCursos)
      .catch(() => setCursos([]));
  }, []);

  const cursoPorId = useMemo(() => new Map(cursos.map((c) => [c.id, c])), [cursos]);

  const statusEpis = useMemo(() => {
    let emDia = 0;
    let vencendo = 0;
    let vencido = 0;
    for (const epi of perfil.episAtivos) {
      if (!epi.dataValidade) {
        emDia++;
        continue;
      }
      const dias = diasAte(epi.dataValidade);
      if (dias < 0) vencido++;
      else if (dias <= DIAS_ALERTA_VENCIMENTO_EPI) vencendo++;
      else emDia++;
    }
    return { emDia, vencendo, vencido };
  }, [perfil.episAtivos]);

  const dadosDonutEpi: FatiaDonut[] = [
    { rotulo: 'Em dia', valor: statusEpis.emDia, cor: designTokens.colorSuccess },
    { rotulo: 'Vencendo', valor: statusEpis.vencendo, cor: designTokens.colorWarning },
    { rotulo: 'Vencido', valor: statusEpis.vencido, cor: designTokens.colorAlert },
  ];

  const percentualDds =
    perfil.assiduidadeDds.totalRealizados > 0
      ? Math.round((perfil.assiduidadeDds.totalParticipados / perfil.assiduidadeDds.totalRealizados) * 100)
      : null;

  const treinamentosValidos = perfil.treinamentos.filter((t) => diasAte(t.dataValidade) >= 0);

  // Campos novos (03/09) — defensivo contra API e web momentaneamente fora de sincronia num
  // rolling deploy (são dois Container Apps separados, ver ObterPerfilCompletoTrabalhadorQuery.cs).
  const motivosTroca = perfil.motivosTroca ?? [];
  const trocasNoAno = perfil.trocasNoAno ?? 0;
  const totalMotivos = motivosTroca.reduce((soma, m) => soma + m.quantidade, 0);

  // asos vem do backend ordenado por DataValidade desc; o "ativo" pra fins de aptidão é o mais
  // recente por DataExame (mesmo critério que o backend usa pro badge statusAptidao do cabeçalho —
  // um retorno ao trabalho pode reexaminar antes do vencimento do ASO anterior).
  const asoAtivo = useMemo(
    () => (perfil.asos.length === 0 ? undefined : [...perfil.asos].sort((a, b) => b.dataExame.localeCompare(a.dataExame))[0]),
    [perfil.asos],
  );

  const kpis: Array<{ rotulo: string; valor: string; icone: ReactNode; cor: 'info' | 'sucesso' | 'atencao' }> = [
    {
      rotulo: 'EPIs Ativos',
      valor: `${perfil.episAtivos.length} ${perfil.episAtivos.length === 1 ? 'item' : 'itens'}`,
      icone: <ShieldCheckmark24Regular />,
      cor: 'info',
    },
    {
      rotulo: 'Presença em DDS',
      valor:
        percentualDds === null
          ? '—'
          : `${perfil.assiduidadeDds.totalParticipados}/${perfil.assiduidadeDds.totalRealizados} (${percentualDds}%)`,
      icone: <People24Regular />,
      cor: 'sucesso',
    },
    {
      rotulo: 'Trocas de EPI (ano)',
      valor: `${trocasNoAno} ${trocasNoAno === 1 ? 'solicitação' : 'solicitações'}`,
      icone: <ArrowSync24Regular />,
      cor: 'atencao',
    },
    {
      rotulo: 'Treinamentos válidos',
      valor: `${treinamentosValidos.length} ${treinamentosValidos.length === 1 ? 'curso' : 'cursos'}`,
      icone: <DocumentCheckmark24Regular />,
      cor: 'info',
    },
  ];

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <div className={kpiEstilos.linha}>
        {kpis.map((kpi) => (
          <div key={kpi.rotulo} className={mergeClasses(estilos.card, kpiEstilos.cartao)}>
            <div className={kpiEstilos.textos}>
              <div className={kpiEstilos.valor}>{kpi.valor}</div>
              <Text className={kpiEstilos.rotulo}>{kpi.rotulo}</Text>
            </div>
            <div
              className={mergeClasses(
                kpiEstilos.icone,
                kpi.cor === 'info' && kpiEstilos.iconeInfo,
                kpi.cor === 'sucesso' && kpiEstilos.iconeSucesso,
                kpi.cor === 'atencao' && kpiEstilos.iconeAtencao,
              )}
            >
              {kpi.icone}
            </div>
          </div>
        ))}
      </div>

      <div style={{ display: 'grid', gridTemplateColumns: '1fr 2fr', gap: 16 }}>
        <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
          <div className={dashEstilos.chartCard}>
            <Text className={dashEstilos.chartTitulo}>Status dos EPIs</Text>
            <div className={dashEstilos.chartSubtitulo}>Validade dos itens em posse do trabalhador</div>
            {perfil.episAtivos.length === 0 ? (
              <Text>Nenhum EPI ativo.</Text>
            ) : (
              <StatusDonutChart dados={dadosDonutEpi} legendaCentral="EPIs ativos" />
            )}
          </div>

          <div className={dashEstilos.chartCard}>
            <Text className={dashEstilos.chartTitulo}>Motivo das trocas (ano)</Text>
            <div className={dashEstilos.chartSubtitulo}>Reposições de EPI em {new Date().getFullYear()}</div>
            {motivosTroca.length === 0 ? (
              <Text>Nenhuma troca registrada este ano.</Text>
            ) : (
              <div style={{ display: 'flex', flexDirection: 'column', gap: 12, marginTop: 10 }}>
                {motivosTroca.map((m) => {
                  const pct = totalMotivos > 0 ? Math.round((m.quantidade / totalMotivos) * 100) : 0;
                  return (
                    <div key={m.motivo}>
                      <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 13, fontWeight: 600, marginBottom: 4 }}>
                        <span>{motivoEntregaEpiLabel[m.motivo]}</span>
                        <span>
                          {pct}% ({m.quantidade})
                        </span>
                      </div>
                      <ProgressBar value={pct / 100} />
                    </div>
                  );
                })}
              </div>
            )}
          </div>
        </div>

        <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
          <div className={estilos.card}>
            <div className={estilos.toolbar}>
              <Text weight="semibold">Treinamentos de NR — base para liberação de PT</Text>
            </div>
            {perfil.treinamentos.length === 0 ? (
              <Text>Nenhum treinamento registrado.</Text>
            ) : (
              <div style={{ display: 'grid', gridTemplateColumns: 'repeat(2, 1fr)', gap: 10 }}>
                {perfil.treinamentos.map((t) => {
                  const curso = cursoPorId.get(t.cursoTreinamentoId);
                  const valido = diasAte(t.dataValidade) >= 0;
                  return (
                    <div
                      key={t.id}
                      style={{
                        display: 'flex',
                        alignItems: 'center',
                        justifyContent: 'space-between',
                        gap: 8,
                        padding: 12,
                        borderRadius: 8,
                        border: `1px solid ${designTokens.colorCardBorder}`,
                        backgroundColor: designTokens.colorNeutralLight,
                      }}
                    >
                      <div style={{ minWidth: 0 }}>
                        <Text weight="semibold" size={200} style={{ display: 'block' }}>
                          {curso?.nome ?? 'Curso não encontrado'}
                        </Text>
                        <Text size={200} style={{ color: designTokens.colorNeutralMedium }}>
                          {valido ? 'Válido até' : 'Vencido em'} {t.dataValidade.slice(0, 10)}
                        </Text>
                      </div>
                      <Badge color={valido ? 'success' : 'danger'} appearance="tint">
                        {valido ? 'APTO' : 'BLOQUEADO'}
                      </Badge>
                    </div>
                  );
                })}
              </div>
            )}
          </div>

          <div className={estilos.card}>
            <div className={estilos.toolbar}>
              <Text weight="semibold">ASO ativo</Text>
            </div>
            {!asoAtivo ? (
              <Text>Nenhum ASO registrado.</Text>
            ) : (
              <div style={{ display: 'flex', gap: 20, flexWrap: 'wrap', alignItems: 'center' }}>
                <Text>
                  <strong>Tipo:</strong> {tipoExameAsoLabel[asoAtivo.tipo]}
                </Text>
                <Text>
                  <strong>Exame:</strong> {asoAtivo.dataExame.slice(0, 10)}
                </Text>
                <Text>
                  <strong>Validade:</strong> {asoAtivo.dataValidade.slice(0, 10)}
                </Text>
                <Badge color={corResultadoAso[asoAtivo.resultadoStatus]} appearance="tint">
                  {resultadoAsoLabel[asoAtivo.resultadoStatus]}
                </Badge>
                <Text size={200} style={{ color: designTokens.colorNeutralMedium }}>
                  {asoAtivo.medicoNome ?? '—'}
                  {asoAtivo.medicoCrm ? ` (CRM ${asoAtivo.medicoCrm})` : ''}
                </Text>
              </div>
            )}
          </div>
        </div>
      </div>
    </div>
  );
}
