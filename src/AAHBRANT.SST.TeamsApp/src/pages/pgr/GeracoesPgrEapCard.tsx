import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Button, Card, DataTable, StatusChip, Text, type Coluna, type Tom } from '@ui';
import { Add24Regular } from '@fluentui/react-icons';
import { api, type ResumoGeracaoPgrEap } from '../../lib/api';

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
  const navigate = useNavigate();

  useEffect(() => {
    api.geracoesPgrEap.listar().then(setLista).catch(() => setLista([]));
  }, []);

  const colunas: Coluna<ResumoGeracaoPgrEap>[] = [
    { chave: 'nome', rotulo: 'PGR' },
    { chave: 'obra', rotulo: 'Obra', render: (g) => g.obraNome },
    { chave: 'status', rotulo: 'Situação', render: (g) => <StatusChip tom={rotuloStatusGeracao[g.status].tom}>{rotuloStatusGeracao[g.status].rotulo}</StatusChip> },
    { chave: 'passo', rotulo: 'Passo', render: (g) => `${g.passo} de 5` },
    { chave: 'criado', rotulo: 'Iniciado em', render: (g) => new Date(g.criadoEmUtc).toLocaleDateString('pt-BR') },
  ];

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
    </Card>
  );
}
