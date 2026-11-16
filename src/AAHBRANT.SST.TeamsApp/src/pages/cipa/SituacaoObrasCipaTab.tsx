import { useEffect, useMemo, useState } from 'react';
import { Card, DataTable, FeedbackInline, KpiCard, PageHeader, StatusChip, type Coluna } from '@ui';
import type { Tom } from '../../ui/tokens/tokens';
import {
  Building24Regular,
  CheckmarkCircle24Regular,
  PeopleTeam24Regular,
  Warning24Regular,
} from '@fluentui/react-icons';
import { FaixaKpis } from '../../components/dashboard/FaixaKpis';
import {
  api,
  SituacaoCipaObraTipo,
  situacaoCipaObraLabel,
  statusProcessoEleitoralCipaLabel,
  type SituacaoCipaObra,
} from '../../lib/api';

// Situação da CIPA por obra (Etapa 1 do plano CIPA, 16/11/2026). O número de empregados vem do
// cadastro de trabalhadores (CLT ativos), não é mais digitado. O sistema só aplica o piso de 20
// empregados da NR-5; o número de titulares/suplentes continua vindo do dimensionamento validado
// pelo técnico de SST (ver disclosure em ObterSituacaoCipaPorObraQuery.cs).
const tomPorSituacao: Record<number, Tom> = {
  [SituacaoCipaObraTipo.PrecisaDeCipa]: 'alerta',
  [SituacaoCipaObraTipo.ConvocarEleicao]: 'alerta',
  [SituacaoCipaObraTipo.NomearRepresentante]: 'atencao',
  [SituacaoCipaObraTipo.EleicaoEmAndamento]: 'info',
  [SituacaoCipaObraTipo.RepresentanteNomeado]: 'ok',
  [SituacaoCipaObraTipo.CipaAtiva]: 'ok',
};

function data(iso: string | null) {
  return iso ? new Date(iso).toLocaleDateString('pt-BR') : '';
}

function detalhe(o: SituacaoCipaObra): string {
  switch (o.situacao) {
    case SituacaoCipaObraTipo.PrecisaDeCipa:
      return 'Tem 20 ou mais empregados e nenhum membro com mandato vigente. Faça o dimensionamento e abra a eleição.';
    case SituacaoCipaObraTipo.ConvocarEleicao:
      return `Mandato termina em ${data(o.fimMandatoVigente)}. A NR-5 pede a convocação da eleição no mínimo 60 dias antes.`;
    case SituacaoCipaObraTipo.NomearRepresentante:
      return 'Abaixo de 20 empregados: a empresa nomeia 1 representante treinado, sem eleição.';
    case SituacaoCipaObraTipo.EleicaoEmAndamento:
      return `${o.statusProcessoEleitoral ? statusProcessoEleitoralCipaLabel[o.statusProcessoEleitoral] : 'Em andamento'}${o.dataVotacao ? ` · votação em ${data(o.dataVotacao)}` : ''}`;
    case SituacaoCipaObraTipo.RepresentanteNomeado:
      return `Representante com mandato até ${data(o.fimMandatoVigente)}.`;
    default:
      return `${o.membrosComMandatoVigente} membros · mandato até ${data(o.fimMandatoVigente)}.`;
  }
}

export function SituacaoObrasCipaTab() {
  const [lista, setLista] = useState<SituacaoCipaObra[]>([]);
  const [carregando, setCarregando] = useState(true);
  const [erro, setErro] = useState<string | null>(null);

  useEffect(() => {
    api.cipa.dimensionamento
      .situacaoObras()
      .then(setLista)
      .catch((e) => setErro(e instanceof Error ? e.message : 'Falha ao carregar a situação das obras.'))
      .finally(() => setCarregando(false));
  }, []);

  const kpis = useMemo(() => {
    const conta = (...s: number[]) => lista.filter((o) => s.includes(o.situacao)).length;
    return {
      obras: lista.length,
      emDia: conta(SituacaoCipaObraTipo.CipaAtiva, SituacaoCipaObraTipo.RepresentanteNomeado, SituacaoCipaObraTipo.EleicaoEmAndamento),
      urgentes: conta(SituacaoCipaObraTipo.PrecisaDeCipa, SituacaoCipaObraTipo.ConvocarEleicao),
      nomear: conta(SituacaoCipaObraTipo.NomearRepresentante),
    };
  }, [lista]);

  const colunas: Coluna<SituacaoCipaObra>[] = [
    {
      chave: 'obraNome',
      rotulo: 'Obra',
      render: (o) => (
        <div style={{ display: 'flex', flexDirection: 'column' }}>
          <strong>{o.obraNome}</strong>
          <span style={{ fontSize: 12, opacity: 0.75 }}>{o.obraCodigo}</span>
        </div>
      ),
    },
    {
      chave: 'empregadosAtivos',
      rotulo: 'Empregados',
      render: (o) => (
        <div style={{ display: 'flex', flexDirection: 'column' }}>
          <strong style={{ fontVariantNumeric: 'tabular-nums' }}>{o.empregadosAtivos}</strong>
          {o.terceirizadosAtivos > 0 && (
            <span style={{ fontSize: 12, opacity: 0.75 }}>
              + {o.terceirizadosAtivos} terceirizados{o.terceirosExigemRepresentante ? ' (contratada nomeia representante)' : ''}
            </span>
          )}
        </div>
      ),
    },
    {
      chave: 'dimensionamento',
      rotulo: 'Dimensionamento',
      render: (o) =>
        o.ultimoDimensionamentoData ? (
          <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
            <span>
              {o.titularesDimensionados} titulares + {o.suplentesDimensionados} suplentes
            </span>
            <span style={{ fontSize: 12, opacity: 0.75 }}>
              {o.ultimoDimensionamentoFuncionarios} empregados em {data(o.ultimoDimensionamentoData)}
            </span>
            {o.dimensionamentoDesatualizado && <StatusChip tom="atencao">Refazer: efetivo mudou</StatusChip>}
          </div>
        ) : o.empregadosAtivos >= 20 ? (
          <StatusChip tom="alerta">Não feito</StatusChip>
        ) : (
          <span style={{ opacity: 0.6 }}>Não se aplica</span>
        ),
    },
    {
      chave: 'situacao',
      rotulo: 'Situação',
      render: (o) => (
        <div style={{ display: 'flex', flexDirection: 'column', gap: 4, maxWidth: 360 }}>
          <StatusChip tom={tomPorSituacao[o.situacao] ?? 'neutro'}>{situacaoCipaObraLabel[o.situacao]}</StatusChip>
          <span style={{ fontSize: 12, opacity: 0.8 }}>{detalhe(o)}</span>
        </div>
      ),
    },
  ];

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <PageHeader titulo="Situação por obra" />
      {erro && (
        <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>
          {erro}
        </FeedbackInline>
      )}
      <FaixaKpis>
        <KpiCard rotulo="Obras ativas" valor={kpis.obras} tom="info" indice={0} icone={<Building24Regular />} carregando={carregando} />
        <KpiCard rotulo="Em dia" valor={kpis.emDia} tom="ok" indice={1} icone={<CheckmarkCircle24Regular />} carregando={carregando} />
        <KpiCard rotulo="Exigem ação" valor={kpis.urgentes} tom="alerta" indice={2} icone={<Warning24Regular />} carregando={carregando} />
        <KpiCard rotulo="Nomear representante" valor={kpis.nomear} tom="atencao" indice={3} icone={<PeopleTeam24Regular />} carregando={carregando} />
      </FaixaKpis>
      <Card>
        <DataTable
          aria-label="Situação da CIPA por obra"
          colunas={colunas}
          linhas={lista}
          chaveLinha={(o) => o.obraId}
          carregando={carregando}
          vazio={{ titulo: 'Nenhuma obra ativa encontrada' }}
        />
      </Card>
      <FeedbackInline tom="info">
        Empregados = trabalhadores CLT ativos da obra, contados automaticamente do cadastro. Terceirizados contam para a CIPA da
        empresa contratada. O piso de 20 empregados e o prazo de 60 dias seguem a NR-5; o número de titulares e suplentes
        continua definido no dimensionamento, conforme o Quadro I, com validação do técnico ou engenheiro de segurança.
      </FeedbackInline>
    </div>
  );
}
