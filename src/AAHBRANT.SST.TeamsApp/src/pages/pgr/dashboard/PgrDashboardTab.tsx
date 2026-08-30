import { useEffect, useMemo, useState } from 'react';
import { Field, Select, Text } from '@fluentui/react-components';
import {
  ShieldCheckmark24Regular,
  CheckmarkCircle24Regular,
  Warning24Regular,
  ClipboardTaskListLtr24Regular,
} from '@fluentui/react-icons';
import {
  api,
  StatusControleRisco,
  StatusPgr,
  statusPgrLabel,
  type Obra,
  type Pgr,
  type PlanoAcaoItem,
} from '../../../lib/api';
import { CardGrid } from '../../../layout/AppShell';
import { designTokens } from '../../../theme';
import { usePageStyles } from '../../pageStyles';
import { useDashboardStyles } from '../../../components/dashboard/dashboardStyles';
import { KpiCard } from '../../../components/dashboard/KpiCard';
import { StatusDonutChart, type FatiaDonut } from '../../../components/dashboard/charts/StatusDonutChart';
import { RankingBarChart, type ItemRanking } from '../../../components/dashboard/charts/RankingBarChart';
import { PgrPlanoAcaoVencidoPanel, type AcaoPlanoComContexto } from './PgrPlanoAcaoVencidoPanel';

const hojeISO = new Date().toISOString().slice(0, 10);

export function PgrDashboardTab() {
  const estilosPagina = usePageStyles();
  const estilos = useDashboardStyles();

  const [obras, setObras] = useState<Obra[]>([]);
  const [pgrs, setPgrs] = useState<Pgr[]>([]);
  const [planoAcaoPorPgr, setPlanoAcaoPorPgr] = useState<Record<string, PlanoAcaoItem[]>>({});
  const [carregando, setCarregando] = useState(true);
  const [erro, setErro] = useState<string | null>(null);

  const [obraId, setObraId] = useState('');
  const [statusFiltro, setStatusFiltro] = useState('');

  useEffect(() => {
    (async () => {
      try {
        setErro(null);
        const [obrasResp, pgrsResp] = await Promise.all([api.obras.listar(), api.pgrs.listar()]);
        setObras(obrasResp);
        setPgrs(pgrsResp);
        const entradas = await Promise.all(
          pgrsResp.map((pgr) => api.planoAcao.listar(pgr.id).then((itens) => [pgr.id, itens] as const)),
        );
        setPlanoAcaoPorPgr(Object.fromEntries(entradas));
      } catch (e) {
        setErro(e instanceof Error ? e.message : 'Falha ao carregar dados do dashboard de PGR.');
      } finally {
        setCarregando(false);
      }
    })();
  }, []);

  const pgrsFiltrados = useMemo(
    () =>
      pgrs.filter(
        (p) => (obraId === '' || p.obraId === obraId) && (statusFiltro === '' || p.status === Number(statusFiltro)),
      ),
    [pgrs, obraId, statusFiltro],
  );

  function nomeObra(id: string) {
    return obras.find((o) => o.id === id)?.nome ?? id;
  }

  const acoesComContexto: AcaoPlanoComContexto[] = useMemo(
    () =>
      pgrsFiltrados.flatMap((pgr) =>
        (planoAcaoPorPgr[pgr.id] ?? []).map((acao) => ({
          ...acao,
          pgrNome: pgr.nome,
          obraNome: nomeObra(pgr.obraId),
        })),
      ),
    [pgrsFiltrados, planoAcaoPorPgr, obras],
  );

  const vigentes = pgrsFiltrados.filter((p) => p.status === StatusPgr.Vigente).length;
  const comRevisaoVencida = pgrsFiltrados.filter(
    (p) => !!p.dataProximaRevisao && p.dataProximaRevisao < hojeISO && p.status !== StatusPgr.Encerrado,
  ).length;
  const acoesEmAberto = acoesComContexto.filter((a) => a.status !== StatusControleRisco.Concluido).length;

  const statusDados: FatiaDonut[] = [
    {
      rotulo: 'Em elaboração',
      valor: pgrsFiltrados.filter((p) => p.status === StatusPgr.EmElaboracao).length,
      cor: designTokens.colorWarning,
    },
    { rotulo: 'Vigente', valor: vigentes, cor: designTokens.colorSuccess },
    {
      rotulo: 'Em revisão',
      valor: pgrsFiltrados.filter((p) => p.status === StatusPgr.EmRevisao).length,
      cor: designTokens.colorInfo,
    },
    {
      rotulo: 'Encerrado',
      valor: pgrsFiltrados.filter((p) => p.status === StatusPgr.Encerrado).length,
      cor: designTokens.colorAlert,
    },
  ];

  const obraDados: ItemRanking[] = useMemo(() => {
    const contagem = new Map<string, number>();
    for (const pgr of pgrsFiltrados) {
      const nome = nomeObra(pgr.obraId);
      contagem.set(nome, (contagem.get(nome) ?? 0) + 1);
    }
    return [...contagem.entries()]
      .map(([rotulo, valor]) => ({ rotulo, valor, cor: designTokens.colorPrimary }))
      .sort((a, b) => b.valor - a.valor)
      .slice(0, 5);
  }, [pgrsFiltrados, obras]);

  const acoesPorStatusDados: ItemRanking[] = [
    {
      rotulo: 'Pendente',
      valor: acoesComContexto.filter((a) => a.status === StatusControleRisco.Pendente).length,
      cor: designTokens.colorAlert,
    },
    {
      rotulo: 'Em andamento',
      valor: acoesComContexto.filter((a) => a.status === StatusControleRisco.EmAndamento).length,
      cor: designTokens.colorWarning,
    },
    {
      rotulo: 'Concluído',
      valor: acoesComContexto.filter((a) => a.status === StatusControleRisco.Concluido).length,
      cor: designTokens.colorSuccess,
    },
  ];

  return (
    <div>
      <div className={estilos.filtros}>
        <Field label="Obra">
          <Select value={obraId} onChange={(_, data) => setObraId(data.value)}>
            <option value="">Todas as obras</option>
            {obras.map((o) => (
              <option key={o.id} value={o.id}>
                {o.nome}
              </option>
            ))}
          </Select>
        </Field>
        <Field label="Status do PGR">
          <Select value={statusFiltro} onChange={(_, data) => setStatusFiltro(data.value)}>
            <option value="">Todos os status</option>
            {Object.entries(statusPgrLabel).map(([valor, rotulo]) => (
              <option key={valor} value={valor}>
                {rotulo}
              </option>
            ))}
          </Select>
        </Field>
      </div>

      {erro && <Text className={estilosPagina.erro}>{erro}</Text>}

      <div style={{ marginBottom: 16 }}>
        <CardGrid>
          <KpiCard
            rotulo="Total de PGRs"
            valor={pgrsFiltrados.length}
            cor={designTokens.colorPrimary}
            icone={<ShieldCheckmark24Regular />}
          />
          <KpiCard
            rotulo="Vigentes"
            valor={vigentes}
            cor={designTokens.colorSuccess}
            icone={<CheckmarkCircle24Regular />}
          />
          <KpiCard
            rotulo="Com revisão vencida"
            valor={comRevisaoVencida}
            cor={designTokens.colorAlert}
            icone={<Warning24Regular />}
          />
          <KpiCard
            rotulo="Ações do plano em aberto"
            valor={acoesEmAberto}
            cor={designTokens.colorWarning}
            icone={<ClipboardTaskListLtr24Regular />}
          />
        </CardGrid>
      </div>

      <div className={estilos.chartRow}>
        <div className={estilos.chartCard}>
          <Text className={estilos.chartTitulo}>Status dos PGRs</Text>
          <div className={estilos.chartSubtitulo}>Situação atual de cada Programa de Gerenciamento de Riscos</div>
          <StatusDonutChart dados={statusDados} legendaCentral="PGRs" />
        </div>
        <div className={estilos.chartCard}>
          <Text className={estilos.chartTitulo}>PGRs por obra</Text>
          <div className={estilos.chartSubtitulo}>Top 5 obras com mais PGRs cadastrados</div>
          <RankingBarChart dados={obraDados} />
        </div>
        <div className={estilos.chartCard}>
          <Text className={estilos.chartTitulo}>Ações do plano por status</Text>
          <div className={estilos.chartSubtitulo}>Itens do plano de ação de todos os PGRs, por situação</div>
          <RankingBarChart dados={acoesPorStatusDados} />
        </div>
      </div>

      <PgrPlanoAcaoVencidoPanel acoes={acoesComContexto} />

      {!carregando && pgrsFiltrados.length === 0 && (
        <div className={estilosPagina.card} style={{ marginTop: 16 }}>
          <Text>Nenhum PGR encontrado para os filtros selecionados.</Text>
        </div>
      )}
    </div>
  );
}
