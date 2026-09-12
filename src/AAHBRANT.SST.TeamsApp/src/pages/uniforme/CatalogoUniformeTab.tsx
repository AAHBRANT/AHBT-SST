import { useEffect, useState } from 'react';
import {
  Button,
  Campo,
  Card,
  DataTable,
  Field,
  FeedbackInline,
  FormGrid,
  FormSection,
  Input,
  Legenda,
  PageHeader,
  PainelLateral,
  useConfirmar,
  type Coluna,
} from '@ui';
import { Add24Regular, Delete24Regular, Save24Regular } from '@fluentui/react-icons';
import { api, type CatalogoUniforme, type NovoCatalogoUniforme } from '../../lib/api';
import { useSucessoToast } from '../../hooks/useSucessoToast';
import { SeletorFotoCamera } from '../../components/SeletorFotoCamera';
import { FotoCatalogoUniforme } from './FotoCatalogoUniforme';

const itemVazio: NovoCatalogoUniforme = { nome: '', categoria: '' };

// Catálogo de Uniforme (peça em si, sem tamanho embutido — o tamanho é uma dimensão do estoque e
// do cadastro do trabalhador, ver EstoqueUniformeTab.tsx e pages/pessoas/TamanhosUniformeSecao.tsx).
// Mesmo padrão de CatalogoTab.tsx (EPI), incluindo foto do item (decisão do usuário, 2026-09-07).
// Camada ui/: o formulário de cadastro empurrava a lista pra baixo (padrão do Guia §2) — sai para um
// PainelLateral, mesmo golden rule já aplicado em CatalogoTab.tsx (EPI). Erro do painel isolado do
// erro da lista. Edição inline por linha preservada (clicar na linha edita nela mesma).
export function CatalogoUniformeTab() {
  const [itens, setItens] = useState<CatalogoUniforme[]>([]);
  const [novoItem, setNovoItem] = useState<NovoCatalogoUniforme>(itemVazio);
  const [fotoNovoItem, setFotoNovoItem] = useState<File | null>(null);
  const [edicaoId, setEdicaoId] = useState<string | null>(null);
  const [edicao, setEdicao] = useState<CatalogoUniforme | null>(null);
  const [erro, setErro] = useState<string | null>(null);
  // Erro do painel de criação fica separado do erro da lista — o PainelLateral é um drawer modal,
  // uma mensagem de nível de página apareceria atrás dele, fora do foco do usuário.
  const [erroPainel, setErroPainel] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(false);
  const [carregandoLista, setCarregandoLista] = useState(true);
  const [painelAberto, setPainelAberto] = useState(false);
  const { confirmar, dialogElement } = useConfirmar();
  const sucessoToast = useSucessoToast();

  async function carregar() {
    try {
      setErro(null);
      setItens(await api.catalogosUniforme.listar());
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar catálogo de uniforme.');
    } finally {
      setCarregandoLista(false);
    }
  }

  useEffect(() => {
    carregar();
  }, []);

  // Todo caminho de fechar o painel limpa o erro e o rascunho — senão reabrir mostra dado/mensagem velha.
  function fecharPainel() {
    setPainelAberto(false);
    setErroPainel(null);
    setNovoItem(itemVazio);
    setFotoNovoItem(null);
  }

  async function criar() {
    try {
      setCarregando(true);
      setErroPainel(null);
      const { id } = await api.catalogosUniforme.criar(novoItem);
      if (fotoNovoItem) {
        await api.catalogosUniforme.anexarFoto(id, fotoNovoItem);
      }
      await carregar();
      sucessoToast('Peça de uniforme cadastrada com sucesso.');
      fecharPainel();
    } catch (e) {
      setErroPainel(e instanceof Error ? e.message : 'Falha ao criar peça de uniforme.');
    } finally {
      setCarregando(false);
    }
  }

  async function trocarFoto(itemId: string, arquivo: File) {
    try {
      setErro(null);
      await api.catalogosUniforme.anexarFoto(itemId, arquivo);
      await carregar();
      sucessoToast('Foto da peça atualizada.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao enviar a foto da peça.');
    }
  }

  function iniciarEdicao(item: CatalogoUniforme) {
    setEdicaoId(item.id);
    setEdicao({ ...item });
  }

  async function salvarEdicao() {
    if (!edicao) return;
    try {
      setCarregando(true);
      setErro(null);
      await api.catalogosUniforme.atualizar(edicao);
      setEdicaoId(null);
      setEdicao(null);
      await carregar();
      sucessoToast('Peça de uniforme atualizada com sucesso.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao atualizar peça de uniforme.');
    } finally {
      setCarregando(false);
    }
  }

  async function excluir(id: string) {
    if (!(await confirmar('Excluir esta peça do catálogo de uniforme? Essa ação não pode ser desfeita.'))) return;
    try {
      await api.catalogosUniforme.excluir(id);
      await carregar();
      sucessoToast('Peça de uniforme excluída com sucesso.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao excluir peça de uniforme.');
    }
  }

  const colunas: Coluna<CatalogoUniforme>[] = [
    {
      chave: 'foto',
      rotulo: 'Foto',
      render: (item) =>
        edicaoId === item.id && edicao ? (
          <div style={{ display: 'flex', alignItems: 'center', gap: 6 }} onClick={(ev) => ev.stopPropagation()}>
            <FotoCatalogoUniforme catalogoUniformeId={item.id} temFoto={item.temFoto} tamanho={36} />
            <SeletorFotoCamera
              apenasIcone
              tamanho="small"
              rotulo="Trocar foto"
              tiposAceitos="image/jpeg,image/png"
              aoSelecionarArquivo={(arquivo) => trocarFoto(item.id, arquivo)}
              aoErroValidacao={setErro}
            />
          </div>
        ) : (
          <FotoCatalogoUniforme catalogoUniformeId={item.id} temFoto={item.temFoto} tamanho={36} />
        ),
    },
    {
      chave: 'nome',
      rotulo: 'Nome',
      render: (item) =>
        edicaoId === item.id && edicao ? (
          <Input value={edicao.nome} onChange={(_, d) => setEdicao({ ...edicao, nome: d.value })} />
        ) : (
          item.nome
        ),
    },
    {
      chave: 'categoria',
      rotulo: 'Categoria',
      render: (item) =>
        edicaoId === item.id && edicao ? (
          <Input value={edicao.categoria ?? ''} onChange={(_, d) => setEdicao({ ...edicao, categoria: d.value })} />
        ) : (
          item.categoria
        ),
    },
  ];

  return (
    <>
      <PageHeader
        titulo="Catálogo de Uniforme"
        acoes={
          <Button appearance="primary" icon={<Add24Regular />} onClick={() => setPainelAberto(true)}>
            Nova peça
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
          aria-label="Catálogo de Uniforme"
          colunas={colunas}
          linhas={itens}
          chaveLinha={(i) => i.id}
          carregando={carregandoLista}
          vazio={{
            titulo: 'Nenhuma peça cadastrada no catálogo de uniforme ainda.',
            acao: { rotulo: 'Cadastrar peça', aoClicar: () => setPainelAberto(true) },
          }}
          aoClicarLinha={(item) => {
            if (edicaoId !== item.id) iniciarEdicao(item);
          }}
          acoesLinha={(item) =>
            edicaoId === item.id ? (
              <Button
                appearance="subtle"
                size="small"
                icon={<Save24Regular />}
                onClick={salvarEdicao}
                disabled={carregando}
                aria-label="Salvar"
              />
            ) : (
              <Button
                appearance="subtle"
                size="small"
                icon={<Delete24Regular />}
                onClick={() => excluir(item.id)}
                aria-label="Excluir"
              />
            )
          }
        />
        <Legenda>
          Clique em uma linha para editar. O estoque (por obra e tamanho) é controlado na aba Estoque.
        </Legenda>
      </Card>

      <PainelLateral
        aberto={painelAberto}
        aoFechar={fecharPainel}
        titulo="Nova peça de uniforme"
        subtitulo="Nada é salvo até você adicionar."
        largura="lg"
        rodape={
          <>
            <Button onClick={fecharPainel}>Cancelar</Button>
            <Button
              appearance="primary"
              icon={<Add24Regular />}
              onClick={criar}
              disabled={carregando || !novoItem.nome.trim()}
            >
              Adicionar peça
            </Button>
          </>
        }
      >
        {erroPainel && (
          <FeedbackInline tom="erro" aoFechar={() => setErroPainel(null)}>
            {erroPainel}
          </FeedbackInline>
        )}

        <FormSection titulo="Informações da peça" numero={1} primeira>
          <FormGrid>
            <Campo span={6}>
              <Field label="Nome (ex.: Camisa, Calça, Bota)">
                <Input value={novoItem.nome} onChange={(_, d) => setNovoItem({ ...novoItem, nome: d.value })} />
              </Field>
            </Campo>
            <Campo span={6}>
              <Field label="Categoria (opcional)">
                <Input
                  value={novoItem.categoria ?? ''}
                  onChange={(_, d) => setNovoItem({ ...novoItem, categoria: d.value })}
                />
              </Field>
            </Campo>
            <Campo span={6}>
              <Field label="Foto da peça">
                <SeletorFotoCamera
                  rotulo={fotoNovoItem ? fotoNovoItem.name : 'Tirar foto ou escolher arquivo'}
                  tiposAceitos="image/jpeg,image/png"
                  aoSelecionarArquivo={(arquivo) => setFotoNovoItem(arquivo)}
                  aoErroValidacao={setErroPainel}
                />
              </Field>
            </Campo>
          </FormGrid>
        </FormSection>
      </PainelLateral>

      {dialogElement}
    </>
  );
}
