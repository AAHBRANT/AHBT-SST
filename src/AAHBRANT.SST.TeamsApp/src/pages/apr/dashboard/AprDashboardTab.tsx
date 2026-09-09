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
import { ClipboardTaskListLtr24Regular, CheckmarkCircle24Regular, Clock24Regular, Warning24Regular } from '@fluentui/react-icons';
import { api, StatusApr, statusAprLabel, type Apr, type Atividade, type Obra } from '../../../lib/api';
import { AprVencidaPanel, type AprComContexto } from './AprVencidaPanel';

const hojeISO = new Date().toISOString().slice(0, 10);

// Onda 2 Task 11 (camada ui/): dashboard de APR — KpiCard + gráficos com cores lidas de
// usePaletaGraficos (paleta.ts, spec §1.6), grade CSS Grid simples (spec §4.4, sem componente de
// grade próprio). A lógica de agregação no cliente não muda nesta frente.
export function AprDashboardTab() {
  const paleta = usePaletaGraficos();

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
    { rotulo: 'Em elaboração', valor: aprsFiltradas.filter((a) => a.status === StatusApr.EmElaboracao).length, cor: paleta.info },
    { rotulo: 'Aguardando aprovação', valor: aguardandoAprovacao, cor: paleta.atencao },
    { rotulo: 'Aprovada', valor: aprovadas, cor: paleta.ok },
    { rotulo: 'Reprovada', valor: aprsFiltradas.filter((a) => a.status === StatusApr.Reprovada).length, cor: paleta.alerta },
    { rotulo: 'Encerrada', valor: aprsFiltradas.filter((a) => a.status === StatusApr.Encerrada).length, cor: paleta.neutro },
  ];

  const obraDados: ItemRanking[] = useMemo(() => {
    const contagem = new Map<string, number>();
    for (const apr of aprsComContexto) {
      contagem.set(apr.obraNome, (contagem.get(apr.obraNome) ?? 0) + 1);
    }
    return [...contagem.entries()]
      .map(([rotulo, valor]) => ({ rotulo, valor, cor: paleta.marca }))
      .sort((a, b) => b.valor - a.valor)
      .slice(0, 5);
  }, [aprsComContexto, paleta.marca]);

  const atividadeDados: ItemRanking[] = useMemo(() => {
    const contagem = new Map<string, number>();
    for (const apr of aprsFiltradas) {
      contagem.set(apr.atividadeNome, (contagem.get(apr.atividadeNome) ?? 0) + 1);
    }
    return [...contagem.entries()]
      .map(([rotulo, valor]) => ({ rotulo, valor, cor: paleta.info }))
      .sort((a, b) => b.valor - a.valor)
      .slice(0, 5);
  }, [aprsFiltradas, paleta.info]);

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

      {erro && (
        <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>
          {erro}
        </FeedbackInline>
      )}

      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(185px, 1fr))', gap: 16, marginBottom: 16 }}>
        <KpiCard rotulo="Total de APRs" valor={aprsFiltradas.length} tom="info" indice={0} icone={<ClipboardTaskListLtr24Regular />} />
        <KpiCard rotulo="Aprovadas" valor={aprovadas} tom="ok" indice={1} icone={<CheckmarkCircle24Regular />} />
        <KpiCard rotulo="Aguardando aprovação" valor={aguardandoAprovacao} tom="atencao" indice={2} icone={<Clock24Regular />} />
        <KpiCard rotulo="Com validade vencida" valor={vencidas} tom="alerta" indice={3} icone={<Warning24Regular />} />
      </div>

      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(340px, 1fr))', gap: 16, marginBottom: 16 }}>
        <Card titulo="Status das APRs" subtitulo="Situação atual de cada Análise Preliminar de Risco">
          <StatusDonutChart dados={statusDados} legendaCentral="APRs" />
        </Card>
        <Card titulo="APRs por obra" subtitulo="Top 5 obras com mais APRs cadastradas">
          <RankingBarChart dados={obraDados} />
        </Card>
        <Card titulo="APRs por atividade" subtitulo="Top 5 atividades com mais APRs cadastradas">
          <RankingBarChart dados={atividadeDados} />
        </Card>
      </div>

      <AprVencidaPanel aprs={aprsComContexto} />

      {!carregando && aprsFiltradas.length === 0 && (
        <div style={{ marginTop: 16 }}>
          <EstadoVazio
            titulo="Nenhuma APR encontrada"
            descricao="Ajuste os filtros de obra ou status para ver resultados."
          />
        </div>
      )}
    </div>
  );
}
