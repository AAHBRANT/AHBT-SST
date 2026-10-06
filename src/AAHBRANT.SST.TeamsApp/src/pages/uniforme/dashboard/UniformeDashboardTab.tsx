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
  ClipboardTaskListLtr24Regular,
  ArrowSync24Regular,
  Shifts24Regular,
} from '@fluentui/react-icons';
import {
  api,
  MotivoEntregaUniforme,
  motivoEntregaUniformeLabel,
  type CatalogoUniforme,
  type EntregaUniforme,
  type Obra,
  type Trabalhador,
} from '../../../lib/api';
import {
  FeedCardPainel,
  PainelModulo,
  dataCurtaPainel,
  ultimosMeses,
  usePainelFiltros,
} from '../../../components/dashboard/PainelModulo';

interface ItemEstoque {
  nome: string;
  saldo: number;
}

// Dashboard do módulo Uniforme no padrão da tela Início (pedido do usuário, 05/10). Entregas não têm
// obra própria: a obra vem do trabalhador. Estoque é por obra (peça + tamanho), então sem filtro de
// obra soma todas as obras.
export function UniformeDashboardTab() {
  const paleta = usePaletaGraficos();
  const { periodo, setPeriodo, obraId, setObraId, noPeriodo } = usePainelFiltros();

  const [obras, setObras] = useState<Obra[]>([]);
  const [entregas, setEntregas] = useState<EntregaUniforme[]>([]);
  const [catalogo, setCatalogo] = useState<CatalogoUniforme[]>([]);
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
        const [entregasResp, catalogoResp, trabalhadoresResp, obrasResp] = await Promise.all([
          api.entregasUniforme.listar(),
          api.catalogosUniforme.listar(),
          api.trabalhadores.listar(filtroObra),
          filtroObra ? Promise.resolve<Obra[]>([]) : api.obras.listar(),
        ]);
        const idsObrasEstoque = filtroObra ? [filtroObra] : obrasResp.map((o) => o.id);
        const estoquesPorObra = await Promise.all(idsObrasEstoque.map((id) => api.estoquesUniforme.listarPorObra(id)));
        if (cancelado) return;

        // Mesma peça+tamanho em obras diferentes soma no saldo total.
        const saldos = new Map<string, ItemEstoque>();
        for (const lista of estoquesPorObra) {
          for (const e of lista) {
            const chave = `${e.catalogoUniformeNome}|${e.tamanho}`;
            const atual = saldos.get(chave);
            if (atual) atual.saldo += e.saldo;
            else saldos.set(chave, { nome: `${e.catalogoUniformeNome} · ${e.tamanho}`, saldo: e.saldo });
          }
        }
        setEntregas(entregasResp);
        setCatalogo(catalogoResp);
        setTrabalhadores(trabalhadoresResp);
        setEstoque([...saldos.values()]);
      } catch (e) {
        if (!cancelado) setErro(e instanceof Error ? e.message : 'Falha ao carregar os dados do dashboard de Uniforme.');
      }
    })();
    return () => {
      cancelado = true;
    };
  }, [obraId]);

  const nomePeca = useMemo(() => new Map(catalogo.map((c) => [c.id, c.nome])), [catalogo]);
  const trabalhadorPorId = useMemo(() => new Map(trabalhadores.map((t) => [t.id, t])), [trabalhadores]);

  // Com obra escolhida, só entram entregas de trabalhadores daquela obra (a lista já veio filtrada).
  const entregasEscopo = useMemo(
    () => (obraId ? entregas.filter((e) => trabalhadorPorId.has(e.trabalhadorId)) : entregas),
    [entregas, obraId, trabalhadorPorId],
  );
  const entregasPeriodo = useMemo(() => entregasEscopo.filter((e) => noPeriodo(e.dataEntrega)), [entregasEscopo, noPeriodo]);

  const pecasEntregues = entregasPeriodo.reduce((soma, e) => soma + e.quantidade, 0);
  const contaMotivo = (motivo: number) => entregasPeriodo.filter((e) => e.motivoTipo === motivo).length;
  const trocasDesgasteExtravio = contaMotivo(MotivoEntregaUniforme.Desgaste) + contaMotivo(MotivoEntregaUniforme.Extravio);
  const itensSemEstoque = estoque.filter((i) => i.saldo <= 0).length;

  const serieMensal = useMemo(() => {
    const contagem = new Map<string, number>();
    for (const e of entregasEscopo) {
      const chave = e.dataEntrega.slice(0, 7);
      contagem.set(chave, (contagem.get(chave) ?? 0) + 1);
    }
    return ultimosMeses(6).map((m) => ({ rotulo: m.rotulo, valor: contagem.get(m.chave) ?? 0 }));
  }, [entregasEscopo]);

  const motivoDados: FatiaDonut[] = [
    { rotulo: motivoEntregaUniformeLabel[MotivoEntregaUniforme.Inicial], valor: contaMotivo(MotivoEntregaUniforme.Inicial), cor: paleta.ok },
    { rotulo: motivoEntregaUniformeLabel[MotivoEntregaUniforme.Desgaste], valor: contaMotivo(MotivoEntregaUniforme.Desgaste), cor: paleta.atencao },
    { rotulo: motivoEntregaUniformeLabel[MotivoEntregaUniforme.Extravio], valor: contaMotivo(MotivoEntregaUniforme.Extravio), cor: paleta.alerta },
    { rotulo: motivoEntregaUniformeLabel[MotivoEntregaUniforme.TrocaDeFuncao], valor: contaMotivo(MotivoEntregaUniforme.TrocaDeFuncao), cor: paleta.info },
  ];

  const menorEstoque: ItemRanking[] = useMemo(
    () =>
      [...estoque]
        .sort((a, b) => a.saldo - b.saldo)
        .slice(0, 5)
        .map((i) => ({ rotulo: i.nome, valor: Math.max(i.saldo, 0), cor: i.saldo <= 0 ? paleta.alerta : paleta.marca })),
    [estoque, paleta.alerta, paleta.marca],
  );

  const pecasMaisEntregues: ItemRanking[] = useMemo(() => {
    const contagem = new Map<string, number>();
    for (const e of entregasPeriodo) {
      const nome = nomePeca.get(e.catalogoUniformeId) ?? 'Peça removida do catálogo';
      contagem.set(nome, (contagem.get(nome) ?? 0) + e.quantidade);
    }
    return [...contagem.entries()]
      .map(([rotulo, valor]) => ({ rotulo, valor, cor: paleta.info }))
      .sort((a, b) => b.valor - a.valor)
      .slice(0, 5);
  }, [entregasPeriodo, nomePeca, paleta.info]);

  const entregasPorObra: ItemRanking[] = useMemo(() => {
    const nomeObra = new Map(obras.map((o) => [o.id, o.nome]));
    const contagem = new Map<string, number>();
    for (const e of entregasPeriodo) {
      const obra = nomeObra.get(trabalhadorPorId.get(e.trabalhadorId)?.obraId ?? '') ?? 'Sem obra';
      contagem.set(obra, (contagem.get(obra) ?? 0) + 1);
    }
    return [...contagem.entries()]
      .map(([rotulo, valor]) => ({ rotulo, valor, cor: paleta.marca }))
      .sort((a, b) => b.valor - a.valor)
      .slice(0, 5);
  }, [entregasPeriodo, obras, trabalhadorPorId, paleta.marca]);

  const ultimasEntregas = useMemo(
    () =>
      [...entregasEscopo]
        .sort((a, b) => b.dataEntrega.localeCompare(a.dataEntrega))
        .slice(0, 5)
        .map((e) => ({
          id: e.id,
          titulo: `${nomePeca.get(e.catalogoUniformeId) ?? 'Peça removida do catálogo'} · ${e.tamanho}`,
          meta: trabalhadorPorId.get(e.trabalhadorId)?.nome ?? 'Trabalhador não encontrado',
          hora: dataCurtaPainel(e.dataEntrega),
          icone: <Shifts24Regular />,
          variante: e.motivoTipo === MotivoEntregaUniforme.Inicial ? ('bom' as const) : ('atencao' as const),
        })),
    [entregasEscopo, nomePeca, trabalhadorPorId],
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
        { rotulo: 'Peças entregues', valor: pecasEntregues, tom: 'info', icone: <Shifts24Regular /> },
        { rotulo: 'Trocas por desgaste ou extravio', valor: trocasDesgasteExtravio, tom: 'atencao', icone: <ArrowSync24Regular /> },
        { rotulo: 'Itens sem estoque', valor: itensSemEstoque, tom: 'alerta', icone: <Box24Regular /> },
      ]}
      largo={
        <Card titulo="Entregas por mês" subtitulo="Entregas de uniforme registradas nos últimos 6 meses">
          <TrendBarChart dados={serieMensal} cor={paleta.marca} rotuloSerie="Entregas" />
        </Card>
      }
      laterais={[
        <Card key="motivo" titulo="Motivo das entregas" subtitulo="Entregas do período por motivo">
          <StatusDonutChart dados={motivoDados} legendaCentral="entregas" />
        </Card>,
      ]}
      inferiores={[
        <Card key="estoque" titulo="Itens com menor estoque" subtitulo="As 5 combinações de peça e tamanho com menor saldo">
          <RankingBarChart dados={menorEstoque} />
        </Card>,
        <Card key="pecas" titulo="Peças mais entregues" subtitulo="Quantidade entregue no período, top 5 peças">
          <RankingBarChart dados={pecasMaisEntregues} />
        </Card>,
        <Card key="obras" titulo="Entregas por obra" subtitulo="Número de entregas no período, top 5 obras">
          <RankingBarChart dados={entregasPorObra} />
        </Card>,
        <FeedCardPainel
          key="ult"
          titulo="Últimas entregas"
          subtitulo="Entregas de uniforme mais recentes"
          itens={ultimasEntregas}
          vazio="Nenhuma entrega registrada."
        />,
      ]}
    />
  );
}
