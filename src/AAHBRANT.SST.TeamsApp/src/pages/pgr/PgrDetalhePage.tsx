import { dataBr } from '../../components/documentos-obra/CartoesVigentesPorObra';
import { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { Abas, useAbaNaUrl, Button, Card, Carregando, FeedbackInline, PageHeader, StatusChip, Text, type Tom } from '@ui';
import { DocumentArrowUp24Regular, DocumentPdf24Regular } from '@fluentui/react-icons';
import { DialogoNovaRevisao } from '../../components/revisoes/DialogoNovaRevisao';
import { BotaoLerComIa, PainelLeituraIa, useLeituraIa } from '../../components/leitura-ia/PainelLeituraIa';
import {
  api,
  statusPgrLabel,
  StatusControleRisco,
  type Obra,
  type PgrDetalhe,
  type ResumoGeracaoPgrEap,
} from '../../lib/api';
import { useVisualizadorPdf } from '../../components/useVisualizadorPdf';
import { useSouAdministrador } from '../../lib/UsuarioLogadoContext';
import { GhesTab } from '../../components/estrutura-sst/GhesTab';
import { InventarioTab } from './InventarioTab';
import { AtividadesPorFuncaoTab, useAtividadesFuncaoGhes } from './AtividadesPorFuncaoTab';
import { PlanoAcaoTab } from './PlanoAcaoTab';
import { PgrRevisoesTab } from './PgrRevisoesTab';
import { DialogoNovaRevisaoPgr } from './DialogoNovaRevisaoPgr';

const abasPgr = ['resumo', 'ghe', 'atividades-funcao', 'inventario', 'planoAcao', 'revisoes'] as const;
type AbaPgr = (typeof abasPgr)[number];

// Mapeamento por julgamento (guia item 5): Vigente é o único estado claramente positivo (ok); Em
// revisão pede atenção; Encerrado é neutro-informativo (info).
const tomPorStatusPgr: Record<number, Tom> = {
  1: 'neutro', // EmElaboracao
  2: 'ok', // Vigente
  3: 'atencao', // EmRevisao
  4: 'info', // Encerrado
};

// Detalhe do PGR (reformulado em 07/11/2026): abre num Resumo com os números do PGR; o PDF virou
// botão no cabeçalho. Toda revisão nasce da EAP ("Nova revisão pela EAP"). "Ler com IA" só para PGR
// importado (legado) e só para o Admin. Origem (EAP x importado) aparece ao lado do status, e um aviso
// surge quando faltam 60 dias ou menos para a revisão (ou ela já passou).
export function PgrDetalhePage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const souAdmin = useSouAdministrador();
  const { visualizar, dialogoVisualizador } = useVisualizadorPdf();
  // ?aba=atividades-funcao abre a aba direto (atalho das pendências da Ordem de Serviço).
  const [aba, setAba] = useAbaNaUrl<AbaPgr>('aba', abasPgr, 'resumo');
  const [atividadeEmFoco, setAtividadeEmFoco] = useState<string | null>(null);
  const [detalhe, setDetalhe] = useState<PgrDetalhe | null>(null);
  const [obras, setObras] = useState<Obra[]>([]);
  const [geracao, setGeracao] = useState<ResumoGeracaoPgrEap | undefined>();
  const [erro, setErro] = useState<string | null>(null);
  const [novaRevisaoAberta, setNovaRevisaoAberta] = useState(false);
  const [revisaoEapAberta, setRevisaoEapAberta] = useState(false);
  const [versaoDocumento, setVersaoDocumento] = useState(0);
  const leituraIa = useLeituraIa('pgr', id ?? '');
  const obraDoPgr = detalhe?.pgr.obraId ?? '';
  const atividadesFuncao = useAtividadesFuncaoGhes(obraDoPgr);

  async function carregar() {
    if (!id) return;
    try {
      setErro(null);
      const [det, obrs, gers] = await Promise.all([
        api.pgrs.obterDetalhe(id),
        api.obras.listar(),
        api.geracoesPgrEap.listar().catch(() => [] as ResumoGeracaoPgrEap[]),
      ]);
      setDetalhe(det);
      setObras(obrs);
      setGeracao(gers.find((g) => g.pgrId === id));
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

  if (!detalhe) {
    return erro ? (
      <FeedbackInline tom="erro" acao={{ rotulo: 'Tentar de novo', aoClicar: () => void carregar() }}>
        {erro}
      </FeedbackInline>
    ) : (
      <Carregando variante="detalhe" linhas={6} />
    );
  }

  const pgrId = id;
  const pgr = detalhe.pgr;
  const importado = !geracao;
  const riscosDisponiveis = detalhe.atividades.flatMap((a) => a.riscos);
  const riscosAltos = riscosDisponiveis.filter((r) => r.nivelRisco >= 4).length;
  const acoesTotal = detalhe.planoDeAcao.length;
  const acoesConcluidas = detalhe.planoDeAcao.filter((a) => a.status === StatusControleRisco.Concluido).length;

  const hoje = new Date(new Date().toISOString().slice(0, 10) + 'T00:00:00');
  const fimVigencia = pgr.dataProximaRevisao ?? pgr.dataTermino ?? null;
  const diasParaRevisao = fimVigencia
    ? Math.round((new Date(fimVigencia.slice(0, 10) + 'T00:00:00').getTime() - hoje.getTime()) / 86400000)
    : null;

  // PGR da EAP: pergunta o motivo e parte da revisão anterior. Importado (legado): gera pela EAP do zero.
  function novaRevisao() {
    if (geracao) setRevisaoEapAberta(true);
    else navigate('/prevencao/pgr/gerar-eap');
  }

  function abrirPdf() {
    void visualizar({
      titulo: pgr.nome,
      nomeArquivo: `${pgr.nome}.pdf`,
      obter: async () => {
        if (geracao) return api.geracoesPgrEap.baixarPdf(geracao.id);
        const blob = await api.pgrs.obterDocumento(pgrId);
        if (!blob) throw new Error('Este PGR não tem PDF anexado.');
        return blob;
      },
    });
  }

  const indicadores: { rotulo: string; valor: string | number; aba: AbaPgr }[] = [
    { rotulo: 'Atividades', valor: detalhe.atividades.length, aba: 'inventario' },
    { rotulo: 'Riscos avaliados', valor: riscosDisponiveis.length, aba: 'inventario' },
    { rotulo: 'Riscos altos ou críticos', valor: riscosAltos, aba: 'inventario' },
    {
      rotulo: 'Plano de ação',
      valor: acoesTotal ? `${acoesConcluidas} de ${acoesTotal} concluídas` : 'Sem ações',
      aba: 'planoAcao',
    },
  ];

  return (
    <div>
      <PageHeader
        titulo={pgr.nome}
        status={
          <span style={{ display: 'inline-flex', gap: 6 }}>
            <StatusChip tom={tomPorStatusPgr[pgr.status] ?? 'neutro'}>{statusPgrLabel[pgr.status]}</StatusChip>
            <StatusChip tom={importado ? 'neutro' : 'atencao'}>
              {importado ? 'Importado (legado)' : 'Gerado pela EAP'}
            </StatusChip>
          </span>
        }
        acoes={
          <>
            {importado && souAdmin && (
              <BotaoLerComIa aoClicar={leituraIa.iniciar} desabilitado={leituraIa.lendo || leituraIa.iniciando} />
            )}
            <Button icon={<DocumentArrowUp24Regular />} onClick={novaRevisao}>
              Nova revisão
            </Button>
            <Button appearance="primary" icon={<DocumentPdf24Regular />} onClick={abrirPdf}>
              Abrir PDF
            </Button>
          </>
        }
      />

      {erro && (
        <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>
          {erro}
        </FeedbackInline>
      )}

      {diasParaRevisao !== null && diasParaRevisao <= 60 && (
        <div style={{ marginBottom: 16 }}>
          <FeedbackInline
            tom="aviso"
            acao={{ rotulo: 'Nova revisão', aoClicar: novaRevisao }}
          >
            {diasParaRevisao < 0
              ? `A data de revisão foi há ${-diasParaRevisao} dias. Gere a nova versão do PGR pela EAP.`
              : `Revisão em ${diasParaRevisao} dias. Já dá para começar a nova versão do PGR pela EAP.`}
          </FeedbackInline>
        </div>
      )}

      <div style={{ marginBottom: 16 }}>
        <Card densidade="compacta">
          <div style={{ display: 'flex', gap: 24, flexWrap: 'wrap', alignItems: 'center' }}>
            <Text>
              <b>Obra:</b> {nomeObra(pgr.obraId)}
            </Text>
            <Text>
              <b>Vigência:</b> {dataBr(pgr.dataElaboracao)} → {dataBr(fimVigencia)}
              {diasParaRevisao !== null && diasParaRevisao > 60 ? ` (faltam ${diasParaRevisao} dias)` : ''}
            </Text>
            {geracao?.aprovadoPorNome && (
              <Text>
                <b>Aprovado por:</b> {geracao.aprovadoPorNome}
              </Text>
            )}
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
            { valor: 'resumo', rotulo: 'Resumo' },
            { valor: 'ghe', rotulo: 'GHE' },
            {
              valor: 'atividades-funcao',
              rotulo: 'Atividades por função',
              ponto: atividadesFuncao.pendente ? { tom: 'atencao', titulo: 'Há funções sem confirmação' } : undefined,
            },
            { valor: 'inventario', rotulo: 'Riscos e medidas' },
            { valor: 'planoAcao', rotulo: 'Plano de ação' },
            { valor: 'revisoes', rotulo: 'Revisões' },
          ]}
        />
      </div>

      {aba === 'resumo' && (
        <>
          {importado && souAdmin && <PainelLeituraIa documento="pgr" estado={leituraIa} />}
          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(220px, 1fr))', gap: 12 }}>
            {indicadores.map((k) => (
              <button
                key={k.rotulo}
                type="button"
                onClick={() => setAba(k.aba)}
                style={{
                  textAlign: 'left',
                  background: '#fff',
                  border: '1px solid #e5e7eb',
                  borderLeft: '4px solid #670000',
                  borderRadius: 8,
                  padding: '14px 16px',
                  cursor: 'pointer',
                  font: 'inherit',
                }}
              >
                <div style={{ fontSize: 12, color: '#6b7280' }}>{k.rotulo}</div>
                <div style={{ fontSize: 24, fontWeight: 700, marginTop: 4 }}>{k.valor}</div>
              </button>
            ))}
          </div>
        </>
      )}
      {aba === 'ghe' && <GhesTab obraId={pgr.obraId} />}
      {aba === 'atividades-funcao' && (
        atividadesFuncao.ghes === null ? (
          <Carregando variante="detalhe" linhas={4} />
        ) : (
          <>
            {atividadesFuncao.falhou && <FeedbackInline tom="erro">{atividadesFuncao.falhou}</FeedbackInline>}
            <AtividadesPorFuncaoTab
              obraId={pgr.obraId}
              ghes={atividadesFuncao.ghes}
              aoAlterar={atividadesFuncao.recarregar}
              aoVincularProcedimento={(atividadeId) => {
                setAtividadeEmFoco(atividadeId);
                setAba('inventario');
              }}
            />
          </>
        )
      )}
      {aba === 'inventario' && (
        <InventarioTab atividades={detalhe.atividades} aoAlterar={carregar} atividadeEmFoco={atividadeEmFoco} />
      )}
      {aba === 'planoAcao' && <PlanoAcaoTab pgrId={pgrId} riscosDisponiveis={riscosDisponiveis} />}
      {aba === 'revisoes' && <PgrRevisoesTab key={versaoDocumento} pgrId={pgrId} />}
      {dialogoVisualizador}
      <DialogoNovaRevisaoPgr
        aberto={revisaoEapAberta}
        obraId={pgr.obraId}
        obraNome={nomeObra(pgr.obraId)}
        rotuloAtual={`PGR atual vigente até ${dataBr(fimVigencia)}`}
        aoFechar={() => setRevisaoEapAberta(false)}
      />
      <DialogoNovaRevisao
        aberto={novaRevisaoAberta}
        documento="PGR"
        aoFechar={() => setNovaRevisaoAberta(false)}
        salvar={(dados) => api.pgrRevisoes.nova(pgrId, dados)}
        aoSalvar={() => {
          setNovaRevisaoAberta(false);
          setVersaoDocumento((v) => v + 1);
        }}
      />
    </div>
  );
}
