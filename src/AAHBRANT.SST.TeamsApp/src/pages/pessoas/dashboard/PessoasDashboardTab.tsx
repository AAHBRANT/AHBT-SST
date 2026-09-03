import { useEffect, useMemo, useState } from 'react';
import { Field, Select, Text } from '@fluentui/react-components';
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
import { CardGrid } from '../../../layout/AppShell';
import { designTokens } from '../../../theme';
import { usePageStyles } from '../../pageStyles';
import { useDashboardStyles } from '../../../components/dashboard/dashboardStyles';
import { KpiCard } from '../../../components/dashboard/KpiCard';
import { StatusDonutChart, type FatiaDonut } from '../../../components/dashboard/charts/StatusDonutChart';
import { RankingBarChart, type ItemRanking } from '../../../components/dashboard/charts/RankingBarChart';
import { AptitudeEnginePanel } from './AptitudeEnginePanel';
import { NR_POR_FUNCAO_NOME, normalizarNomeFuncao } from './nrPorFuncao';

const hojeISO = new Date().toISOString().slice(0, 10);

function diffDias(dataISO: string): number {
  const diffMs = new Date(dataISO).getTime() - new Date(hojeISO).getTime();
  return Math.round(diffMs / (1000 * 60 * 60 * 24));
}

function corPorCobertura(cobertura: number): string {
  if (cobertura >= 90) return designTokens.colorSuccess;
  if (cobertura >= 70) return designTokens.colorWarning;
  return designTokens.colorAlert;
}

// PENDÊNCIA DE SCHEMA: o filtro "Empresa terceirizada" existia apenas no mock desta tela —
// não há campo equivalente em Trabalhador no backend. Removido daqui até que um campo real
// (ex.: Trabalhador.EmpresaTerceirizadaId ou .NomeEmpresaTerceirizada) seja adicionado ao domínio.
export function PessoasDashboardTab() {
  const estilosPagina = usePageStyles();
  const estilos = useDashboardStyles();

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
    { rotulo: 'Aptos', valor: statusGeral.aptos, cor: designTokens.colorSuccess },
    { rotulo: 'Restrição temporária', valor: statusGeral.restricao, cor: designTokens.colorWarning },
    { rotulo: 'Inaptos', valor: statusGeral.inaptos, cor: designTokens.colorAlert },
    { rotulo: 'Documentação pendente', valor: statusGeral.pendentes, cor: designTokens.colorInfo },
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

  const vencimentoAsoDados: FatiaDonut[] = [
    { rotulo: 'Vencidos', valor: vencimentoAso.vencidos, cor: designTokens.colorAlert },
    { rotulo: 'Vencem em 15 dias', valor: vencimentoAso.em15, cor: '#F97316' },
    { rotulo: 'Vencem em 30 dias', valor: vencimentoAso.em30, cor: designTokens.colorWarning },
    { rotulo: 'Em dia', valor: vencimentoAso.emDia, cor: designTokens.colorSuccess },
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
        cor: corPorCobertura(cobertura),
        detalhe: `${emDia.length} de ${elegiveis.length} em dia`,
      };
    });
  }, [trabalhadoresFiltrados, treinamentos, cursos, funcoes]);

  const asosVencendo30Dias = vencimentoAso.em15 + vencimentoAso.em30;
  const bloqueados = statusGeral.inaptos + statusGeral.pendentes;

  return (
    <div>
      <div className={estilos.filtros}>
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

      {erro && <Text className={estilosPagina.erro}>{erro}</Text>}

      <div style={{ marginBottom: 16 }}>
        <CardGrid>
          <KpiCard
            rotulo="Total na força de trabalho"
            valor={trabalhadoresFiltrados.length}
            cor={designTokens.colorPrimary}
            icone={<People24Regular />}
          />
          <KpiCard
            rotulo="Aptos"
            valor={statusGeral.aptos}
            cor={designTokens.colorSuccess}
            icone={<CheckmarkCircle24Regular />}
          />
          <KpiCard
            rotulo="Bloqueados"
            valor={bloqueados}
            cor={designTokens.colorAlert}
            icone={<DismissCircle24Regular />}
          />
          <KpiCard
            rotulo="ASOs vencendo em 30 dias"
            valor={asosVencendo30Dias}
            cor={designTokens.colorWarning}
            icone={<Warning24Regular />}
          />
        </CardGrid>
      </div>

      <div className={estilos.chartRow}>
        <div className={estilos.chartCard}>
          <Text className={estilos.chartTitulo}>Status geral da força de trabalho</Text>
          <div className={estilos.chartSubtitulo}>Situação clínica do ASO mais recente de cada funcionário</div>
          <StatusDonutChart dados={statusGeralDados} legendaCentral="funcionários" />
        </div>
        <div className={estilos.chartCard}>
          <Text className={estilos.chartTitulo}>Cobertura de treinamentos por NR</Text>
          <div className={estilos.chartSubtitulo}>% da equipe elegível com o curso em dia · meta 90%</div>
          <RankingBarChart dados={coberturaNrDados} dominio={[0, 100]} valorReferencia={90} sufixo="%" />
        </div>
        <div className={estilos.chartCard}>
          <Text className={estilos.chartTitulo}>Vencimento de ASOs</Text>
          <div className={estilos.chartSubtitulo}>Considera apenas funcionários com ASO cadastrado</div>
          <StatusDonutChart dados={vencimentoAsoDados} legendaCentral="ASOs" />
        </div>
      </div>

      <AptitudeEnginePanel trabalhadores={trabalhadoresFiltrados} obraId={obraId} />

      {!carregando && trabalhadoresFiltrados.length === 0 && (
        <div className={estilosPagina.card} style={{ marginTop: 16 }}>
          <Text>Nenhum funcionário encontrado para os filtros selecionados.</Text>
        </div>
      )}
    </div>
  );
}
