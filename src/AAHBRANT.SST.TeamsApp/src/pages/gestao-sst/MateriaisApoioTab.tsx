import { useEffect, useState } from 'react';
import {
  BotaoAcao,
  Button,
  Card,
  Campo,
  DataTable,
  Field,
  FeedbackInline,
  FormGrid,
  FormRodape,
  FormSection,
  Abas,
  Checkbox,
  Input,
  PageHeader,
  Select,
  StatusChip,
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

// Ordem de Serviço (20/10/2026): procedimentos, ITs e políticas também moram aqui, com código SGI,
// revisão e situação. Só documento Vigente entra na OS; política com o selo entra em todas.
const rotuloTipo: Record<number, string> = { 1: 'Material de apoio', 2: 'Procedimento', 3: 'Instrução de trabalho', 4: 'Política' };
const rotuloSituacao: Record<number, string> = { 1: 'Vigente', 2: 'Em revisão', 3: 'Obsoleto' };
type Filtro = 'todos' | 'procedimentos' | 'politicas' | 'apoio';
const passaNoFiltro: Record<Filtro, (m: MaterialApoio) => boolean> = {
  todos: () => true,
  procedimentos: (m) => m.tipo === 2 || m.tipo === 3,
  politicas: (m) => m.tipo === 4,
  apoio: (m) => m.tipo === 1,
};
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
  const [tipo, setTipo] = useState<1 | 2 | 3 | 4>(2);
  const [codigo, setCodigo] = useState('');
  const [revisao, setRevisao] = useState('');
  const [situacao, setSituacao] = useState<1 | 2 | 3>(1);
  const [formaConsulta, setFormaConsulta] = useState('');
  const [todasOs, setTodasOs] = useState(false);
  const [filtro, setFiltro] = useState<Filtro>('todos');
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
    setTipo(2);
    setCodigo('');
    setRevisao('');
    setSituacao(1);
    setFormaConsulta('');
    setTodasOs(false);
  }

  async function enviar() {
    if (!nome.trim()) {
      setErroPainel('Informe o título do documento.');
      return;
    }
    if (tipo !== 1 && (!codigo.trim() || !revisao.trim())) {
      setErroPainel('Informe o código SGI e a revisão do documento.');
      return;
    }
    if (!arquivo) {
      setErroPainel('Selecione um arquivo.');
      return;
    }
    try {
      setEnviando(true);
      setErroPainel(null);
      await api.materiaisApoio.criar(nome, categoria.trim() || null, arquivo, {
        tipo,
        codigo: tipo === 1 ? null : codigo.trim(),
        revisao: tipo === 1 ? null : revisao.trim(),
        situacao: tipo === 1 ? null : situacao,
        formaConsulta: formaConsulta.trim() || null,
        incluirEmTodasOs: tipo === 4 && todasOs,
      });
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

  async function mudarSituacao(m: MaterialApoio, nova: 1 | 2 | 3) {
    try {
      await api.materiaisApoio.atualizar(m.id, m.nome, {
        codigo: m.codigo, revisao: m.revisao, situacao: nova, formaConsulta: m.formaConsulta, incluirEmTodasOs: m.incluirEmTodasOs,
      });
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao atualizar o documento.');
    }
  }

  const colunas: Coluna<MaterialApoio>[] = [
    { chave: 'icone', rotulo: '', render: (m) => <IconePorTipo contentType={m.contentType} /> },
    { chave: 'tipoDoc', rotulo: 'Tipo', render: (m) => <StatusChip tom={m.tipo === 1 ? 'neutro' : 'info'}>{rotuloTipo[m.tipo]}</StatusChip> },
    { chave: 'codigo', rotulo: 'Código', render: (m) => m.codigo || '—' },
    { chave: 'nome', rotulo: 'Título' },
    { chave: 'revisao', rotulo: 'Revisão', render: (m) => m.revisao || '—' },
    {
      chave: 'situacao',
      rotulo: 'Situação',
      render: (m) =>
        m.situacao ? (
          <Select
            size="small"
            value={String(m.situacao)}
            aria-label={`Situação de ${m.nome}`}
            onClick={(e) => e.stopPropagation()}
            onChange={(_, d) => void mudarSituacao(m, Number(d.value) as 1 | 2 | 3)}
          >
            {[1, 2, 3].map((s) => <option key={s} value={s}>{rotuloSituacao[s]}</option>)}
          </Select>
        ) : '—',
    },
    {
      chave: 'usadoEm',
      rotulo: 'Usado em',
      render: (m) => (m.incluirEmTodasOs ? 'Todas as OS' : m.atividades > 0 ? `${m.atividades} atividade(s)` : '—'),
    },
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
        subtitulo="Procedimentos, instruções de trabalho, políticas e materiais de apoio. Só documentos vigentes entram na Ordem de Serviço."
        acoes={
          <Button
            appearance="primary"
            icon={<Add24Regular />}
            onClick={() => (painelUploadAberto ? fecharPainelUpload() : setPainelUploadAberto(true))}
            aria-expanded={painelUploadAberto}
            aria-controls="painel-novo-material-apoio"
          >
            {painelUploadAberto ? 'Fechar' : 'Enviar documento'}
          </Button>
        }
      />

      {erro && (
        <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>
          {erro}
        </FeedbackInline>
      )}

      <div id="painel-novo-material-apoio">
        <PainelCriacaoInline aberto={painelUploadAberto} titulo="Novo documento">
          <FormSection titulo="Dados do documento" numero={1} primeira>
            {erroPainel && (
              <FeedbackInline tom="erro" aoFechar={() => setErroPainel(null)}>
                {erroPainel}
              </FeedbackInline>
            )}
            <FormGrid>
              <Campo span={6}>
                <Field label="Tipo" required>
                  <Select value={String(tipo)} onChange={(_, d) => setTipo(Number(d.value) as 1 | 2 | 3 | 4)}>
                    {[2, 3, 4, 1].map((t) => <option key={t} value={t}>{rotuloTipo[t]}</option>)}
                  </Select>
                </Field>
              </Campo>
              {tipo !== 1 && (
                <Campo span={6}>
                  <Field label="Código SGI" required>
                    <Input value={codigo} onChange={(_, d) => setCodigo(d.value)} placeholder="[ORG]-[TD]-[AR]-[SEQ]" />
                  </Field>
                </Campo>
              )}
              <Campo span={12}>
                <Field label="Título" required>
                  <Input value={nome} onChange={(_, d) => setNome(d.value)} />
                </Field>
              </Campo>
              {tipo !== 1 && (
                <>
                  <Campo span={6}>
                    <Field label="Revisão" required>
                      <Input value={revisao} onChange={(_, d) => setRevisao(d.value)} placeholder="00" />
                    </Field>
                  </Campo>
                  <Campo span={6}>
                    <Field label="Situação" required>
                      <Select value={String(situacao)} onChange={(_, d) => setSituacao(Number(d.value) as 1 | 2 | 3)}>
                        {[1, 2, 3].map((s) => <option key={s} value={s}>{rotuloSituacao[s]}</option>)}
                      </Select>
                    </Field>
                  </Campo>
                  <Campo span={12}>
                    <Field label="Forma de consulta (aparece na Ordem de Serviço)">
                      <Input value={formaConsulta} onChange={(_, d) => setFormaConsulta(d.value)} placeholder="Ex.: Sistema SST › Documentos e quadro de avisos da obra" />
                    </Field>
                  </Campo>
                </>
              )}
              {tipo === 4 && (
                <Campo span={12}>
                  <Checkbox checked={todasOs} onChange={(_, d) => setTodasOs(!!d.checked)} label="Incluir em todas as Ordens de Serviço" />
                </Campo>
              )}
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

      <Abas
        nivel="interno"
        aria-label="Filtrar documentos"
        valor={filtro}
        aoMudar={setFiltro}
        abas={[
          { valor: 'todos', rotulo: 'Todos' },
          { valor: 'procedimentos', rotulo: 'Procedimentos e ITs' },
          { valor: 'politicas', rotulo: 'Políticas' },
          { valor: 'apoio', rotulo: 'Materiais de apoio' },
        ]}
      />

      <Card>
        <DataTable
          aria-label="Documentos cadastrados"
          colunas={colunas}
          linhas={materiais.filter(passaNoFiltro[filtro])}
          chaveLinha={(m) => m.id}
          carregando={carregandoLista}
          aoClicarLinha={(m) => (podeVisualizar(m) ? visualizar(m) : baixar(m))}
          vazio={{
            titulo: 'Nenhum material cadastrado ainda.',
            descricao: 'Envie o primeiro material de apoio para começar.',
            acao: { rotulo: 'Enviar material', aoClicar: () => setPainelUploadAberto(true) },
          }}
          acoesLinha={(m) => (
            <div style={{ display: 'flex', gap: 6 }}>
              {podeVisualizar(m) && (
                <BotaoAcao
                  tom="ver"
                  icon={<Eye24Regular />}
                  onClick={(evento) => {
                    evento.stopPropagation();
                    visualizar(m);
                  }}
                  aria-label="Visualizar"
                  title="Visualizar"
                />
              )}
              <BotaoAcao
                tom="baixar"
                icon={<ArrowDownload24Regular />}
                onClick={(evento) => {
                  evento.stopPropagation();
                  baixar(m);
                }}
                disabled={baixandoId === m.id}
                aria-label="Baixar"
              />
              <BotaoAcao
                tom="excluir"
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
