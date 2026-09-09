import { useEffect, useState } from 'react';
import {
  Button,
  Card,
  Campo,
  DataTable,
  Field,
  FeedbackInline,
  FormGrid,
  Input,
  PageHeader,
  PainelLateral,
  Select,
  useConfirmar,
  type Coluna,
} from '@ui';
import { Add24Regular, Delete24Regular } from '@fluentui/react-icons';
import { api, type Atividade, type NovaAtividade, type Obra } from '../../lib/api';
import { useSucessoToast } from '../../hooks/useSucessoToast';

const atividadeVazia: NovaAtividade = { obraId: '', nome: '', descricao: '' };

// Camada ui/ (Onda 2, Task 13): mesmo padrão de FuncoesTab.tsx (Task 1) — formulário de 3 campos que
// empurrava a lista pra baixo saiu para um PainelLateral aberto pelo "+ Adicionar atividade" do
// PageHeader. Erro do formulário fica em estado próprio, separado do erro de carga da lista.
export function AtividadesTab() {
  const [atividades, setAtividades] = useState<Atividade[]>([]);
  const [obras, setObras] = useState<Obra[]>([]);
  const [novaAtividade, setNovaAtividade] = useState<NovaAtividade>(atividadeVazia);
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
      const [ativs, obrs] = await Promise.all([api.atividades.listar(), api.obras.listar()]);
      setAtividades(ativs);
      setObras(obrs);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar atividades.');
    } finally {
      setCarregandoLista(false);
    }
  }

  useEffect(() => {
    carregar();
  }, []);

  function nomeObra(id: string) {
    return obras.find((o) => o.id === id)?.nome ?? id;
  }

  function fecharPainel() {
    setPainelAberto(false);
    setErroPainel(null);
  }

  async function criar() {
    try {
      setCarregando(true);
      setErroPainel(null);
      await api.atividades.criar(novaAtividade);
      setNovaAtividade(atividadeVazia);
      await carregar();
      sucessoToast('Atividade criada com sucesso.');
      fecharPainel();
    } catch (e) {
      setErroPainel(e instanceof Error ? e.message : 'Falha ao criar atividade.');
    } finally {
      setCarregando(false);
    }
  }

  async function excluir(id: string) {
    if (!(await confirmar('Excluir esta atividade? Essa ação não pode ser desfeita.'))) return;
    try {
      await api.atividades.excluir(id);
      await carregar();
      sucessoToast('Atividade excluída com sucesso.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao excluir atividade.');
    }
  }

  const colunas: Coluna<Atividade>[] = [
    { chave: 'nome', rotulo: 'Nome' },
    { chave: 'obra', rotulo: 'Obra', render: (a) => nomeObra(a.obraId) },
    { chave: 'descricao', rotulo: 'Descrição' },
  ];

  return (
    <div>
      {dialogElement}
      <PageHeader
        titulo="Atividades cadastradas"
        acoes={
          <Button appearance="primary" icon={<Add24Regular />} onClick={() => setPainelAberto(true)}>
            Adicionar atividade
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
          aria-label="Atividades cadastradas"
          colunas={colunas}
          linhas={atividades}
          chaveLinha={(a) => a.id}
          carregando={carregandoLista}
          vazio={{
            titulo: 'Nenhuma atividade cadastrada ainda',
            acao: { rotulo: 'Adicionar atividade', aoClicar: () => setPainelAberto(true) },
          }}
          acoesLinha={(a) => (
            <Button appearance="subtle" icon={<Delete24Regular />} onClick={() => excluir(a.id)} aria-label="Excluir" />
          )}
        />
      </Card>
      <PainelLateral
        aberto={painelAberto}
        aoFechar={fecharPainel}
        titulo="Nova atividade"
        rodape={
          <>
            <Button onClick={fecharPainel}>Cancelar</Button>
            <Button appearance="primary" onClick={criar} disabled={carregando}>
              Adicionar atividade
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
          <Campo span={3}>
            <Field label="Obra">
              <Select
                value={novaAtividade.obraId}
                onChange={(_, d) => setNovaAtividade({ ...novaAtividade, obraId: d.value })}
              >
                <option value="">Selecione</option>
                {obras.map((obra) => (
                  <option key={obra.id} value={obra.id}>
                    {obra.nome}
                  </option>
                ))}
              </Select>
            </Field>
          </Campo>
          <Campo span={4}>
            <Field label="Nome da atividade">
              <Input
                value={novaAtividade.nome}
                onChange={(_, d) => setNovaAtividade({ ...novaAtividade, nome: d.value })}
              />
            </Field>
          </Campo>
          <Campo span={5}>
            <Field label="Descrição">
              <Input
                value={novaAtividade.descricao ?? ''}
                onChange={(_, d) => setNovaAtividade({ ...novaAtividade, descricao: d.value })}
              />
            </Field>
          </Campo>
        </FormGrid>
      </PainelLateral>
    </div>
  );
}
