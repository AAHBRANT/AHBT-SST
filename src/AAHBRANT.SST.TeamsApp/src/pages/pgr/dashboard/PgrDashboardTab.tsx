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
import {
  ShieldCheckmark24Regular,
  CheckmarkCircle24Regular,
  Warning24Regular,
  ClipboardTaskListLtr24Regular,
} from '@fluentui/react-icons';
import {
  api,
  StatusControleRisco,
  StatusPgr,
  statusPgrLabel,
  type Obra,
  type Pgr,
  type PlanoAcaoItem,
} from '../../../lib/api';
import { PgrPlanoAcaoVencidoPanel, type AcaoPlanoComContexto } from './PgrPlanoAcaoVencidoPanel';

const hojeISO = new Date().toISOString().slice(0, 10);

// Onda 2 Task 8 (camada ui/): dashboard de PGR — mesmo formato de AprDashboardTab.tsx (Task 11):
// KpiCard + gráficos com cores de usePaletaGraficos, grade CSS Grid simples (spec §4.4). A lógica de
// agregação no cliente não muda nesta frente.
export function PgrDashboardTab() {
  const paleta = usePaletaGraficos();

  const [obras, setObras] = useState<Obra[]>([]);
  const [pgrs, setPgrs] = useState<Pgr[]>([]);
  const [planoAcaoPorPgr, setPlanoAcaoPorPgr] = useState<Record<string, PlanoAcaoItem[]>>({});
  const [carregando, setCarregando] = useState(true);
  const [erro, setErro] = useState<string | null>(null);

  const [obraId, setObraId] = useState('');
  const [statusFiltro, setStatusFiltro] = useState('');

  useEffect(() => {
    (async () => {
      try {
        setErro(null);
        const [obrasResp, pgrsResp] = await Promise.all([api.obras.listar(), api.pgrs.listar()]);
        setObras(obrasResp);
        setPgrs(pgrsResp);
        const entradas = await Promise.all(
          pgrsResp.map((pgr) => api.planoAcao.listar(pgr.id).then((itens) => [pgr.id, itens] as const)),
        );
        setPlanoAcaoPorPgr(Object.fromEntries(entradas));
      } catch (e) {
        setErro(e instanceof Error ? e.message : 'Falha ao carregar dados do dashboard de PGR.');
      } finally {
        setCarregando(false);
      }
    })();
  }, []);

  const pgrsFiltrados = useMemo(
    () =>
      pgrs.filter(
        (p) => (obraId === '' || p.obraId === obraId) && (statusFiltro === '' || p.status === Number(statusFiltro)),
      ),
    [pgrs, obraId, statusFiltro],
  );

  function nomeObra(id: string) {
    return obras.find((o) => o.id === id)?.nome ?? id;
  }

  const acoesComContexto: AcaoPlanoComContexto[] = useMemo(
    () =>
      pgrsFiltrados.flatMap((pgr) =>
        (planoAcaoPorPgr[pgr.id] ?? []).map((acao) => ({
          ...acao,
          pgrNome: pgr.nome,
          obraNome: nomeObra(pgr.obraId),
        })),
      ),
    [pgrsFiltrados, planoAcaoPorPgr, obras],
  );

  const vigentes = pgrsFiltrados.filter((p) => p.status === StatusPgr.Vigente).length;
  const comRevisaoVencida = pgrsFiltrados.filter(
    (p) => !!p.dataProximaRevisao && p.dataProximaRevisao < hojeISO && p.status !== StatusPgr.Encerrado,
  ).length;
  const acoesEmAberto = acoesComContexto.filter((a) => a.status !== StatusControleRisco.Concluido).length;

  const statusDados: FatiaDonut[] = [
    { rotulo: 'Em elaboração', valor: pgrsFiltrados.filter((p) => p.status === StatusPgr.EmElaboracao).length, cor: paleta.atencao },
    { rotulo: 'Vigente', valor: vigentes, cor: paleta.ok },
    { rotulo: 'Em revisão', valor: pgrsFiltrados.filter((p) => p.status === StatusPgr.EmRevisao).length, cor: paleta.info },
    { rotulo: 'Encerrado', valor: pgrsFiltrados.filter((p) => p.status === StatusPgr.Encerrado).length, cor: paleta.neutro },
  ];

  const obraDados: ItemRanking[] = useMemo(() => {
    const contagem = new Map<string, number>();
    for (const pgr of pgrsFiltrados) {
      const nome = nomeObra(pgr.obraId);
      contagem.set(nome, (contagem.get(nome) ?? 0) + 1);
    }
    return [...contagem.entries()]
      .map(([rotulo, valor]) => ({ rotulo, valor, cor: paleta.marca }))
      .sort((a, b) => b.valor - a.valor)
      .slice(0, 5);
  }, [pgrsFiltrados, obras, paleta.marca]);

  const acoesPorStatusDados: ItemRanking[] = [
    { rotulo: 'Pendente', valor: acoesComContexto.filter((a) => a.status === StatusControleRisco.Pendente).length, cor: paleta.alerta },
    { rotulo: 'Em andamento', valor: acoesComContexto.filter((a) => a.status === StatusControleRisco.EmAndamento).length, cor: paleta.atencao },
    { rotulo: 'Concluído', valor: acoesComContexto.filter((a) => a.status === StatusControleRisco.Concluido).length, cor: paleta.ok },
  ];

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
        <Field label="Status do PGR">
          <Select value={statusFiltro} onChange={(_, data) => setStatusFiltro(data.value)}>
            <option value="">Todos os status</option>
            {Object.entries(statusPgrLabel).map(([valor, rotulo]) => (
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
        <KpiCard rotulo="Total de PGRs" valor={pgrsFiltrados.length} tom="info" indice={0} icone={<ShieldCheckmark24Regular />} />
        <KpiCard rotulo="Vigentes" valor={vigentes} tom="ok" indice={1} icone={<CheckmarkCircle24Regular />} />
        <KpiCard rotulo="Com revisão vencida" valor={comRevisaoVencida} tom="alerta" indice={2} icone={<Warning24Regular />} />
        <KpiCard rotulo="Ações do plano em aberto" valor={acoesEmAberto} tom="atencao" indice={3} icone={<ClipboardTaskListLtr24Regular />} />
      </div>

      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(340px, 1fr))', gap: 16, marginBottom: 16 }}>
        <Card titulo="Status dos PGRs" subtitulo="Situação atual de cada Programa de Gerenciamento de Riscos">
          <StatusDonutChart dados={statusDados} legendaCentral="PGRs" />
        </Card>
        <Card titulo="PGRs por obra" subtitulo="Top 5 obras com mais PGRs cadastrados">
          <RankingBarChart dados={obraDados} />
        </Card>
        <Card titulo="Ações do plano por status" subtitulo="Itens do plano de ação de todos os PGRs, por situação">
          <RankingBarChart dados={acoesPorStatusDados} />
        </Card>
      </div>

      <PgrPlanoAcaoVencidoPanel acoes={acoesComContexto} />

      {!carregando && pgrsFiltrados.length === 0 && (
        <div style={{ marginTop: 16 }}>
          <EstadoVazio
            titulo="Nenhum PGR encontrado"
            descricao="Ajuste os filtros de obra ou status para ver resultados."
          />
        </div>
      )}
    </div>
  );
}
