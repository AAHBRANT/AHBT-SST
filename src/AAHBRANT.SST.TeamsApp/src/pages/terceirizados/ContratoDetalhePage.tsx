import { useEffect, useState } from 'react';
import { useParams } from 'react-router-dom';
import {
  Button,
  Card,
  Carregando,
  PageHeader,
  DataTable,
  PainelCriacaoInline,
  FormGrid,
  FormRodape,
  FormSection,
  Campo,
  Field,
  Input,
  FeedbackInline,
  type Coluna,
} from '@ui';
import { PersonAdd24Regular } from '@fluentui/react-icons';
import { api, type ContratoDetalhe, type VagaFuncao } from '../../lib/api';
import { useSucessoToast } from '../../hooks/useSucessoToast';
import { formatarCpf } from '../../lib/cpf';

const novaPessoaVazia = { nome: '', matricula: '', cpf: '', dataAdmissao: '' };

// Cadastro de pessoa terceirizada numa vaga do contrato (docs/superpowers/specs/2026-09-18-modulo-
// terceirizado-design.md §9) — dispara a automação de EPI no backend (reserva/alerta de estoque);
// aqui só mostramos o resultado (a vaga preenchida sobe de contagem) e um aviso de sucesso.
export function ContratoDetalhePage() {
  const { id } = useParams<{ id: string }>();
  const [contrato, setContrato] = useState<ContratoDetalhe | null>(null);
  const [erro, setErro] = useState<string | null>(null);
  const [erroPainel, setErroPainel] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(true);
  const [salvando, setSalvando] = useState(false);
  const [vagaSelecionada, setVagaSelecionada] = useState<VagaFuncao | null>(null);
  const [novaPessoa, setNovaPessoa] = useState(novaPessoaVazia);
  const sucessoToast = useSucessoToast();

  async function carregar() {
    if (!id) return;
    try {
      setErro(null);
      setCarregando(true);
      setContrato(await api.terceirizados.contratos.obterDetalhe(id));
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar o contrato.');
    } finally {
      setCarregando(false);
    }
  }

  useEffect(() => {
    carregar();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [id]);

  function fecharPainel() {
    setVagaSelecionada(null);
    setNovaPessoa(novaPessoaVazia);
    setErroPainel(null);
  }

  async function cadastrarPessoa() {
    if (!id || !vagaSelecionada) return;
    try {
      setSalvando(true);
      setErroPainel(null);
      await api.terceirizados.contratos.cadastrarPessoaNaVaga(id, vagaSelecionada.funcaoId, novaPessoa);
      await carregar();
      sucessoToast('Pessoa cadastrada — verifique EPI/treinamentos pendentes na aba Pessoas.');
      fecharPainel();
    } catch (e) {
      setErroPainel(e instanceof Error ? e.message : 'Falha ao cadastrar pessoa nesta vaga.');
    } finally {
      setSalvando(false);
    }
  }

  const colunas: Coluna<VagaFuncao>[] = [
    { chave: 'funcaoNome', rotulo: 'Função' },
    {
      chave: 'vagas',
      rotulo: 'Vagas',
      render: (v) => `${v.quantidadePreenchidas} / ${v.quantidadeVagas}`,
    },
  ];

  if (!contrato) {
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
        titulo={`Contrato ${contrato.numeroContrato}`}
        subtitulo={`${contrato.empresaRazaoSocial} — Obra ${contrato.obraNome} — ${contrato.status}`}
        voltarPara={`/terceirizados/empresas/${contrato.empresaId}`}
        rotuloVoltar="Voltar para a empresa"
      />
      {erro && <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>{erro}</FeedbackInline>}

      {vagaSelecionada && (
        <PainelCriacaoInline aberto titulo={`Cadastrar pessoa — ${vagaSelecionada.funcaoNome}`}>
          <FormSection titulo="Dados da pessoa" numero={1} primeira>
            {erroPainel && (
              <FeedbackInline tom="erro" aoFechar={() => setErroPainel(null)}>
                {erroPainel}
              </FeedbackInline>
            )}
            <FormGrid>
              <Campo span={6}>
                <Field label="Nome">
                  <Input value={novaPessoa.nome} onChange={(_, d) => setNovaPessoa({ ...novaPessoa, nome: d.value })} />
                </Field>
              </Campo>
              <Campo span={3}>
                <Field label="Matrícula">
                  <Input value={novaPessoa.matricula} onChange={(_, d) => setNovaPessoa({ ...novaPessoa, matricula: d.value })} />
                </Field>
              </Campo>
              <Campo span={3}>
                <Field label="CPF (só números)">
                  <Input
                    value={formatarCpf(novaPessoa.cpf)}
                    onChange={(_, d) => setNovaPessoa({ ...novaPessoa, cpf: d.value.replace(/\D/g, '').slice(0, 11) })}
                  />
                </Field>
              </Campo>
              <Campo span={4}>
                <Field label="Data de admissão">
                  <Input
                    type="date"
                    value={novaPessoa.dataAdmissao}
                    onChange={(_, d) => setNovaPessoa({ ...novaPessoa, dataAdmissao: d.value })}
                  />
                </Field>
              </Campo>
            </FormGrid>
            <FormRodape info="EPI e treinamento obrigatórios da função são verificados automaticamente após o cadastro.">
              <Button onClick={fecharPainel}>Cancelar</Button>
              <Button appearance="primary" onClick={cadastrarPessoa} disabled={salvando}>
                Cadastrar pessoa
              </Button>
            </FormRodape>
          </FormSection>
        </PainelCriacaoInline>
      )}

      <Card titulo="Vagas por função">
        <DataTable
          aria-label="Vagas do contrato"
          colunas={colunas}
          linhas={contrato.vagas}
          chaveLinha={(v) => v.id}
          carregando={carregando}
          vazio={{ titulo: 'Este contrato não tem vagas cadastradas' }}
          acoesLinha={(v) => (
            <Button
              appearance="subtle"
              icon={<PersonAdd24Regular />}
              disabled={v.quantidadePreenchidas >= v.quantidadeVagas}
              onClick={() => setVagaSelecionada(v)}
              aria-label="Cadastrar pessoa nesta vaga"
            >
              Cadastrar pessoa
            </Button>
          )}
        />
      </Card>
    </div>
  );
}
