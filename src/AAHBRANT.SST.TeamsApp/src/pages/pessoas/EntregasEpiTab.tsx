import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Button, Card, DataTable, StatusChip, FeedbackInline, type Coluna } from '@ui';
import { ArrowDownload24Regular, Open24Regular } from '@fluentui/react-icons';
import { api, type CatalogoEpi, type EntregaEpi } from '../../lib/api';

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
      const blob = await api.entregasEpi.baixarFichaTrabalhador(trabalhadorId);
      const url = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = `ficha-epi-${trabalhadorId}.pdf`;
      link.click();
      URL.revokeObjectURL(url);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao baixar a ficha em PDF.');
    } finally {
      setBaixando(false);
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
  ];

  return (
    <Card
      titulo="Entregas de EPI do funcionário"
      acoes={
        <div style={{ display: 'flex', gap: 8 }}>
          <Button
            appearance="subtle"
            icon={<ArrowDownload24Regular />}
            onClick={baixarFicha}
            disabled={baixando || entregas.length === 0}
          >
            Baixar ficha (PDF)
          </Button>
          <Button appearance="primary" icon={<Open24Regular />} onClick={() => navigate('/epi')}>
            Registrar nova entrega
          </Button>
        </div>
      }
    >
      {erro && <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>{erro}</FeedbackInline>}

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
      />
    </Card>
  );
}
