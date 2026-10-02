import { useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  BotaoAcao,
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
  ArrowLeft24Regular,
  ClipboardTaskListLtr24Regular,
  Delete24Regular,
  Edit24Regular,
  Eye24Regular,
  Open24Regular,
  Send24Regular,
  VehicleTruckProfile24Regular,
} from '@fluentui/react-icons';
import {
  api,
  StatusDocumentoAssinatura,
  StatusInspecao,
  statusDocumentoAssinaturaLabel,
  statusInspecaoLabel,
  tipoVeiculoLabel,
  tipoVeiculoLabelPlural,
  type AlojamentoDocumentoAssinaturaResumo,
  type AlojamentoInspecaoResumo,
  type DadosVeiculo,
  type Obra,
  type VeiculoResumo,
} from '../../lib/api';
import { useVisualizadorPdf } from '../../components/useVisualizadorPdf';
import { useSouAdministrador } from '../../lib/UsuarioLogadoContext';
import { useSucessoToast } from '../../hooks/useSucessoToast';

// Ordem fixa dos 5 tipos nos cards (mesma da planilha "CHECK LIST - AT CUIA").
const TIPOS_VEICULO = [1, 2, 3, 4, 5] as const;

const tomStatus: Record<VeiculoResumo['statusUltimaInspecao'], Tom> = {
  inspecionado: 'ok',
  nunca: 'atencao',
};

function rotuloStatus(status: VeiculoResumo['statusUltimaInspecao'], dias: number | null) {
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

function nomePdfInspecao(veiculo: VeiculoResumo, inspecao: AlojamentoInspecaoResumo, assinado = false) {
  const placa = veiculo.placaPrefixo.replace(/[^\w-]+/g, '-');
  return `inspecao-veiculo-${placa}${assinado ? '-assinada' : ''}-${formatarData(inspecao.data).replaceAll('/', '-')}.pdf`;
}

interface FormularioVeiculo {
  obraId: string;
  tipo: number;
  placaPrefixo: string;
  marcaModelo: string;
  ano: string;
  cor: string;
  empresa: string;
  responsavel: string;
}

function formularioVazio(obraId = '', tipo = 0): FormularioVeiculo {
  return { obraId, tipo, placaPrefixo: '', marcaModelo: '', ano: '', cor: '', empresa: '', responsavel: '' };
}

// Sub-aba "Veículos" (02/10/2026): obra → tipo de veículo → veículos daquele tipo na obra. Mesmo
// padrão visual de AlojamentoTab (inspeção avulsa, "Continuar/Nova inspeção", histórico e PDF), mas
// com um nível extra de cards por tipo e cadastro MANUAL dos veículos (sem integração com outro
// sistema). Quem cadastra é o Técnico; só o Administrador exclui.
export function VeiculosTab() {
  const navigate = useNavigate();
  const souAdministrador = useSouAdministrador();
  const { confirmar, dialogElement } = useConfirmar();
  const sucessoToast = useSucessoToast();
  const [obras, setObras] = useState<Obra[]>([]);
  const [veiculos, setVeiculos] = useState<VeiculoResumo[]>([]);
  const [erro, setErro] = useState<string | null>(null);
  const [erroPainel, setErroPainel] = useState<string | null>(null);
  const [carregandoLista, setCarregandoLista] = useState(true);
  const [obraSelecionadaId, setObraSelecionadaId] = useState<string | null>(null);
  const [tipoSelecionado, setTipoSelecionado] = useState<number | null>(null);
  const [painelAberto, setPainelAberto] = useState(false);
  const [editandoId, setEditandoId] = useState<string | null>(null);
  const [formulario, setFormulario] = useState<FormularioVeiculo>(formularioVazio());
  const [salvando, setSalvando] = useState(false);
  const [baixandoPdfId, setBaixandoPdfId] = useState<string | null>(null);
  const [enviandoAssinaturaId, setEnviandoAssinaturaId] = useState<string | null>(null);
  const [baixandoAssinadoId, setBaixandoAssinadoId] = useState<string | null>(null);
  const { visualizar, dialogoVisualizador } = useVisualizadorPdf();

  async function carregar() {
    try {
      setErro(null);
      const [obs, lista] = await Promise.all([api.obras.listar(), api.veiculos.listar()]);
      setObras(obs);
      setVeiculos(lista);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar veículos.');
    } finally {
      setCarregandoLista(false);
    }
  }

  useEffect(() => {
    carregar();
  }, []);

  // Clique no card do veículo obtém (ou cria, se não houver uma em andamento) a inspeção atômica e
  // abre o detalhe — mesmo fluxo de AlojamentoTab.abrirInspecao.
  async function abrirInspecao(veiculoId: string) {
    try {
      const resultado = await api.veiculos.obterOuCriarInspecaoAtual(veiculoId);
      navigate(`/prevencao/inspecoes/${resultado.inspecaoId}`, {
        state: resultado.foiCriadaAgora
          ? undefined
          : {
              avisoRetomada: `Inspeção em andamento, criada em ${new Date(resultado.criadaEm).toLocaleString('pt-BR')}.`,
            },
      });
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Não foi possível abrir a inspeção deste veículo.');
    }
  }

  async function baixarPdfInspecao(veiculo: VeiculoResumo, inspecao: AlojamentoInspecaoResumo) {
    try {
      setBaixandoPdfId(inspecao.id);
      setErro(null);
      baixarBlob(await api.inspecoes.baixarPdf(inspecao.id), nomePdfInspecao(veiculo, inspecao));
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Não foi possível baixar o PDF da inspeção.');
    } finally {
      setBaixandoPdfId(null);
    }
  }

  async function enviarParaAssinatura(inspecao: AlojamentoInspecaoResumo) {
    try {
      setEnviandoAssinaturaId(inspecao.id);
      setErro(null);
      await api.assinatura.criar('Inspecao', inspecao.id);
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Não foi possível enviar a inspeção para assinatura.');
    } finally {
      setEnviandoAssinaturaId(null);
    }
  }

  async function baixarPdfAssinado(
    veiculo: VeiculoResumo,
    inspecao: AlojamentoInspecaoResumo,
    documento: AlojamentoDocumentoAssinaturaResumo,
  ) {
    try {
      setBaixandoAssinadoId(documento.id);
      setErro(null);
      baixarBlob(await api.assinatura.baixarPdf(documento.id), nomePdfInspecao(veiculo, inspecao, true));
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Não foi possível baixar o documento assinado.');
    } finally {
      setBaixandoAssinadoId(null);
    }
  }

  function visualizarPdfInspecao(veiculo: VeiculoResumo, inspecao: AlojamentoInspecaoResumo) {
    void visualizar({
      titulo: `Inspeção de veículo ${veiculo.placaPrefixo} — ${formatarData(inspecao.data)}`,
      nomeArquivo: nomePdfInspecao(veiculo, inspecao),
      obter: () => api.inspecoes.baixarPdf(inspecao.id),
    });
  }

  function visualizarPdfAssinado(
    veiculo: VeiculoResumo,
    inspecao: AlojamentoInspecaoResumo,
    documento: AlojamentoDocumentoAssinaturaResumo,
  ) {
    void visualizar({
      titulo: `Inspeção de veículo ${veiculo.placaPrefixo} assinada — ${formatarData(inspecao.data)}`,
      nomeArquivo: nomePdfInspecao(veiculo, inspecao, true),
      obter: () => api.assinatura.baixarPdf(documento.id),
    });
  }

  // ---- cadastro ----
  function abrirCadastro() {
    setEditandoId(null);
    setFormulario(formularioVazio(obraSelecionadaId ?? '', tipoSelecionado ?? 0));
    setErroPainel(null);
    setPainelAberto(true);
  }

  function abrirEdicao(veiculo: VeiculoResumo) {
    setEditandoId(veiculo.id);
    setFormulario({
      obraId: veiculo.obraId,
      tipo: veiculo.tipo,
      placaPrefixo: veiculo.placaPrefixo,
      marcaModelo: veiculo.marcaModelo ?? '',
      ano: veiculo.ano ? String(veiculo.ano) : '',
      cor: veiculo.cor ?? '',
      empresa: veiculo.empresa ?? '',
      responsavel: veiculo.responsavel ?? '',
    });
    setErroPainel(null);
    setPainelAberto(true);
  }

  function fecharPainel() {
    setPainelAberto(false);
    setEditandoId(null);
    setErroPainel(null);
  }

  async function salvarVeiculo() {
    if (!formulario.obraId) return setErroPainel('Selecione a obra do veículo.');
    if (!formulario.tipo) return setErroPainel('Selecione o tipo do veículo.');
    if (!formulario.placaPrefixo.trim()) return setErroPainel('Informe a placa ou o prefixo do veículo.');
    const ano = formulario.ano.trim() ? Number(formulario.ano) : null;
    if (ano !== null && (!Number.isInteger(ano) || ano < 1950 || ano > 2100)) return setErroPainel('Ano inválido.');

    const dados: DadosVeiculo = {
      obraId: formulario.obraId,
      tipo: formulario.tipo,
      placaPrefixo: formulario.placaPrefixo.trim(),
      marcaModelo: formulario.marcaModelo.trim() || null,
      ano,
      cor: formulario.cor.trim() || null,
      empresa: formulario.empresa.trim() || null,
      responsavel: formulario.responsavel.trim() || null,
    };
    try {
      setSalvando(true);
      setErroPainel(null);
      if (editandoId) await api.veiculos.atualizar(editandoId, dados);
      else await api.veiculos.criar(dados);
      fecharPainel();
      await carregar();
      sucessoToast(editandoId ? 'Veículo atualizado com sucesso.' : 'Veículo cadastrado com sucesso.');
    } catch (e) {
      setErroPainel(e instanceof Error ? e.message : 'Falha ao salvar o veículo.');
    } finally {
      setSalvando(false);
    }
  }

  async function excluirVeiculo(veiculo: VeiculoResumo) {
    if (!(await confirmar(`Excluir o veículo ${veiculo.placaPrefixo}? O histórico de inspeções é preservado.`))) return;
    try {
      setErro(null);
      await api.veiculos.excluir(veiculo.id);
      await carregar();
      sucessoToast('Veículo excluído com sucesso.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao excluir o veículo.');
    }
  }

  const resumoPorObra = useMemo(
    () =>
      obras.map((obra) => {
        const daObra = veiculos.filter((v) => v.obraId === obra.id);
        const semInspecao = daObra.filter((v) => v.statusUltimaInspecao === 'nunca').length;
        return { obra, total: daObra.length, semInspecao };
      }),
    [obras, veiculos],
  );

  const obraAtual = obraSelecionadaId ? obras.find((o) => o.id === obraSelecionadaId) : null;
  const veiculosDaObra = useMemo(
    () => veiculos.filter((v) => v.obraId === obraSelecionadaId),
    [veiculos, obraSelecionadaId],
  );

  const painelCadastro = (
    <PainelCriacaoInline aberto={painelAberto} titulo={editandoId ? 'Editar veículo' : 'Cadastrar veículo'}>
      <FormSection titulo="Dados do veículo" numero={1} primeira>
        {erroPainel && (
          <FeedbackInline tom="erro" aoFechar={() => setErroPainel(null)}>
            {erroPainel}
          </FeedbackInline>
        )}
        <FormGrid>
          <Campo span={6}>
            <Field label="Obra" required>
              <Select value={formulario.obraId} onChange={(_, d) => setFormulario({ ...formulario, obraId: d.value })}>
                <option value="">Selecione</option>
                {obras.map((obra) => (
                  <option key={obra.id} value={obra.id}>
                    {obra.nome}
                  </option>
                ))}
              </Select>
            </Field>
          </Campo>
          <Campo span={6}>
            <Field label="Tipo de veículo" required>
              <Select
                value={formulario.tipo ? String(formulario.tipo) : ''}
                onChange={(_, d) => setFormulario({ ...formulario, tipo: d.value ? Number(d.value) : 0 })}
              >
                <option value="">Selecione</option>
                {TIPOS_VEICULO.map((tipo) => (
                  <option key={tipo} value={tipo}>
                    {tipoVeiculoLabel[tipo]}
                  </option>
                ))}
              </Select>
            </Field>
          </Campo>
          <Campo span={6}>
            <Field label="Placa / Prefixo" required>
              <Input
                value={formulario.placaPrefixo}
                maxLength={60}
                onChange={(_, d) => setFormulario({ ...formulario, placaPrefixo: d.value.toUpperCase() })}
              />
            </Field>
          </Campo>
          <Campo span={6}>
            <Field label="Marca / Modelo">
              <Input
                value={formulario.marcaModelo}
                maxLength={150}
                onChange={(_, d) => setFormulario({ ...formulario, marcaModelo: d.value })}
                placeholder="Ex.: JOHN DEERE 310 P"
              />
            </Field>
          </Campo>
          <Campo span={3}>
            <Field label="Ano">
              <Input
                type="number"
                value={formulario.ano}
                onChange={(_, d) => setFormulario({ ...formulario, ano: d.value })}
              />
            </Field>
          </Campo>
          <Campo span={3}>
            <Field label="Cor">
              <Input value={formulario.cor} maxLength={40} onChange={(_, d) => setFormulario({ ...formulario, cor: d.value })} />
            </Field>
          </Campo>
          <Campo span={6}>
            <Field label="Subcontratada / Empresa">
              <Input
                value={formulario.empresa}
                maxLength={150}
                onChange={(_, d) => setFormulario({ ...formulario, empresa: d.value })}
                placeholder="Ex.: AAHBRANT"
              />
            </Field>
          </Campo>
          <Campo span={6}>
            <Field label="Responsável / Motorista">
              <Input
                value={formulario.responsavel}
                maxLength={150}
                onChange={(_, d) => setFormulario({ ...formulario, responsavel: d.value })}
              />
            </Field>
          </Campo>
        </FormGrid>
        <FormRodape>
          <Button onClick={fecharPainel}>Cancelar</Button>
          <Button appearance="primary" onClick={salvarVeiculo} disabled={salvando}>
            {editandoId ? 'Salvar alterações' : 'Cadastrar veículo'}
          </Button>
        </FormRodape>
      </FormSection>
    </PainelCriacaoInline>
  );

  // ---- nível 3: veículos de um tipo dentro da obra ----
  if (obraAtual && tipoSelecionado) {
    const doTipo = veiculosDaObra.filter((v) => v.tipo === tipoSelecionado);
    return (
      <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
        {dialogElement}
        {dialogoVisualizador}
        <PageHeader
          titulo={`${tipoVeiculoLabelPlural[tipoSelecionado]} — ${obraAtual.nome}`}
          filtros={
            <Button appearance="subtle" icon={<ArrowLeft24Regular />} onClick={() => setTipoSelecionado(null)}>
              Voltar aos tipos
            </Button>
          }
          acoes={
            <Button appearance="primary" icon={<Add24Regular />} onClick={painelAberto ? fecharPainel : abrirCadastro}>
              {painelAberto ? 'Fechar' : 'Cadastrar veículo'}
            </Button>
          }
        />

        {erro && <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>{erro}</FeedbackInline>}

        {painelCadastro}

        {doTipo.length === 0 && (
          <Card>
            <EstadoVazio
              titulo="Nenhum veículo cadastrado neste tipo para esta obra."
              descricao="Use “Cadastrar veículo” para incluir o primeiro e começar as inspeções."
            />
          </Card>
        )}

        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(300px, 1fr))', gap: 16 }}>
          {doTipo.map((veiculo) => {
            const ultima = veiculo.ultimaInspecaoConcluida;
            return (
              <Card key={veiculo.id}>
                <div style={{ display: 'flex', flexDirection: 'column', gap: 10 }}>
                  <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', gap: 8 }}>
                    <div>
                      <div style={{ fontWeight: 600 }}>{veiculo.placaPrefixo}</div>
                      {veiculo.marcaModelo && <div style={{ fontSize: 12 }}>{veiculo.marcaModelo}</div>}
                    </div>
                    <div style={{ display: 'flex', gap: 4 }}>
                      <BotaoAcao tom="ver" icon={<Edit24Regular />} onClick={() => abrirEdicao(veiculo)} aria-label="Editar veículo" />
                      {souAdministrador && (
                        <BotaoAcao tom="excluir" icon={<Delete24Regular />} onClick={() => excluirVeiculo(veiculo)} aria-label="Excluir veículo" />
                      )}
                    </div>
                  </div>

                  {(veiculo.empresa || veiculo.responsavel || veiculo.ano || veiculo.cor) && (
                    <div style={{ fontSize: 12, display: 'flex', flexDirection: 'column', gap: 2 }}>
                      {veiculo.empresa && <span>Subcontratada: {veiculo.empresa}</span>}
                      {veiculo.responsavel && <span>Responsável: {veiculo.responsavel}</span>}
                      {(veiculo.ano || veiculo.cor) && (
                        <span>{[veiculo.ano, veiculo.cor].filter(Boolean).join(' · ')}</span>
                      )}
                    </div>
                  )}

                  <div style={{ display: 'flex', gap: 6, flexWrap: 'wrap' }}>
                    <StatusChip tom={tomStatus[veiculo.statusUltimaInspecao]}>
                      {rotuloStatus(veiculo.statusUltimaInspecao, veiculo.diasDesdeUltimaInspecao)}
                    </StatusChip>
                    {veiculo.inspecaoEmAndamento && <StatusChip tom="info">Inspeção em andamento</StatusChip>}
                  </div>

                  {ultima && (
                    <div style={{ fontSize: 12 }}>
                      Última concluída: {formatarData(ultima.data)} · {ultima.itensNaoConformes} não conformidade(s)
                    </div>
                  )}

                  {ultima?.documentoAssinatura && (
                    <StatusChip tom={ultima.documentoAssinatura.status === StatusDocumentoAssinatura.Finalizado ? 'ok' : 'info'}>
                      Assinatura: {statusDocumentoAssinaturaLabel[ultima.documentoAssinatura.status]}
                    </StatusChip>
                  )}

                  <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap', marginTop: 4 }}>
                    <Button appearance="primary" icon={<ClipboardTaskListLtr24Regular />} onClick={() => abrirInspecao(veiculo.id)}>
                      {veiculo.inspecaoEmAndamento ? 'Continuar inspeção' : 'Nova inspeção'}
                    </Button>
                    {ultima && (
                      <BotaoAcao tom="ver" icon={<Eye24Regular />} onClick={() => visualizarPdfInspecao(veiculo, ultima)} aria-label="Visualizar última inspeção">
                        Visualizar última
                      </BotaoAcao>
                    )}
                    {ultima && (
                      <BotaoAcao
                        tom="baixar"
                        icon={<ArrowDownload24Regular />}
                        disabled={baixandoPdfId === ultima.id}
                        onClick={() => baixarPdfInspecao(veiculo, ultima)}
                        aria-label="Baixar última inspeção"
                      >
                        Baixar última
                      </BotaoAcao>
                    )}
                    {ultima && !ultima.documentoAssinatura && (
                      <Button
                        appearance="secondary"
                        icon={<Send24Regular />}
                        disabled={enviandoAssinaturaId === ultima.id}
                        onClick={() => enviarParaAssinatura(ultima)}
                      >
                        Enviar para assinatura
                      </Button>
                    )}
                    {ultima?.documentoAssinatura?.temPdf && (
                      <BotaoAcao
                        tom="ver"
                        icon={<Eye24Regular />}
                        onClick={() => visualizarPdfAssinado(veiculo, ultima, ultima.documentoAssinatura!)}
                        aria-label="Visualizar última inspeção assinada"
                      >
                        Visualizar assinado
                      </BotaoAcao>
                    )}
                    {ultima?.documentoAssinatura?.temPdf && (
                      <BotaoAcao
                        tom="baixar"
                        icon={<ArrowDownload24Regular />}
                        disabled={baixandoAssinadoId === ultima.documentoAssinatura.id}
                        onClick={() => baixarPdfAssinado(veiculo, ultima, ultima.documentoAssinatura!)}
                        aria-label="Baixar última inspeção assinada"
                      >
                        Baixar assinado
                      </BotaoAcao>
                    )}
                  </div>

                  {veiculo.historicoInspecoes.length > 0 && (
                    <div style={{ borderTop: '1px solid rgba(0, 0, 0, 0.08)', paddingTop: 10, marginTop: 2 }}>
                      <div style={{ fontSize: 12, fontWeight: 700, marginBottom: 8 }}>Histórico de inspeções</div>
                      <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
                        {veiculo.historicoInspecoes.map((inspecao) => {
                          const concluida = inspecao.status === StatusInspecao.Concluida;
                          const assinatura = inspecao.documentoAssinatura;
                          return (
                            <div
                              key={inspecao.id}
                              style={{ display: 'grid', gridTemplateColumns: '1fr auto', alignItems: 'center', gap: 8, fontSize: 12 }}
                            >
                              <div>
                                <div style={{ fontWeight: 600 }}>
                                  {formatarData(inspecao.data)} · {statusInspecaoLabel[inspecao.status]}
                                </div>
                                <div>
                                  {inspecao.itensRespondidos}/{inspecao.totalItens} itens · {inspecao.itensNaoConformes} não conformidade(s)
                                </div>
                                {concluida && assinatura && (
                                  <div>Assinatura: {statusDocumentoAssinaturaLabel[assinatura.status]}</div>
                                )}
                              </div>
                              <div style={{ display: 'flex', gap: 6 }}>
                                <BotaoAcao
                                  tom="ver"
                                  icon={<Open24Regular />}
                                  onClick={() =>
                                    inspecao.status === StatusInspecao.EmAndamento
                                      ? abrirInspecao(veiculo.id)
                                      : navigate(`/prevencao/inspecoes/${inspecao.id}`)
                                  }
                                  aria-label="Abrir inspeção"
                                />
                                {concluida && (
                                  <BotaoAcao tom="ver" icon={<Eye24Regular />} onClick={() => visualizarPdfInspecao(veiculo, inspecao)} aria-label="Visualizar PDF" />
                                )}
                                {concluida && (
                                  <BotaoAcao
                                    tom="baixar"
                                    icon={<ArrowDownload24Regular />}
                                    disabled={baixandoPdfId === inspecao.id}
                                    onClick={() => baixarPdfInspecao(veiculo, inspecao)}
                                    aria-label="Baixar PDF"
                                  />
                                )}
                                {concluida && !assinatura && (
                                  <BotaoAcao
                                    tom="ver"
                                    icon={<Send24Regular />}
                                    disabled={enviandoAssinaturaId === inspecao.id}
                                    onClick={() => enviarParaAssinatura(inspecao)}
                                    aria-label="Enviar para assinatura"
                                  />
                                )}
                                {concluida && assinatura?.temPdf && (
                                  <BotaoAcao
                                    tom="ver"
                                    icon={<Eye24Regular />}
                                    onClick={() => visualizarPdfAssinado(veiculo, inspecao, assinatura)}
                                    aria-label="Visualizar PDF assinado"
                                  />
                                )}
                                {concluida && assinatura?.temPdf && (
                                  <BotaoAcao
                                    tom="baixar"
                                    icon={<ArrowDownload24Regular />}
                                    disabled={baixandoAssinadoId === assinatura.id}
                                    onClick={() => baixarPdfAssinado(veiculo, inspecao, assinatura)}
                                    aria-label="Baixar PDF assinado"
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

  // ---- nível 2: tipos de veículo da obra ----
  if (obraAtual) {
    return (
      <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
        {dialogElement}
        <PageHeader
          titulo={`Veículos — ${obraAtual.nome}`}
          subtitulo="Selecione o tipo de veículo para ver os veículos da obra."
          filtros={
            <Button appearance="subtle" icon={<ArrowLeft24Regular />} onClick={() => setObraSelecionadaId(null)}>
              Voltar às obras
            </Button>
          }
          acoes={
            <Button appearance="primary" icon={<Add24Regular />} onClick={painelAberto ? fecharPainel : abrirCadastro}>
              {painelAberto ? 'Fechar' : 'Cadastrar veículo'}
            </Button>
          }
        />

        {erro && <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>{erro}</FeedbackInline>}

        {painelCadastro}

        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(240px, 1fr))', gap: 16 }}>
          {TIPOS_VEICULO.map((tipo) => {
            const doTipo = veiculosDaObra.filter((v) => v.tipo === tipo);
            const semInspecao = doTipo.filter((v) => v.statusUltimaInspecao === 'nunca').length;
            const emAndamento = doTipo.filter((v) => v.inspecaoEmAndamento).length;
            return (
              <Card key={tipo}>
                <div
                  role="button"
                  tabIndex={0}
                  onClick={() => setTipoSelecionado(tipo)}
                  onKeyDown={(e) => {
                    if (e.key === 'Enter' || e.key === ' ') setTipoSelecionado(tipo);
                  }}
                  style={{ cursor: 'pointer', display: 'flex', flexDirection: 'column', gap: 8 }}
                >
                  <div style={{ display: 'flex', alignItems: 'center', gap: 8, fontWeight: 600 }}>
                    <VehicleTruckProfile24Regular />
                    {tipoVeiculoLabel[tipo]}
                  </div>
                  <div style={{ fontSize: 12 }}>
                    <span style={{ fontWeight: 600 }}>{doTipo.length}</span> {doTipo.length === 1 ? 'veículo' : 'veículos'}
                  </div>
                  <div style={{ display: 'flex', gap: 6, flexWrap: 'wrap' }}>
                    {doTipo.length > 0 && (
                      <StatusChip tom={semInspecao === 0 ? 'ok' : 'atencao'}>
                        {semInspecao === 0 ? 'Todos inspecionados' : `${semInspecao} sem inspeção`}
                      </StatusChip>
                    )}
                    {emAndamento > 0 && <StatusChip tom="info">{emAndamento} em andamento</StatusChip>}
                  </div>
                </div>
              </Card>
            );
          })}
        </div>
      </div>
    );
  }

  // ---- nível 1: obras ----
  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <PageHeader titulo="Veículos" subtitulo="Selecione uma obra para ver os veículos e equipamentos cadastrados nela." />

      {erro && <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>{erro}</FeedbackInline>}

      {!carregandoLista && obras.length === 0 && (
        <Card>
          <EstadoVazio
            titulo="Nenhuma obra cadastrada ainda."
            descricao="Cadastre uma obra em Obras para depois vincular veículos a ela."
          />
        </Card>
      )}

      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(280px, 1fr))', gap: 16 }}>
        {resumoPorObra.map(({ obra, total, semInspecao }) => (
          <Card key={obra.id}>
            <div
              role="button"
              tabIndex={0}
              onClick={() => setObraSelecionadaId(obra.id)}
              onKeyDown={(e) => {
                if (e.key === 'Enter' || e.key === ' ') setObraSelecionadaId(obra.id);
              }}
              style={{ cursor: 'pointer', display: 'flex', flexDirection: 'column', gap: 8 }}
            >
              <div style={{ fontWeight: 600 }}>{obra.nome}</div>
              <div style={{ fontSize: 12 }}>{obra.codigo}</div>
              <div style={{ display: 'flex', alignItems: 'center', gap: 6, fontSize: 12, marginTop: 4 }}>
                <VehicleTruckProfile24Regular />
                <span style={{ fontWeight: 600 }}>{total}</span>
                <span>{total === 1 ? 'veículo' : 'veículos'}</span>
              </div>
              {total > 0 && (
                <div style={{ display: 'flex', gap: 6, flexWrap: 'wrap' }}>
                  <StatusChip tom={semInspecao === 0 ? 'ok' : 'atencao'}>
                    {semInspecao === 0 ? 'Todos inspecionados' : `${semInspecao} sem inspeção`}
                  </StatusChip>
                </div>
              )}
            </div>
          </Card>
        ))}
      </div>
    </div>
  );
}
