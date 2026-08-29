import { useEffect, useMemo, useState } from 'react';
import { Field, Select, Text } from '@fluentui/react-components';
import {
  api,
  StatusPt,
  statusPtLabel,
  type Atividade,
  type Obra,
  type PermissaoTrabalho,
} from '../../../lib/api';
import { CardGrid } from '../../../layout/AppShell';
import { designTokens } from '../../../theme';
import { usePageStyles } from '../../pageStyles';
import { useDashboardStyles } from '../../../components/dashboard/dashboardStyles';
import { KpiCard } from '../../../components/dashboard/KpiCard';
import { StatusDonutChart, type FatiaDonut } from '../../../components/dashboard/charts/StatusDonutChart';
import { RankingBarChart, type ItemRanking } from '../../../components/dashboard/charts/RankingBarChart';
import { PtVencidaPanel, type PtComContexto } from './PtVencidaPanel';

const hojeISO = new Date().toISOString().slice(0, 10);

export function PtDashboardTab() {
  const estilosPagina = usePageStyles();
  const estilos = useDashboardStyles();

  const [obras, setObras] = useState<Obra[]>([]);
  const [atividades, setAtividades] = useState<Atividade[]>([]);
  const [permissoes, setPermissoes] = useState<PermissaoTrabalho[]>([]);
  const [carregando, setCarregando] = useState(true);
  const [erro, setErro] = useState<string | null>(null);

  const [obraId, setObraId] = useState('');
  const [statusFiltro, setStatusFiltro] = useState('');

  useEffect(() => {
    (async () => {
      try {
        setErro(null);
        const [obrasResp, atividadesResp, ptResp] = await Promise.all([
          api.obras.listar(),
          api.atividades.listar(),
          api.permissoesTrabalho.listar(),
        ]);
        setObras(obrasResp);
        setAtividades(atividadesResp);
        setPermissoes(ptResp);
      } catch (e) {
        setErro(e instanceof Error ? e.message : 'Falha ao carregar dados do dashboard de PT.');
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

  const permissoesFiltradas = useMemo(
    () =>
      permissoes.filter(
        (p) =>
          (obraId === '' || idsAtividadesObra.has(p.atividadeId)) &&
          (statusFiltro === '' || p.status === Number(statusFiltro)),
      ),
    [permissoes, obraId, idsAtividadesObra, statusFiltro],
  );

  const permissoesComContexto: PtComContexto[] = useMemo(
    () =>
      permissoesFiltradas.map((pt) => ({
        ...pt,
        obraNome: nomeObra(obraDaAtividade(pt.atividadeId)),
      })),
    [permissoesFiltradas, atividades, obras],
  );

  const autorizadas = permissoesFiltradas.filter((p) => p.status === StatusPt.Autorizada).length;
  const emElaboracao = permissoesFiltradas.filter((p) => p.status === StatusPt.EmElaboracao).length;
  const suspensas = permissoesFiltradas.filter((p) => p.status === StatusPt.Suspensa).length;
  const vencidas = permissoesComContexto.filter(
    (p) => !!p.validade && p.validade < hojeISO && p.status !== StatusPt.Encerrada,
  ).length;

  const statusDados: FatiaDonut[] = [
    { rotulo: 'Em elaboração', valor: emElaboracao, cor: designTokens.colorWarning },
    { rotulo: 'Autorizada', valor: autorizadas, cor: designTokens.colorSuccess },
    { rotulo: 'Suspensa', valor: suspensas, cor: designTokens.colorAlert },
    {
      rotulo: 'Encerrada',
      valor: permissoesFiltradas.filter((p) => p.status === StatusPt.Encerrada).length,
      cor: designTokens.colorInfo,
    },
  ];

  const obraDados: ItemRanking[] = useMemo(() => {
    const contagem = new Map<string, number>();
    for (const pt of permissoesComContexto) {
      contagem.set(pt.obraNome, (contagem.get(pt.obraNome) ?? 0) + 1);
    }
    return [...contagem.entries()]
      .map(([rotulo, valor]) => ({ rotulo, valor, cor: designTokens.colorPrimary }))
      .sort((a, b) => b.valor - a.valor)
      .slice(0, 5);
  }, [permissoesComContexto]);

  const atividadeDados: ItemRanking[] = useMemo(() => {
    const contagem = new Map<string, number>();
    for (const pt of permissoesFiltradas) {
      contagem.set(pt.atividadeNome, (contagem.get(pt.atividadeNome) ?? 0) + 1);
    }
    return [...contagem.entries()]
      .map(([rotulo, valor]) => ({ rotulo, valor, cor: designTokens.colorInfo }))
      .sort((a, b) => b.valor - a.valor)
      .slice(0, 5);
  }, [permissoesFiltradas]);

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
        <Field label="Status da PT">
          <Select value={statusFiltro} onChange={(_, data) => setStatusFiltro(data.value)}>
            <option value="">Todos os status</option>
            {Object.entries(statusPtLabel).map(([valor, rotulo]) => (
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
          <KpiCard rotulo="Total de PTs" valor={permissoesFiltradas.length} cor={designTokens.colorPrimary} />
          <KpiCard rotulo="Autorizadas" valor={autorizadas} cor={designTokens.colorSuccess} />
          <KpiCard rotulo="Em elaboração" valor={emElaboracao} cor={designTokens.colorWarning} />
          <KpiCard rotulo="Com validade vencida" valor={vencidas} cor={designTokens.colorAlert} />
        </CardGrid>
      </div>

      <div className={estilos.chartRow}>
        <div className={estilos.chartCard}>
          <Text className={estilos.chartTitulo}>Status das PTs</Text>
          <div className={estilos.chartSubtitulo}>Situação atual de cada Permissão de Trabalho</div>
          <StatusDonutChart dados={statusDados} legendaCentral="PTs" />
        </div>
        <div className={estilos.chartCard}>
          <Text className={estilos.chartTitulo}>PTs por obra</Text>
          <div className={estilos.chartSubtitulo}>Top 5 obras com mais PTs cadastradas</div>
          <RankingBarChart dados={obraDados} />
        </div>
        <div className={estilos.chartCard}>
          <Text className={estilos.chartTitulo}>PTs por atividade</Text>
          <div className={estilos.chartSubtitulo}>Top 5 atividades com mais PTs cadastradas</div>
          <RankingBarChart dados={atividadeDados} />
        </div>
      </div>

      <PtVencidaPanel permissoes={permissoesComContexto} />

      {!carregando && permissoesFiltradas.length === 0 && (
        <div className={estilosPagina.card} style={{ marginTop: 16 }}>
          <Text>Nenhuma PT encontrada para os filtros selecionados.</Text>
        </div>
      )}
    </div>
  );
}
