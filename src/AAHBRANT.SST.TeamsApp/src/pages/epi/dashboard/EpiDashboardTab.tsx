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
  Box24Regular,
  CheckmarkCircle24Regular,
  ClipboardTaskListLtr24Regular,
  ShieldCheckmark24Regular,
  Warning24Regular,
} from '@fluentui/react-icons';
import { api, type CatalogoEpi, type EntregaEpi, type Obra, type Trabalhador } from '../../../lib/api';
import {
  FeedCardPainel,
  PainelModulo,
  dataCurtaPainel,
  hojeIsoPainel,
  ultimosMeses,
  usePainelFiltros,
  type ItemFeedPainel,
} from '../../../components/dashboard/PainelModulo';

interface ItemEstoque {
  nome: string;
  saldo: number;
}

function maisDias(iso: string, dias: number): string {
  const d = new Date(iso);
  d.setDate(d.getDate() + dias);
  return d.toISOString().slice(0, 10);
}

// Dashboard do módulo EPI no padrão da tela Início (pedido do usuário, 05/10). Os números saem das
// listas que o módulo já usa (entregas, catálogo, estoque) e são agregados no cliente, como no Início.
export function EpiDashboardTab() {
  const paleta = usePaletaGraficos();
  const { periodo, setPeriodo, obraId, setObraId, noPeriodo } = usePainelFiltros();

  const [obras, setObras] = useState<Obra[]>([]);
  const [entregas, setEntregas] = useState<EntregaEpi[]>([]);
  const [catalogo, setCatalogo] = useState<CatalogoEpi[]>([]);
  const [trabalhadores, setTrabalhadores] = useState<Trabalhador[]>([]);
  const [estoque, setEstoque] = useState<ItemEstoque[]>([]);
  const [erro, setErro] = useState<string | null>(null);

  useEffect(() => {
    api.obras.listar().then(setObras).catch(() => setObras([]));
  }, []);

  useEffect(() => {
    let cancelado = false;
    (async () => {
      try {
        setErro(null);
        const filtroObra = obraId || undefined;
        const [entregasResp, catalogoResp, trabalhadoresResp, estoqueObra] = await Promise.all([
          api.entregasEpi.listar(undefined, filtroObra),
          api.catalogosEpi.listar(),
          api.trabalhadores.listar(filtroObra),
          filtroObra ? api.estoquesEpi.listarPorObra(filtroObra) : Promise.resolve(null),
        ]);
        if (cancelado) return;
        setEntregas(entregasResp);
        setCatalogo(catalogoResp);
        setTrabalhadores(trabalhadoresResp);
        setEstoque(
          estoqueObra
            ? estoqueObra.map((e) => ({ nome: e.catalogoEpiNome, saldo: e.saldo }))
            : catalogoResp.map((c) => ({ nome: c.nome, saldo: c.saldoTotal })),
        );
      } catch (e) {
        if (!cancelado) setErro(e instanceof Error ? e.message : 'Falha ao carregar os dados do dashboard de EPI.');
      }
    })();
    return () => {
      cancelado = true;
    };
  }, [obraId]);

  const hojeISO = hojeIsoPainel();
  const limite30ISO = maisDias(hojeISO, 30);

  const nomeEpi = useMemo(() => new Map(catalogo.map((c) => [c.id, c.nome])), [catalogo]);
  const trabalhadorPorId = useMemo(() => new Map(trabalhadores.map((t) => [t.id, t])), [trabalhadores]);

  const entregasPeriodo = useMemo(() => entregas.filter((e) => noPeriodo(e.dataEntrega)), [entregas, noPeriodo]);

  // Conformidade: mesma fórmula provisória do Início (% de entregas ativas dentro da validade).
  const ativas = useMemo(() => entregas.filter((e) => !e.dataDevolucao), [entregas]);
  const vencidas = ativas.filter((e) => e.dataValidade && e.dataValidade < hojeISO);
  const vencendo30 = ativas.filter((e) => e.dataValidade && e.dataValidade >= hojeISO && e.dataValidade <= limite30ISO);
  const emDia = ativas.length - vencidas.length - vencendo30.length;
  const conformidadePct = ativas.length > 0 ? Math.round(((ativas.length - vencidas.length) / ativas.length) * 100) : null;
  const itensSemEstoque = estoque.filter((i) => i.saldo <= 0).length;

  const serieMensal = useMemo(() => {
    const meses = ultimosMeses(6);
    const contagem = new Map<string, number>();
    for (const e of entregas) {
      const chave = e.dataEntrega.slice(0, 7);
      contagem.set(chave, (contagem.get(chave) ?? 0) + 1);
    }
    return meses.map((m) => ({ rotulo: m.rotulo, valor: contagem.get(m.chave) ?? 0 }));
  }, [entregas]);

  const situacaoDados: FatiaDonut[] = [
    { rotulo: 'Em dia', valor: emDia, cor: paleta.ok },
    { rotulo: 'Vencem em 30 dias', valor: vencendo30.length, cor: paleta.atencao },
    { rotulo: 'Vencidos', valor: vencidas.length, cor: paleta.alerta },
    { rotulo: 'Devolvidos', valor: entregas.length - ativas.length, cor: paleta.neutro },
  ];

  const menorEstoque: ItemRanking[] = useMemo(
    () =>
      [...estoque]
        .sort((a, b) => a.saldo - b.saldo)
        .slice(0, 5)
        .map((i) => ({ rotulo: i.nome, valor: Math.max(i.saldo, 0), cor: i.saldo <= 0 ? paleta.alerta : paleta.marca })),
    [estoque, paleta.alerta, paleta.marca],
  );

  const maisEntregues: ItemRanking[] = useMemo(() => {
    const contagem = new Map<string, number>();
    for (const e of entregasPeriodo) {
      const nome = nomeEpi.get(e.catalogoEpiId) ?? 'Item removido do catálogo';
      contagem.set(nome, (contagem.get(nome) ?? 0) + e.quantidade);
    }
    return [...contagem.entries()]
      .map(([rotulo, valor]) => ({ rotulo, valor, cor: paleta.info }))
      .sort((a, b) => b.valor - a.valor)
      .slice(0, 5);
  }, [entregasPeriodo, nomeEpi, paleta.info]);

  function itemFeed(e: EntregaEpi, variante: ItemFeedPainel['variante'], hora: string): ItemFeedPainel {
    return {
      id: e.id,
      titulo: nomeEpi.get(e.catalogoEpiId) ?? 'Item removido do catálogo',
      meta: trabalhadorPorId.get(e.trabalhadorId)?.nome ?? 'Trabalhador não encontrado',
      hora,
      icone: <ShieldCheckmark24Regular />,
      variante,
    };
  }

  const proximosVencimentos = useMemo(
    () =>
      ativas
        .filter((e) => e.dataValidade && e.dataValidade <= limite30ISO)
        .sort((a, b) => (a.dataValidade ?? '').localeCompare(b.dataValidade ?? ''))
        .slice(0, 5)
        .map((e) => itemFeed(e, (e.dataValidade ?? '') < hojeISO ? 'alerta' : 'atencao', dataCurtaPainel(e.dataValidade ?? ''))),
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [ativas, limite30ISO, hojeISO, nomeEpi, trabalhadorPorId],
  );

  const ultimasEntregas = useMemo(
    () =>
      [...entregas]
        .sort((a, b) => b.dataEntrega.localeCompare(a.dataEntrega))
        .slice(0, 5)
        .map((e) => itemFeed(e, 'bom', dataCurtaPainel(e.dataEntrega))),
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [entregas, nomeEpi, trabalhadorPorId],
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
        { rotulo: 'Entregas no período', valor: entregasPeriodo.length, tom: 'info', icone: <ClipboardTaskListLtr24Regular /> },
        {
          rotulo: 'Conformidade de EPI',
          valor: conformidadePct === null ? '—' : `${conformidadePct}%`,
          tom: conformidadePct === null ? 'neutro' : conformidadePct >= 90 ? 'ok' : 'atencao',
          icone: <CheckmarkCircle24Regular />,
        },
        { rotulo: 'EPIs vencendo em 30 dias', valor: vencendo30.length, tom: 'atencao', icone: <Warning24Regular /> },
        { rotulo: 'Itens sem estoque', valor: itensSemEstoque, tom: 'alerta', icone: <Box24Regular /> },
      ]}
      largo={
        <Card titulo="Entregas por mês" subtitulo="Entregas de EPI registradas nos últimos 6 meses">
          <TrendBarChart dados={serieMensal} cor={paleta.marca} rotuloSerie="Entregas" />
        </Card>
      }
      laterais={[
        <Card key="situacao" titulo="Situação dos EPIs entregues" subtitulo="Validade das entregas ativas e devoluções">
          <StatusDonutChart dados={situacaoDados} legendaCentral="entregas" />
        </Card>,
      ]}
      inferiores={[
        <Card key="estoque" titulo="Itens com menor estoque" subtitulo="Os 5 itens com menor saldo">
          <RankingBarChart dados={menorEstoque} />
        </Card>,
        <Card key="itens" titulo="EPIs mais entregues" subtitulo="Quantidade entregue no período, top 5 itens">
          <RankingBarChart dados={maisEntregues} />
        </Card>,
        <FeedCardPainel
          key="venc"
          titulo="Próximos vencimentos"
          subtitulo="Entregas vencidas ou que vencem em até 30 dias"
          itens={proximosVencimentos}
          vazio="Nenhum EPI vencido ou vencendo."
        />,
        <FeedCardPainel
          key="ult"
          titulo="Últimas entregas"
          subtitulo="Entregas de EPI mais recentes"
          itens={ultimasEntregas}
          vazio="Nenhuma entrega registrada."
        />,
      ]}
    />
  );
}
