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
  type PaletaGraficos,
} from '@ui';
import { People24Regular, CheckmarkCircle24Regular, DismissCircle24Regular, Warning24Regular } from '@fluentui/react-icons';
import {
  api,
  ResultadoAso,
  type Aso,
  type CursoTreinamento,
  type Funcao,
  type Obra,
  type Trabalhador,
  type Treinamento,
} from '../../../lib/api';
import { AptitudeEnginePanel } from './AptitudeEnginePanel';
import { NR_POR_FUNCAO_NOME, normalizarNomeFuncao } from './nrPorFuncao';

const hojeISO = new Date().toISOString().slice(0, 10);

function diffDias(dataISO: string): number {
  const diffMs = new Date(dataISO).getTime() - new Date(hojeISO).getTime();
  return Math.round(diffMs / (1000 * 60 * 60 * 24));
}

function corPorCobertura(cobertura: number, paleta: PaletaGraficos): string {
  if (cobertura >= 90) return paleta.ok;
  if (cobertura >= 70) return paleta.atencao;
  return paleta.alerta;
}

// PENDÊNCIA DE SCHEMA: o filtro "Empresa terceirizada" existia apenas no mock desta tela —
// não há campo equivalente em Trabalhador no backend. Removido daqui até que um campo real
// (ex.: Trabalhador.EmpresaTerceirizadaId ou .NomeEmpresaTerceirizada) seja adicionado ao domínio.
//
// Onda 2 Task 3 (camada ui/): dashboard de Pessoas — KpiCard + gráficos com cores lidas de
// usePaletaGraficos (spec §1.6), grade CSS Grid simples (spec §4.4). "Total na força de trabalho"
// deixa de usar designTokens.colorPrimary (vinho é reservado a ação, nunca a dado, spec §1.1) e
// passa a tom="info", igual ao "Total de X" dos outros dashboards já migrados. A lógica de
// agregação no cliente não muda nesta frente.
export function PessoasDashboardTab() {
  const paleta = usePaletaGraficos();

  const [obras, setObras] = useState<Obra[]>([]);
  const [funcoes, setFuncoes] = useState<Funcao[]>([]);
  const [trabalhadores, setTrabalhadores] = useState<Trabalhador[]>([]);
  const [asos, setAsos] = useState<Aso[]>([]);
  const [treinamentos, setTreinamentos] = useState<Treinamento[]>([]);
  const [cursos, setCursos] = useState<CursoTreinamento[]>([]);
  const [carregando, setCarregando] = useState(true);
  const [erro, setErro] = useState<string | null>(null);

  const [obraId, setObraId] = useState('');
  const [funcaoId, setFuncaoId] = useState('');

  useEffect(() => {
    (async () => {
      try {
        setErro(null);
        const [obrasResp, funcoesResp, trabalhadoresResp, asosResp, treinamentosResp, cursosResp] = await Promise.all([
          api.obras.listar(),
          api.funcoes.listar(),
          api.trabalhadores.listar(),
          api.asos.listar(),
          api.treinamentos.listar(),
          api.cursosTreinamento.listar(),
        ]);
        setObras(obrasResp);
        setFuncoes(funcoesResp);
        setTrabalhadores(trabalhadoresResp);
        setAsos(asosResp);
        setTreinamentos(treinamentosResp);
        setCursos(cursosResp);
      } catch (e) {
        setErro(e instanceof Error ? e.message : 'Falha ao carregar dados do dashboard de Pessoas.');
      } finally {
        setCarregando(false);
      }
    })();
  }, []);

  const trabalhadoresFiltrados = useMemo(
    () =>
      trabalhadores.filter(
        (t) => (obraId === '' || t.obraId === obraId) && (funcaoId === '' || t.funcaoId === funcaoId),
      ),
    [trabalhadores, obraId, funcaoId],
  );

  const asoMaisRecentePorTrabalhador = useMemo(() => {
    const mapa = new Map<string, Aso>();
    for (const trabalhador of trabalhadoresFiltrados) {
      const aso = asos
        .filter((a) => a.trabalhadorId === trabalhador.id)
        .sort((a, b) => b.dataValidade.localeCompare(a.dataValidade))[0];
      if (aso) mapa.set(trabalhador.id, aso);
    }
    return mapa;
  }, [trabalhadoresFiltrados, asos]);

  const statusGeral = useMemo(() => {
    let aptos = 0;
    let restricao = 0;
    let inaptos = 0;
    let pendentes = 0;
    for (const trabalhador of trabalhadoresFiltrados) {
      const aso = asoMaisRecentePorTrabalhador.get(trabalhador.id);
      if (!aso || aso.resultadoStatus === ResultadoAso.Pendente) pendentes += 1;
      else if (aso.resultadoStatus === ResultadoAso.Inapto) inaptos += 1;
      else if (aso.resultadoStatus === ResultadoAso.AptoComRestricao) restricao += 1;
      else aptos += 1;
    }
    return { aptos, restricao, inaptos, pendentes };
  }, [trabalhadoresFiltrados, asoMaisRecentePorTrabalhador]);

  const statusGeralDados: FatiaDonut[] = [
    { rotulo: 'Aptos', valor: statusGeral.aptos, cor: paleta.ok },
    { rotulo: 'Restrição temporária', valor: statusGeral.restricao, cor: paleta.atencao },
    { rotulo: 'Inaptos', valor: statusGeral.inaptos, cor: paleta.alerta },
    { rotulo: 'Documentação pendente', valor: statusGeral.pendentes, cor: paleta.info },
  ];

  const vencimentoAso = useMemo(() => {
    let vencidos = 0;
    let em15 = 0;
    let em30 = 0;
    let emDia = 0;
    for (const aso of asoMaisRecentePorTrabalhador.values()) {
      const dias = diffDias(aso.dataValidade);
      if (dias < 0) vencidos += 1;
      else if (dias <= 15) em15 += 1;
      else if (dias <= 30) em30 += 1;
      else emDia += 1;
    }
    return { vencidos, em15, em30, emDia };
  }, [asoMaisRecentePorTrabalhador]);

  // 4 baldes distintos por urgência — a paleta de status tem tons de sobra (5) pra não precisar
  // colapsar dois níveis no mesmo tom aqui (diferente do caso de 4→3 tons do Guia): "vencidos" fica
  // com o tom mais forte (alerta), "15 dias" com atencao, "30 dias" com info (só pra manter as 4
  // fatias visualmente distintas) e "em dia" com ok.
  const vencimentoAsoDados: FatiaDonut[] = [
    { rotulo: 'Vencidos', valor: vencimentoAso.vencidos, cor: paleta.alerta },
    { rotulo: 'Vencem em 15 dias', valor: vencimentoAso.em15, cor: paleta.atencao },
    { rotulo: 'Vencem em 30 dias', valor: vencimentoAso.em30, cor: paleta.info },
    { rotulo: 'Em dia', valor: vencimentoAso.emDia, cor: paleta.ok },
  ];

  const coberturaNrDados: ItemRanking[] = useMemo(() => {
    const base = cursos
      .filter((curso) => !!curso.normaReferencia)
      .map((curso) => {
        const elegiveis = trabalhadoresFiltrados.filter((t) => {
          const nomeFuncao = funcoes.find((f) => f.id === t.funcaoId)?.nome;
          if (!nomeFuncao) return false;
          const nrsExigidas = NR_POR_FUNCAO_NOME[normalizarNomeFuncao(nomeFuncao)] ?? [];
          return nrsExigidas.includes(curso.normaReferencia as string);
        });
        const emDia = elegiveis.filter((t) =>
          treinamentos.some(
            (tr) => tr.trabalhadorId === t.id && tr.cursoTreinamentoId === curso.id && tr.dataValidade >= hojeISO,
          ),
        );
        return { curso, elegiveis, emDia };
      })
      .filter((b) => b.elegiveis.length > 0);

    return base.map(({ curso, elegiveis, emDia }) => {
      const cobertura = Math.round((emDia.length / elegiveis.length) * 100);
      return {
        rotulo: curso.normaReferencia ?? curso.nome,
        valor: cobertura,
        cor: corPorCobertura(cobertura, paleta),
        detalhe: `${emDia.length} de ${elegiveis.length} em dia`,
      };
    });
  }, [trabalhadoresFiltrados, treinamentos, cursos, funcoes, paleta]);

  const asosVencendo30Dias = vencimentoAso.em15 + vencimentoAso.em30;
  const bloqueados = statusGeral.inaptos + statusGeral.pendentes;

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
        <Field label="Função">
          <Select value={funcaoId} onChange={(_, data) => setFuncaoId(data.value)}>
            <option value="">Todas as funções</option>
            {funcoes.map((f) => (
              <option key={f.id} value={f.id}>
                {f.nome}
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
        <KpiCard rotulo="Total na força de trabalho" valor={trabalhadoresFiltrados.length} tom="info" indice={0} icone={<People24Regular />} />
        <KpiCard rotulo="Aptos" valor={statusGeral.aptos} tom="ok" indice={1} icone={<CheckmarkCircle24Regular />} />
        <KpiCard rotulo="Bloqueados" valor={bloqueados} tom="alerta" indice={2} icone={<DismissCircle24Regular />} />
        <KpiCard rotulo="ASOs vencendo em 30 dias" valor={asosVencendo30Dias} tom="atencao" indice={3} icone={<Warning24Regular />} />
      </div>

      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(340px, 1fr))', gap: 16, marginBottom: 16 }}>
        <Card titulo="Status geral da força de trabalho" subtitulo="Situação clínica do ASO mais recente de cada funcionário">
          <StatusDonutChart dados={statusGeralDados} legendaCentral="funcionários" />
        </Card>
        <Card titulo="Cobertura de treinamentos por NR" subtitulo="% da equipe elegível com o curso em dia · meta 90%">
          <RankingBarChart dados={coberturaNrDados} dominio={[0, 100]} valorReferencia={90} sufixo="%" />
        </Card>
        <Card titulo="Vencimento de ASOs" subtitulo="Considera apenas funcionários com ASO cadastrado">
          <StatusDonutChart dados={vencimentoAsoDados} legendaCentral="ASOs" />
        </Card>
      </div>

      <AptitudeEnginePanel trabalhadores={trabalhadoresFiltrados} obraId={obraId} />

      {!carregando && trabalhadoresFiltrados.length === 0 && (
        <div style={{ marginTop: 16 }}>
          <EstadoVazio
            titulo="Nenhum funcionário encontrado"
            descricao="Ajuste os filtros de obra ou função para ver resultados."
          />
        </div>
      )}
    </div>
  );
}
