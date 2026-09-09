import { useEffect, useState } from 'react';
import { useParams } from 'react-router-dom';
import { Abas, Card, Carregando, FeedbackInline, PageHeader, StatusChip, Text, type Tom } from '@ui';
import { api, statusPgrLabel, type Obra, type PgrDetalhe } from '../../lib/api';
import { InventarioTab } from './InventarioTab';
import { PlanoAcaoTab } from './PlanoAcaoTab';
import { PgrRevisoesTab } from './PgrRevisoesTab';

type AbaPgr = 'inventario' | 'planoAcao' | 'revisoes';

// Mapeamento por julgamento (guia item 5, "não é 1:1 mecânico"): Vigente é o único estado
// claramente positivo (ok); Em revisão pede atenção (atencao); Encerrado é neutro-informativo, não
// um alerta (info) — mesmo raciocínio de tomPorStatusApr (AprsTab.tsx), aplicado ao enum de PGR.
const tomPorStatusPgr: Record<number, Tom> = {
  1: 'neutro', // EmElaboracao
  2: 'ok', // Vigente
  3: 'atencao', // EmRevisao
  4: 'info', // Encerrado
};

// Onda 2 Task 8 (camada ui/): detalhe do PGR (inventário de riscos + plano de ação + revisões). Sem
// DetailPageLayout/WorkflowActions — `api.pgrs` só expõe CRUD (criar/atualizar/excluir), não há um
// conjunto de ações de fluxo nomeadas (aprovar/reprovar) como em AprDetalhePage.tsx; o status muda por
// edição normal do registro, não por uma transição de estado dedicada. Por isso PageHeader + Card
// empilhados, mesmo julgamento já usado em TrabalhadorDetalhePage.tsx/PcmsoDetalhePage.tsx — e mesma
// razão para as abas internas usarem `Abas nivel="modulo"` sem `useAbaNaUrl` (reservado aos dois
// níveis da página-pilar, spec §4.1).
export function PgrDetalhePage() {
  const { id } = useParams<{ id: string }>();
  const [aba, setAba] = useState<AbaPgr>('inventario');
  const [detalhe, setDetalhe] = useState<PgrDetalhe | null>(null);
  const [obras, setObras] = useState<Obra[]>([]);
  const [erro, setErro] = useState<string | null>(null);

  async function carregar() {
    if (!id) return;
    try {
      setErro(null);
      const [det, obrs] = await Promise.all([api.pgrs.obterDetalhe(id), api.obras.listar()]);
      setDetalhe(det);
      setObras(obrs);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar PGR.');
    }
  }

  useEffect(() => {
    carregar();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [id]);

  function nomeObra(obraId: string) {
    return obras.find((o) => o.id === obraId)?.nome ?? obraId;
  }

  if (!id) {
    return <FeedbackInline tom="erro">PGR não encontrado.</FeedbackInline>;
  }

  // Erro na carga inicial precisa aparecer aqui: sem isso o skeleton ficaria para sempre e a falha
  // (API fora, id inexistente) não teria onde ser lida — mesmo padrão de AprDetalhePage.tsx (Task
  // 11): retorno antecipado com OU o erro OU o skeleton, nunca cabeçalho+skeleton+erro empilhados.
  if (!detalhe) {
    return erro ? (
      <FeedbackInline tom="erro" acao={{ rotulo: 'Tentar de novo', aoClicar: () => void carregar() }}>
        {erro}
      </FeedbackInline>
    ) : (
      <Carregando variante="detalhe" linhas={6} />
    );
  }

  const riscosDisponiveis = detalhe.atividades.flatMap((a) => a.riscos);

  return (
    <div>
      <PageHeader
        titulo={detalhe.pgr.nome}
        status={
          <StatusChip tom={tomPorStatusPgr[detalhe.pgr.status] ?? 'neutro'}>
            {statusPgrLabel[detalhe.pgr.status]}
          </StatusChip>
        }
        voltarPara="/prevencao/pgr"
        rotuloVoltar="Voltar para PGR"
      />

      {erro && (
        <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>
          {erro}
        </FeedbackInline>
      )}

      <div style={{ marginBottom: 16 }}>
        <Card densidade="compacta">
          <div style={{ display: 'flex', gap: 16, flexWrap: 'wrap', alignItems: 'center' }}>
            <Text>Obra: {nomeObra(detalhe.pgr.obraId)}</Text>
            <Text>Elaboração: {detalhe.pgr.dataElaboracao?.slice(0, 10)}</Text>
            {detalhe.pgr.dataProximaRevisao && (
              <Text>Próxima revisão: {detalhe.pgr.dataProximaRevisao.slice(0, 10)}</Text>
            )}
            {detalhe.pgr.dataTermino && <Text>Término: {detalhe.pgr.dataTermino.slice(0, 10)}</Text>}
          </div>
        </Card>
      </div>

      <div style={{ marginBottom: 16 }}>
        <Abas
          nivel="modulo"
          aria-label="Seções do PGR"
          valor={aba}
          aoMudar={setAba}
          abas={[
            { valor: 'inventario', rotulo: 'Inventário de riscos' },
            { valor: 'planoAcao', rotulo: 'Plano de ação' },
            { valor: 'revisoes', rotulo: 'Revisões' },
          ]}
        />
      </div>

      {aba === 'inventario' && <InventarioTab atividades={detalhe.atividades} />}
      {aba === 'planoAcao' && <PlanoAcaoTab pgrId={id} riscosDisponiveis={riscosDisponiveis} />}
      {aba === 'revisoes' && <PgrRevisoesTab pgrId={id} />}
    </div>
  );
}
