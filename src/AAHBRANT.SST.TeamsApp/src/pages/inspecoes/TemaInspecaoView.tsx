import { useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  BotaoAcao,
  BotaoVoltar,
  Button,
  Campo,
  Card,
  EstadoVazio,
  FeedbackInline,
  Field,
  FormGrid,
  FormRodape,
  FormSection,
  Input,
  PageHeader,
  PainelCriacaoInline,
  Select,
  StatusChip,
  useConfirmar,
  type Tom,
} from '@ui';
import {
  Add24Regular,
  ArrowDownload24Regular,
  ClipboardTaskListLtr24Regular,
  Delete24Regular,
  Edit24Regular,
  Eye24Regular,
  Open24Regular,
} from '@fluentui/react-icons';
import {
  api,
  StatusInspecao,
  statusInspecaoLabel,
  type AlojamentoInspecaoResumo,
  type ItemInspecionavelResumo,
  type Obra,
  type TemaInspecaoResumo,
  type Usuario,
} from '../../lib/api';
import { CamposPeriodicidadeInspecao, payloadPeriodicidade, pendenciaPeriodicidade, SeloProximaInspecao } from './PeriodicidadeInspecao';
import { useVisualizadorPdf } from '../../components/useVisualizadorPdf';
import { useSouAdministrador } from '../../lib/UsuarioLogadoContext';
import { useSucessoToast } from '../../hooks/useSucessoToast';

const tomStatus: Record<ItemInspecionavelResumo['statusUltimaInspecao'], Tom> = {
  inspecionado: 'ok',
  nunca: 'atencao',
};

function rotuloStatus(status: ItemInspecionavelResumo['statusUltimaInspecao'], dias?: number | null) {
  if (status === 'nunca') return 'Nunca inspecionado';
  if (!dias) return 'Inspecionado hoje';
  return `Inspecionado · há ${dias} ${dias === 1 ? 'dia' : 'dias'}`;
}

function formatarData(data: string) {
  return new Date(data).toLocaleDateString('pt-BR');
}

function baixarBlob(blob: Blob, nomeArquivo: string) {
  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.href = url;
  link.download = nomeArquivo;
  link.click();
  URL.revokeObjectURL(url);
}

function nomePdf(tema: TemaInspecaoResumo, item: ItemInspecionavelResumo, inspecao: AlojamentoInspecaoResumo) {
  const nome = `${tema.nome}-${item.identificacao}`.replace(/[^\w-]+/g, '-');
  return `inspecao-${nome}-${formatarData(inspecao.data).replaceAll('/', '-')}.pdf`;
}

// Dentro de um tema (ex.: Andaime): todos os itens cadastrados, de todas as obras, cada um com o seu
// histórico de inspeções. Mesmo desenho dos Veículos — "Nova/Continuar inspeção" abre a inspeção atômica
// do item, com o checklist do tema (Conforme / Não Conforme, foto na não conformidade).
export function TemaInspecaoView({
  tema,
  aoVoltar,
  aoAlterado,
}: {
  tema: TemaInspecaoResumo;
  aoVoltar: () => void;
  aoAlterado: () => void;
}) {
  const navigate = useNavigate();
  const souAdministrador = useSouAdministrador();
  const { confirmar, dialogElement } = useConfirmar();
  const sucessoToast = useSucessoToast();
  const { visualizar, dialogoVisualizador } = useVisualizadorPdf();
  const [obras, setObras] = useState<Obra[]>([]);
  const [usuarios, setUsuarios] = useState<Usuario[]>([]);
  const [editandoId, setEditandoId] = useState<string | null>(null);
  const [itens, setItens] = useState<ItemInspecionavelResumo[]>([]);
  const [obraFiltro, setObraFiltro] = useState('');
  const [erro, setErro] = useState<string | null>(null);
  const [erroPainel, setErroPainel] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(true);
  const [painelAberto, setPainelAberto] = useState(false);
  const [formulario, setFormulario] = useState({ obraId: '', identificacao: '', descricao: '', periodicidadeDias: '', responsavelInspecaoId: '' });
  const [salvando, setSalvando] = useState(false);
  const [baixandoId, setBaixandoId] = useState<string | null>(null);

  async function carregar() {
    try {
      setErro(null);
      const [obs, lista, listaUsuarios] = await Promise.all([
        api.obras.listar(),
        api.temasInspecao.listarItens(tema.id),
        api.usuarios.listar().catch(() => [] as Usuario[]),
      ]);
      setObras(obs);
      setUsuarios(listaUsuarios);
      setItens(lista);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar os itens do tema.');
    } finally {
      setCarregando(false);
    }
  }

  useEffect(() => {
    void carregar();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [tema.id]);

  const visiveis = useMemo(() => (obraFiltro ? itens.filter((i) => i.obraId === obraFiltro) : itens), [itens, obraFiltro]);

  function abrirCadastro() {
    // O item novo herda a periodicidade padrão do tema (editável).
    setEditandoId(null);
    setFormulario({
      obraId: obraFiltro, identificacao: '', descricao: '',
      periodicidadeDias: tema.periodicidadeDiasPadrao ? String(tema.periodicidadeDiasPadrao) : '', responsavelInspecaoId: '',
    });
    setErroPainel(null);
    setPainelAberto(true);
  }

  function abrirEdicao(item: ItemInspecionavelResumo) {
    setEditandoId(item.id);
    setFormulario({
      obraId: item.obraId, identificacao: item.identificacao, descricao: item.descricao ?? '',
      periodicidadeDias: item.periodicidadeInspecaoDias ? String(item.periodicidadeInspecaoDias) : '',
      responsavelInspecaoId: item.responsavelInspecaoUsuarioId ?? '',
    });
    setErroPainel(null);
    setPainelAberto(true);
  }

  async function salvarItem() {
    if (!formulario.obraId) return setErroPainel('Selecione a obra do item.');
    if (!formulario.identificacao.trim()) return setErroPainel(`Informe como o item é identificado (ex.: ${tema.nome} A-03).`);
    const periodicidade = { dias: formulario.periodicidadeDias, responsavelId: formulario.responsavelInspecaoId };
    const pendencia = pendenciaPeriodicidade(periodicidade);
    if (pendencia) return setErroPainel(pendencia);
    try {
      setSalvando(true);
      setErroPainel(null);
      if (editandoId) {
        await api.temasInspecao.atualizarItem(editandoId, { descricao: formulario.descricao.trim() || null, ...payloadPeriodicidade(periodicidade) });
      } else {
        await api.temasInspecao.criarItem(tema.id, {
          obraId: formulario.obraId,
          identificacao: formulario.identificacao.trim(),
          descricao: formulario.descricao.trim() || null,
          ...payloadPeriodicidade(periodicidade),
        });
      }
      sucessoToast(editandoId ? 'Item atualizado.' : 'Item cadastrado.');
      setPainelAberto(false);
      await carregar();
      aoAlterado();
    } catch (e) {
      setErroPainel(e instanceof Error ? e.message : 'Não foi possível cadastrar o item.');
    } finally {
      setSalvando(false);
    }
  }

  async function excluirItem(item: ItemInspecionavelResumo) {
    if (!(await confirmar(`Excluir "${item.identificacao}"? O histórico de inspeções é preservado.`))) return;
    try {
      setErro(null);
      await api.temasInspecao.excluirItem(item.id);
      await carregar();
      aoAlterado();
      sucessoToast('Item excluído com sucesso.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Não foi possível excluir o item.');
    }
  }

  // Clique em "Nova/Continuar inspeção" obtém (ou cria, se não houver uma em andamento) a inspeção atômica
  // do item e abre o detalhe — mesmo fluxo de VeiculosTab.abrirInspecao.
  async function abrirInspecao(itemId: string) {
    try {
      const resultado = await api.temasInspecao.obterOuCriarInspecaoAtual(itemId);
      navigate(`/prevencao/inspecoes/${resultado.inspecaoId}`, {
        state: resultado.foiCriadaAgora
          ? undefined
          : { avisoRetomada: `Inspeção em andamento, criada em ${new Date(resultado.criadaEm).toLocaleString('pt-BR')}.` },
      });
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Não foi possível abrir a inspeção deste item.');
    }
  }

  async function baixarPdfInspecao(item: ItemInspecionavelResumo, inspecao: AlojamentoInspecaoResumo) {
    try {
      setBaixandoId(inspecao.id);
      setErro(null);
      baixarBlob(await api.inspecoes.baixarPdf(inspecao.id), nomePdf(tema, item, inspecao));
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Não foi possível baixar o PDF da inspeção.');
    } finally {
      setBaixandoId(null);
    }
  }

  function visualizarPdfInspecao(item: ItemInspecionavelResumo, inspecao: AlojamentoInspecaoResumo) {
    void visualizar({
      titulo: `${tema.nome} — ${item.identificacao} — ${formatarData(inspecao.data)}`,
      nomeArquivo: nomePdf(tema, item, inspecao),
      obter: () => api.inspecoes.baixarPdf(inspecao.id),
    });
  }

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      {dialogElement}
      {dialogoVisualizador}
      <PageHeader
        titulo={tema.nome}
        subtitulo={`${tema.totalItens} ${tema.totalItens === 1 ? 'item cadastrado' : 'itens cadastrados'} · checklist com ${tema.totalItensChecklist} ${tema.totalItensChecklist === 1 ? 'item' : 'itens'}`}
        filtros={<BotaoVoltar rotulo="Voltar aos cards de inspeção" onClick={aoVoltar} />}
        acoes={
          <Button appearance="primary" icon={<Add24Regular />} onClick={painelAberto ? () => setPainelAberto(false) : abrirCadastro}>
            {painelAberto ? 'Fechar' : `Cadastrar ${tema.nome.toLowerCase()}`}
          </Button>
        }
      />

      {erro && <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>{erro}</FeedbackInline>}

      <PainelCriacaoInline aberto={painelAberto} titulo={editandoId ? `Editar ${tema.nome.toLowerCase()}` : `Cadastrar ${tema.nome.toLowerCase()}`}>
        <FormSection titulo="Dados do item" numero={1} primeira>
          {erroPainel && <FeedbackInline tom="erro" aoFechar={() => setErroPainel(null)}>{erroPainel}</FeedbackInline>}
          <FormGrid>
            <Campo span={6}>
              <Field label="Obra" required>
                <Select value={formulario.obraId} disabled={!!editandoId} onChange={(_, d) => setFormulario({ ...formulario, obraId: d.value })}>
                  <option value="">Selecione</option>
                  {obras.map((obra) => (
                    <option key={obra.id} value={obra.id}>{obra.nome}</option>
                  ))}
                </Select>
              </Field>
            </Campo>
            <Campo span={6}>
              <Field label="Identificação" required>
                <Input
                  value={formulario.identificacao}
                  disabled={!!editandoId}
                  maxLength={150}
                  placeholder={`Ex.: ${tema.nome} A-03 — fachada norte`}
                  onChange={(_, d) => setFormulario({ ...formulario, identificacao: d.value })}
                />
              </Field>
            </Campo>
            <Campo span={12}>
              <Field label="Descrição">
                <Input
                  value={formulario.descricao}
                  maxLength={500}
                  onChange={(_, d) => setFormulario({ ...formulario, descricao: d.value })}
                />
              </Field>
            </Campo>
            <CamposPeriodicidadeInspecao
              valor={{ dias: formulario.periodicidadeDias, responsavelId: formulario.responsavelInspecaoId }}
              aoMudar={(v) => setFormulario({ ...formulario, periodicidadeDias: v.dias, responsavelInspecaoId: v.responsavelId })}
              usuarios={usuarios}
            />
          </FormGrid>
          <FormRodape>
            <Button onClick={() => setPainelAberto(false)}>Cancelar</Button>
            <Button appearance="primary" onClick={salvarItem} disabled={salvando}>{editandoId ? 'Salvar alterações' : 'Cadastrar'}</Button>
          </FormRodape>
        </FormSection>
      </PainelCriacaoInline>

      <div style={{ maxWidth: 320 }}>
        <Field label="Filtrar por obra">
          <Select value={obraFiltro} onChange={(_, d) => setObraFiltro(d.value)} aria-label="Filtrar itens por obra">
            <option value="">Todas as obras</option>
            {obras.map((obra) => (
              <option key={obra.id} value={obra.id}>{obra.nome}</option>
            ))}
          </Select>
        </Field>
      </div>

      {!carregando && visiveis.length === 0 && (
        <Card>
          <EstadoVazio
            titulo={`Nenhum item de ${tema.nome.toLowerCase()} cadastrado${obraFiltro ? ' nesta obra' : ''}.`}
            descricao="Use o botão de cadastro para incluir o primeiro e começar as inspeções."
          />
        </Card>
      )}

      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(300px, 1fr))', gap: 16 }}>
        {visiveis.map((item) => {
          const ultima = item.ultimaInspecaoConcluida;
          return (
            <Card key={item.id}>
              <div style={{ display: 'flex', flexDirection: 'column', gap: 10 }}>
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', gap: 8 }}>
                  <div>
                    <div style={{ fontWeight: 600 }}>{item.identificacao}</div>
                    <div style={{ fontSize: 12 }}>{item.obraNome}</div>
                    {item.descricao && <div style={{ fontSize: 12 }}>{item.descricao}</div>}
                  </div>
                  <div style={{ display: 'flex', gap: 4 }}>
                    <BotaoAcao tom="ver" icon={<Edit24Regular />} onClick={() => abrirEdicao(item)} aria-label="Editar item" />
                    {souAdministrador && (
                      <BotaoAcao tom="excluir" icon={<Delete24Regular />} onClick={() => excluirItem(item)} aria-label="Excluir item" />
                    )}
                  </div>
                </div>

                <div style={{ display: 'flex', gap: 6, flexWrap: 'wrap' }}>
                  <StatusChip tom={tomStatus[item.statusUltimaInspecao]}>
                    {rotuloStatus(item.statusUltimaInspecao, item.diasDesdeUltimaInspecao)}
                  </StatusChip>
                  {item.inspecaoEmAndamento && <StatusChip tom="info">Inspeção em andamento</StatusChip>}
                </div>
                <SeloProximaInspecao
                  proximaEm={item.proximaInspecaoEm}
                  dias={item.periodicidadeInspecaoDias}
                  responsavelNome={item.responsavelInspecaoNome}
                />

                {ultima && (
                  <div style={{ fontSize: 12 }}>
                    Última concluída: {formatarData(ultima.data)} · {ultima.itensNaoConformes} não conformidade(s)
                  </div>
                )}

                <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap', marginTop: 4 }}>
                  <Button appearance="primary" icon={<ClipboardTaskListLtr24Regular />} onClick={() => abrirInspecao(item.id)}>
                    {item.inspecaoEmAndamento ? 'Continuar inspeção' : 'Nova inspeção'}
                  </Button>
                  {ultima && (
                    <BotaoAcao tom="ver" icon={<Eye24Regular />} onClick={() => visualizarPdfInspecao(item, ultima)} aria-label="Visualizar última inspeção">
                      Visualizar última
                    </BotaoAcao>
                  )}
                  {ultima && (
                    <BotaoAcao
                      tom="baixar"
                      icon={<ArrowDownload24Regular />}
                      disabled={baixandoId === ultima.id}
                      onClick={() => baixarPdfInspecao(item, ultima)}
                      aria-label="Baixar última inspeção"
                    >
                      Baixar última
                    </BotaoAcao>
                  )}
                </div>

                {item.historicoInspecoes.length > 0 && (
                  <div style={{ borderTop: '1px solid rgba(0, 0, 0, 0.08)', paddingTop: 10, marginTop: 2 }}>
                    <div style={{ fontSize: 12, fontWeight: 700, marginBottom: 8 }}>Histórico de inspeções</div>
                    <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
                      {item.historicoInspecoes.map((inspecao) => {
                        const concluida = inspecao.status === StatusInspecao.Concluida;
                        return (
                          <div key={inspecao.id} style={{ display: 'grid', gridTemplateColumns: '1fr auto', alignItems: 'center', gap: 8, fontSize: 12 }}>
                            <div>
                              <div style={{ fontWeight: 600 }}>
                                {formatarData(inspecao.data)} · {statusInspecaoLabel[inspecao.status]}
                              </div>
                              <div>
                                {inspecao.itensRespondidos}/{inspecao.totalItens} itens · {inspecao.itensNaoConformes} não conformidade(s)
                              </div>
                            </div>
                            <div style={{ display: 'flex', gap: 6 }}>
                              <BotaoAcao
                                tom="ver"
                                icon={<Open24Regular />}
                                onClick={() =>
                                  inspecao.status === StatusInspecao.EmAndamento
                                    ? abrirInspecao(item.id)
                                    : navigate(`/prevencao/inspecoes/${inspecao.id}`)
                                }
                                aria-label="Abrir inspeção"
                              />
                              {concluida && (
                                <BotaoAcao tom="ver" icon={<Eye24Regular />} onClick={() => visualizarPdfInspecao(item, inspecao)} aria-label="Visualizar PDF" />
                              )}
                              {concluida && (
                                <BotaoAcao
                                  tom="baixar"
                                  icon={<ArrowDownload24Regular />}
                                  disabled={baixandoId === inspecao.id}
                                  onClick={() => baixarPdfInspecao(item, inspecao)}
                                  aria-label="Baixar PDF"
                                />
                              )}
                            </div>
                          </div>
                        );
                      })}
                    </div>
                  </div>
                )}
              </div>
            </Card>
          );
        })}
      </div>
    </div>
  );
}
