import { useEffect, useMemo, useState } from 'react';
import { Field, Select, Text } from '@fluentui/react-components';
import { ClipboardTaskListLtr24Regular, ArrowSync24Regular, CheckmarkCircle24Regular, Warning24Regular } from '@fluentui/react-icons';
import {
  api,
  StatusInspecao,
  statusInspecaoLabel,
  tipoInspecaoLabel,
  type Inspecao,
  type Obra,
} from '../../../lib/api';
import { CardGrid } from '../../../layout/AppShell';
import { designTokens } from '../../../theme';
import { usePageStyles } from '../../pageStyles';
import { useDashboardStyles } from '../../../components/dashboard/dashboardStyles';
import { KpiCard } from '../../../components/dashboard/KpiCard';
import { StatusDonutChart, type FatiaDonut } from '../../../components/dashboard/charts/StatusDonutChart';
import { RankingBarChart, type ItemRanking } from '../../../components/dashboard/charts/RankingBarChart';
import { InspecoesNaoConformesPanel } from './InspecoesNaoConformesPanel';

export function InspecoesDashboardTab() {
  const estilosPagina = usePageStyles();
  const estilos = useDashboardStyles();

  const [obras, setObras] = useState<Obra[]>([]);
  const [inspecoes, setInspecoes] = useState<Inspecao[]>([]);
  const [carregando, setCarregando] = useState(true);
  const [erro, setErro] = useState<string | null>(null);

  const [obraId, setObraId] = useState('');
  const [tipoFiltro, setTipoFiltro] = useState('');
  const [statusFiltro, setStatusFiltro] = useState('');

  useEffect(() => {
    (async () => {
      try {
        setErro(null);
        const [obrasResp, inspecoesResp] = await Promise.all([api.obras.listar(), api.inspecoes.listar()]);
        setObras(obrasResp);
        setInspecoes(inspecoesResp);
      } catch (e) {
        setErro(e instanceof Error ? e.message : 'Falha ao carregar dados do dashboard de inspeções.');
      } finally {
        setCarregando(false);
      }
    })();
  }, []);

  const inspecoesFiltradas = useMemo(
    () =>
      inspecoes.filter(
        (i) =>
          (obraId === '' || i.obraId === obraId) &&
          (tipoFiltro === '' || i.tipoInspecao === Number(tipoFiltro)) &&
          (statusFiltro === '' || i.status === Number(statusFiltro)),
      ),
    [inspecoes, obraId, tipoFiltro, statusFiltro],
  );

  const emAndamento = inspecoesFiltradas.filter((i) => i.status === StatusInspecao.EmAndamento).length;
  const concluidas = inspecoesFiltradas.filter((i) => i.status === StatusInspecao.Concluida).length;
  const itensNaoConformesTotal = inspecoesFiltradas.reduce((soma, i) => soma + i.itensNaoConformes, 0);

  const statusDados: FatiaDonut[] = [
    { rotulo: 'Em andamento', valor: emAndamento, cor: designTokens.colorInfo },
    { rotulo: 'Concluída', valor: concluidas, cor: designTokens.colorSuccess },
  ];

  const tipoDados: ItemRanking[] = useMemo(() => {
    const contagem = new Map<number, number>();
    for (const inspecao of inspecoesFiltradas) {
      contagem.set(inspecao.tipoInspecao, (contagem.get(inspecao.tipoInspecao) ?? 0) + 1);
    }
    return [...contagem.entries()]
      .map(([tipo, valor]) => ({
        rotulo: tipoInspecaoLabel[tipo] ?? String(tipo),
        valor,
        cor: designTokens.colorInfo,
      }))
      .sort((a, b) => b.valor - a.valor);
  }, [inspecoesFiltradas]);

  const obrasNaoConformesDados: ItemRanking[] = useMemo(() => {
    const contagem = new Map<string, number>();
    for (const inspecao of inspecoesFiltradas) {
      if (inspecao.itensNaoConformes > 0) {
        contagem.set(inspecao.obraNome, (contagem.get(inspecao.obraNome) ?? 0) + inspecao.itensNaoConformes);
      }
    }
    return [...contagem.entries()]
      .map(([rotulo, valor]) => ({ rotulo, valor, cor: designTokens.colorAlert }))
      .sort((a, b) => b.valor - a.valor)
      .slice(0, 5);
  }, [inspecoesFiltradas]);

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
        <Field label="Tipo de inspeção">
          <Select value={tipoFiltro} onChange={(_, data) => setTipoFiltro(data.value)}>
            <option value="">Todos os tipos</option>
            {Object.entries(tipoInspecaoLabel).map(([valor, rotulo]) => (
              <option key={valor} value={valor}>
                {rotulo}
              </option>
            ))}
          </Select>
        </Field>
        <Field label="Status">
          <Select value={statusFiltro} onChange={(_, data) => setStatusFiltro(data.value)}>
            <option value="">Todos os status</option>
            {Object.entries(statusInspecaoLabel).map(([valor, rotulo]) => (
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
            rotulo="Total de inspeções"
            valor={inspecoesFiltradas.length}
            cor={designTokens.colorPrimary}
            icone={<ClipboardTaskListLtr24Regular />}
          />
          <KpiCard
            rotulo="Em andamento"
            valor={emAndamento}
            cor={designTokens.colorInfo}
            icone={<ArrowSync24Regular />}
          />
          <KpiCard
            rotulo="Concluídas"
            valor={concluidas}
            cor={designTokens.colorSuccess}
            icone={<CheckmarkCircle24Regular />}
          />
          <KpiCard
            rotulo="Itens não conformes"
            valor={itensNaoConformesTotal}
            cor={designTokens.colorAlert}
            icone={<Warning24Regular />}
          />
        </CardGrid>
      </div>

      <div className={estilos.chartRow}>
        <div className={estilos.chartCard}>
          <Text className={estilos.chartTitulo}>Status das inspeções</Text>
          <div className={estilos.chartSubtitulo}>Execuções em andamento vs. concluídas</div>
          <StatusDonutChart dados={statusDados} legendaCentral="inspeções" />
        </div>
        <div className={estilos.chartCard}>
          <Text className={estilos.chartTitulo}>Inspeções por tipo</Text>
          <div className={estilos.chartSubtitulo}>Distribuição das execuções pelo tipo de inspeção</div>
          <RankingBarChart dados={tipoDados} />
        </div>
        <div className={estilos.chartCard}>
          <Text className={estilos.chartTitulo}>Obras com mais itens não conformes</Text>
          <div className={estilos.chartSubtitulo}>Top 5 obras por total de itens não conformes encontrados</div>
          <RankingBarChart dados={obrasNaoConformesDados} />
        </div>
      </div>

      <InspecoesNaoConformesPanel inspecoes={inspecoesFiltradas} />

      {!carregando && inspecoesFiltradas.length === 0 && (
        <div className={estilosPagina.card} style={{ marginTop: 16 }}>
          <Text>Nenhuma inspeção encontrada para os filtros selecionados.</Text>
        </div>
      )}
    </div>
  );
}
