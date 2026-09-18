import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  Button,
  Field,
  Input,
  Card,
  PageHeader,
  DataTable,
  PainelCriacaoInline,
  FormGrid,
  FormRodape,
  FormSection,
  Campo,
  FeedbackInline,
  useConfirmar,
  type Coluna,
} from '@ui';
import { Add24Regular, Delete24Regular, Eye24Regular } from '@fluentui/react-icons';
import { api, type Empresa, type NovaEmpresa } from '../../lib/api';
import { useSucessoToast } from '../../hooks/useSucessoToast';

const empresaVazia: NovaEmpresa = {
  razaoSocial: '', nomeFantasia: '', cnpj: '', tipoServicoPrestado: '',
  contatoNome: '', contatoTelefone: '', contatoEmail: '',
};

// CRUD de Empresa terceirizada — mesmo padrão de FuncoesTab.tsx (PainelCriacaoInline em vez de
// drawer). Contratos e pessoas vinculadas ficam na ficha da empresa (EmpresaDetalhePage, Task 15).
export function EmpresasTab() {
  const navigate = useNavigate();
  const [empresas, setEmpresas] = useState<Empresa[]>([]);
  const [novaEmpresa, setNovaEmpresa] = useState<NovaEmpresa>(empresaVazia);
  const [erro, setErro] = useState<string | null>(null);
  const [erroPainel, setErroPainel] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(false);
  const [carregandoLista, setCarregandoLista] = useState(true);
  const [painelAberto, setPainelAberto] = useState(false);
  const { confirmar, dialogElement } = useConfirmar();
  const sucessoToast = useSucessoToast();

  async function carregar() {
    try {
      setErro(null);
      setEmpresas(await api.terceirizados.empresas.listar());
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar empresas.');
    } finally {
      setCarregandoLista(false);
    }
  }

  useEffect(() => {
    carregar();
  }, []);

  function fecharPainel() {
    setPainelAberto(false);
    setErroPainel(null);
  }

  async function criar() {
    try {
      setCarregando(true);
      setErroPainel(null);
      await api.terceirizados.empresas.criar(novaEmpresa);
      setNovaEmpresa(empresaVazia);
      await carregar();
      sucessoToast('Empresa cadastrada com sucesso.');
      fecharPainel();
    } catch (e) {
      setErroPainel(e instanceof Error ? e.message : 'Falha ao cadastrar empresa.');
    } finally {
      setCarregando(false);
    }
  }

  async function excluir(id: string) {
    if (!(await confirmar('Inativar esta empresa? Ela deixa de aparecer para novos contratos.'))) return;
    try {
      await api.terceirizados.empresas.excluir(id);
      await carregar();
      sucessoToast('Empresa inativada com sucesso.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao inativar empresa.');
    }
  }

  const colunas: Coluna<Empresa>[] = [
    { chave: 'razaoSocial', rotulo: 'Razão social' },
    { chave: 'cnpj', rotulo: 'CNPJ' },
    { chave: 'tipoServicoPrestado', rotulo: 'Serviço prestado' },
    { chave: 'status', rotulo: 'Status' },
  ];

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      {dialogElement}
      <PageHeader
        titulo="Empresas terceirizadas"
        acoes={
          <Button
            appearance="primary"
            icon={<Add24Regular />}
            onClick={() => (painelAberto ? fecharPainel() : setPainelAberto(true))}
            aria-expanded={painelAberto}
            aria-controls="painel-nova-empresa"
          >
            {painelAberto ? 'Fechar' : 'Cadastrar empresa'}
          </Button>
        }
      />
      {erro && (
        <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>
          {erro}
        </FeedbackInline>
      )}
      <div id="painel-nova-empresa">
        <PainelCriacaoInline aberto={painelAberto} titulo="Nova empresa terceirizada">
          <FormSection titulo="Dados da empresa" numero={1} primeira>
            {erroPainel && (
              <FeedbackInline tom="erro" aoFechar={() => setErroPainel(null)}>
                {erroPainel}
              </FeedbackInline>
            )}
            <FormGrid>
              <Campo span={6}>
                <Field label="Razão social">
                  <Input value={novaEmpresa.razaoSocial} onChange={(_, d) => setNovaEmpresa({ ...novaEmpresa, razaoSocial: d.value })} />
                </Field>
              </Campo>
              <Campo span={6}>
                <Field label="Nome fantasia">
                  <Input
                    value={novaEmpresa.nomeFantasia ?? ''}
                    onChange={(_, d) => setNovaEmpresa({ ...novaEmpresa, nomeFantasia: d.value })}
                  />
                </Field>
              </Campo>
              <Campo span={4}>
                <Field label="CNPJ (só números)">
                  <Input value={novaEmpresa.cnpj} onChange={(_, d) => setNovaEmpresa({ ...novaEmpresa, cnpj: d.value })} />
                </Field>
              </Campo>
              <Campo span={4}>
                <Field label="Tipo de serviço prestado">
                  <Input
                    value={novaEmpresa.tipoServicoPrestado ?? ''}
                    onChange={(_, d) => setNovaEmpresa({ ...novaEmpresa, tipoServicoPrestado: d.value })}
                  />
                </Field>
              </Campo>
              <Campo span={4}>
                <Field label="Contato — nome">
                  <Input
                    value={novaEmpresa.contatoNome ?? ''}
                    onChange={(_, d) => setNovaEmpresa({ ...novaEmpresa, contatoNome: d.value })}
                  />
                </Field>
              </Campo>
              <Campo span={4}>
                <Field label="Contato — telefone">
                  <Input
                    value={novaEmpresa.contatoTelefone ?? ''}
                    onChange={(_, d) => setNovaEmpresa({ ...novaEmpresa, contatoTelefone: d.value })}
                  />
                </Field>
              </Campo>
              <Campo span={4}>
                <Field label="Contato — e-mail">
                  <Input
                    value={novaEmpresa.contatoEmail ?? ''}
                    onChange={(_, d) => setNovaEmpresa({ ...novaEmpresa, contatoEmail: d.value })}
                  />
                </Field>
              </Campo>
            </FormGrid>
            <FormRodape>
              <Button onClick={fecharPainel}>Cancelar</Button>
              <Button appearance="primary" onClick={criar} disabled={carregando}>
                Cadastrar empresa
              </Button>
            </FormRodape>
          </FormSection>
        </PainelCriacaoInline>
      </div>
      <Card>
        <DataTable
          aria-label="Empresas terceirizadas cadastradas"
          colunas={colunas}
          linhas={empresas}
          chaveLinha={(e) => e.id}
          carregando={carregandoLista}
          vazio={{
            titulo: 'Nenhuma empresa terceirizada cadastrada ainda',
            acao: { rotulo: 'Cadastrar empresa', aoClicar: () => setPainelAberto(true) },
          }}
          acoesLinha={(e) => (
            <>
              <Button
                appearance="subtle"
                icon={<Eye24Regular />}
                onClick={() => navigate(`/terceirizados/empresas/${e.id}`)}
                aria-label="Ver detalhes"
              />
              <Button appearance="subtle" icon={<Delete24Regular />} onClick={() => excluir(e.id)} aria-label="Inativar" />
            </>
          )}
        />
      </Card>
    </div>
  );
}
