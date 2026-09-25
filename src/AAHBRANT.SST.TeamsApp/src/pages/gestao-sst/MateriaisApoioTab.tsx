import { useEffect, useState } from 'react';
import {
  Button,
  Card,
  Campo,
  DataTable,
  Field,
  FeedbackInline,
  FormGrid,
  FormRodape,
  FormSection,
  Input,
  PageHeader,
  PainelCriacaoInline,
  useConfirmar,
  type Coluna,
} from '@ui';
import {
  Add24Regular,
  ArrowDownload24Regular,
  Delete24Regular,
  Document24Regular,
  DocumentPdf24Regular,
  Eye24Regular,
  Image24Regular,
} from '@fluentui/react-icons';
import { api, type MaterialApoio } from '../../lib/api';
import { useSucessoToast } from '../../hooks/useSucessoToast';
import { salvarBlob, useVisualizadorPdf } from '../../components/useVisualizadorPdf';

function formatarTamanho(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(0)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}

function IconePorTipo({ contentType }: { contentType: string }) {
  if (contentType.startsWith('image/')) return <Image24Regular />;
  if (contentType === 'application/pdf') return <DocumentPdf24Regular />;
  return <Document24Regular />;
}

// A janela de visualização só desenha PDF (pdf.js) ou imagem; Word e demais tipos ficam só com "Baixar".
function podeVisualizar(material: MaterialApoio) {
  return material.contentType.startsWith('image/') || material.contentType === 'application/pdf';
}

// Aba "Documentos & Procedimentos" de Gestão de SST (pedido do usuário, 2026-09-09): catálogo
// global de materiais de apoio (pôsteres de sinalização, instruções técnicas). Espaço reservado no
// menu desde a remoção do módulo de Gestão Documental em 28/08 — deliberadamente mais simples que
// aquele: sem workflow de aprovação/versão, só nome + categoria + arquivo (ver MaterialApoio.cs).
// Onda A do spec de formulário inline (2026-09-11): os dois PainelLateral (upload e preview) saem —
// mesmo padrão de AtividadesTab.tsx/InspecoesTab.tsx. Agora são PainelCriacaoInline, que cresce
// acima da lista só até a altura do próprio conteúdo, em vez de cobrir a tela com uma gaveta.
export function MateriaisApoioTab() {
  const [materiais, setMateriais] = useState<MaterialApoio[]>([]);
  const [carregandoLista, setCarregandoLista] = useState(true);
  const [erro, setErro] = useState<string | null>(null);

  const [painelUploadAberto, setPainelUploadAberto] = useState(false);
  const [nome, setNome] = useState('');
  const [categoria, setCategoria] = useState('');
  const [arquivo, setArquivo] = useState<File | null>(null);
  const [enviando, setEnviando] = useState(false);
  const [erroPainel, setErroPainel] = useState<string | null>(null);

  const [baixandoId, setBaixandoId] = useState<string | null>(null);

  const { confirmar, dialogElement } = useConfirmar();
  const { visualizar: abrirVisualizador, dialogoVisualizador } = useVisualizadorPdf();
  const sucessoToast = useSucessoToast();

  async function carregar() {
    try {
      setErro(null);
      setMateriais(await api.materiaisApoio.listar());
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar materiais de apoio.');
    } finally {
      setCarregandoLista(false);
    }
  }

  useEffect(() => {
    carregar();
  }, []);

  function fecharPainelUpload() {
    setPainelUploadAberto(false);
    setErroPainel(null);
    setNome('');
    setCategoria('');
    setArquivo(null);
  }

  async function enviar() {
    if (!nome.trim()) {
      setErroPainel('Informe o nome do material.');
      return;
    }
    if (!arquivo) {
      setErroPainel('Selecione um arquivo.');
      return;
    }
    try {
      setEnviando(true);
      setErroPainel(null);
      await api.materiaisApoio.criar(nome, categoria.trim() || null, arquivo);
      fecharPainelUpload();
      await carregar();
      sucessoToast('Material enviado com sucesso.');
    } catch (e) {
      setErroPainel(e instanceof Error ? e.message : 'Falha ao enviar o material.');
    } finally {
      setEnviando(false);
    }
  }

  async function excluir(material: MaterialApoio) {
    if (!(await confirmar(`Excluir "${material.nome}"? Essa ação não pode ser desfeita.`))) return;
    try {
      await api.materiaisApoio.excluir(material.id);
      await carregar();
      sucessoToast('Material excluído com sucesso.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao excluir material.');
    }
  }

  // Janela única de visualização (useVisualizadorPdf): o <iframe> do antigo painel de preview
  // ficava em branco no Teams. Busca sob demanda porque o endpoint exige autenticação.
  function visualizar(material: MaterialApoio) {
    if (!podeVisualizar(material)) return;
    void abrirVisualizador({
      titulo: material.nome,
      nomeArquivo: material.nomeArquivo,
      obter: () => api.materiaisApoio.baixarConteudo(material.id),
    });
  }

  async function baixar(material: MaterialApoio) {
    try {
      setBaixandoId(material.id);
      salvarBlob(await api.materiaisApoio.baixarConteudo(material.id), material.nomeArquivo);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao baixar o material.');
    } finally {
      setBaixandoId(null);
    }
  }

  const colunas: Coluna<MaterialApoio>[] = [
    { chave: 'tipo', rotulo: '', render: (m) => <IconePorTipo contentType={m.contentType} /> },
    { chave: 'nome', rotulo: 'Nome' },
    { chave: 'categoria', rotulo: 'Categoria', render: (m) => m.categoria || '—' },
    { chave: 'tamanhoBytes', rotulo: 'Tamanho', render: (m) => formatarTamanho(m.tamanhoBytes) },
    { chave: 'createdAtUtc', rotulo: 'Enviado em', render: (m) => new Date(m.createdAtUtc).toLocaleDateString('pt-BR') },
  ];

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      {dialogElement}
      {dialogoVisualizador}
      <PageHeader
        titulo="Documentos & Procedimentos"
        subtitulo="Materiais de apoio (sinalização, instruções técnicas) disponíveis para consulta e download."
        acoes={
          <Button
            appearance="primary"
            icon={<Add24Regular />}
            onClick={() => (painelUploadAberto ? fecharPainelUpload() : setPainelUploadAberto(true))}
            aria-expanded={painelUploadAberto}
            aria-controls="painel-novo-material-apoio"
          >
            {painelUploadAberto ? 'Fechar' : 'Enviar material'}
          </Button>
        }
      />

      {erro && (
        <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>
          {erro}
        </FeedbackInline>
      )}

      <div id="painel-novo-material-apoio">
        <PainelCriacaoInline aberto={painelUploadAberto} titulo="Novo material de apoio">
          <FormSection titulo="Dados do material" numero={1} primeira>
            {erroPainel && (
              <FeedbackInline tom="erro" aoFechar={() => setErroPainel(null)}>
                {erroPainel}
              </FeedbackInline>
            )}
            <FormGrid>
              <Campo span={12}>
                <Field label="Nome">
                  <Input value={nome} onChange={(_, d) => setNome(d.value)} />
                </Field>
              </Campo>
              <Campo span={12}>
                <Field label="Categoria (opcional)">
                  <Input value={categoria} onChange={(_, d) => setCategoria(d.value)} placeholder="Ex.: Sinalização - Alojamento" />
                </Field>
              </Campo>
              <Campo span={12}>
                <Field label="Arquivo (imagem JPEG/PNG, PDF ou Word)">
                  <input
                    type="file"
                    accept="image/jpeg,image/png,application/pdf,application/vnd.openxmlformats-officedocument.wordprocessingml.document"
                    onChange={(e) => setArquivo(e.target.files?.[0] ?? null)}
                  />
                </Field>
              </Campo>
            </FormGrid>
            <FormRodape>
              <Button onClick={fecharPainelUpload}>Cancelar</Button>
              <Button appearance="primary" onClick={enviar} disabled={enviando}>
                Enviar
              </Button>
            </FormRodape>
          </FormSection>
        </PainelCriacaoInline>
      </div>

      <Card>
        <DataTable
          aria-label="Materiais de apoio cadastrados"
          colunas={colunas}
          linhas={materiais}
          chaveLinha={(m) => m.id}
          carregando={carregandoLista}
          aoClicarLinha={(m) => (podeVisualizar(m) ? visualizar(m) : baixar(m))}
          vazio={{
            titulo: 'Nenhum material cadastrado ainda.',
            descricao: 'Envie o primeiro material de apoio para começar.',
            acao: { rotulo: 'Enviar material', aoClicar: () => setPainelUploadAberto(true) },
          }}
          acoesLinha={(m) => (
            <div style={{ display: 'flex', gap: 4 }}>
              {podeVisualizar(m) && (
                <Button
                  appearance="subtle"
                  icon={<Eye24Regular />}
                  onClick={(evento) => {
                    evento.stopPropagation();
                    visualizar(m);
                  }}
                  aria-label="Visualizar"
                  title="Visualizar"
                />
              )}
              <Button
                appearance="subtle"
                icon={<ArrowDownload24Regular />}
                onClick={(evento) => {
                  evento.stopPropagation();
                  baixar(m);
                }}
                disabled={baixandoId === m.id}
                aria-label="Baixar"
              />
              <Button
                appearance="subtle"
                icon={<Delete24Regular />}
                onClick={(evento) => {
                  evento.stopPropagation();
                  excluir(m);
                }}
                aria-label="Excluir"
              />
            </div>
          )}
        />
      </Card>
    </div>
  );
}
