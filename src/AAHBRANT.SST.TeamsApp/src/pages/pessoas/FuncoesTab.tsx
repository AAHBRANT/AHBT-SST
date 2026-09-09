import { useEffect, useState } from 'react';
import {
  Button,
  Field,
  Input,
  Card,
  PageHeader,
  DataTable,
  PainelLateral,
  FormGrid,
  Campo,
  FeedbackInline,
  useConfirmar,
  type Coluna,
} from '@ui';
import { Add24Regular, Delete24Regular } from '@fluentui/react-icons';
import { api, type Funcao, type NovaFuncao } from '../../lib/api';
import { useSucessoToast } from '../../hooks/useSucessoToast';

const funcaoVazia: NovaFuncao = { nome: '', cboCodigo: '', descricao: '' };

// A matriz de EPI por função fica no módulo EPI (ver MatrizEpiTab.tsx em pages/epi) — aqui é só o
// cadastro (CRUD) da função em si, usado também por Trabalhadores/Equipes. Camada ui/ (Onda 2,
// Task 1): formulário de criação saiu de cima da tabela (empurrava a lista pra baixo) e foi para um
// PainelLateral, aberto pelo "+ Adicionar função" do PageHeader — mesmo padrão do piloto 1
// (EntregasTab). Erro do formulário fica em estado próprio, separado do erro de carga da lista.
export function FuncoesTab() {
  const [funcoes, setFuncoes] = useState<Funcao[]>([]);
  const [novaFuncao, setNovaFuncao] = useState<NovaFuncao>(funcaoVazia);
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
      const listaFuncoes = await api.funcoes.listar();
      setFuncoes(listaFuncoes);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar funções.');
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
      await api.funcoes.criar(novaFuncao);
      setNovaFuncao(funcaoVazia);
      await carregar();
      sucessoToast('Função criada com sucesso.');
      fecharPainel();
    } catch (e) {
      setErroPainel(e instanceof Error ? e.message : 'Falha ao criar função.');
    } finally {
      setCarregando(false);
    }
  }

  async function excluir(id: string) {
    if (!(await confirmar('Excluir esta função? Essa ação não pode ser desfeita.'))) return;
    try {
      await api.funcoes.excluir(id);
      await carregar();
      sucessoToast('Função excluída com sucesso.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao excluir função.');
    }
  }

  const colunas: Coluna<Funcao>[] = [
    { chave: 'nome', rotulo: 'Nome' },
    { chave: 'cboCodigo', rotulo: 'CBO' },
    { chave: 'descricao', rotulo: 'Descrição' },
  ];

  return (
    <div>
      {dialogElement}
      <PageHeader
        titulo="Funções cadastradas"
        acoes={
          <Button appearance="primary" icon={<Add24Regular />} onClick={() => setPainelAberto(true)}>
            Adicionar função
          </Button>
        }
      />
      {erro && (
        <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>
          {erro}
        </FeedbackInline>
      )}
      <Card>
        <DataTable
          aria-label="Funções cadastradas"
          colunas={colunas}
          linhas={funcoes}
          chaveLinha={(f) => f.id}
          carregando={carregandoLista}
          vazio={{
            titulo: 'Nenhuma função cadastrada ainda',
            acao: { rotulo: 'Adicionar função', aoClicar: () => setPainelAberto(true) },
          }}
          acoesLinha={(f) => (
            <Button appearance="subtle" icon={<Delete24Regular />} onClick={() => excluir(f.id)} aria-label="Excluir" />
          )}
        />
      </Card>
      <PainelLateral
        aberto={painelAberto}
        aoFechar={fecharPainel}
        titulo="Nova função"
        subtitulo="A matriz de EPI de cada função é definida em EPI → Matriz de EPI por Função."
        rodape={
          <>
            <Button onClick={fecharPainel}>Cancelar</Button>
            <Button appearance="primary" onClick={criar} disabled={carregando}>
              Adicionar função
            </Button>
          </>
        }
      >
        {erroPainel && (
          <FeedbackInline tom="erro" aoFechar={() => setErroPainel(null)}>
            {erroPainel}
          </FeedbackInline>
        )}
        <FormGrid>
          <Campo span={6}>
            <Field label="Nome">
              <Input value={novaFuncao.nome} onChange={(_, d) => setNovaFuncao({ ...novaFuncao, nome: d.value })} />
            </Field>
          </Campo>
          <Campo span={3}>
            <Field label="Código CBO">
              <Input
                value={novaFuncao.cboCodigo ?? ''}
                onChange={(_, d) => setNovaFuncao({ ...novaFuncao, cboCodigo: d.value })}
              />
            </Field>
          </Campo>
          <Campo span={3}>
            <Field label="Descrição">
              <Input
                value={novaFuncao.descricao ?? ''}
                onChange={(_, d) => setNovaFuncao({ ...novaFuncao, descricao: d.value })}
              />
            </Field>
          </Campo>
        </FormGrid>
      </PainelLateral>
    </div>
  );
}
