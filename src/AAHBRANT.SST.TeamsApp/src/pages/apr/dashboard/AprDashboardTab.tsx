import { useEffect, useMemo, useState } from 'react';
import { Field, Select, Text } from '@fluentui/react-components';
import {
  api,
  StatusApr,
  statusAprLabel,
  type Apr,
  type Atividade,
  type Obra,
} from '../../../lib/api';
import { CardGrid } from '../../../layout/AppShell';
import { designTokens } from '../../../theme';
import { usePageStyles } from '../../pageStyles';
import { useDashboardStyles } from '../../../components/dashboard/dashboardStyles';
import { KpiCard } from '../../../components/dashboard/KpiCard';
import { StatusDonutChart, type FatiaDonut } from '../../../components/dashboard/charts/StatusDonutChart';
import { RankingBarChart, type ItemRanking } from '../../../components/dashboard/charts/RankingBarChart';
import { AprVencidaPanel, type AprComContexto } from './AprVencidaPanel';

const hojeISO = new Date().toISOString().slice(0, 10);

export function AprDashboardTab() {
  const estilosPagina = usePageStyles();
  const estilos = useDashboardStyles();

  const [obras, setObras] = useState<Obra[]>([]);
  const [atividades, setAtividades] = useState<Atividade[]>([]);
  const [aprs, setAprs] = useState<Apr[]>([]);
  const [carregando, setCarregando] = useState(true);
  const [erro, setErro] = useState<string | null>(null);

  const [obraId, setObraId] = useState('');
  const [statusFiltro, setStatusFiltro] = useState('');

  useEffect(() => {
    (async () => {
      try {
        setErro(null);
        const [obrasResp, atividadesResp, aprsResp] = await Promise.all([
          api.obras.listar(),
          api.atividades.listar(),
          api.aprs.listar(),
        ]);
        setObras(obrasResp);
        setAtividades(atividadesResp);
        setAprs(aprsResp);
      } catch (e) {
        setErro(e instanceof Error ? e.message : 'Falha ao carregar dados do dashboard de APR.');
      } finally {
        setCarregando(false);
      }
    })();
  }, []);

  function obraDaAtividade(atividadeId: string) {
    return atividades.find((a) => a.id === atividadeId)?.obraId;
  }

  function nomeObra(id: string | undefined) {
    return obras.find((o) => o.id === id)?.nome ?? 'Sem obra';
  }

  const idsAtividadesObra = useMemo(() => {
    const filtradas = obraId === '' ? atividades : atividades.filter((a) => a.obraId === obraId);
    return new Set(filtradas.map((a) => a.id));
  }, [atividades, obraId]);

  const aprsFiltradas = useMemo(
    () =>
      aprs.filter(
        (a) =>
          (obraId === '' || idsAtividadesObra.has(a.atividadeId)) &&
          (statusFiltro === '' || a.status === Number(statusFiltro)),
      ),
    [aprs, obraId, idsAtividadesObra, statusFiltro],
  );

  const aprsComContexto: AprComContexto[] = useMemo(
    () =>
      aprsFiltradas.map((apr) => ({
        ...apr,
        obraNome: nomeObra(obraDaAtividade(apr.atividadeId)),
      })),
    [aprsFiltradas, atividades, obras],
  );

  const aprovadas = aprsFiltradas.filter((a) => a.status === StatusApr.Aprovada).length;
  const aguardandoAprovacao = aprsFiltradas.filter((a) => a.status === StatusApr.AguardandoAprovacao).length;
  const vencidas = aprsComContexto.filter(
    (a) => !!a.validade && a.validade < hojeISO && a.status !== StatusApr.Encerrada && a.status !== StatusApr.Reprovada,
  ).length;

  const statusDados: FatiaDonut[] = [
    {
      rotulo: 'Em elaboração',
      valor: aprsFiltradas.filter((a) => a.status === StatusApr.EmElaboracao).length,
      cor: designTokens.colorInfo,
    },
    { rotulo: 'Aguardando aprovação', valor: aguardandoAprovacao, cor: designTokens.colorWarning },
    { rotulo: 'Aprovada', valor: aprovadas, cor: designTokens.colorSuccess },
    {
      rotulo: 'Reprovada',
      valor: aprsFiltradas.filter((a) => a.status === StatusApr.Reprovada).length,
      cor: designTokens.colorAlert,
    },
    {
      rotulo: 'Encerrada',
      valor: aprsFiltradas.filter((a) => a.status === StatusApr.Encerrada).length,
      cor: designTokens.colorPrimary,
    },
  ];

  const obraDados: ItemRanking[] = useMemo(() => {
    const contagem = new Map<string, number>();
    for (const apr of aprsComContexto) {
      contagem.set(apr.obraNome, (contagem.get(apr.obraNome) ?? 0) + 1);
    }
    return [...contagem.entries()]
      .map(([rotulo, valor]) => ({ rotulo, valor, cor: designTokens.colorPrimary }))
      .sort((a, b) => b.valor - a.valor)
      .slice(0, 5);
  }, [aprsComContexto]);

  const atividadeDados: ItemRanking[] = useMemo(() => {
    const contagem = new Map<string, number>();
    for (const apr of aprsFiltradas) {
      contagem.set(apr.atividadeNome, (contagem.get(apr.atividadeNome) ?? 0) + 1);
    }
    return [...contagem.entries()]
      .map(([rotulo, valor]) => ({ rotulo, valor, cor: designTokens.colorInfo }))
      .sort((a, b) => b.valor - a.valor)
      .slice(0, 5);
  }, [aprsFiltradas]);

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
        <Field label="Status da APR">
          <Select value={statusFiltro} onChange={(_, data) => setStatusFiltro(data.value)}>
            <option value="">Todos os status</option>
            {Object.entries(statusAprLabel).map(([valor, rotulo]) => (
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
          <KpiCard rotulo="Total de APRs" valor={aprsFiltradas.length} cor={designTokens.colorPrimary} />
          <KpiCard rotulo="Aprovadas" valor={aprovadas} cor={designTokens.colorSuccess} />
          <KpiCard rotulo="Aguardando aprovação" valor={aguardandoAprovacao} cor={designTokens.colorWarning} />
          <KpiCard rotulo="Com validade vencida" valor={vencidas} cor={designTokens.colorAlert} />
        </CardGrid>
      </div>

      <div className={estilos.chartRow}>
        <div className={estilos.chartCard}>
          <Text className={estilos.chartTitulo}>Status das APRs</Text>
          <div className={estilos.chartSubtitulo}>Situação atual de cada Análise Preliminar de Risco</div>
          <StatusDonutChart dados={statusDados} legendaCentral="APRs" />
        </div>
        <div className={estilos.chartCard}>
          <Text className={estilos.chartTitulo}>APRs por obra</Text>
          <div className={estilos.chartSubtitulo}>Top 5 obras com mais APRs cadastradas</div>
          <RankingBarChart dados={obraDados} />
        </div>
        <div className={estilos.chartCard}>
          <Text className={estilos.chartTitulo}>APRs por atividade</Text>
          <div className={estilos.chartSubtitulo}>Top 5 atividades com mais APRs cadastradas</div>
          <RankingBarChart dados={atividadeDados} />
        </div>
      </div>

      <AprVencidaPanel aprs={aprsComContexto} />

      {!carregando && aprsFiltradas.length === 0 && (
        <div className={estilosPagina.card} style={{ marginTop: 16 }}>
          <Text>Nenhuma APR encontrada para os filtros selecionados.</Text>
        </div>
      )}
    </div>
  );
}
