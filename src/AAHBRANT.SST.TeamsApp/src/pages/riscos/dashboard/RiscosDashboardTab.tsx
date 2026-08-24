import { useEffect, useMemo, useState } from 'react';
import { Field, Select, Text } from '@fluentui/react-components';
import {
  api,
  nivelRiscoLabel,
  StatusControleRisco,
  statusControleRiscoLabel,
  type Atividade,
  type Obra,
  type Perigo,
  type Risco,
} from '../../../lib/api';
import { CardGrid } from '../../../layout/AppShell';
import { designTokens } from '../../../theme';
import { usePageStyles } from '../../pageStyles';
import { useDashboardStyles } from '../../../components/dashboard/dashboardStyles';
import { KpiCard } from '../../../components/dashboard/KpiCard';
import { StatusDonutChart, type FatiaDonut } from '../../../components/dashboard/charts/StatusDonutChart';
import { RankingBarChart, type ItemRanking } from '../../../components/dashboard/charts/RankingBarChart';
import { RiscosCriticosPanel } from './RiscosCriticosPanel';

const hojeISO = new Date().toISOString().slice(0, 10);

const NIVEIS_DESC = [5, 4, 3, 2, 1];

const CORES_NIVEL: Record<number, string> = {
  5: designTokens.colorAlert,
  4: '#F97316',
  3: designTokens.colorWarning,
  2: designTokens.colorSuccess,
  1: '#86EFAC',
};

export function RiscosDashboardTab() {
  const estilosPagina = usePageStyles();
  const estilos = useDashboardStyles();

  const [obras, setObras] = useState<Obra[]>([]);
  const [atividades, setAtividades] = useState<Atividade[]>([]);
  const [perigos, setPerigos] = useState<Perigo[]>([]);
  const [riscos, setRiscos] = useState<Risco[]>([]);
  const [carregando, setCarregando] = useState(true);
  const [erro, setErro] = useState<string | null>(null);

  const [obraId, setObraId] = useState('');
  const [nivelFiltro, setNivelFiltro] = useState('');
  const [statusFiltro, setStatusFiltro] = useState('');

  useEffect(() => {
    (async () => {
      try {
        setErro(null);
        const [obrasResp, atividadesResp, perigosResp, riscosResp] = await Promise.all([
          api.obras.listar(),
          api.atividades.listar(),
          api.perigos.listar(),
          api.riscos.listar(),
        ]);
        setObras(obrasResp);
        setAtividades(atividadesResp);
        setPerigos(perigosResp);
        setRiscos(riscosResp);
      } catch (e) {
        setErro(e instanceof Error ? e.message : 'Falha ao carregar dados do dashboard de riscos.');
      } finally {
        setCarregando(false);
      }
    })();
  }, []);

  const atividadesDaObra = useMemo(
    () => (obraId === '' ? atividades : atividades.filter((a) => a.obraId === obraId)),
    [atividades, obraId],
  );

  const riscosFiltrados = useMemo(() => {
    const idsAtividades = new Set(atividadesDaObra.map((a) => a.id));
    return riscos.filter(
      (r) =>
        (obraId === '' || idsAtividades.has(r.atividadeId)) &&
        (nivelFiltro === '' || r.nivelRisco === Number(nivelFiltro)) &&
        (statusFiltro === '' || r.status === Number(statusFiltro)),
    );
  }, [riscos, atividadesDaObra, obraId, nivelFiltro, statusFiltro]);

  const criticosAltos = riscosFiltrados.filter((r) => r.nivelRisco >= 4).length;
  const controlePendente = riscosFiltrados.filter((r) => r.status === StatusControleRisco.Pendente).length;
  const prazoVencido = riscosFiltrados.filter(
    (r) => !!r.prazo && r.prazo < hojeISO && r.status !== StatusControleRisco.Concluido,
  ).length;

  const statusDados: FatiaDonut[] = [
    {
      rotulo: 'Pendente',
      valor: riscosFiltrados.filter((r) => r.status === StatusControleRisco.Pendente).length,
      cor: designTokens.colorAlert,
    },
    {
      rotulo: 'Em andamento',
      valor: riscosFiltrados.filter((r) => r.status === StatusControleRisco.EmAndamento).length,
      cor: designTokens.colorWarning,
    },
    {
      rotulo: 'Concluído',
      valor: riscosFiltrados.filter((r) => r.status === StatusControleRisco.Concluido).length,
      cor: designTokens.colorSuccess,
    },
  ];

  const nivelDados: ItemRanking[] = useMemo(
    () =>
      NIVEIS_DESC.map((nivel) => ({
        rotulo: nivelRiscoLabel[nivel],
        valor: riscosFiltrados.filter((r) => r.nivelRisco === nivel).length,
        cor: CORES_NIVEL[nivel],
      })),
    [riscosFiltrados],
  );

  const perigosDados: ItemRanking[] = useMemo(() => {
    const contagem = new Map<string, number>();
    for (const risco of riscosFiltrados) {
      const nome = perigos.find((p) => p.id === risco.perigoId)?.nome ?? risco.perigoId;
      contagem.set(nome, (contagem.get(nome) ?? 0) + 1);
    }
    return [...contagem.entries()]
      .map(([rotulo, valor]) => ({ rotulo, valor, cor: designTokens.colorInfo }))
      .sort((a, b) => b.valor - a.valor)
      .slice(0, 5);
  }, [riscosFiltrados, perigos]);

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
        <Field label="Nível de risco">
          <Select value={nivelFiltro} onChange={(_, data) => setNivelFiltro(data.value)}>
            <option value="">Todos os níveis</option>
            {NIVEIS_DESC.map((n) => (
              <option key={n} value={n}>
                {nivelRiscoLabel[n]}
              </option>
            ))}
          </Select>
        </Field>
        <Field label="Status do controle">
          <Select value={statusFiltro} onChange={(_, data) => setStatusFiltro(data.value)}>
            <option value="">Todos os status</option>
            {Object.entries(statusControleRiscoLabel).map(([valor, rotulo]) => (
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
          <KpiCard rotulo="Riscos avaliados" valor={riscosFiltrados.length} cor={designTokens.colorPrimary} />
          <KpiCard rotulo="Alto/Crítico" valor={criticosAltos} cor={designTokens.colorAlert} />
          <KpiCard rotulo="Controle pendente" valor={controlePendente} cor={designTokens.colorWarning} />
          <KpiCard rotulo="Com prazo vencido" valor={prazoVencido} cor={designTokens.colorAlert} />
        </CardGrid>
      </div>

      <div className={estilos.chartRow}>
        <div className={estilos.chartCard}>
          <Text className={estilos.chartTitulo}>Status de controle dos riscos</Text>
          <div className={estilos.chartSubtitulo}>Situação do plano de ação de cada avaliação de risco</div>
          <StatusDonutChart dados={statusDados} legendaCentral="riscos" />
        </div>
        <div className={estilos.chartCard}>
          <Text className={estilos.chartTitulo}>Riscos por nível</Text>
          <div className={estilos.chartSubtitulo}>Distribuição das avaliações pela matriz de probabilidade × severidade</div>
          <RankingBarChart dados={nivelDados} />
        </div>
        <div className={estilos.chartCard}>
          <Text className={estilos.chartTitulo}>Perigos mais frequentes</Text>
          <div className={estilos.chartSubtitulo}>Top 5 perigos com mais avaliações de risco vinculadas</div>
          <RankingBarChart dados={perigosDados} />
        </div>
      </div>

      <RiscosCriticosPanel riscos={riscosFiltrados} atividades={atividades} perigos={perigos} />

      {!carregando && riscosFiltrados.length === 0 && (
        <div className={estilosPagina.card} style={{ marginTop: 16 }}>
          <Text>Nenhuma avaliação de risco encontrada para os filtros selecionados.</Text>
        </div>
      )}
    </div>
  );
}
