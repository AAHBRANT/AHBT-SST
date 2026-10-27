import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Button, Card, DataTable, FeedbackInline, StatusChip, Text, useConfirmar, type Coluna, type Tom } from '@ui';
import { Add24Regular } from '@fluentui/react-icons';
import { api, type ResumoGeracaoPgrEap } from '../../lib/api';
import { useSucessoToast } from '../../hooks/useSucessoToast';
import { useSouAdministrador } from '../../lib/UsuarioLogadoContext';

export const rotuloStatusGeracao: Record<number, { rotulo: string; tom: Tom }> = {
  1: { rotulo: 'Em elaboração', tom: 'info' },
  2: { rotulo: 'Aguardando aprovação', tom: 'atencao' },
  3: { rotulo: 'Aprovado', tom: 'ok' },
  4: { rotulo: 'Devolvido para ajustes', tom: 'alerta' },
};

// Entrada do gerador de PGR pela EAP (21/10/2026) na lista de PGRs: botão para começar e as gerações
// em andamento. O PGR por PDF da consultoria continua logo abaixo, igual.
export function GeracoesPgrEapCard() {
  const [lista, setLista] = useState<ResumoGeracaoPgrEap[]>([]);
  const [erro, setErro] = useState<string | null>(null);
  const navigate = useNavigate();
  const souAdministrador = useSouAdministrador();
  const sucessoToast = useSucessoToast();
  const { confirmar, dialogElement } = useConfirmar();

  useEffect(() => {
    api.geracoesPgrEap.listar().then(setLista).catch(() => setLista([]));
  }, []);

  // Rascunho = nunca foi enviado para aprovação (situação 1). Excluir é privilégio de Administrador.
  async function excluir(g: ResumoGeracaoPgrEap) {
    const ok = await confirmar({
      titulo: 'Excluir este rascunho?',
      mensagem: `${g.nome} · ${g.obraNome}. Os GHEs, riscos e medidas sugeridos pela IA neste rascunho serão perdidos. Isso não altera nenhum PGR vigente.`,
      rotuloConfirmar: 'Excluir rascunho',
    });
    if (!ok) return;
    setErro(null);
    try {
      await api.geracoesPgrEap.excluir(g.id);
      setLista((l) => l.filter((x) => x.id !== g.id));
      sucessoToast(`Rascunho excluído: ${g.nome}`);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Não foi possível excluir o rascunho.');
    }
  }

  const colunas: Coluna<ResumoGeracaoPgrEap>[] = [
    { chave: 'nome', rotulo: 'PGR' },
    { chave: 'obra', rotulo: 'Obra', render: (g) => g.obraNome },
    { chave: 'status', rotulo: 'Situação', render: (g) => <StatusChip tom={rotuloStatusGeracao[g.status].tom}>{rotuloStatusGeracao[g.status].rotulo}</StatusChip> },
    { chave: 'passo', rotulo: 'Passo', render: (g) => `${g.passo} de 5` },
    { chave: 'criado', rotulo: 'Iniciado em', render: (g) => new Date(g.criadoEmUtc).toLocaleDateString('pt-BR') },
  ];
  if (souAdministrador) {
    colunas.push({
      chave: 'acao',
      rotulo: 'Ação',
      render: (g) => g.status === 1
        ? <Button size="small" aria-label={`Excluir rascunho ${g.nome}`} onClick={(ev) => { ev.stopPropagation(); void excluir(g); }}>Excluir</Button>
        : <Text size={200}>{g.status === 3 ? 'Aprovado não se exclui' : 'Já foi para aprovação'}</Text>,
    });
  }

  return (
    <Card
      titulo="Gerar PGR pela EAP da obra"
      acoes={
        <Button appearance="primary" icon={<Add24Regular />} onClick={() => navigate('/prevencao/pgr/gerar-eap')}>
          Gerar pela EAP
        </Button>
      }
    >
      <Text size={200} style={{ display: 'block', marginBottom: lista.length ? 8 : 0 }}>
        Envie a EAP da obra e a IA monta GHEs, funções, inventário de riscos e medidas. Tudo fica como sugestão até o Engenheiro de Segurança aprovar.
      </Text>
      {erro && <FeedbackInline tom="erro">{erro}</FeedbackInline>}
      {lista.length > 0 && (
        <DataTable
          aria-label="Gerações de PGR pela EAP"
          colunas={colunas}
          linhas={lista}
          chaveLinha={(g) => g.id}
          densidade="compacta"
          aoClicarLinha={(g) => navigate(`/prevencao/pgr/gerar-eap/${g.id}`)}
        />
      )}
      {dialogElement}
    </Card>
  );
}
