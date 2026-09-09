import { useEffect, useState } from 'react';
import {
  Button,
  Field,
  Input,
  Textarea,
  Card,
  PageHeader,
  DataTable,
  FeedbackInline,
  PainelLateral,
  FormSection,
  FormGrid,
  Campo,
  useConfirmar,
  type Coluna,
} from '@ui';
import { Add24Regular, Delete24Regular, Edit24Regular } from '@fluentui/react-icons';
import { api, type CatalogoTemaDds } from '../../lib/api';
import { useSucessoToast } from '../../hooks/useSucessoToast';

// Catálogo de temas livres de DDS (ex.: "Outubro Amarelo") — administração própria, separada da
// tela de conduzir o DDS do dia (DdsSemanalDetalhePage só lista/seleciona um tema já cadastrado).
// Onda 2 (Task 14): conversões 1 (Table → DataTable), 4 (erro → FeedbackInline), 6
// (useConfirmarExclusao → useConfirmar). O formulário de criação/edição saía empurrando a tabela
// para baixo (mesmo padrão do Guia §2) — mesmo julgamento já aplicado em FuncoesTab.tsx/AprsTab.tsx:
// sai para um PainelLateral aberto pelo "+ Novo tema", com a lista sempre visível atrás.
export function CatalogoTemasDdsPage() {
  const [temas, setTemas] = useState<CatalogoTemaDds[]>([]);
  const [nome, setNome] = useState('');
  const [descricao, setDescricao] = useState('');
  const [editandoId, setEditandoId] = useState<string | null>(null);
  const [erro, setErro] = useState<string | null>(null);
  // Erro do painel é estado próprio — nunca reaproveita o erro de nível de página (aprendizado
  // da Onda 1: um erro atrás do backdrop do painel nunca é visto pelo usuário).
  const [erroPainel, setErroPainel] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(false);
  const [carregandoLista, setCarregandoLista] = useState(true);
  const [painelAberto, setPainelAberto] = useState(false);
  const { confirmar, dialogElement } = useConfirmar();
  const sucessoToast = useSucessoToast();

  async function carregar() {
    try {
      setErro(null);
      setCarregandoLista(true);
      setTemas(await api.catalogoTemasDds.listar());
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar os temas de DDS.');
    } finally {
      setCarregandoLista(false);
    }
  }

  useEffect(() => {
    carregar();
  }, []);

  function limparFormulario() {
    setNome('');
    setDescricao('');
    setEditandoId(null);
    setErroPainel(null);
  }

  // Todo caminho de fechar o painel (X, Cancelar) limpa formulário + erro — nenhum deles é só
  // "concluir com sucesso" (aprendizado da Onda 1).
  function fecharPainel() {
    setPainelAberto(false);
    limparFormulario();
  }

  function iniciarCriacao() {
    limparFormulario();
    setPainelAberto(true);
  }

  function iniciarEdicao(tema: CatalogoTemaDds) {
    setEditandoId(tema.id);
    setNome(tema.nome);
    setDescricao(tema.descricao ?? '');
    setErroPainel(null);
    setPainelAberto(true);
  }

  async function salvar() {
    if (!nome.trim()) {
      setErroPainel('Informe o nome do tema.');
      return;
    }
    try {
      setCarregando(true);
      setErroPainel(null);
      if (editandoId) {
        await api.catalogoTemasDds.atualizar(editandoId, nome.trim(), descricao.trim() || null);
        sucessoToast('Tema atualizado com sucesso.');
      } else {
        await api.catalogoTemasDds.criar(nome.trim(), descricao.trim() || null);
        sucessoToast('Tema criado com sucesso.');
      }
      setPainelAberto(false);
      limparFormulario();
      await carregar();
    } catch (e) {
      setErroPainel(e instanceof Error ? e.message : 'Falha ao salvar o tema.');
    } finally {
      setCarregando(false);
    }
  }

  async function excluir(id: string) {
    if (!(await confirmar('Excluir este tema de DDS? Essa ação não pode ser desfeita.'))) return;
    try {
      await api.catalogoTemasDds.excluir(id);
      await carregar();
      sucessoToast('Tema excluído com sucesso.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao excluir o tema.');
    }
  }

  const colunas: Coluna<CatalogoTemaDds>[] = [
    { chave: 'nome', rotulo: 'Nome' },
    { chave: 'descricao', rotulo: 'Descrição', render: (t) => t.descricao ?? '—' },
  ];

  return (
    <div>
      {dialogElement}
      <PageHeader
        titulo="Temas de DDS"
        acoes={
          <Button appearance="primary" icon={<Add24Regular />} onClick={iniciarCriacao}>
            Novo tema
          </Button>
        }
      />

      {erro && (
        <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>
          {erro}
        </FeedbackInline>
      )}

      <Card densidade="compacta">
        <DataTable
          aria-label="Temas de DDS cadastrados"
          colunas={colunas}
          linhas={temas}
          chaveLinha={(t) => t.id}
          carregando={carregandoLista}
          vazio={{
            titulo: 'Nenhum tema de DDS cadastrado ainda.',
            acao: { rotulo: 'Novo tema', aoClicar: iniciarCriacao },
          }}
          acoesLinha={(t) => (
            <>
              <Button appearance="subtle" size="small" icon={<Edit24Regular />} onClick={() => iniciarEdicao(t)} aria-label="Editar" />
              <Button appearance="subtle" size="small" icon={<Delete24Regular />} onClick={() => excluir(t.id)} aria-label="Excluir" />
            </>
          )}
        />
      </Card>

      <PainelLateral
        aberto={painelAberto}
        aoFechar={fecharPainel}
        titulo={editandoId ? 'Editar tema' : 'Novo tema de DDS'}
        rodape={
          <>
            <Button onClick={fecharPainel}>Cancelar</Button>
            <Button appearance="primary" onClick={salvar} disabled={carregando}>
              {editandoId ? 'Salvar alterações' : 'Criar tema'}
            </Button>
          </>
        }
      >
        {erroPainel && (
          <FeedbackInline tom="erro" aoFechar={() => setErroPainel(null)}>
            {erroPainel}
          </FeedbackInline>
        )}

        <FormSection titulo="Dados do tema" numero={1} primeira>
          <FormGrid>
            <Campo span={6}>
              <Field label="Nome" required>
                <Input value={nome} onChange={(_, d) => setNome(d.value)} />
              </Field>
            </Campo>
            <Campo span={12}>
              <Field label="Descrição">
                <Textarea
                  value={descricao}
                  onChange={(_, d) => setDescricao(d.value)}
                  resize="vertical"
                  rows={10}
                  style={{ width: '100%' }}
                />
              </Field>
            </Campo>
          </FormGrid>
        </FormSection>
      </PainelLateral>
    </div>
  );
}
