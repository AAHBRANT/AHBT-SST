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
import { ShieldCheckmark24Regular, Warning24Regular, ClipboardTaskListLtr24Regular, Alert24Regular } from '@fluentui/react-icons';
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
import { RiscosCriticosPanel } from './RiscosCriticosPanel';
import { ListaRiscosPanel } from './ListaRiscosPanel';

const hojeISO = new Date().toISOString().slice(0, 10);

const NIVEIS_DESC = [5, 4, 3, 2, 1];

// Onda 2 Task 13 (camada ui/): dashboard de Riscos — mesmo formato de AprDashboardTab.tsx (Task 11):
// KpiCard + gráficos com cores de usePaletaGraficos, grade CSS Grid simples (spec §4.4, sem
// componente de grade próprio). Os 5 níveis de risco colapsam em 4 cores da paleta (ok/info/atencao/
// alerta), mesmo colapso já usado em ListaRiscosPanel.tsx/RiscosCriticosPanel.tsx desta task — Alto
// (4) e Crítico (5) dividem a cor "alerta", o rótulo textual continua distinguindo os dois.
export function RiscosDashboardTab() {
  const paleta = usePaletaGraficos();

  const [obras, setObras] = useState<Obra[]>([]);
  const [atividades, setAtividades] = useState<Atividade[]>([]);
  const [perigos, setPerigos] = useState<Perigo[]>([]);
  const [riscos, setRiscos] = useState<Risco[]>([]);
  const [carregando, setCarregando] = useState(true);
  const [erro, setErro] = useState<string | null>(null);

  const [obraId, setObraId] = useState('');
  const [nivelFiltro, setNivelFiltro] = useState('');
  const [statusFiltro, setStatusFiltro] = useState('');

  async function carregar() {
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
  }

  useEffect(() => {
    carregar();
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
      cor: paleta.alerta,
    },
    {
      rotulo: 'Em andamento',
      valor: riscosFiltrados.filter((r) => r.status === StatusControleRisco.EmAndamento).length,
      cor: paleta.atencao,
    },
    {
      rotulo: 'Concluído',
      valor: riscosFiltrados.filter((r) => r.status === StatusControleRisco.Concluido).length,
      cor: paleta.ok,
    },
  ];

  const coresPorNivel: Record<number, string> = {
    5: paleta.alerta,
    4: paleta.alerta,
    3: paleta.atencao,
    2: paleta.info,
    1: paleta.ok,
  };

  const nivelDados: ItemRanking[] = useMemo(
    () =>
      NIVEIS_DESC.map((nivel) => ({
        rotulo: nivelRiscoLabel[nivel],
        valor: riscosFiltrados.filter((r) => r.nivelRisco === nivel).length,
        cor: coresPorNivel[nivel],
      })),
    [riscosFiltrados, paleta],
  );

  const perigosDados: ItemRanking[] = useMemo(() => {
    const contagem = new Map<string, number>();
    for (const risco of riscosFiltrados) {
      const nome = perigos.find((p) => p.id === risco.perigoId)?.nome ?? risco.perigoId;
      contagem.set(nome, (contagem.get(nome) ?? 0) + 1);
    }
    return [...contagem.entries()]
      .map(([rotulo, valor]) => ({ rotulo, valor, cor: paleta.info }))
      .sort((a, b) => b.valor - a.valor)
      .slice(0, 5);
  }, [riscosFiltrados, perigos, paleta.info]);

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

      {erro && (
        <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>
          {erro}
        </FeedbackInline>
      )}

      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(185px, 1fr))', gap: 16, marginBottom: 16 }}>
        <KpiCard rotulo="Riscos avaliados" valor={riscosFiltrados.length} tom="info" indice={0} icone={<ShieldCheckmark24Regular />} />
        <KpiCard rotulo="Alto/Crítico" valor={criticosAltos} tom="alerta" indice={1} icone={<Warning24Regular />} />
        <KpiCard rotulo="Controle pendente" valor={controlePendente} tom="atencao" indice={2} icone={<ClipboardTaskListLtr24Regular />} />
        <KpiCard rotulo="Com prazo vencido" valor={prazoVencido} tom="alerta" indice={3} icone={<Alert24Regular />} />
      </div>

      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(340px, 1fr))', gap: 16, marginBottom: 16 }}>
        <Card titulo="Status de controle dos riscos" subtitulo="Situação do plano de ação de cada avaliação de risco">
          <StatusDonutChart dados={statusDados} legendaCentral="riscos" />
        </Card>
        <Card titulo="Riscos por nível" subtitulo="Distribuição das avaliações pela matriz de probabilidade × severidade">
          <RankingBarChart dados={nivelDados} />
        </Card>
        <Card titulo="Perigos mais frequentes" subtitulo="Top 5 perigos com mais avaliações de risco vinculadas">
          <RankingBarChart dados={perigosDados} />
        </Card>
      </div>

      <RiscosCriticosPanel riscos={riscosFiltrados} atividades={atividades} perigos={perigos} />

      <ListaRiscosPanel
        riscos={riscosFiltrados}
        atividades={atividades}
        perigos={perigos}
        aoExcluir={carregar}
      />

      {!carregando && riscosFiltrados.length === 0 && (
        <div style={{ marginTop: 16 }}>
          <EstadoVazio
            titulo="Nenhuma avaliação de risco encontrada"
            descricao="Ajuste os filtros de obra, nível ou status para ver resultados."
          />
        </div>
      )}
    </div>
  );
}
