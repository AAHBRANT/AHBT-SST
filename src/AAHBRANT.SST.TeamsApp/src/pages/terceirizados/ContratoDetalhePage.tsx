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
  useConfirmar,
  type Coluna,
} from '@ui';
import { PersonAdd24Regular, Edit24Regular, DocumentDismiss24Regular } from '@fluentui/react-icons';
import { api, type ContratoDetalhe, type Funcao, type VagaFuncao } from '../../lib/api';
import { useSucessoToast } from '../../hooks/useSucessoToast';
import { formatarCpf } from '../../lib/cpf';
import { FormularioContrato } from './FormularioContrato';
import { paraDadosContrato, type ValorFormularioContrato } from './valorFormularioContrato';

function hojeIso() {
  const agora = new Date();
  return new Date(agora.getTime() - agora.getTimezoneOffset() * 60000).toISOString().slice(0, 10);
}

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
  const { confirmar, dialogElement } = useConfirmar();
  const [editando, setEditando] = useState(false);
  const [funcoes, setFuncoes] = useState<Funcao[]>([]);
  const [erroEdicao, setErroEdicao] = useState<string | null>(null);
  const [salvandoEdicao, setSalvandoEdicao] = useState(false);

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

  async function abrirEdicao() {
    setErroEdicao(null);
    setVagaSelecionada(null);
    setEditando(true);
    if (funcoes.length > 0) return;
    try {
      const lista = await api.funcoes.listar();
      setFuncoes([...lista].sort((a, b) => a.nome.localeCompare(b.nome)));
    } catch (e) {
      setErroEdicao(e instanceof Error ? e.message : 'Falha ao carregar as funções.');
    }
  }

  async function salvarEdicao(valor: ValorFormularioContrato) {
    if (!id) return;
    try {
      setSalvandoEdicao(true);
      setErroEdicao(null);
      await api.terceirizados.contratos.atualizar(id, paraDadosContrato(valor));
      await carregar();
      sucessoToast('Contrato atualizado.');
      setEditando(false);
    } catch (e) {
      setErroEdicao(e instanceof Error ? e.message : 'Falha ao atualizar o contrato.');
    } finally {
      setSalvandoEdicao(false);
    }
  }

  async function encerrarContrato() {
    if (!id || !contrato) return;
    const ok = await confirmar({
      titulo: 'Encerrar contrato',
      mensagem: `Encerrar o contrato ${contrato.numeroContrato} com data de hoje? Depois de encerrado, ele não recebe mais pessoas nem pode ser editado. As pessoas já cadastradas não são desligadas automaticamente.`,
      rotuloConfirmar: 'Encerrar contrato',
    });
    if (!ok) return;
    try {
      setErro(null);
      const { pessoasAtivas } = await api.terceirizados.contratos.encerrar(id, hojeIso());
      await carregar();
      sucessoToast(
        pessoasAtivas > 0
          ? `Contrato encerrado. ${pessoasAtivas} pessoa(s) continuam ativas — faça o desligamento na aba Pessoas.`
          : 'Contrato encerrado.',
      );
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao encerrar o contrato.');
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
        acoes={
          !contrato.origemGJuri && contrato.status === 'Validado' && !editando ? (
            <>
              <Button icon={<DocumentDismiss24Regular />} onClick={() => void encerrarContrato()}>
                Encerrar contrato
              </Button>
              <Button appearance="primary" icon={<Edit24Regular />} onClick={() => void abrirEdicao()}>
                Editar contrato
              </Button>
            </>
          ) : undefined
        }
      />
      {dialogElement}
      {contrato.origemGJuri && (
        <FeedbackInline tom="info">Contrato recebido do G-Juri. Alterações e encerramento são feitos no G-Juri.</FeedbackInline>
      )}
      <PainelCriacaoInline aberto={editando} titulo="Editar contrato">
        {editando && (
          <FormularioContrato
            valorInicial={{
              obraId: contrato.obraId,
              numeroContrato: contrato.numeroContrato,
              dataInicioVigencia: contrato.dataInicioVigencia,
              dataFimVigencia: contrato.dataFimVigencia,
              vagas: contrato.vagas.map((v) => ({ funcaoId: v.funcaoId, quantidade: String(v.quantidadeVagas) })),
            }}
            funcoes={funcoes}
            preenchidasPorFuncao={Object.fromEntries(contrato.vagas.map((v) => [v.funcaoId, v.quantidadePreenchidas]))}
            erro={erroEdicao}
            aoFecharErro={() => setErroEdicao(null)}
            salvando={salvandoEdicao}
            rotuloSalvar="Salvar alterações"
            aoSalvar={(valor) => void salvarEdicao(valor)}
            aoCancelar={() => setEditando(false)}
          />
        )}
      </PainelCriacaoInline>
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
              disabled={v.quantidadePreenchidas >= v.quantidadeVagas || contrato.status !== 'Validado'}
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
