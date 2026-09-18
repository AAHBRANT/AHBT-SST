import { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import {
  Button,
  Card,
  Carregando,
  PageHeader,
  DataTable,
  FeedbackInline,
  StatusChip,
  Text,
  PainelCriacaoInline,
  FormGrid,
  FormRodape,
  FormSection,
  Campo,
  Field,
  Input,
  type Coluna,
} from '@ui';
import { Open24Regular, Edit24Regular } from '@fluentui/react-icons';
import { api, type Empresa, type Contrato, type AtualizarEmpresa } from '../../lib/api';
import { useSucessoToast } from '../../hooks/useSucessoToast';

type EdicaoEmpresa = Omit<AtualizarEmpresa, 'id' | 'status'>;

function paraEdicao(empresa: Empresa): EdicaoEmpresa {
  return {
    razaoSocial: empresa.razaoSocial,
    nomeFantasia: empresa.nomeFantasia,
    cnpj: empresa.cnpj,
    tipoServicoPrestado: empresa.tipoServicoPrestado,
    contatoNome: empresa.contatoNome,
    contatoTelefone: empresa.contatoTelefone,
    contatoEmail: empresa.contatoEmail,
  };
}

export function EmpresaDetalhePage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const [empresa, setEmpresa] = useState<Empresa | null>(null);
  const [contratos, setContratos] = useState<Contrato[]>([]);
  const [erro, setErro] = useState<string | null>(null);
  const [erroPainel, setErroPainel] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(true);
  const [painelAberto, setPainelAberto] = useState(false);
  const [salvando, setSalvando] = useState(false);
  const [edicao, setEdicao] = useState<EdicaoEmpresa | null>(null);
  const sucessoToast = useSucessoToast();

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

  function abrirEdicao() {
    if (!empresa) return;
    setEdicao(paraEdicao(empresa));
    setErroPainel(null);
    setPainelAberto(true);
  }

  function fecharEdicao() {
    setPainelAberto(false);
    setErroPainel(null);
  }

  async function salvar() {
    if (!empresa || !edicao) return;
    try {
      setSalvando(true);
      setErroPainel(null);
      await api.terceirizados.empresas.atualizar({
        id: empresa.id,
        ...edicao,
        status: empresa.status === 'Ativa' ? 1 : 2,
      });
      await carregar();
      sucessoToast('Empresa atualizada com sucesso.');
      fecharEdicao();
    } catch (e) {
      setErroPainel(e instanceof Error ? e.message : 'Falha ao atualizar empresa.');
    } finally {
      setSalvando(false);
    }
  }

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
        acoes={
          <Button
            appearance="primary"
            icon={<Edit24Regular />}
            onClick={() => (painelAberto ? fecharEdicao() : abrirEdicao())}
            aria-expanded={painelAberto}
            aria-controls="painel-edicao-empresa"
          >
            {painelAberto ? 'Fechar' : 'Editar'}
          </Button>
        }
      />
      {erro && <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>{erro}</FeedbackInline>}

      <div id="painel-edicao-empresa">
        <PainelCriacaoInline aberto={painelAberto && !!edicao} titulo="Editar empresa terceirizada">
          {edicao && (
            <FormSection titulo="Dados da empresa" numero={1} primeira>
              {erroPainel && (
                <FeedbackInline tom="erro" aoFechar={() => setErroPainel(null)}>
                  {erroPainel}
                </FeedbackInline>
              )}
              <FormGrid>
                <Campo span={6}>
                  <Field label="Razão social">
                    <Input value={edicao.razaoSocial} onChange={(_, d) => setEdicao({ ...edicao, razaoSocial: d.value })} />
                  </Field>
                </Campo>
                <Campo span={6}>
                  <Field label="Nome fantasia">
                    <Input
                      value={edicao.nomeFantasia ?? ''}
                      onChange={(_, d) => setEdicao({ ...edicao, nomeFantasia: d.value })}
                    />
                  </Field>
                </Campo>
                <Campo span={4}>
                  <Field label="CNPJ (só números)">
                    <Input
                      value={edicao.cnpj}
                      onChange={(_, d) => setEdicao({ ...edicao, cnpj: d.value.replace(/\D/g, '').slice(0, 14) })}
                    />
                  </Field>
                </Campo>
                <Campo span={4}>
                  <Field label="Tipo de serviço prestado">
                    <Input
                      value={edicao.tipoServicoPrestado ?? ''}
                      onChange={(_, d) => setEdicao({ ...edicao, tipoServicoPrestado: d.value })}
                    />
                  </Field>
                </Campo>
                <Campo span={4}>
                  <Field label="Contato — nome">
                    <Input
                      value={edicao.contatoNome ?? ''}
                      onChange={(_, d) => setEdicao({ ...edicao, contatoNome: d.value })}
                    />
                  </Field>
                </Campo>
                <Campo span={4}>
                  <Field label="Contato — telefone">
                    <Input
                      value={edicao.contatoTelefone ?? ''}
                      onChange={(_, d) => setEdicao({ ...edicao, contatoTelefone: d.value })}
                    />
                  </Field>
                </Campo>
                <Campo span={4}>
                  <Field label="Contato — e-mail">
                    <Input
                      value={edicao.contatoEmail ?? ''}
                      onChange={(_, d) => setEdicao({ ...edicao, contatoEmail: d.value })}
                    />
                  </Field>
                </Campo>
              </FormGrid>
              <FormRodape>
                <Button onClick={fecharEdicao}>Cancelar</Button>
                <Button appearance="primary" onClick={salvar} disabled={salvando}>
                  Salvar alterações
                </Button>
              </FormRodape>
            </FormSection>
          )}
        </PainelCriacaoInline>
      </div>

      <Card titulo="Dados cadastrais">
        <div style={{ display: 'flex', gap: 20, flexWrap: 'wrap', alignItems: 'center' }}>
          <Text>
            <strong>Nome fantasia:</strong> {empresa.nomeFantasia || '—'}
          </Text>
          <Text>
            <strong>Tipo de serviço prestado:</strong> {empresa.tipoServicoPrestado || '—'}
          </Text>
          <Text>
            <strong>Contato:</strong> {empresa.contatoNome || '—'}
          </Text>
          <Text>
            <strong>Telefone:</strong> {empresa.contatoTelefone || '—'}
          </Text>
          <Text>
            <strong>E-mail:</strong> {empresa.contatoEmail || '—'}
          </Text>
        </div>
      </Card>

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
