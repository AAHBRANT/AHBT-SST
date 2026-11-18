import { useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Card, DataTable, StatusChip, FeedbackInline, type Coluna } from '@ui';
import { api, type Contrato, type PessoaTerceirizada } from '../../lib/api';
import { useFiltroObra } from '../../lib/ObraAtivaContext';

export function PessoasTab() {
  const navigate = useNavigate();
  const [obraId] = useFiltroObra();
  const [pessoas, setPessoas] = useState<PessoaTerceirizada[]>([]);
  const [contratos, setContratos] = useState<Contrato[]>([]);
  const [erro, setErro] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(true);

  useEffect(() => {
    (async () => {
      try {
        setErro(null);
        const [listaPessoas, listaEmpresas] = await Promise.all([
          api.terceirizados.pessoas.listar(),
          api.terceirizados.empresas.listar(),
        ]);
        const contratosPorEmpresa = await Promise.all(
          listaEmpresas.map((e) => api.terceirizados.contratos.listarPorEmpresa(e.id).catch(() => [] as Contrato[])),
        );
        setPessoas(listaPessoas);
        setContratos(contratosPorEmpresa.flat());
      } catch (e) {
        setErro(e instanceof Error ? e.message : 'Falha ao carregar pessoas terceirizadas.');
      } finally {
        setCarregando(false);
      }
    })();
  }, []);

  const pessoasFiltradas = useMemo(() => {
    if (!obraId) return pessoas;
    const idsContratosNaObra = new Set(contratos.filter((c) => c.obraId === obraId).map((c) => c.id));
    return pessoas.filter((p) => idsContratosNaObra.has(p.contratoId));
  }, [pessoas, contratos, obraId]);

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
        linhas={pessoasFiltradas}
        chaveLinha={(p) => p.trabalhadorId}
        carregando={carregando}
        aoClicarLinha={(p) => navigate(`/pessoas/${p.trabalhadorId}`)}
        vazio={{ titulo: 'Nenhuma pessoa terceirizada cadastrada ainda' }}
      />
    </Card>
  );
}
