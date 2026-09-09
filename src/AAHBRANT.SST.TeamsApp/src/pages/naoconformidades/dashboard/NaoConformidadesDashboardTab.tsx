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
import { NaoConformidadesCriticasPanel } from './NaoConformidadesCriticasPanel';

const hojeISO = new Date().toISOString().slice(0, 10);

// Onda 2 Task 15 (camada ui/): dashboard de Não Conformidades — mesmo padrão de AprDashboardTab.tsx
// (Task 11)/PgrDashboardTab.tsx (Task 8): KpiCard (tom em vez de cor), gráficos com cor de
// usePaletaGraficos() (spec §1.6) em vez de designTokens.colorX cru, grade CSS Grid simples (spec
// §4.4, sem componente de grade próprio). A lógica de agregação no cliente não muda nesta frente.
export function NaoConformidadesDashboardTab() {
  const paleta = usePaletaGraficos();

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
    { rotulo: 'Aberta', valor: abertas, cor: paleta.alerta },
    {
      rotulo: 'Em andamento',
      valor: naoConformidadesFiltradas.filter((nc) => nc.status === StatusNaoConformidade.EmAndamento).length,
      cor: paleta.atencao,
    },
    {
      rotulo: 'Aguardando validação',
      valor: naoConformidadesFiltradas.filter((nc) => nc.status === StatusNaoConformidade.AguardandoValidacao)
        .length,
      cor: paleta.info,
    },
    { rotulo: 'Encerrada', valor: encerradas, cor: paleta.ok },
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
        cor: paleta.info,
      }))
      .sort((a, b) => b.valor - a.valor);
  }, [naoConformidadesFiltradas, paleta.info]);

  const responsaveisDados: ItemRanking[] = useMemo(() => {
    const contagem = new Map<string, number>();
    for (const nc of naoConformidadesFiltradas) {
      if (nc.status === StatusNaoConformidade.Encerrada) continue;
      const nome = nc.responsavelUsuarioNome ?? 'Sem responsável';
      contagem.set(nome, (contagem.get(nome) ?? 0) + 1);
    }
    return [...contagem.entries()]
      .map(([rotulo, valor]) => ({ rotulo, valor, cor: paleta.atencao }))
      .sort((a, b) => b.valor - a.valor)
      .slice(0, 5);
  }, [naoConformidadesFiltradas, paleta.atencao]);

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

      {erro && (
        <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>
          {erro}
        </FeedbackInline>
      )}

      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(185px, 1fr))', gap: 16, marginBottom: 16 }}>
        <KpiCard rotulo="Total de NCs" valor={naoConformidadesFiltradas.length} tom="info" indice={0} icone={<DocumentError24Regular />} />
        <KpiCard rotulo="Abertas" valor={abertas} tom="alerta" indice={1} icone={<Warning24Regular />} />
        <KpiCard rotulo="Em tratamento" valor={emTratamento} tom="atencao" indice={2} icone={<ArrowSync24Regular />} />
        <KpiCard rotulo="Com prazo vencido" valor={prazoVencido} tom="alerta" indice={3} icone={<Alert24Regular />} />
      </div>

      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(340px, 1fr))', gap: 16, marginBottom: 16 }}>
        <Card titulo="Status das não conformidades" subtitulo="Situação atual do tratamento de cada NC">
          <StatusDonutChart dados={statusDados} legendaCentral="NCs" />
        </Card>
        <Card titulo="NCs por origem de detecção" subtitulo="Onde as não conformidades foram identificadas">
          <RankingBarChart dados={origemDados} />
        </Card>
        <Card titulo="Responsáveis com mais NCs em aberto" subtitulo="Top 5 responsáveis por NCs ainda não encerradas">
          <RankingBarChart dados={responsaveisDados} />
        </Card>
      </div>

      <NaoConformidadesCriticasPanel naoConformidades={naoConformidadesFiltradas} />

      {!carregando && naoConformidadesFiltradas.length === 0 && (
        <div style={{ marginTop: 16 }}>
          <EstadoVazio
            titulo="Nenhuma não conformidade encontrada"
            descricao="Ajuste os filtros de obra, origem ou status para ver resultados."
          />
        </div>
      )}
    </div>
  );
}
