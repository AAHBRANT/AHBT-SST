import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { BotaoAcao, Button, Card, DataTable, StatusChip, FeedbackInline, type Coluna } from '@ui';
import { ArrowDownload24Regular, Eye24Regular, Open24Regular, Signature24Regular } from '@fluentui/react-icons';
import { api, SituacaoTermoCompromissoEpi, type CatalogoEpi, type EntregaEpi, type TermoCompromissoEpi } from '../../lib/api';
import { TermoCompromissoEpiDialog } from '../../components/assinatura/TermoCompromissoEpiDialog';
import { salvarBlob, useVisualizadorPdf } from '../../components/useVisualizadorPdf';

// Histórico somente-leitura das entregas de EPI deste trabalhador. O registro de novas entregas,
// devoluções e a assinatura da ficha passaram a viver no módulo dedicado /epi (sidebar fixa "EPI",
// decisão confirmada com o usuário) — aqui fica só a consulta, com atalho para lá. A matriz de EPI por
// função (o que É obrigatório para cada função) é editada em MatrizEpiTab, não aqui.
export function EntregasEpiTab({ trabalhadorId }: { trabalhadorId: string }) {
  const navigate = useNavigate();
  const [entregas, setEntregas] = useState<EntregaEpi[]>([]);
  const [epis, setEpis] = useState<CatalogoEpi[]>([]);
  const [erro, setErro] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(true);
  const [baixando, setBaixando] = useState(false);
  const [termo, setTermo] = useState<TermoCompromissoEpi | null>(null);
  const [termoAberto, setTermoAberto] = useState(false);
  const { visualizar, dialogoVisualizador } = useVisualizadorPdf();

  async function carregar() {
    try {
      setErro(null);
      setCarregando(true);
      const [lista, listaEpis] = await Promise.all([
        api.entregasEpi.listar(trabalhadorId),
        api.catalogosEpi.listar(),
      ]);
      setEntregas(lista);
      setEpis(listaEpis);
      setTermo(await api.termosCompromissoEpi.obter(trabalhadorId).catch(() => null));
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar entregas de EPI.');
    } finally {
      setCarregando(false);
    }
  }

  useEffect(() => {
    carregar();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [trabalhadorId]);

  function nomeEpi(id: string) {
    return epis.find((e) => e.id === id)?.nome ?? id;
  }

  function vencido(dataValidade?: string | null) {
    if (!dataValidade) return false;
    return new Date(dataValidade) < new Date(new Date().toDateString());
  }

  async function baixarFicha() {
    try {
      setBaixando(true);
      salvarBlob(await api.entregasEpi.baixarFichaTrabalhador(trabalhadorId), `ficha-epi-${trabalhadorId}.pdf`);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao baixar a ficha em PDF.');
    } finally {
      setBaixando(false);
    }
  }

  // Mesma ficha do "Baixar", aberta na janela de visualização.
  function visualizarFicha() {
    void visualizar({
      titulo: 'Ficha de EPI do funcionário',
      nomeArquivo: `ficha-epi-${trabalhadorId}.pdf`,
      obter: () => api.entregasEpi.baixarFichaTrabalhador(trabalhadorId),
    });
  }

  async function confirmar(id: string) {
    try {
      await api.entregasEpi.confirmar(id);
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao confirmar a entrega.');
    }
  }

  const colunas: Coluna<EntregaEpi>[] = [
    { chave: 'epi', rotulo: 'EPI', render: (e) => nomeEpi(e.catalogoEpiId) },
    { chave: 'quantidade', rotulo: 'Qtd.', alinhar: 'direita', largura: '64px' },
    { chave: 'entrega', rotulo: 'Entrega', render: (e) => e.dataEntrega?.slice(0, 10) },
    {
      chave: 'validade',
      rotulo: 'Validade',
      render: (e) => (
        <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
          <span>{e.dataValidade?.slice(0, 10) ?? '—'}</span>
          {vencido(e.dataValidade) && !e.dataDevolucao && <StatusChip tom="alerta">Vencido</StatusChip>}
        </div>
      ),
    },
    { chave: 'devolucao', rotulo: 'Devolução', render: (e) => e.dataDevolucao?.slice(0, 10) ?? '—' },
    {
      chave: 'confirmacao',
      rotulo: 'Confirmação',
      render: (e) =>
        e.confirmada ? (
          <StatusChip tom="ok">Confirmada</StatusChip>
        ) : (
          <StatusChip tom="atencao">Pendente</StatusChip>
        ),
    },
  ];

  return (
    <Card
      titulo="Entregas de EPI do funcionário"
      acoes={
        <div style={{ display: 'flex', gap: 8 }}>
          <BotaoAcao
            tom="ver"
            icon={<Eye24Regular />}
            onClick={visualizarFicha}
            disabled={entregas.length === 0}
            aria-label="Visualizar ficha"
          >
            Visualizar ficha
          </BotaoAcao>
          <BotaoAcao
            tom="baixar"
            icon={<ArrowDownload24Regular />}
            onClick={baixarFicha}
            disabled={baixando || entregas.length === 0}
            aria-label="Baixar ficha (PDF)"
          >
            Baixar ficha (PDF)
          </BotaoAcao>
          {termo && (
            <StatusChip tom={termo.situacao === SituacaoTermoCompromissoEpi.Pendente ? 'atencao' : termo.situacao === SituacaoTermoCompromissoEpi.Manual ? 'info' : 'ok'}>
              {termo.situacao === SituacaoTermoCompromissoEpi.Digital
                ? 'Termo assinado digitalmente'
                : termo.situacao === SituacaoTermoCompromissoEpi.Manual
                  ? 'Termo assinado em papel'
                  : 'Termo pendente'}
            </StatusChip>
          )}
          <Button icon={<Signature24Regular />} onClick={() => setTermoAberto(true)}>
            Assinatura de termo de recebimento e compromisso
          </Button>
          <Button appearance="primary" icon={<Open24Regular />} onClick={() => navigate('/epi')}>
            Registrar nova entrega
          </Button>
        </div>
      }
    >
      {erro && <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>{erro}</FeedbackInline>}
      {dialogoVisualizador}
      <TermoCompromissoEpiDialog
        open={termoAberto}
        onClose={() => setTermoAberto(false)}
        trabalhadorId={trabalhadorId}
        aoAlterar={() => void carregar()}
      />

      <DataTable
        aria-label="Entregas de EPI do funcionário"
        colunas={colunas}
        linhas={entregas}
        chaveLinha={(e) => e.id}
        carregando={carregando}
        vazio={{
          titulo: 'Nenhuma entrega de EPI registrada',
          descricao: 'Registre a entrega no módulo EPI.',
          acao: { rotulo: 'Ir para EPI', aoClicar: () => navigate('/epi') },
        }}
        acoesLinha={(e) =>
          !e.confirmada && (
            <Button appearance="subtle" onClick={() => confirmar(e.id)}>
              Confirmar entrega
            </Button>
          )
        }
      />
    </Card>
  );
}
