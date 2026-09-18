import { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { Button, Card, Carregando, PageHeader, DataTable, FeedbackInline, StatusChip, type Coluna } from '@ui';
import { Open24Regular } from '@fluentui/react-icons';
import { api, type Empresa, type Contrato } from '../../lib/api';

export function EmpresaDetalhePage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const [empresa, setEmpresa] = useState<Empresa | null>(null);
  const [contratos, setContratos] = useState<Contrato[]>([]);
  const [erro, setErro] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(true);

  async function carregar() {
    if (!id) return;
    try {
      setErro(null);
      setCarregando(true);
      const [dadosEmpresa, listaContratos] = await Promise.all([
        api.terceirizados.empresas.obterPorId(id),
        api.terceirizados.contratos.listarPorEmpresa(id),
      ]);
      setEmpresa(dadosEmpresa);
      setContratos(listaContratos);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar a empresa.');
    } finally {
      setCarregando(false);
    }
  }

  useEffect(() => {
    carregar();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [id]);

  const colunas: Coluna<Contrato>[] = [
    { chave: 'numeroContrato', rotulo: 'Nº do contrato' },
    { chave: 'obraNome', rotulo: 'Obra' },
    { chave: 'vigencia', rotulo: 'Vigência', render: (c) => `${c.dataInicioVigencia} a ${c.dataFimVigencia}` },
    {
      chave: 'status',
      rotulo: 'Status',
      render: (c) => (
        <StatusChip tom={c.status === 'Validado' ? 'ok' : c.status === 'Encerrado' ? 'atencao' : 'alerta'}>
          {c.status}
        </StatusChip>
      ),
    },
  ];

  if (!empresa) {
    return erro ? (
      <FeedbackInline tom="erro" acao={{ rotulo: 'Tentar de novo', aoClicar: () => void carregar() }}>
        {erro}
      </FeedbackInline>
    ) : (
      <Carregando variante="detalhe" linhas={6} />
    );
  }

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <PageHeader
        titulo={empresa.razaoSocial}
        subtitulo={`CNPJ ${empresa.cnpj} — ${empresa.status}`}
        voltarPara="/terceirizados"
        rotuloVoltar="Voltar para Terceirizado"
      />
      {erro && <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>{erro}</FeedbackInline>}

      <Card titulo="Contratos">
        <DataTable
          aria-label="Contratos da empresa"
          colunas={colunas}
          linhas={contratos}
          chaveLinha={(c) => c.id}
          carregando={carregando}
          vazio={{ titulo: 'Nenhum contrato recebido do G-Juri ainda para esta empresa' }}
          acoesLinha={(c) => (
            <Button
              appearance="subtle"
              icon={<Open24Regular />}
              onClick={() => navigate(`/terceirizados/contratos/${c.id}`)}
              aria-label="Ver vagas do contrato"
            />
          )}
        />
      </Card>
    </div>
  );
}
