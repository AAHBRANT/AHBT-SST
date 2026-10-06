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
  Clock24Regular,
  People24Regular,
  Ribbon24Regular,
  Warning24Regular,
} from '@fluentui/react-icons';
import {
  api,
  StatusSessaoTreinamento,
  type AptidaoCurso,
  type CursoTreinamento,
  type Obra,
  type SessaoTreinamento,
  type Trabalhador,
  type Treinamento,
} from '../../../lib/api';
import {
  FeedCardPainel,
  PainelModulo,
  dataCurtaPainel,
  hojeIsoPainel,
  ultimosMeses,
  usePainelFiltros,
} from '../../../components/dashboard/PainelModulo';

function maisDias(iso: string, dias: number): string {
  const d = new Date(iso);
  d.setDate(d.getDate() + dias);
  return d.toISOString().slice(0, 10);
}

function corPorCobertura(pct: number, p: { ok: string; atencao: string; alerta: string }): string {
  if (pct >= 90) return p.ok;
  if (pct >= 70) return p.atencao;
  return p.alerta;
}

// Dashboard do módulo Treinamentos no padrão da tela Início (pedido do usuário, 05/10). Cobertura e
// situação dos treinamentos exigidos vêm da mesma consulta de aptidão por curso que o Início usa.
export function TreinamentosDashboardTab() {
  const paleta = usePaletaGraficos();
  const { periodo, setPeriodo, obraId, setObraId, noPeriodo } = usePainelFiltros();

  const [obras, setObras] = useState<Obra[]>([]);
  const [sessoes, setSessoes] = useState<SessaoTreinamento[]>([]);
  const [aptidao, setAptidao] = useState<AptidaoCurso[]>([]);
  const [treinamentos, setTreinamentos] = useState<Treinamento[]>([]);
  const [cursos, setCursos] = useState<CursoTreinamento[]>([]);
  const [trabalhadores, setTrabalhadores] = useState<Trabalhador[]>([]);
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
        const [sessoesResp, aptidaoResp, treinamentosResp, cursosResp, trabalhadoresResp] = await Promise.all([
          api.sessoesTreinamento.listar(filtroObra),
          api.cursosTreinamento.aptidao(filtroObra),
          api.treinamentos.listar(undefined, filtroObra),
          api.cursosTreinamento.listar(),
          api.trabalhadores.listar(filtroObra),
        ]);
        if (cancelado) return;
        setSessoes(sessoesResp);
        setAptidao(aptidaoResp);
        setTreinamentos(treinamentosResp);
        setCursos(cursosResp);
        setTrabalhadores(trabalhadoresResp);
      } catch (e) {
        if (!cancelado) setErro(e instanceof Error ? e.message : 'Falha ao carregar os dados do dashboard de Treinamentos.');
      }
    })();
    return () => {
      cancelado = true;
    };
  }, [obraId]);

  const hojeISO = hojeIsoPainel();
  const limite30ISO = maisDias(hojeISO, 30);

  const nomeCurso = useMemo(() => new Map(cursos.map((c) => [c.id, c.nome])), [cursos]);
  const trabalhadorPorId = useMemo(() => new Map(trabalhadores.map((t) => [t.id, t])), [trabalhadores]);

  const sessoesPeriodo = useMemo(() => sessoes.filter((s) => noPeriodo(s.dataRealizacao)), [sessoes, noPeriodo]);

  const soma = (campo: keyof Pick<AptidaoCurso, 'exigidos' | 'emDia' | 'vencemEm30Dias' | 'vencidos' | 'semCurso'>) =>
    aptidao.reduce((total, a) => total + a[campo], 0);
  const exigidos = soma('exigidos');
  const emDia = soma('emDia');
  const vencem30 = soma('vencemEm30Dias');
  const vencidos = soma('vencidos');
  const semCurso = soma('semCurso');
  const coberturaPct = exigidos > 0 ? Math.round(((emDia + vencem30) / exigidos) * 100) : null;

  const serieMensal = useMemo(() => {
    const contagem = new Map<string, number>();
    for (const s of sessoes) {
      const chave = s.dataRealizacao.slice(0, 7);
      contagem.set(chave, (contagem.get(chave) ?? 0) + 1);
    }
    return ultimosMeses(6).map((m) => ({ rotulo: m.rotulo, valor: contagem.get(m.chave) ?? 0 }));
  }, [sessoes]);

  const situacaoDados: FatiaDonut[] = [
    { rotulo: 'Em dia', valor: emDia, cor: paleta.ok },
    { rotulo: 'Vencem em 30 dias', valor: vencem30, cor: paleta.atencao },
    { rotulo: 'Vencidos', valor: vencidos, cor: paleta.alerta },
    { rotulo: 'Sem curso', valor: semCurso, cor: paleta.neutro },
  ];

  const menorCobertura: ItemRanking[] = useMemo(
    () =>
      aptidao
        .filter((a) => a.exigidos > 0)
        .map((a) => {
          const pct = Math.round(((a.emDia + a.vencemEm30Dias) / a.exigidos) * 100);
          return { rotulo: a.nome, valor: pct, cor: corPorCobertura(pct, paleta) };
        })
        .sort((a, b) => a.valor - b.valor)
        .slice(0, 5),
    [aptidao, paleta],
  );

  const participantesPorObra: ItemRanking[] = useMemo(() => {
    const contagem = new Map<string, number>();
    for (const s of sessoesPeriodo) {
      contagem.set(s.obraNome, (contagem.get(s.obraNome) ?? 0) + s.totalParticipantes);
    }
    return [...contagem.entries()]
      .map(([rotulo, valor]) => ({ rotulo, valor, cor: paleta.marca }))
      .sort((a, b) => b.valor - a.valor)
      .slice(0, 5);
  }, [sessoesPeriodo, paleta.marca]);

  // Só o certificado mais recente de cada pessoa e curso conta: uma renovação não deixa o anterior
  // aparecendo como vencido.
  const proximosVencimentos = useMemo(() => {
    const maisRecente = new Map<string, Treinamento>();
    for (const t of treinamentos) {
      const chave = `${t.trabalhadorId}|${t.cursoTreinamentoId}`;
      const atual = maisRecente.get(chave);
      if (!atual || t.dataValidade > atual.dataValidade) maisRecente.set(chave, t);
    }
    return [...maisRecente.values()]
      .filter((t) => t.dataValidade <= limite30ISO)
      .sort((a, b) => a.dataValidade.localeCompare(b.dataValidade))
      .slice(0, 5)
      .map((t) => ({
        id: t.id,
        titulo: nomeCurso.get(t.cursoTreinamentoId) ?? 'Curso removido do catálogo',
        meta: trabalhadorPorId.get(t.trabalhadorId)?.nome ?? 'Trabalhador não encontrado',
        hora: dataCurtaPainel(t.dataValidade),
        icone: <Ribbon24Regular />,
        variante: t.dataValidade < hojeISO ? ('alerta' as const) : ('atencao' as const),
      }));
  }, [treinamentos, limite30ISO, hojeISO, nomeCurso, trabalhadorPorId]);

  const ultimasTurmas = useMemo(
    () =>
      [...sessoes]
        .sort((a, b) => b.dataRealizacao.localeCompare(a.dataRealizacao))
        .slice(0, 5)
        .map((s) => ({
          id: s.id,
          titulo: s.cursoTreinamentoNome,
          meta: `${s.obraNome} · ${s.totalParticipantes} participante${s.totalParticipantes === 1 ? '' : 's'}`,
          hora: dataCurtaPainel(s.dataRealizacao),
          icone: <People24Regular />,
          variante: s.status === StatusSessaoTreinamento.Concluida ? ('bom' as const) : ('info' as const),
        })),
    [sessoes],
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
        { rotulo: 'Turmas no período', valor: sessoesPeriodo.length, tom: 'info', icone: <People24Regular /> },
        {
          rotulo: 'Cobertura de treinamentos',
          valor: coberturaPct === null ? '—' : `${coberturaPct}%`,
          tom: coberturaPct === null ? 'neutro' : coberturaPct >= 90 ? 'ok' : 'atencao',
          icone: <CheckmarkCircle24Regular />,
        },
        { rotulo: 'Vencem em 30 dias', valor: vencem30, tom: 'atencao', icone: <Clock24Regular /> },
        { rotulo: 'Pendentes de treinamento', valor: vencidos + semCurso, tom: 'alerta', icone: <Warning24Regular /> },
      ]}
      largo={
        <Card titulo="Turmas por mês" subtitulo="Turmas de treinamento realizadas nos últimos 6 meses">
          <TrendBarChart dados={serieMensal} cor={paleta.marca} rotuloSerie="Turmas" />
        </Card>
      }
      laterais={[
        <Card key="situacao" titulo="Situação dos treinamentos exigidos" subtitulo="Pela matriz de treinamento por função">
          <StatusDonutChart dados={situacaoDados} legendaCentral="exigidos" />
        </Card>,
      ]}
      inferiores={[
        <Card key="cobertura" titulo="Cursos com menor cobertura" subtitulo="% da equipe elegível com o curso em dia · meta 90%">
          <RankingBarChart dados={menorCobertura} dominio={[0, 100]} valorReferencia={90} sufixo="%" />
        </Card>,
        <Card key="obras" titulo="Participantes por obra" subtitulo="Participantes das turmas do período, top 5 obras">
          <RankingBarChart dados={participantesPorObra} />
        </Card>,
        <FeedCardPainel
          key="venc"
          titulo="Próximos vencimentos"
          subtitulo="Certificados vencidos ou que vencem em até 30 dias"
          itens={proximosVencimentos}
          vazio="Nenhum certificado vencido ou vencendo."
        />,
        <FeedCardPainel
          key="turmas"
          titulo="Últimas turmas"
          subtitulo="Turmas de treinamento mais recentes"
          itens={ultimasTurmas}
          vazio="Nenhuma turma registrada."
        />,
      ]}
    />
  );
}
