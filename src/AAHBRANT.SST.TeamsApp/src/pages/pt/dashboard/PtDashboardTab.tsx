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
import { DocumentLock24Regular, CheckmarkCircle24Regular, Edit24Regular, Warning24Regular } from '@fluentui/react-icons';
import { api, StatusPt, statusPtLabel, type Atividade, type Obra, type PermissaoTrabalho } from '../../../lib/api';
import { PtVencidaPanel, type PtComContexto } from './PtVencidaPanel';

const hojeISO = new Date().toISOString().slice(0, 10);

// Onda 2 Task 7 (camada ui/): dashboard de PT — mesmo padrão de AprDashboardTab.tsx (Task 11):
// KpiCard + gráficos com cores de usePaletaGraficos, grade CSS Grid simples (spec §4.4). Tons do
// donut seguem o mesmo mapeamento de tomPorStatusPt usado em PermissaoTrabalhoDetalhePage.tsx
// (EmElaboracao=neutro, Autorizada=ok, Suspensa=atencao, Encerrada=info) para as duas telas ficarem
// coerentes (ruling do Guia, item 5).
export function PtDashboardTab() {
  const paleta = usePaletaGraficos();

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
    { rotulo: 'Em elaboração', valor: emElaboracao, cor: paleta.neutro },
    { rotulo: 'Autorizada', valor: autorizadas, cor: paleta.ok },
    { rotulo: 'Suspensa', valor: suspensas, cor: paleta.atencao },
    {
      rotulo: 'Encerrada',
      valor: permissoesFiltradas.filter((p) => p.status === StatusPt.Encerrada).length,
      cor: paleta.info,
    },
  ];

  const obraDados: ItemRanking[] = useMemo(() => {
    const contagem = new Map<string, number>();
    for (const pt of permissoesComContexto) {
      contagem.set(pt.obraNome, (contagem.get(pt.obraNome) ?? 0) + 1);
    }
    return [...contagem.entries()]
      .map(([rotulo, valor]) => ({ rotulo, valor, cor: paleta.marca }))
      .sort((a, b) => b.valor - a.valor)
      .slice(0, 5);
  }, [permissoesComContexto, paleta.marca]);

  const atividadeDados: ItemRanking[] = useMemo(() => {
    const contagem = new Map<string, number>();
    for (const pt of permissoesFiltradas) {
      contagem.set(pt.atividadeNome, (contagem.get(pt.atividadeNome) ?? 0) + 1);
    }
    return [...contagem.entries()]
      .map(([rotulo, valor]) => ({ rotulo, valor, cor: paleta.info }))
      .sort((a, b) => b.valor - a.valor)
      .slice(0, 5);
  }, [permissoesFiltradas, paleta.info]);

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

      {erro && (
        <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>
          {erro}
        </FeedbackInline>
      )}

      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(185px, 1fr))', gap: 16, marginBottom: 16 }}>
        <KpiCard rotulo="Total de PTs" valor={permissoesFiltradas.length} tom="info" indice={0} icone={<DocumentLock24Regular />} />
        <KpiCard rotulo="Autorizadas" valor={autorizadas} tom="ok" indice={1} icone={<CheckmarkCircle24Regular />} />
        <KpiCard rotulo="Em elaboração" valor={emElaboracao} tom="neutro" indice={2} icone={<Edit24Regular />} />
        <KpiCard rotulo="Com validade vencida" valor={vencidas} tom="alerta" indice={3} icone={<Warning24Regular />} />
      </div>

      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(340px, 1fr))', gap: 16, marginBottom: 16 }}>
        <Card titulo="Status das PTs" subtitulo="Situação atual de cada Permissão de Trabalho">
          <StatusDonutChart dados={statusDados} legendaCentral="PTs" />
        </Card>
        <Card titulo="PTs por obra" subtitulo="Top 5 obras com mais PTs cadastradas">
          <RankingBarChart dados={obraDados} />
        </Card>
        <Card titulo="PTs por atividade" subtitulo="Top 5 atividades com mais PTs cadastradas">
          <RankingBarChart dados={atividadeDados} />
        </Card>
      </div>

      <PtVencidaPanel permissoes={permissoesComContexto} />

      {!carregando && permissoesFiltradas.length === 0 && (
        <div style={{ marginTop: 16 }}>
          <EstadoVazio
            titulo="Nenhuma PT encontrada"
            descricao="Ajuste os filtros de obra ou status para ver resultados."
          />
        </div>
      )}
    </div>
  );
}
