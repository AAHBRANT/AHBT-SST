import { useEffect, useMemo, useState } from 'react';
import { Field, Select, Text } from '@fluentui/react-components';
import { DocumentError24Regular, Warning24Regular, ArrowSync24Regular, Alert24Regular } from '@fluentui/react-icons';
import {
  api,
  origemNaoConformidadeLabel,
  StatusNaoConformidade,
  statusNaoConformidadeLabel,
  type Atividade,
  type NaoConformidade,
  type Obra,
} from '../../../lib/api';
import { CardGrid } from '../../../layout/AppShell';
import { designTokens } from '../../../theme';
import { usePageStyles } from '../../pageStyles';
import { useDashboardStyles } from '../../../components/dashboard/dashboardStyles';
import { KpiCard } from '../../../components/dashboard/KpiCard';
import { StatusDonutChart, type FatiaDonut } from '../../../components/dashboard/charts/StatusDonutChart';
import { RankingBarChart, type ItemRanking } from '../../../components/dashboard/charts/RankingBarChart';
import { NaoConformidadesCriticasPanel } from './NaoConformidadesCriticasPanel';

const hojeISO = new Date().toISOString().slice(0, 10);

export function NaoConformidadesDashboardTab() {
  const estilosPagina = usePageStyles();
  const estilos = useDashboardStyles();

  const [obras, setObras] = useState<Obra[]>([]);
  const [atividades, setAtividades] = useState<Atividade[]>([]);
  const [naoConformidades, setNaoConformidades] = useState<NaoConformidade[]>([]);
  const [carregando, setCarregando] = useState(true);
  const [erro, setErro] = useState<string | null>(null);

  const [obraId, setObraId] = useState('');
  const [origemFiltro, setOrigemFiltro] = useState('');
  const [statusFiltro, setStatusFiltro] = useState('');

  useEffect(() => {
    (async () => {
      try {
        setErro(null);
        const [obrasResp, atividadesResp, ncResp] = await Promise.all([
          api.obras.listar(),
          api.atividades.listar(),
          api.naoConformidades.listar(),
        ]);
        setObras(obrasResp);
        setAtividades(atividadesResp);
        setNaoConformidades(ncResp);
      } catch (e) {
        setErro(e instanceof Error ? e.message : 'Falha ao carregar dados do dashboard de não conformidades.');
      } finally {
        setCarregando(false);
      }
    })();
  }, []);

  const idsAtividadesObra = useMemo(() => {
    const filtradas = obraId === '' ? atividades : atividades.filter((a) => a.obraId === obraId);
    return new Set(filtradas.map((a) => a.id));
  }, [atividades, obraId]);

  const naoConformidadesFiltradas = useMemo(
    () =>
      naoConformidades.filter(
        (nc) =>
          (obraId === '' || (!!nc.atividadeId && idsAtividadesObra.has(nc.atividadeId))) &&
          (origemFiltro === '' || nc.origemDeteccao === Number(origemFiltro)) &&
          (statusFiltro === '' || nc.status === Number(statusFiltro)),
      ),
    [naoConformidades, obraId, idsAtividadesObra, origemFiltro, statusFiltro],
  );

  const abertas = naoConformidadesFiltradas.filter((nc) => nc.status === StatusNaoConformidade.Aberta).length;
  const emTratamento = naoConformidadesFiltradas.filter(
    (nc) =>
      nc.status === StatusNaoConformidade.EmAndamento || nc.status === StatusNaoConformidade.AguardandoValidacao,
  ).length;
  const encerradas = naoConformidadesFiltradas.filter((nc) => nc.status === StatusNaoConformidade.Encerrada).length;
  const prazoVencido = naoConformidadesFiltradas.filter(
    (nc) => !!nc.prazo && nc.prazo < hojeISO && nc.status !== StatusNaoConformidade.Encerrada,
  ).length;

  const statusDados: FatiaDonut[] = [
    { rotulo: 'Aberta', valor: abertas, cor: designTokens.colorAlert },
    {
      rotulo: 'Em andamento',
      valor: naoConformidadesFiltradas.filter((nc) => nc.status === StatusNaoConformidade.EmAndamento).length,
      cor: designTokens.colorWarning,
    },
    {
      rotulo: 'Aguardando validação',
      valor: naoConformidadesFiltradas.filter((nc) => nc.status === StatusNaoConformidade.AguardandoValidacao)
        .length,
      cor: designTokens.colorInfo,
    },
    { rotulo: 'Encerrada', valor: encerradas, cor: designTokens.colorSuccess },
  ];

  const origemDados: ItemRanking[] = useMemo(() => {
    const contagem = new Map<number, number>();
    for (const nc of naoConformidadesFiltradas) {
      contagem.set(nc.origemDeteccao, (contagem.get(nc.origemDeteccao) ?? 0) + 1);
    }
    return [...contagem.entries()]
      .map(([origem, valor]) => ({
        rotulo: origemNaoConformidadeLabel[origem] ?? String(origem),
        valor,
        cor: designTokens.colorInfo,
      }))
      .sort((a, b) => b.valor - a.valor);
  }, [naoConformidadesFiltradas]);

  const responsaveisDados: ItemRanking[] = useMemo(() => {
    const contagem = new Map<string, number>();
    for (const nc of naoConformidadesFiltradas) {
      if (nc.status === StatusNaoConformidade.Encerrada) continue;
      const nome = nc.responsavelUsuarioNome ?? 'Sem responsável';
      contagem.set(nome, (contagem.get(nome) ?? 0) + 1);
    }
    return [...contagem.entries()]
      .map(([rotulo, valor]) => ({ rotulo, valor, cor: designTokens.colorWarning }))
      .sort((a, b) => b.valor - a.valor)
      .slice(0, 5);
  }, [naoConformidadesFiltradas]);

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
        <Field label="Origem">
          <Select value={origemFiltro} onChange={(_, data) => setOrigemFiltro(data.value)}>
            <option value="">Todas as origens</option>
            {Object.entries(origemNaoConformidadeLabel).map(([valor, rotulo]) => (
              <option key={valor} value={valor}>
                {rotulo}
              </option>
            ))}
          </Select>
        </Field>
        <Field label="Status">
          <Select value={statusFiltro} onChange={(_, data) => setStatusFiltro(data.value)}>
            <option value="">Todos os status</option>
            {Object.entries(statusNaoConformidadeLabel).map(([valor, rotulo]) => (
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
            rotulo="Total de NCs"
            valor={naoConformidadesFiltradas.length}
            cor={designTokens.colorPrimary}
            icone={<DocumentError24Regular />}
          />
          <KpiCard rotulo="Abertas" valor={abertas} cor={designTokens.colorAlert} icone={<Warning24Regular />} />
          <KpiCard
            rotulo="Em tratamento"
            valor={emTratamento}
            cor={designTokens.colorWarning}
            icone={<ArrowSync24Regular />}
          />
          <KpiCard
            rotulo="Com prazo vencido"
            valor={prazoVencido}
            cor={designTokens.colorAlert}
            icone={<Alert24Regular />}
          />
        </CardGrid>
      </div>

      <div className={estilos.chartRow}>
        <div className={estilos.chartCard}>
          <Text className={estilos.chartTitulo}>Status das não conformidades</Text>
          <div className={estilos.chartSubtitulo}>Situação atual do tratamento de cada NC</div>
          <StatusDonutChart dados={statusDados} legendaCentral="NCs" />
        </div>
        <div className={estilos.chartCard}>
          <Text className={estilos.chartTitulo}>NCs por origem de detecção</Text>
          <div className={estilos.chartSubtitulo}>Onde as não conformidades foram identificadas</div>
          <RankingBarChart dados={origemDados} />
        </div>
        <div className={estilos.chartCard}>
          <Text className={estilos.chartTitulo}>Responsáveis com mais NCs em aberto</Text>
          <div className={estilos.chartSubtitulo}>Top 5 responsáveis por NCs ainda não encerradas</div>
          <RankingBarChart dados={responsaveisDados} />
        </div>
      </div>

      <NaoConformidadesCriticasPanel naoConformidades={naoConformidadesFiltradas} />

      {!carregando && naoConformidadesFiltradas.length === 0 && (
        <div className={estilosPagina.card} style={{ marginTop: 16 }}>
          <Text>Nenhuma não conformidade encontrada para os filtros selecionados.</Text>
        </div>
      )}
    </div>
  );
}
