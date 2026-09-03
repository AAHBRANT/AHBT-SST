import { useEffect, useMemo, useState } from 'react';
import { Field, Select, Text } from '@fluentui/react-components';
import {
  Warning24Regular,
  Alert24Regular,
  DocumentError24Regular,
  PersonSubtract24Regular,
  ClipboardTaskListLtr24Regular,
  ShieldError24Regular,
} from '@fluentui/react-icons';
import {
  api,
  StatusNaoConformidade,
  TipoOcorrencia,
  tipoOcorrenciaLabel,
  gravidadeAcidenteLabel,
  type Acidente,
  type Atividade,
  type NaoConformidade,
  type Obra,
  type RegistroHhtMensal,
} from '../../../lib/api';
import { CardGrid } from '../../../layout/AppShell';
import { designTokens } from '../../../theme';
import { usePageStyles } from '../../pageStyles';
import { useDashboardStyles } from '../../../components/dashboard/dashboardStyles';
import { KpiCard } from '../../../components/dashboard/KpiCard';
import { TaxaGravidadeCard } from '../../../components/dashboard/TaxaGravidadeCard';
import { StatusDonutChart, type FatiaDonut } from '../../../components/dashboard/charts/StatusDonutChart';
import { RankingBarChart, type ItemRanking } from '../../../components/dashboard/charts/RankingBarChart';
import { TrendBarChart, type PontoTendencia } from '../../../components/dashboard/charts/TrendBarChart';

const NOMES_MESES_ABREVIADOS = ['Jan', 'Fev', 'Mar', 'Abr', 'Mai', 'Jun', 'Jul', 'Ago', 'Set', 'Out', 'Nov', 'Dez'];

function ultimosSeisMeses(): Array<{ ano: number; mes: number; rotulo: string }> {
  const agora = new Date();
  const meses: Array<{ ano: number; mes: number; rotulo: string }> = [];
  for (let i = 5; i >= 0; i -= 1) {
    const data = new Date(agora.getFullYear(), agora.getMonth() - i, 1);
    meses.push({ ano: data.getFullYear(), mes: data.getMonth() + 1, rotulo: NOMES_MESES_ABREVIADOS[data.getMonth()] });
  }
  return meses;
}

const CORES_TIPO_OCORRENCIA: Record<number, string> = {
  [TipoOcorrencia.Acidente]: designTokens.colorAlert,
  [TipoOcorrencia.Incidente]: designTokens.colorWarning,
  [TipoOcorrencia.QuaseAcidente]: designTokens.colorInfo,
  [TipoOcorrencia.CondicaoInsegura]: designTokens.colorNeutralMedium,
  [TipoOcorrencia.AtoInseguro]: designTokens.colorNeutralMedium,
  [TipoOcorrencia.DoencaOcupacional]: designTokens.colorSuccess,
};

// Dashboard do pilar Ocorrências (pedido do usuário, 03/09) — reúne Acidentes/Incidentes/
// Quase-acidentes (mesma entidade Acidente, diferenciada por Tipo — ver OcorrenciasPage.tsx) e Não
// Conformidades num único painel, no mesmo padrão visual dos outros dashboards de módulo (ver
// NaoConformidadesDashboardTab.tsx). Reaproveita TaxaGravidadeCard (mesmo cálculo NBR 14280 do
// Dashboard principal) em vez de duplicar a fórmula.
export function OcorrenciasDashboardTab() {
  const estilosPagina = usePageStyles();
  const estilos = useDashboardStyles();

  const [obras, setObras] = useState<Obra[]>([]);
  const [atividades, setAtividades] = useState<Atividade[]>([]);
  const [acidentes, setAcidentes] = useState<Acidente[]>([]);
  const [naoConformidades, setNaoConformidades] = useState<NaoConformidade[]>([]);
  const [registrosHht, setRegistrosHht] = useState<RegistroHhtMensal[]>([]);
  const [carregando, setCarregando] = useState(true);
  const [erro, setErro] = useState<string | null>(null);

  const [obraId, setObraId] = useState('');

  useEffect(() => {
    (async () => {
      try {
        setErro(null);
        const [obrasResp, atividadesResp, acidentesResp, ncResp, hhtResp] = await Promise.all([
          api.obras.listar(),
          api.atividades.listar(),
          api.acidentes.listar(),
          api.naoConformidades.listar(),
          api.registrosHht.listar(),
        ]);
        setObras(obrasResp);
        setAtividades(atividadesResp);
        setAcidentes(acidentesResp);
        setNaoConformidades(ncResp);
        setRegistrosHht(hhtResp);
      } catch (e) {
        setErro(e instanceof Error ? e.message : 'Falha ao carregar dados do dashboard de ocorrências.');
      } finally {
        setCarregando(false);
      }
    })();
  }, []);

  const idsAtividadesObra = useMemo(() => {
    const filtradas = obraId === '' ? atividades : atividades.filter((a) => a.obraId === obraId);
    return new Set(filtradas.map((a) => a.id));
  }, [atividades, obraId]);

  const acidentesFiltrados = useMemo(
    () => (obraId === '' ? acidentes : acidentes.filter((a) => a.obraId === obraId)),
    [acidentes, obraId],
  );
  const registrosHhtFiltrados = useMemo(
    () => (obraId === '' ? registrosHht : registrosHht.filter((r) => r.obraId === obraId)),
    [registrosHht, obraId],
  );
  const naoConformidadesFiltradas = useMemo(
    () =>
      naoConformidades.filter(
        (nc) => obraId === '' || (!!nc.atividadeId && idsAtividadesObra.has(nc.atividadeId)),
      ),
    [naoConformidades, obraId, idsAtividadesObra],
  );

  const totalAcidentes = acidentesFiltrados.filter((a) => a.tipo === TipoOcorrencia.Acidente).length;
  const totalIncidentes = acidentesFiltrados.filter((a) => a.tipo === TipoOcorrencia.Incidente).length;
  const totalQuaseAcidentes = acidentesFiltrados.filter((a) => a.tipo === TipoOcorrencia.QuaseAcidente).length;
  const comAfastamento = acidentesFiltrados.filter((a) => a.houveAfastamento).length;
  const naoConformidadesAbertas = naoConformidadesFiltradas.filter(
    (nc) => nc.status !== StatusNaoConformidade.Encerrada,
  ).length;

  const tipoDados: FatiaDonut[] = useMemo(() => {
    const contagem = new Map<number, number>();
    for (const a of acidentesFiltrados) {
      contagem.set(a.tipo, (contagem.get(a.tipo) ?? 0) + 1);
    }
    return [...contagem.entries()]
      .map(([tipo, valor]) => ({
        rotulo: tipoOcorrenciaLabel[tipo] ?? String(tipo),
        valor,
        cor: CORES_TIPO_OCORRENCIA[tipo] ?? designTokens.colorNeutralMedium,
      }))
      .sort((a, b) => b.valor - a.valor);
  }, [acidentesFiltrados]);

  const gravidadeDados: ItemRanking[] = useMemo(() => {
    const contagem = new Map<number, number>();
    for (const a of acidentesFiltrados) {
      if (a.tipo !== TipoOcorrencia.Acidente) continue;
      contagem.set(a.gravidade, (contagem.get(a.gravidade) ?? 0) + 1);
    }
    return [...contagem.entries()]
      .map(([gravidade, valor]) => ({
        rotulo: gravidadeAcidenteLabel[gravidade] ?? String(gravidade),
        valor,
        cor: gravidade >= 3 ? designTokens.colorAlert : designTokens.colorWarning,
      }))
      .sort((a, b) => b.valor - a.valor);
  }, [acidentesFiltrados]);

  const porObraDados: ItemRanking[] = useMemo(() => {
    const contagem = new Map<string, number>();
    for (const a of acidentesFiltrados) {
      const nome = a.obraNome ?? 'Sem obra';
      contagem.set(nome, (contagem.get(nome) ?? 0) + 1);
    }
    return [...contagem.entries()]
      .map(([rotulo, valor]) => ({ rotulo, valor, cor: designTokens.colorInfo }))
      .sort((a, b) => b.valor - a.valor)
      .slice(0, 6);
  }, [acidentesFiltrados]);

  const tendenciaDados: PontoTendencia[] = useMemo(
    () =>
      ultimosSeisMeses().map(({ ano, mes, rotulo }) => ({
        rotulo,
        valor: acidentesFiltrados.filter((a) => {
          const data = new Date(a.data);
          return data.getFullYear() === ano && data.getMonth() + 1 === mes;
        }).length,
      })),
    [acidentesFiltrados],
  );

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
      </div>

      {erro && <Text className={estilosPagina.erro}>{erro}</Text>}

      <div style={{ marginBottom: 16 }}>
        <CardGrid>
          <KpiCard
            rotulo="Total de ocorrências"
            valor={acidentesFiltrados.length}
            cor={designTokens.colorPrimary}
            icone={<ClipboardTaskListLtr24Regular />}
          />
          <KpiCard rotulo="Acidentes" valor={totalAcidentes} cor={designTokens.colorAlert} icone={<Alert24Regular />} />
          <KpiCard
            rotulo="Incidentes"
            valor={totalIncidentes}
            cor={designTokens.colorWarning}
            icone={<ShieldError24Regular />}
          />
          <KpiCard
            rotulo="Quase-acidentes"
            valor={totalQuaseAcidentes}
            cor={designTokens.colorInfo}
            icone={<Warning24Regular />}
          />
          <KpiCard
            rotulo="Com afastamento"
            valor={comAfastamento}
            cor={designTokens.colorAlert}
            icone={<PersonSubtract24Regular />}
          />
          <KpiCard
            rotulo="Não conformidades abertas"
            valor={naoConformidadesAbertas}
            cor={designTokens.colorWarning}
            icone={<DocumentError24Regular />}
          />
        </CardGrid>
      </div>

      <div style={{ marginBottom: 16 }}>
        <TaxaGravidadeCard acidentes={acidentesFiltrados} registrosHht={registrosHhtFiltrados} />
      </div>

      <div className={estilos.chartRow}>
        <div className={estilos.chartCard}>
          <Text className={estilos.chartTitulo}>Ocorrências por tipo</Text>
          <div className={estilos.chartSubtitulo}>Distribuição entre acidentes, incidentes e demais tipos</div>
          <StatusDonutChart dados={tipoDados} legendaCentral="ocorrências" />
        </div>
        <div className={estilos.chartCard}>
          <Text className={estilos.chartTitulo}>Gravidade dos acidentes</Text>
          <div className={estilos.chartSubtitulo}>Classificação NBR 14280 dos acidentes registrados</div>
          <RankingBarChart dados={gravidadeDados} />
        </div>
        <div className={estilos.chartCard}>
          <Text className={estilos.chartTitulo}>Ocorrências por obra</Text>
          <div className={estilos.chartSubtitulo}>Top obras com mais registros</div>
          <RankingBarChart dados={porObraDados} corPadrao={designTokens.colorInfo} />
        </div>
      </div>

      <div className={estilos.chartCard} style={{ marginBottom: 16 }}>
        <Text className={estilos.chartTitulo}>Ocorrências — últimos 6 meses</Text>
        <div className={estilos.chartSubtitulo}>Acidentes, incidentes, quase-acidentes e demais tipos, por mês</div>
        <TrendBarChart dados={tendenciaDados} cor={designTokens.colorAlert} />
      </div>

      {!carregando && acidentesFiltrados.length === 0 && naoConformidadesFiltradas.length === 0 && (
        <div className={estilosPagina.card}>
          <Text>Nenhuma ocorrência encontrada para os filtros selecionados.</Text>
        </div>
      )}
    </div>
  );
}
