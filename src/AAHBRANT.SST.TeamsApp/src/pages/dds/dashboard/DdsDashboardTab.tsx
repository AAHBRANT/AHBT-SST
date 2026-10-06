import { useEffect, useMemo, useState } from 'react';
import {
  Card,
  FeedbackInline,
  RankingBarChart,
  StatusDonutChart,
  TrendBarChart,
  usePaletaGraficos,
  type FatiaDonut,
  type ItemRanking,
} from '@ui';
import {
  CheckmarkCircle24Regular,
  ClipboardTaskListLtr24Regular,
  Clock24Regular,
  PeopleTeam24Regular,
} from '@fluentui/react-icons';
import { api, StatusDds, type CatalogoTemaDds, type Dds, type Obra } from '../../../lib/api';
import { FeedCardPainel, PainelModulo, dataCurtaPainel, ultimosMeses, usePainelFiltros } from '../../../components/dashboard/PainelModulo';

// Dashboard do módulo DDS no padrão da tela Início (pedido do usuário, 05/10). Dias sem expediente
// (feriado, folga) aparecem na rosca de situação, mas não contam como DDS realizado nem entram nas
// médias de participantes.
export function DdsDashboardTab() {
  const paleta = usePaletaGraficos();
  const { periodo, setPeriodo, obraId, setObraId, noPeriodo } = usePainelFiltros();

  const [obras, setObras] = useState<Obra[]>([]);
  const [ddsLista, setDdsLista] = useState<Dds[]>([]);
  const [temas, setTemas] = useState<CatalogoTemaDds[]>([]);
  const [erro, setErro] = useState<string | null>(null);

  useEffect(() => {
    api.obras.listar().then(setObras).catch(() => setObras([]));
  }, []);

  useEffect(() => {
    let cancelado = false;
    (async () => {
      try {
        setErro(null);
        const [ddsResp, temasResp] = await Promise.all([api.dds.listar(obraId || undefined), api.catalogoTemasDds.listar()]);
        if (cancelado) return;
        setDdsLista(ddsResp);
        setTemas(temasResp);
      } catch (e) {
        if (!cancelado) setErro(e instanceof Error ? e.message : 'Falha ao carregar os dados do dashboard de DDS.');
      }
    })();
    return () => {
      cancelado = true;
    };
  }, [obraId]);

  const nomeTema = useMemo(() => new Map(temas.map((t) => [t.id, t.nome])), [temas]);
  function tituloDds(d: Dds): string {
    if (d.semExpediente) return 'Dia sem expediente';
    const atividades = d.atividadesNomes?.length ? `Atividades: ${d.atividadesNomes.join(', ')}` : null;
    return (d.catalogoTemaDdsId && nomeTema.get(d.catalogoTemaDdsId)) || d.temaLivreNome || atividades || 'DDS sem tema informado';
  }

  const ddsPeriodo = useMemo(() => ddsLista.filter((d) => noPeriodo(d.data)), [ddsLista, noPeriodo]);
  const realizados = useMemo(() => ddsPeriodo.filter((d) => !d.semExpediente), [ddsPeriodo]);
  const emAndamento = realizados.filter((d) => d.status === StatusDds.EmAndamento);
  const participacoes = realizados.reduce((soma, d) => soma + d.totalParticipantes, 0);
  const mediaParticipantes = realizados.length > 0 ? Math.round(participacoes / realizados.length) : 0;

  const serieMensal = useMemo(() => {
    const contagem = new Map<string, number>();
    for (const d of ddsLista) {
      if (d.semExpediente) continue;
      const chave = d.data.slice(0, 7);
      contagem.set(chave, (contagem.get(chave) ?? 0) + 1);
    }
    return ultimosMeses(6).map((m) => ({ rotulo: m.rotulo, valor: contagem.get(m.chave) ?? 0 }));
  }, [ddsLista]);

  const situacaoDados: FatiaDonut[] = [
    { rotulo: 'Concluído', valor: realizados.filter((d) => d.status === StatusDds.Concluido).length, cor: paleta.ok },
    { rotulo: 'Em andamento', valor: emAndamento.length, cor: paleta.atencao },
    { rotulo: 'Dia sem expediente', valor: ddsPeriodo.length - realizados.length, cor: paleta.neutro },
  ];

  const temasMaisUsados: ItemRanking[] = useMemo(() => {
    const contagem = new Map<string, number>();
    for (const d of realizados) {
      const nome = tituloDds(d);
      contagem.set(nome, (contagem.get(nome) ?? 0) + 1);
    }
    return [...contagem.entries()]
      .map(([rotulo, valor]) => ({ rotulo, valor, cor: paleta.info }))
      .sort((a, b) => b.valor - a.valor)
      .slice(0, 5);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [realizados, nomeTema, paleta.info]);

  const participantesPorObra: ItemRanking[] = useMemo(() => {
    const contagem = new Map<string, number>();
    for (const d of realizados) {
      contagem.set(d.obraNome, (contagem.get(d.obraNome) ?? 0) + d.totalParticipantes);
    }
    return [...contagem.entries()]
      .map(([rotulo, valor]) => ({ rotulo, valor, cor: paleta.marca }))
      .sort((a, b) => b.valor - a.valor)
      .slice(0, 5);
  }, [realizados, paleta.marca]);

  function itemFeed(d: Dds, variante: 'bom' | 'atencao') {
    return {
      id: d.id,
      titulo: tituloDds(d),
      meta: `${d.obraNome} · ${d.totalParticipantes} participante${d.totalParticipantes === 1 ? '' : 's'}`,
      hora: dataCurtaPainel(d.data),
      icone: <PeopleTeam24Regular />,
      variante,
    };
  }

  const ddsEmAndamentoFeed = useMemo(
    () =>
      [...emAndamento]
        .sort((a, b) => b.data.localeCompare(a.data))
        .slice(0, 5)
        .map((d) => itemFeed(d, 'atencao')),
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [emAndamento, nomeTema],
  );

  const ultimosDds = useMemo(
    () =>
      [...ddsLista]
        .filter((d) => !d.semExpediente)
        .sort((a, b) => b.data.localeCompare(a.data))
        .slice(0, 5)
        .map((d) => itemFeed(d, d.status === StatusDds.Concluido ? 'bom' : 'atencao')),
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [ddsLista, nomeTema],
  );

  return (
    <PainelModulo
      obras={obras}
      periodo={periodo}
      aoMudarPeriodo={setPeriodo}
      obraId={obraId}
      aoMudarObra={setObraId}
      erro={
        erro && (
          <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>
            {erro}
          </FeedbackInline>
        )
      }
      kpis={[
        { rotulo: 'DDS realizados', valor: realizados.length, tom: 'info', icone: <ClipboardTaskListLtr24Regular /> },
        { rotulo: 'Participações registradas', valor: participacoes, tom: 'info', icone: <PeopleTeam24Regular /> },
        { rotulo: 'Média de participantes por DDS', valor: mediaParticipantes, tom: 'ok', icone: <CheckmarkCircle24Regular /> },
        { rotulo: 'DDS em andamento', valor: emAndamento.length, tom: 'atencao', icone: <Clock24Regular /> },
      ]}
      largo={
        <Card titulo="DDS por mês" subtitulo="DDS realizados nos últimos 6 meses, sem contar dias sem expediente">
          <TrendBarChart dados={serieMensal} cor={paleta.marca} rotuloSerie="DDS" />
        </Card>
      }
      laterais={[
        <Card key="situacao" titulo="Situação dos DDS" subtitulo="DDS do período por situação">
          <StatusDonutChart dados={situacaoDados} legendaCentral="DDS" />
        </Card>,
      ]}
      inferiores={[
        <Card key="temas" titulo="Temas mais usados" subtitulo="DDS do período por tema, top 5">
          <RankingBarChart dados={temasMaisUsados} />
        </Card>,
        <Card key="obras" titulo="Participantes por obra" subtitulo="Participações registradas no período, top 5 obras">
          <RankingBarChart dados={participantesPorObra} />
        </Card>,
        <FeedCardPainel
          key="andamento"
          titulo="DDS em andamento"
          subtitulo="DDS do período ainda não concluídos"
          itens={ddsEmAndamentoFeed}
          vazio="Nenhum DDS em andamento."
        />,
        <FeedCardPainel
          key="ultimos"
          titulo="Últimos DDS"
          subtitulo="DDS mais recentes registrados"
          itens={ultimosDds}
          vazio="Nenhum DDS registrado."
        />,
      ]}
    />
  );
}
