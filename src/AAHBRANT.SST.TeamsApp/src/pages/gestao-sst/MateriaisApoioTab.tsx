import { useEffect, useState } from 'react';
import {
  Button,
  Card,
  Campo,
  Carregando,
  DataTable,
  Field,
  FeedbackInline,
  FormGrid,
  FormSection,
  Input,
  PageHeader,
  PainelLateral,
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

// Aba "Documentos & Procedimentos" de Gestão de SST (pedido do usuário, 2026-09-09): catálogo
// global de materiais de apoio (pôsteres de sinalização, instruções técnicas). Espaço reservado no
// menu desde a remoção do módulo de Gestão Documental em 28/08 — deliberadamente mais simples que
// aquele: sem workflow de aprovação/versão, só nome + categoria + arquivo (ver MaterialApoio.cs).
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

  const [materialPreview, setMaterialPreview] = useState<MaterialApoio | null>(null);
  const [urlPreview, setUrlPreview] = useState<string | null>(null);
  const [carregandoPreview, setCarregandoPreview] = useState(false);
  const [baixandoId, setBaixandoId] = useState<string | null>(null);

  const { confirmar, dialogElement } = useConfirmar();
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

  // Abre o painel de pré-visualização e busca o conteúdo sob demanda (endpoint exige autenticação,
  // então <img src> direto pra API não funcionaria — mesmo raciocínio de baixarFoto em Inspeções).
  async function visualizar(material: MaterialApoio) {
    setMaterialPreview(material);
    setUrlPreview(null);
    setCarregandoPreview(true);
    try {
      const blob = await api.materiaisApoio.baixarConteudo(material.id);
      setUrlPreview(URL.createObjectURL(blob));
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar pré-visualização.');
      setMaterialPreview(null);
    } finally {
      setCarregandoPreview(false);
    }
  }

  function fecharPreview() {
    if (urlPreview) URL.revokeObjectURL(urlPreview);
    setMaterialPreview(null);
    setUrlPreview(null);
  }

  async function baixar(material: MaterialApoio) {
    try {
      setBaixandoId(material.id);
      const blob = await api.materiaisApoio.baixarConteudo(material.id);
      const url = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = material.nomeArquivo;
      link.click();
      URL.revokeObjectURL(url);
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

  const podePreVisualizar = materialPreview?.contentType.startsWith('image/') || materialPreview?.contentType === 'application/pdf';

  return (
    <div>
      {dialogElement}
      <PageHeader
        titulo="Documentos & Procedimentos"
        subtitulo="Materiais de apoio (sinalização, instruções técnicas) disponíveis para consulta e download."
        acoes={
          <Button appearance="primary" icon={<Add24Regular />} onClick={() => setPainelUploadAberto(true)}>
            Enviar material
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
          aria-label="Materiais de apoio cadastrados"
          colunas={colunas}
          linhas={materiais}
          chaveLinha={(m) => m.id}
          carregando={carregandoLista}
          aoClicarLinha={visualizar}
          vazio={{
            titulo: 'Nenhum material cadastrado ainda.',
            descricao: 'Envie o primeiro material de apoio para começar.',
            acao: { rotulo: 'Enviar material', aoClicar: () => setPainelUploadAberto(true) },
          }}
          acoesLinha={(m) => (
            <div style={{ display: 'flex', gap: 4 }}>
              <Button
                appearance="subtle"
                icon={<Eye24Regular />}
                onClick={(evento) => {
                  evento.stopPropagation();
                  visualizar(m);
                }}
                aria-label="Visualizar"
              />
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

      <PainelLateral
        aberto={painelUploadAberto}
        aoFechar={fecharPainelUpload}
        titulo="Novo material de apoio"
        largura="md"
        rodape={
          <>
            <Button onClick={fecharPainelUpload}>Cancelar</Button>
            <Button appearance="primary" onClick={enviar} disabled={enviando}>
              Enviar
            </Button>
          </>
        }
      >
        {erroPainel && (
          <FeedbackInline tom="erro" aoFechar={() => setErroPainel(null)}>
            {erroPainel}
          </FeedbackInline>
        )}

        <FormSection titulo="Dados do material" numero={1} primeira>
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
        </FormSection>
      </PainelLateral>

      <PainelLateral
        aberto={!!materialPreview}
        aoFechar={fecharPreview}
        titulo={materialPreview?.nome ?? ''}
        largura="lg"
        rodape={
          materialPreview && (
            <Button
              appearance="primary"
              icon={<ArrowDownload24Regular />}
              onClick={() => baixar(materialPreview)}
              disabled={baixandoId === materialPreview.id}
            >
              Baixar
            </Button>
          )
        }
      >
        {carregandoPreview && <Carregando variante="detalhe" linhas={4} />}
        {!carregandoPreview && urlPreview && materialPreview && (
          <>
            {materialPreview.contentType.startsWith('image/') && (
              <img src={urlPreview} alt={materialPreview.nome} style={{ maxWidth: '100%', borderRadius: 8 }} />
            )}
            {materialPreview.contentType === 'application/pdf' && (
              <iframe src={urlPreview} title={materialPreview.nome} style={{ width: '100%', height: '70vh', border: 'none' }} />
            )}
            {!podePreVisualizar && (
              <FeedbackInline tom="info">
                Pré-visualização não disponível para este tipo de arquivo. Use o botão "Baixar" abaixo.
              </FeedbackInline>
            )}
          </>
        )}
      </PainelLateral>
    </div>
  );
}
