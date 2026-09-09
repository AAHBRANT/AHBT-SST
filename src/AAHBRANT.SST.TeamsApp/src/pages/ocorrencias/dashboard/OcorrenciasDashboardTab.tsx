import { useEffect, useMemo, useState } from 'react';
import {
  Card,
  EstadoVazio,
  Field,
  FeedbackInline,
  KpiCard,
  RankingBarChart,
  Select,
  StatusDonutChart,
  TrendBarChart,
  usePaletaGraficos,
  type FatiaDonut,
  type ItemRanking,
  type PontoTendencia,
} from '@ui';
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
import { TaxaGravidadeCard } from '../../../components/dashboard/TaxaGravidadeCard';

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

// Dashboard do pilar Ocorrências (pedido do usuário, 03/09) — reúne Acidentes/Incidentes/
// Quase-acidentes (mesma entidade Acidente, diferenciada por Tipo — ver OcorrenciasPage.tsx) e Não
// Conformidades num único painel, no mesmo padrão visual dos outros dashboards de módulo (ver
// NaoConformidadesDashboardTab.tsx). Reaproveita TaxaGravidadeCard (mesmo cálculo NBR 14280 do
// Dashboard principal) em vez de duplicar a fórmula.
// Onda 2 Task 21 (camada ui/, conversão 8): mesmo formato de AprDashboardTab.tsx (Task 11)/
// PgrDashboardTab.tsx (Task 8) — KpiCard+Card+usePaletaGraficos, grade CSS Grid simples (spec §4.4).
export function OcorrenciasDashboardTab() {
  const paleta = usePaletaGraficos();

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

  const coresTipoOcorrencia: Record<number, string> = useMemo(
    () => ({
      [TipoOcorrencia.Acidente]: paleta.alerta,
      [TipoOcorrencia.Incidente]: paleta.atencao,
      [TipoOcorrencia.QuaseAcidente]: paleta.info,
      [TipoOcorrencia.CondicaoInsegura]: paleta.neutro,
      [TipoOcorrencia.AtoInseguro]: paleta.neutro,
      [TipoOcorrencia.DoencaOcupacional]: paleta.ok,
    }),
    [paleta],
  );

  const tipoDados: FatiaDonut[] = useMemo(() => {
    const contagem = new Map<number, number>();
    for (const a of acidentesFiltrados) {
      contagem.set(a.tipo, (contagem.get(a.tipo) ?? 0) + 1);
    }
    return [...contagem.entries()]
      .map(([tipo, valor]) => ({
        rotulo: tipoOcorrenciaLabel[tipo] ?? String(tipo),
        valor,
        cor: coresTipoOcorrencia[tipo] ?? paleta.neutro,
      }))
      .sort((a, b) => b.valor - a.valor);
  }, [acidentesFiltrados, coresTipoOcorrencia, paleta.neutro]);

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
        cor: gravidade >= 3 ? paleta.alerta : paleta.atencao,
      }))
      .sort((a, b) => b.valor - a.valor);
  }, [acidentesFiltrados, paleta]);

  const porObraDados: ItemRanking[] = useMemo(() => {
    const contagem = new Map<string, number>();
    for (const a of acidentesFiltrados) {
      const nome = a.obraNome ?? 'Sem obra';
      contagem.set(nome, (contagem.get(nome) ?? 0) + 1);
    }
    return [...contagem.entries()]
      .map(([rotulo, valor]) => ({ rotulo, valor, cor: paleta.info }))
      .sort((a, b) => b.valor - a.valor)
      .slice(0, 6);
  }, [acidentesFiltrados, paleta.info]);

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
      <div style={{ display: 'flex', gap: 16, marginBottom: 16, flexWrap: 'wrap' }}>
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

      {erro && (
        <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>
          {erro}
        </FeedbackInline>
      )}

      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(185px, 1fr))', gap: 16, marginBottom: 16 }}>
        <KpiCard rotulo="Total de ocorrências" valor={acidentesFiltrados.length} tom="info" indice={0} icone={<ClipboardTaskListLtr24Regular />} />
        <KpiCard rotulo="Acidentes" valor={totalAcidentes} tom="alerta" indice={1} icone={<Alert24Regular />} />
        <KpiCard rotulo="Incidentes" valor={totalIncidentes} tom="atencao" indice={2} icone={<ShieldError24Regular />} />
        <KpiCard rotulo="Quase-acidentes" valor={totalQuaseAcidentes} tom="info" indice={3} icone={<Warning24Regular />} />
        <KpiCard rotulo="Com afastamento" valor={comAfastamento} tom="alerta" indice={4} icone={<PersonSubtract24Regular />} />
        <KpiCard rotulo="Não conformidades abertas" valor={naoConformidadesAbertas} tom="atencao" indice={5} icone={<DocumentError24Regular />} />
      </div>

      <div style={{ marginBottom: 16 }}>
        <TaxaGravidadeCard acidentes={acidentesFiltrados} registrosHht={registrosHhtFiltrados} />
      </div>

      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(340px, 1fr))', gap: 16, marginBottom: 16 }}>
        <Card titulo="Ocorrências por tipo" subtitulo="Distribuição entre acidentes, incidentes e demais tipos">
          <StatusDonutChart dados={tipoDados} legendaCentral="ocorrências" />
        </Card>
        <Card titulo="Gravidade dos acidentes" subtitulo="Classificação NBR 14280 dos acidentes registrados">
          <RankingBarChart dados={gravidadeDados} />
        </Card>
        <Card titulo="Ocorrências por obra" subtitulo="Top obras com mais registros">
          <RankingBarChart dados={porObraDados} corPadrao={paleta.info} />
        </Card>
      </div>

      <div style={{ marginBottom: 16 }}>
        <Card titulo="Ocorrências — últimos 6 meses" subtitulo="Acidentes, incidentes, quase-acidentes e demais tipos, por mês">
          <TrendBarChart dados={tendenciaDados} cor={paleta.alerta} />
        </Card>
      </div>

      {!carregando && acidentesFiltrados.length === 0 && naoConformidadesFiltradas.length === 0 && (
        <EstadoVazio
          titulo="Nenhuma ocorrência encontrada"
          descricao="Ajuste o filtro de obra para ver resultados."
        />
      )}
    </div>
  );
}
