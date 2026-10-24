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
  Building24Regular,
  CheckmarkCircle24Regular,
  DocumentText24Regular,
  Person24Regular,
  Warning24Regular,
} from '@fluentui/react-icons';
import {
  api,
  type Contrato,
  type Empresa,
  type Obra,
  type PainelPendenciasTerceirizado,
  type PessoaTerceirizada,
} from '../../../lib/api';
import { FeedCardPainel, PainelModulo, dataCurtaPainel, hojeIsoPainel } from '../../../components/dashboard/PainelModulo';

function maisDias(iso: string, dias: number): string {
  const d = new Date(iso);
  d.setDate(d.getDate() + dias);
  return d.toISOString().slice(0, 10);
}

const painelVazio: PainelPendenciasTerceirizado = {
  pessoasBloqueadas: [],
  contratosEncerradosComPessoasAtivas: [],
  alertasEstoqueInsuficiente: [],
};

// Dashboard do módulo Terceirizados no padrão da tela Início (pedido do usuário, 05/10). Sem filtro
// de período: o módulo não registra datas de movimento, só a vigência dos contratos. O filtro de
// obra atua pelos contratos (empresa, pessoa e pendência entram se ligadas a um contrato da obra).
export function TerceirizadosDashboardTab() {
  const paleta = usePaletaGraficos();
  const [obraId, setObraId] = useState('');

  const [obras, setObras] = useState<Obra[]>([]);
  const [empresas, setEmpresas] = useState<Empresa[]>([]);
  const [contratos, setContratos] = useState<Contrato[]>([]);
  const [pessoas, setPessoas] = useState<PessoaTerceirizada[]>([]);
  const [painel, setPainel] = useState<PainelPendenciasTerceirizado>(painelVazio);
  const [erro, setErro] = useState<string | null>(null);
  const [pendenciasIndisponiveis, setPendenciasIndisponiveis] = useState(false);

  useEffect(() => {
    let cancelado = false;
    (async () => {
      try {
        setErro(null);
        const [obrasResp, empresasResp, pessoasResp, painelResp] = await Promise.all([
          api.obras.listar(),
          api.terceirizados.empresas.listar(),
          api.terceirizados.pessoas.listar(),
          // O painel de pendências falha de forma independente (consulta própria no servidor): sem ele
          // o resto do dashboard continua valendo.
          api.terceirizados.pendencias.listar().catch(() => null),
        ]);
        const contratosPorEmpresa = await Promise.all(empresasResp.map((e) => api.terceirizados.contratos.listarPorEmpresa(e.id)));
        if (cancelado) return;
        setObras(obrasResp);
        setEmpresas(empresasResp);
        setPessoas(pessoasResp);
        setPainel(painelResp ?? painelVazio);
        setPendenciasIndisponiveis(painelResp === null);
        setContratos(contratosPorEmpresa.flat());
      } catch (e) {
        if (!cancelado) setErro(e instanceof Error ? e.message : 'Falha ao carregar os dados do dashboard de Terceirizados.');
      }
    })();
    return () => {
      cancelado = true;
    };
  }, []);

  const hojeISO = hojeIsoPainel();

  const contratosEscopo = useMemo(() => (obraId ? contratos.filter((c) => c.obraId === obraId) : contratos), [contratos, obraId]);
  const idsContratosEscopo = useMemo(() => new Set(contratosEscopo.map((c) => c.id)), [contratosEscopo]);
  const idsEmpresasEscopo = useMemo(() => new Set(contratosEscopo.map((c) => c.empresaId)), [contratosEscopo]);

  const vigentes = useMemo(
    () => contratosEscopo.filter((c) => c.status === 'Validado' && c.dataFimVigencia.slice(0, 10) >= hojeISO),
    [contratosEscopo, hojeISO],
  );
  const empresasAtivas = empresas.filter((e) => e.status === 'Ativa' && (!obraId || idsEmpresasEscopo.has(e.id)));
  const pessoasEscopo = useMemo(
    () => (obraId ? pessoas.filter((p) => idsContratosEscopo.has(p.contratoId)) : pessoas),
    [pessoas, obraId, idsContratosEscopo],
  );
  const liberadas = pessoasEscopo.filter((p) => p.status === 'Liberada').length;
  const pendentes = pessoasEscopo.length - liberadas;

  // Mesma composição da aba Pendências: pessoas bloqueadas, contratos encerrados com gente ativa e
  // alertas de estoque insuficiente (estes não têm obra, só entram sem filtro).
  const idsPessoasEscopo = useMemo(() => new Set(pessoasEscopo.map((p) => p.trabalhadorId)), [pessoasEscopo]);
  const bloqueadasEscopo = painel.pessoasBloqueadas.filter((p) => !obraId || idsPessoasEscopo.has(p.trabalhadorId));
  const encerradosEscopo = painel.contratosEncerradosComPessoasAtivas.filter((c) => !obraId || idsContratosEscopo.has(c.contratoId));
  const alertasEstoque = obraId ? 0 : painel.alertasEstoqueInsuficiente.length;
  const pendenciasAbertas = bloqueadasEscopo.length + encerradosEscopo.length + alertasEstoque;

  const faixasPrazo = useMemo(() => {
    const dias = (c: Contrato) => Math.round((new Date(c.dataFimVigencia.slice(0, 10)).getTime() - new Date(hojeISO).getTime()) / 86_400_000);
    const ativos = contratosEscopo.filter((c) => c.status === 'Validado');
    return [
      { rotulo: 'Vencidos', valor: ativos.filter((c) => dias(c) < 0).length },
      { rotulo: 'Até 30 dias', valor: ativos.filter((c) => dias(c) >= 0 && dias(c) <= 30).length },
      { rotulo: '31 a 60', valor: ativos.filter((c) => dias(c) > 30 && dias(c) <= 60).length },
      { rotulo: '61 a 90', valor: ativos.filter((c) => dias(c) > 60 && dias(c) <= 90).length },
      { rotulo: 'Mais de 90', valor: ativos.filter((c) => dias(c) > 90).length },
    ];
  }, [contratosEscopo, hojeISO]);

  const situacaoDados: FatiaDonut[] = [
    { rotulo: 'Liberada', valor: liberadas, cor: paleta.ok },
    { rotulo: 'Pendente', valor: pendentes, cor: paleta.alerta },
  ];

  const pessoasPorEmpresa: ItemRanking[] = useMemo(() => {
    const contagem = new Map<string, number>();
    for (const p of pessoasEscopo) {
      contagem.set(p.empresaRazaoSocial, (contagem.get(p.empresaRazaoSocial) ?? 0) + 1);
    }
    return [...contagem.entries()]
      .map(([rotulo, valor]) => ({ rotulo, valor, cor: paleta.marca }))
      .sort((a, b) => b.valor - a.valor)
      .slice(0, 5);
  }, [pessoasEscopo, paleta.marca]);

  const contratosPorObra: ItemRanking[] = useMemo(() => {
    const contagem = new Map<string, number>();
    for (const c of vigentes) {
      contagem.set(c.obraNome, (contagem.get(c.obraNome) ?? 0) + 1);
    }
    return [...contagem.entries()]
      .map(([rotulo, valor]) => ({ rotulo, valor, cor: paleta.info }))
      .sort((a, b) => b.valor - a.valor)
      .slice(0, 5);
  }, [vigentes, paleta.info]);

  const razaoEmpresa = useMemo(() => new Map(empresas.map((e) => [e.id, e.nomeFantasia || e.razaoSocial])), [empresas]);
  const limite60ISO = maisDias(hojeISO, 60);

  const contratosAVencer = useMemo(
    () =>
      vigentes
        .filter((c) => c.dataFimVigencia.slice(0, 10) <= limite60ISO)
        .sort((a, b) => a.dataFimVigencia.localeCompare(b.dataFimVigencia))
        .slice(0, 5)
        .map((c) => ({
          id: c.id,
          titulo: `Contrato ${c.numeroContrato}`,
          meta: `${razaoEmpresa.get(c.empresaId) ?? 'Empresa'} · ${c.obraNome}`,
          hora: dataCurtaPainel(c.dataFimVigencia),
          icone: <DocumentText24Regular />,
          variante: 'atencao' as const,
        })),
    [vigentes, limite60ISO, razaoEmpresa],
  );

  const pessoasComPendencia = useMemo(
    () =>
      bloqueadasEscopo.slice(0, 5).map((p) => ({
        id: p.trabalhadorId,
        titulo: p.nome,
        meta: `${p.empresaRazaoSocial}${p.pendencias[0] ? ` · ${p.pendencias[0]}` : ''}`,
        icone: <Person24Regular />,
        variante: 'alerta' as const,
      })),
    [bloqueadasEscopo],
  );

  return (
    <PainelModulo
      semPeriodo
      obras={obras}
      periodo="tudo"
      aoMudarPeriodo={() => undefined}
      obraId={obraId}
      aoMudarObra={setObraId}
      erro={
        <>
          {erro && (
            <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>
              {erro}
            </FeedbackInline>
          )}
          {pendenciasIndisponiveis && (
            <FeedbackInline tom="erro" aoFechar={() => setPendenciasIndisponiveis(false)}>
              Não foi possível carregar as pendências agora. Os demais indicadores continuam corretos.
            </FeedbackInline>
          )}
        </>
      }
      kpis={[
        { rotulo: 'Empresas ativas', valor: empresasAtivas.length, tom: 'info', icone: <Building24Regular /> },
        { rotulo: 'Contratos vigentes', valor: vigentes.length, tom: 'ok', icone: <DocumentText24Regular /> },
        { rotulo: 'Pessoas liberadas', valor: liberadas, tom: 'ok', icone: <CheckmarkCircle24Regular /> },
        { rotulo: 'Pendências abertas', valor: pendenciasIndisponiveis ? '—' : pendenciasAbertas, tom: pendenciasIndisponiveis ? 'neutro' : 'alerta', icone: <Warning24Regular /> },
      ]}
      largo={
        <Card titulo="Contratos por prazo de vencimento" subtitulo="Contratos validados agrupados pelos dias até o fim da vigência">
          <TrendBarChart dados={faixasPrazo} cor={paleta.marca} rotuloSerie="Contratos" />
        </Card>
      }
      laterais={[
        <Card key="situacao" titulo="Situação das pessoas" subtitulo="Pessoas terceirizadas liberadas e com pendência">
          <StatusDonutChart dados={situacaoDados} legendaCentral="pessoas" />
        </Card>,
      ]}
      inferiores={[
        <Card key="empresa" titulo="Pessoas por empresa" subtitulo="Pessoas terceirizadas por empresa, top 5">
          <RankingBarChart dados={pessoasPorEmpresa} />
        </Card>,
        <Card key="obra" titulo="Contratos vigentes por obra" subtitulo="Contratos validados e dentro da vigência, top 5 obras">
          <RankingBarChart dados={contratosPorObra} />
        </Card>,
        <FeedCardPainel
          key="avencer"
          titulo="Contratos a vencer"
          subtitulo="Contratos vigentes que terminam em até 60 dias"
          itens={contratosAVencer}
          vazio="Nenhum contrato vencendo."
        />,
        <FeedCardPainel
          key="pendencia"
          titulo="Pessoas com pendência"
          subtitulo="Pessoas bloqueadas e o motivo principal"
          itens={pessoasComPendencia}
          vazio="Nenhuma pessoa com pendência."
        />,
      ]}
    />
  );
}
