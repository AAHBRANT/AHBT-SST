import { useEffect, useMemo, useState, type ReactElement } from 'react';
import {
  Card,
  KpiCard,
  StatusChip,
  Text,
  designTokens,
  usePaletaGraficos,
  StatusDonutChart,
  type FatiaDonut,
  type Tom,
} from '@ui';
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

const tomResultadoAso: Record<number, Tom> = {
  1: 'ok',
  2: 'atencao',
  3: 'alerta',
  4: 'info',
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
  const [cursos, setCursos] = useState<CursoTreinamento[]>([]);
  const paleta = usePaletaGraficos();

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
    { rotulo: 'Em dia', valor: statusEpis.emDia, cor: paleta.ok },
    { rotulo: 'Vencendo', valor: statusEpis.vencendo, cor: paleta.atencao },
    { rotulo: 'Vencido', valor: statusEpis.vencido, cor: paleta.alerta },
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

  const kpis: Array<{ rotulo: string; valor: string; icone: ReactElement; tom: Tom }> = [
    {
      rotulo: 'EPIs Ativos',
      valor: `${perfil.episAtivos.length} ${perfil.episAtivos.length === 1 ? 'item' : 'itens'}`,
      icone: <ShieldCheckmark24Regular />,
      tom: 'info',
    },
    {
      rotulo: 'Presença em DDS',
      valor:
        percentualDds === null
          ? '—'
          : `${perfil.assiduidadeDds.totalParticipados}/${perfil.assiduidadeDds.totalRealizados} (${percentualDds}%)`,
      icone: <People24Regular />,
      tom: 'ok',
    },
    {
      rotulo: 'Trocas de EPI (ano)',
      valor: `${trocasNoAno} ${trocasNoAno === 1 ? 'solicitação' : 'solicitações'}`,
      icone: <ArrowSync24Regular />,
      tom: 'atencao',
    },
    {
      rotulo: 'Treinamentos válidos',
      valor: `${treinamentosValidos.length} ${treinamentosValidos.length === 1 ? 'curso' : 'cursos'}`,
      icone: <DocumentCheckmark24Regular />,
      tom: 'info',
    },
  ];

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(185px, 1fr))', gap: 16 }}>
        {kpis.map((kpi, i) => (
          <KpiCard key={kpi.rotulo} rotulo={kpi.rotulo} valor={kpi.valor} tom={kpi.tom} icone={kpi.icone} indice={i} />
        ))}
      </div>

      <div style={{ display: 'grid', gridTemplateColumns: '1fr 2fr', gap: 16 }}>
        <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
          <Card titulo="Status dos EPIs" subtitulo="Validade dos itens em posse do trabalhador">
            {perfil.episAtivos.length === 0 ? (
              <Text>Nenhum EPI ativo.</Text>
            ) : (
              <StatusDonutChart dados={dadosDonutEpi} legendaCentral="EPIs ativos" />
            )}
          </Card>

          <Card titulo="Motivo das trocas (ano)" subtitulo={`Reposições de EPI em ${new Date().getFullYear()}`}>
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
                      <div style={{ height: 6, borderRadius: 999, backgroundColor: designTokens.colorNeutralLight, overflow: 'hidden' }}>
                        <div style={{ width: `${pct}%`, height: '100%', borderRadius: 999, backgroundColor: designTokens.colorPrimary }} />
                      </div>
                    </div>
                  );
                })}
              </div>
            )}
          </Card>
        </div>

        <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
          <Card titulo="Treinamentos de NR — base para liberação de PT">
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
                      <StatusChip tom={valido ? 'ok' : 'alerta'}>{valido ? 'APTO' : 'BLOQUEADO'}</StatusChip>
                    </div>
                  );
                })}
              </div>
            )}
          </Card>

          <Card titulo="ASO ativo">
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
                <StatusChip tom={tomResultadoAso[asoAtivo.resultadoStatus] ?? 'info'}>
                  {resultadoAsoLabel[asoAtivo.resultadoStatus]}
                </StatusChip>
                <Text size={200} style={{ color: designTokens.colorNeutralMedium }}>
                  {asoAtivo.medicoNome ?? '—'}
                  {asoAtivo.medicoCrm ? ` (CRM ${asoAtivo.medicoCrm})` : ''}
                </Text>
              </div>
            )}
          </Card>
        </div>
      </div>
    </div>
  );
}
