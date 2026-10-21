import { useEffect, useMemo, useState, type ReactElement } from 'react';
import {
  Card,
  KpiCard,
  StatusChip,
  Text,
  Legenda,
  BarraProgresso,
  designTokens,
  tokensUi,
  useTipografia,
  nivelVencimento,
  tomDeVencimento,
  type Tom,
} from '@ui';
import {
  ShieldCheckmark24Regular,
  People24Regular,
  ArrowSync24Regular,
  DocumentCheckmark24Regular,
  Checkmark20Regular,
  Warning20Regular,
} from '@fluentui/react-icons';
import {
  api,
  resultadoAsoLabel,
  tipoExameAsoLabel,
  type CursoTreinamento,
  type PerfilCompletoTrabalhador,
} from '../../lib/api';
import { AnelProgresso } from './AnelProgresso';

const tomResultadoAso: Record<number, Tom> = {
  1: 'ok',
  2: 'atencao',
  3: 'alerta',
  4: 'info',
};

// Ciclo usado pelo anel do ASO: o periódico mais comum no PCMSO é anual.
const DIAS_CICLO_ASO = 365;

function diasAte(data: string): number {
  return Math.round((new Date(data).getTime() - new Date(new Date().toDateString()).getTime()) / 86_400_000);
}

function dataBr(iso?: string | null): string {
  return iso ? iso.slice(0, 10).split('-').reverse().join('/') : '—';
}

// Fração do período de validade já consumida (0 a 1) — base da barra de prazo de cada NR.
function consumoValidade(inicio: string, fim: string): number {
  const i = new Date(inicio).getTime();
  const f = new Date(fim).getTime();
  if (!(f > i)) return 1;
  return Math.max(0, Math.min(1, (Date.now() - i) / (f - i)));
}

function textoPrazo(dias: number): string {
  if (dias < 0) return `vencido há ${-dias} ${-dias === 1 ? 'dia' : 'dias'}`;
  if (dias === 0) return 'vence hoje';
  return `faltam ${dias} ${dias === 1 ? 'dia' : 'dias'}`;
}

const estiloRotuloDado = {
  fontSize: 11,
  fontWeight: 700,
  letterSpacing: '0.06em',
  textTransform: 'uppercase',
  color: designTokens.colorNeutralMedium,
} as const;

// Dashboard do Trabalhador — Proposta 1 de cards (pedido do usuário, 21/10): cada card mostra o
// estado primeiro (KPIs, ASO com anel de validade, NRs com barra de prazo), sem card dentro de card.
// Status dos EPIs e motivos de troca foram para a aba EPI (ver EpiPerfilSecao). "PTs Liberadas" segue
// interpretado como "treinamentos de NR do trabalhador com validade em dia" — não existe vínculo
// Atividade→CursoTreinamento no sistema hoje.
export function PerfilGeralTab({ perfil }: { perfil: PerfilCompletoTrabalhador }) {
  const [cursos, setCursos] = useState<CursoTreinamento[]>([]);
  const tipo = useTipografia();

  useEffect(() => {
    api.cursosTreinamento
      .listar()
      .then(setCursos)
      .catch(() => setCursos([]));
  }, []);

  const cursoPorId = useMemo(() => new Map(cursos.map((c) => [c.id, c])), [cursos]);

  const epis = useMemo(() => {
    let vencidos = 0;
    let vencendo = 0;
    for (const epi of perfil.episAtivos) {
      const nivel = nivelVencimento(epi.dataValidade);
      if (nivel === 'vencido') vencidos++;
      else if (nivel === 'alerta') vencendo++;
    }
    return { vencidos, vencendo };
  }, [perfil.episAtivos]);

  const percentualDds =
    perfil.assiduidadeDds.totalRealizados > 0
      ? Math.round((perfil.assiduidadeDds.totalParticipados / perfil.assiduidadeDds.totalRealizados) * 100)
      : null;

  const treinamentosValidos = perfil.treinamentos.filter((t) => diasAte(t.dataValidade) >= 0);
  const treinamentosBloqueando = perfil.treinamentos.length - treinamentosValidos.length;
  const treinamentosOrdenados = useMemo(
    () => [...perfil.treinamentos].sort((a, b) => a.dataValidade.localeCompare(b.dataValidade)),
    [perfil.treinamentos],
  );

  // Defensivo contra API e web momentaneamente fora de sincronia num rolling deploy (são dois
  // Container Apps separados, ver ObterPerfilCompletoTrabalhadorQuery.cs).
  const trocasNoAno = perfil.trocasNoAno ?? 0;

  // asos vem do backend ordenado por DataValidade desc; o "ativo" pra fins de aptidão é o mais
  // recente por DataExame (mesmo critério que o backend usa pro badge statusAptidao do cabeçalho —
  // um retorno ao trabalho pode reexaminar antes do vencimento do ASO anterior).
  const asoAtivo = useMemo(
    () => (perfil.asos.length === 0 ? undefined : [...perfil.asos].sort((a, b) => b.dataExame.localeCompare(a.dataExame))[0]),
    [perfil.asos],
  );
  const diasAso = asoAtivo ? diasAte(asoAtivo.dataValidade) : 0;
  const nivelAso = asoAtivo ? nivelVencimento(asoAtivo.dataValidade) : null;

  const kpis: Array<{ rotulo: string; valor: string; icone: ReactElement; tom: Tom; detalhe?: { texto: string; tom: Tom } }> = [
    {
      rotulo: 'EPIs em posse',
      valor: `${perfil.episAtivos.length}`,
      icone: <ShieldCheckmark24Regular />,
      tom: 'info',
      detalhe:
        epis.vencidos + epis.vencendo > 0
          ? { texto: `${epis.vencidos} vencido · ${epis.vencendo} vencendo`, tom: epis.vencidos > 0 ? 'alerta' : 'atencao' }
          : { texto: 'Todos em dia', tom: 'ok' },
    },
    {
      rotulo: 'Presença em DDS',
      valor: percentualDds === null ? '—' : `${percentualDds}%`,
      icone: <People24Regular />,
      tom: 'ok',
      detalhe:
        percentualDds === null
          ? undefined
          : { texto: `${perfil.assiduidadeDds.totalParticipados} de ${perfil.assiduidadeDds.totalRealizados} realizados`, tom: 'neutro' },
    },
    {
      rotulo: 'Trocas de EPI no ano',
      valor: `${trocasNoAno}`,
      icone: <ArrowSync24Regular />,
      tom: 'atencao',
      detalhe: { texto: trocasNoAno === 1 ? '1 solicitação' : `${trocasNoAno} solicitações`, tom: 'neutro' },
    },
    {
      rotulo: 'NRs válidas',
      valor: `${treinamentosValidos.length}`,
      icone: <DocumentCheckmark24Regular />,
      tom: treinamentosBloqueando > 0 ? 'atencao' : 'ok',
      detalhe: { texto: `de ${perfil.treinamentos.length} registradas`, tom: 'neutro' },
    },
  ];

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: tokensUi.espaco.lg }}>
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(185px, 1fr))', gap: tokensUi.espaco.lg }}>
        {kpis.map((kpi, i) => (
          <KpiCard
            key={kpi.rotulo}
            rotulo={kpi.rotulo}
            valor={kpi.valor}
            tom={kpi.tom}
            icone={kpi.icone}
            indice={i}
            deltas={kpi.detalhe ? [kpi.detalhe] : undefined}
          />
        ))}
      </div>

      {!asoAtivo ? (
        <Card titulo="ASO ativo">
          <Text>Nenhum ASO registrado.</Text>
        </Card>
      ) : (
        <Card>
          <div style={{ display: 'flex', gap: tokensUi.espaco.xl, alignItems: 'center', flexWrap: 'wrap' }}>
            <AnelProgresso
              fracao={Math.max(0, diasAso) / DIAS_CICLO_ASO}
              tom={nivelAso ? tomDeVencimento(nivelAso) : 'neutro'}
              centro={Math.max(0, diasAso)}
              legenda={diasAso < 0 ? 'ASO vencido' : 'dias de validade'}
              aria-label={`ASO ${textoPrazo(diasAso)}`}
            />
            <div style={{ flex: '1 1 260px', minWidth: 0, display: 'flex', flexDirection: 'column', gap: tokensUi.espaco.md }}>
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', gap: tokensUi.espaco.sm }}>
                <div>
                  <div className={tipo.micro} style={{ color: designTokens.colorNeutralMedium }}>
                    ASO ativo
                  </div>
                  <div className={tipo.titulo}>{tipoExameAsoLabel[asoAtivo.tipo]}</div>
                </div>
                <StatusChip tom={tomResultadoAso[asoAtivo.resultadoStatus] ?? 'info'}>
                  {resultadoAsoLabel[asoAtivo.resultadoStatus]}
                </StatusChip>
              </div>
              <dl
                style={{
                  display: 'grid',
                  gridTemplateColumns: 'repeat(auto-fit, minmax(120px, 1fr))',
                  gap: `${tokensUi.espaco.sm} ${tokensUi.espaco.lg}`,
                  margin: 0,
                }}
              >
                {[
                  ['Exame', dataBr(asoAtivo.dataExame)],
                  ['Validade', dataBr(asoAtivo.dataValidade)],
                  ['Médico', asoAtivo.medicoNome ?? '—'],
                  ['CRM', asoAtivo.medicoCrm ?? '—'],
                ].map(([rotulo, valor]) => (
                  <div key={rotulo} style={{ minWidth: 0 }}>
                    <dt style={estiloRotuloDado}>{rotulo}</dt>
                    <dd style={{ margin: '2px 0 0', fontWeight: 600, fontVariantNumeric: 'tabular-nums' }}>{valor}</dd>
                  </div>
                ))}
              </dl>
              <div style={{ paddingTop: tokensUi.espaco.md, borderTop: `1px solid ${designTokens.colorCardBorder}` }}>
                <Legenda>
                  {diasAso < 0
                    ? `Vencido em ${dataBr(asoAtivo.dataValidade)} — novo exame necessário`
                    : `Próximo exame até ${dataBr(asoAtivo.dataValidade)}`}
                </Legenda>
              </div>
            </div>
          </div>
        </Card>
      )}

      <Card
        titulo="Treinamentos de NR"
        subtitulo="Base para liberação de Permissão de Trabalho"
        acoes={
          perfil.treinamentos.length > 0 ? (
            <StatusChip tom={treinamentosBloqueando > 0 ? 'alerta' : 'ok'}>
              {treinamentosBloqueando > 0 ? `${treinamentosBloqueando} bloqueando` : 'Todos válidos'}
            </StatusChip>
          ) : undefined
        }
      >
        {perfil.treinamentos.length === 0 ? (
          <Text>Nenhum treinamento registrado.</Text>
        ) : (
          <ul style={{ listStyle: 'none', margin: 0, padding: 0 }}>
            {treinamentosOrdenados.map((t, i) => {
              const curso = cursoPorId.get(t.cursoTreinamentoId);
              const dias = diasAte(t.dataValidade);
              const nivel = nivelVencimento(t.dataValidade) ?? 'valido';
              const tom = tomDeVencimento(nivel);
              const bloqueado = dias < 0;
              return (
                <li
                  key={t.id}
                  style={{
                    display: 'grid',
                    gridTemplateColumns: 'auto minmax(0, 1fr) auto',
                    gap: tokensUi.espaco.md,
                    alignItems: 'center',
                    padding: '11px 0',
                    borderTop: i === 0 ? 'none' : `1px solid ${designTokens.colorCardBorder}`,
                  }}
                >
                  <span
                    aria-hidden="true"
                    style={{
                      width: 32,
                      height: 32,
                      borderRadius: tokensUi.raio.sm,
                      display: 'grid',
                      placeItems: 'center',
                      backgroundColor: tokensUi.status[tom].fundo,
                      color: tokensUi.status[tom].tinta,
                    }}
                  >
                    {bloqueado ? <Warning20Regular /> : <Checkmark20Regular />}
                  </span>
                  <div style={{ display: 'flex', flexDirection: 'column', gap: 6, minWidth: 0 }}>
                    <div style={{ display: 'flex', justifyContent: 'space-between', gap: tokensUi.espaco.sm, flexWrap: 'wrap' }}>
                      <Text weight="semibold">{curso?.nome ?? 'Curso não encontrado'}</Text>
                      <Legenda>{textoPrazo(dias)}</Legenda>
                    </div>
                    <BarraProgresso
                      percentual={consumoValidade(t.dataRealizacao, t.dataValidade) * 100}
                      tom={tom}
                      aria-label={`${curso?.nome ?? 'Treinamento'}: ${textoPrazo(dias)}`}
                    />
                  </div>
                  <StatusChip tom={bloqueado ? 'alerta' : tom}>{bloqueado ? 'BLOQUEADO' : 'APTO'}</StatusChip>
                </li>
              );
            })}
          </ul>
        )}
      </Card>
    </div>
  );
}
