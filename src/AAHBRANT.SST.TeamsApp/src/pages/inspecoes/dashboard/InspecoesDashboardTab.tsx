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
  usePaletaGraficos,
  type FatiaDonut,
  type ItemRanking,
} from '@ui';
import { ClipboardTaskListLtr24Regular, ArrowSync24Regular, CheckmarkCircle24Regular, Warning24Regular } from '@fluentui/react-icons';
import {
  api,
  StatusInspecao,
  statusInspecaoLabel,
  tipoInspecaoLabel,
  type Inspecao,
  type Obra,
} from '../../../lib/api';
import { InspecoesNaoConformesPanel } from './InspecoesNaoConformesPanel';

// Onda 2 Task 10 (camada ui/): dashboard de Inspeções — KpiCard + gráficos com cores lidas de
// usePaletaGraficos (paleta.ts, spec §1.6), grade CSS Grid simples (spec §4.4). A lógica de
// agregação no cliente não muda nesta frente.
export function InspecoesDashboardTab() {
  const paleta = usePaletaGraficos();

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
    { rotulo: 'Em andamento', valor: emAndamento, cor: paleta.info },
    { rotulo: 'Concluída', valor: concluidas, cor: paleta.ok },
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
        cor: paleta.info,
      }))
      .sort((a, b) => b.valor - a.valor);
  }, [inspecoesFiltradas, paleta.info]);

  const obrasNaoConformesDados: ItemRanking[] = useMemo(() => {
    const contagem = new Map<string, number>();
    for (const inspecao of inspecoesFiltradas) {
      if (inspecao.itensNaoConformes > 0) {
        contagem.set(inspecao.obraNome, (contagem.get(inspecao.obraNome) ?? 0) + inspecao.itensNaoConformes);
      }
    }
    return [...contagem.entries()]
      .map(([rotulo, valor]) => ({ rotulo, valor, cor: paleta.alerta }))
      .sort((a, b) => b.valor - a.valor)
      .slice(0, 5);
  }, [inspecoesFiltradas, paleta.alerta]);

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

      {erro && (
        <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>
          {erro}
        </FeedbackInline>
      )}

      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(185px, 1fr))', gap: 16, marginBottom: 16 }}>
        <KpiCard rotulo="Total de inspeções" valor={inspecoesFiltradas.length} tom="info" indice={0} icone={<ClipboardTaskListLtr24Regular />} />
        <KpiCard rotulo="Em andamento" valor={emAndamento} tom="info" indice={1} icone={<ArrowSync24Regular />} />
        <KpiCard rotulo="Concluídas" valor={concluidas} tom="ok" indice={2} icone={<CheckmarkCircle24Regular />} />
        <KpiCard rotulo="Itens não conformes" valor={itensNaoConformesTotal} tom="alerta" indice={3} icone={<Warning24Regular />} />
      </div>

      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(340px, 1fr))', gap: 16, marginBottom: 16 }}>
        <Card titulo="Status das inspeções" subtitulo="Execuções em andamento vs. concluídas">
          <StatusDonutChart dados={statusDados} legendaCentral="inspeções" />
        </Card>
        <Card titulo="Inspeções por tipo" subtitulo="Distribuição das execuções pelo tipo de inspeção">
          <RankingBarChart dados={tipoDados} />
        </Card>
        <Card titulo="Obras com mais itens não conformes" subtitulo="Top 5 obras por total de itens não conformes encontrados">
          <RankingBarChart dados={obrasNaoConformesDados} />
        </Card>
      </div>

      <InspecoesNaoConformesPanel inspecoes={inspecoesFiltradas} />

      {!carregando && inspecoesFiltradas.length === 0 && (
        <div style={{ marginTop: 16 }}>
          <EstadoVazio
            titulo="Nenhuma inspeção encontrada"
            descricao="Ajuste os filtros de obra, tipo ou status para ver resultados."
          />
        </div>
      )}
    </div>
  );
}
