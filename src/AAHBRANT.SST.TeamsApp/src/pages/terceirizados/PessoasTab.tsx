import { useEffect, useState } from 'react';
import { Card, DataTable, StatusChip, FeedbackInline, type Coluna } from '@ui';
import { api, type PessoaTerceirizada } from '../../lib/api';

export function PessoasTab() {
  const [pessoas, setPessoas] = useState<PessoaTerceirizada[]>([]);
  const [erro, setErro] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(true);

  useEffect(() => {
    (async () => {
      try {
        setErro(null);
        setPessoas(await api.terceirizados.pessoas.listar());
      } catch (e) {
        setErro(e instanceof Error ? e.message : 'Falha ao carregar pessoas terceirizadas.');
      } finally {
        setCarregando(false);
      }
    })();
  }, []);

  const colunas: Coluna<PessoaTerceirizada>[] = [
    { chave: 'nome', rotulo: 'Nome' },
    { chave: 'empresaRazaoSocial', rotulo: 'Empresa' },
    { chave: 'funcaoNome', rotulo: 'Função' },
    { chave: 'numeroContrato', rotulo: 'Contrato' },
    {
      chave: 'status',
      rotulo: 'Status',
      render: (p) => <StatusChip tom={p.status === 'Liberada' ? 'ok' : 'alerta'}>{p.status}</StatusChip>,
    },
    {
      chave: 'pendencias',
      rotulo: 'Pendências',
      render: (p) => (p.pendencias.length === 0 ? '—' : p.pendencias.join('; ')),
    },
  ];

  return (
    <Card titulo="Pessoas terceirizadas">
      {erro && <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>{erro}</FeedbackInline>}
      <DataTable
        aria-label="Pessoas terceirizadas"
        colunas={colunas}
        linhas={pessoas}
        chaveLinha={(p) => p.trabalhadorId}
        carregando={carregando}
        vazio={{ titulo: 'Nenhuma pessoa terceirizada cadastrada ainda' }}
      />
    </Card>
  );
}
