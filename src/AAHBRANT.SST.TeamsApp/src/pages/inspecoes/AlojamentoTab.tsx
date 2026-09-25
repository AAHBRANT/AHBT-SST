import { useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { BotaoAcao, Button, Card, EstadoVazio, FeedbackInline, PageHeader, StatusChip, type Tom } from '@ui';
import { ArrowDownload24Regular, ArrowLeft24Regular, ClipboardTaskListLtr24Regular, Eye24Regular, Home24Regular, Open24Regular, Send24Regular } from '@fluentui/react-icons';
import {
  api,
  StatusDocumentoAssinatura,
  StatusInspecao,
  statusDocumentoAssinaturaLabel,
  statusInspecaoLabel,
  type AlojamentoDocumentoAssinaturaResumo,
  type AlojamentoInspecaoResumo,
  type AlojamentoResumo,
  type Obra,
} from '../../lib/api';
import { useVisualizadorPdf } from '../../components/useVisualizadorPdf';

const tomStatus: Record<AlojamentoResumo['statusUltimaInspecao'], Tom> = {
  'em-dia': 'ok',
  atrasada: 'alerta',
  nunca: 'atencao',
};

function rotuloStatus(status: AlojamentoResumo['statusUltimaInspecao'], dias: number | null) {
  if (status === 'nunca') return 'Nunca inspecionado';
  if (status === 'atrasada') return `Atrasada · há ${dias} dias`;
  return `Em dia · há ${dias} dias`;
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

// Mesmos nomes usados no download e no botão "Baixar" do visualizador.
function nomePdfInspecao(inspecao: AlojamentoInspecaoResumo) {
  return `inspecao-alojamento-${formatarData(inspecao.data).replaceAll('/', '-')}.pdf`;
}

function nomePdfAssinado(inspecao: AlojamentoInspecaoResumo) {
  return `inspecao-alojamento-assinada-${formatarData(inspecao.data).replaceAll('/', '-')}.pdf`;
}

// Sub-aba "Alojamento" (Task 8, 2026-09-15): mesmo padrão obra → sub-itens de TrabalhadoresTab.tsx —
// grade de obras com resumo, clique abre a grade de alojamentos daquela obra. Diferente de
// TrabalhadoresTab, aqui os "sub-itens" também são cards (não uma DataTable), pois um alojamento
// não tem colunas tabulares ricas o bastante para justificar DataTable (regra dos 3 pilotos).
export function AlojamentoTab() {
  const navigate = useNavigate();
  const [obras, setObras] = useState<Obra[]>([]);
  const [alojamentos, setAlojamentos] = useState<AlojamentoResumo[]>([]);
  const [erro, setErro] = useState<string | null>(null);
  const [carregandoLista, setCarregandoLista] = useState(true);
  const [obraSelecionadaId, setObraSelecionadaId] = useState<string | null>(null);
  const [baixandoPdfId, setBaixandoPdfId] = useState<string | null>(null);
  const [enviandoAssinaturaId, setEnviandoAssinaturaId] = useState<string | null>(null);
  const [baixandoAssinadoId, setBaixandoAssinadoId] = useState<string | null>(null);
  const { visualizar, dialogoVisualizador } = useVisualizadorPdf();

  async function carregar() {
    try {
      setErro(null);
      const [obs, alojs] = await Promise.all([api.obras.listar(), api.alojamentos.listar()]);
      setObras(obs);
      setAlojamentos(alojs);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar alojamentos.');
    } finally {
      setCarregandoLista(false);
    }
  }

  useEffect(() => {
    carregar();
  }, []);

  // Task 9 (2026-09-15): clique no card do alojamento obtém (ou cria, se não houver uma em
  // andamento) a inspeção do dia via endpoint atômico (Task 7) e navega direto pro detalhe. Quando
  // já existia uma inspeção em andamento, leva um aviso em location.state pra InspecaoDetalhePage
  // mostrar que a inspeção foi retomada, não criada agora.
  async function abrirInspecao(alojamentoId: string) {
    try {
      const resultado = await api.alojamentos.obterOuCriarInspecaoAtual(alojamentoId);
      navigate(`/prevencao/inspecoes/${resultado.inspecaoId}`, {
        state: resultado.foiCriadaAgora
          ? undefined
          : {
              avisoRetomada: `Inspeção em andamento, criada em ${new Date(resultado.criadaEm).toLocaleString('pt-BR')}.`,
            },
      });
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Não foi possível abrir a inspeção deste alojamento.');
    }
  }

  async function baixarPdfInspecao(inspecao: AlojamentoInspecaoResumo) {
    try {
      setBaixandoPdfId(inspecao.id);
      setErro(null);
      const blob = await api.inspecoes.baixarPdf(inspecao.id);
      baixarBlob(blob, nomePdfInspecao(inspecao));
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

  async function baixarPdfAssinado(inspecao: AlojamentoInspecaoResumo, documento: AlojamentoDocumentoAssinaturaResumo) {
    try {
      setBaixandoAssinadoId(documento.id);
      setErro(null);
      const blob = await api.assinatura.baixarPdf(documento.id);
      baixarBlob(blob, nomePdfAssinado(inspecao));
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Não foi possível baixar o documento assinado.');
    } finally {
      setBaixandoAssinadoId(null);
    }
  }

  // Abrem os mesmos PDFs dos botões de baixar numa janela, sem baixar.
  function visualizarPdfInspecao(inspecao: AlojamentoInspecaoResumo) {
    void visualizar({
      titulo: `Inspeção de alojamento — ${formatarData(inspecao.data)}`,
      nomeArquivo: nomePdfInspecao(inspecao),
      obter: () => api.inspecoes.baixarPdf(inspecao.id),
    });
  }

  function visualizarPdfAssinado(inspecao: AlojamentoInspecaoResumo, documento: AlojamentoDocumentoAssinaturaResumo) {
    void visualizar({
      titulo: `Inspeção de alojamento assinada — ${formatarData(inspecao.data)}`,
      nomeArquivo: nomePdfAssinado(inspecao),
      obter: () => api.assinatura.baixarPdf(documento.id),
    });
  }

  const resumoPorObra = useMemo(
    () =>
      obras.map((obra) => {
        const doAlojamentos = alojamentos.filter((a) => a.obraId === obra.id);
        const totalMoradores = doAlojamentos.reduce((soma, a) => soma + a.totalMoradores, 0);
        const pendentes = doAlojamentos.filter((a) => a.statusUltimaInspecao !== 'em-dia').length;
        return { obra, alojamentos: doAlojamentos, totalMoradores, pendentes };
      }),
    [obras, alojamentos],
  );

  const obraAtual = obraSelecionadaId ? resumoPorObra.find((r) => r.obra.id === obraSelecionadaId) : null;

  function abrirObra(obraId: string) {
    setObraSelecionadaId(obraId);
  }

  function voltarParaObras() {
    setObraSelecionadaId(null);
  }

  if (obraSelecionadaId && obraAtual) {
    return (
      <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
        <PageHeader
          titulo={`Alojamento — ${obraAtual.obra.nome}`}
          filtros={
            <Button appearance="subtle" icon={<ArrowLeft24Regular />} onClick={voltarParaObras}>
              Voltar às obras
            </Button>
          }
        />

        {erro && <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>{erro}</FeedbackInline>}

        {dialogoVisualizador}

        {obraAtual.alojamentos.length === 0 && (
          <Card>
            <EstadoVazio
              titulo="Nenhum alojamento cadastrado para esta obra ainda."
              descricao="Cadastre um alojamento para começar a controlar moradores e inspeções."
            />
          </Card>
        )}

        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(280px, 1fr))', gap: 16 }}>
          {obraAtual.alojamentos.map((alojamento) => (
            <Card key={alojamento.id}>
              <div style={{ display: 'flex', flexDirection: 'column', gap: 10 }}>
                <div style={{ fontWeight: 600 }}>{alojamento.nome}</div>
                {alojamento.endereco && <div style={{ fontSize: 12 }}>{alojamento.endereco}</div>}
                <div style={{ display: 'flex', alignItems: 'center', gap: 6, fontSize: 12, marginTop: 4 }}>
                  <Home24Regular />
                  <span style={{ fontWeight: 600 }}>{alojamento.totalMoradores}</span>
                  <span>{alojamento.totalMoradores === 1 ? 'morador' : 'moradores'}</span>
                </div>
                <div style={{ display: 'flex', gap: 6, flexWrap: 'wrap' }}>
                  <StatusChip tom={tomStatus[alojamento.statusUltimaInspecao]}>
                    {rotuloStatus(alojamento.statusUltimaInspecao, alojamento.diasDesdeUltimaInspecao)}
                  </StatusChip>
                  {alojamento.inspecaoEmAndamento && <StatusChip tom="info">Inspeção em andamento</StatusChip>}
                </div>

                {alojamento.ultimaInspecaoConcluida && (
                  <div style={{ fontSize: 12 }}>
                    Última concluída: {formatarData(alojamento.ultimaInspecaoConcluida.data)} ·{' '}
                    {alojamento.ultimaInspecaoConcluida.itensNaoConformes} pendência(s)
                  </div>
                )}

                {alojamento.ultimaInspecaoConcluida?.documentoAssinatura && (
                  <StatusChip
                    tom={
                      alojamento.ultimaInspecaoConcluida.documentoAssinatura.status === StatusDocumentoAssinatura.Finalizado
                        ? 'ok'
                        : 'info'
                    }
                  >
                    Assinatura: {statusDocumentoAssinaturaLabel[alojamento.ultimaInspecaoConcluida.documentoAssinatura.status]}
                  </StatusChip>
                )}

                <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap', marginTop: 4 }}>
                  <Button
                    appearance="primary"
                    icon={<ClipboardTaskListLtr24Regular />}
                    onClick={() => abrirInspecao(alojamento.id)}
                  >
                    {alojamento.inspecaoEmAndamento ? 'Continuar inspeção' : 'Nova inspeção'}
                  </Button>
                  {alojamento.ultimaInspecaoConcluida && (
                    <BotaoAcao
                      tom="ver"
                      icon={<Eye24Regular />}
                      onClick={() => visualizarPdfInspecao(alojamento.ultimaInspecaoConcluida!)}
                      aria-label="Visualizar última inspeção"
                    >
                      Visualizar última
                    </BotaoAcao>
                  )}
                  {alojamento.ultimaInspecaoConcluida && (
                    <BotaoAcao
                      tom="baixar"
                      icon={<ArrowDownload24Regular />}
                      disabled={baixandoPdfId === alojamento.ultimaInspecaoConcluida.id}
                      onClick={() => baixarPdfInspecao(alojamento.ultimaInspecaoConcluida!)}
                      aria-label="Baixar última inspeção"
                    >
                      Baixar última
                    </BotaoAcao>
                  )}
                  {alojamento.ultimaInspecaoConcluida && !alojamento.ultimaInspecaoConcluida.documentoAssinatura && (
                    <Button
                      appearance="secondary"
                      icon={<Send24Regular />}
                      disabled={enviandoAssinaturaId === alojamento.ultimaInspecaoConcluida.id}
                      onClick={() => enviarParaAssinatura(alojamento.ultimaInspecaoConcluida!)}
                    >
                      Enviar para assinatura
                    </Button>
                  )}
                  {alojamento.ultimaInspecaoConcluida?.documentoAssinatura?.temPdf && (
                    <BotaoAcao
                      tom="ver"
                      icon={<Eye24Regular />}
                      onClick={() =>
                        visualizarPdfAssinado(
                          alojamento.ultimaInspecaoConcluida!,
                          alojamento.ultimaInspecaoConcluida!.documentoAssinatura!,
                        )
                      }
                      aria-label="Visualizar última inspeção assinada"
                    >
                      Visualizar assinado
                    </BotaoAcao>
                  )}
                  {alojamento.ultimaInspecaoConcluida?.documentoAssinatura?.temPdf && (
                    <BotaoAcao
                      tom="baixar"
                      icon={<ArrowDownload24Regular />}
                      disabled={baixandoAssinadoId === alojamento.ultimaInspecaoConcluida.documentoAssinatura.id}
                      onClick={() =>
                        baixarPdfAssinado(
                          alojamento.ultimaInspecaoConcluida!,
                          alojamento.ultimaInspecaoConcluida!.documentoAssinatura!,
                        )
                      }
                      aria-label="Baixar última inspeção assinada"
                    >
                      Baixar assinado
                    </BotaoAcao>
                  )}
                </div>

                {alojamento.historicoInspecoes.length > 0 && (
                  <div style={{ borderTop: '1px solid rgba(0, 0, 0, 0.08)', paddingTop: 10, marginTop: 2 }}>
                    <div style={{ fontSize: 12, fontWeight: 700, marginBottom: 8 }}>Histórico de inspeções</div>
                    <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
                      {alojamento.historicoInspecoes.map((inspecao) => (
                        <div
                          key={inspecao.id}
                          style={{ display: 'grid', gridTemplateColumns: '1fr auto', alignItems: 'center', gap: 8, fontSize: 12 }}
                        >
                          <div>
                            <div style={{ fontWeight: 600 }}>
                              {formatarData(inspecao.data)} · {statusInspecaoLabel[inspecao.status]}
                            </div>
                            <div>
                              {inspecao.itensRespondidos}/{inspecao.totalItens} itens · {inspecao.itensNaoConformes} pendência(s)
                            </div>
                            {inspecao.status === StatusInspecao.Concluida && inspecao.documentoAssinatura && (
                              <div>
                                Assinatura: {statusDocumentoAssinaturaLabel[inspecao.documentoAssinatura.status]}
                              </div>
                            )}
                          </div>
                          <div style={{ display: 'flex', gap: 6 }}>
                            <BotaoAcao
                              tom="ver"
                              icon={<Open24Regular />}
                              onClick={() =>
                                inspecao.status === StatusInspecao.EmAndamento
                                  ? abrirInspecao(alojamento.id)
                                  : navigate(`/prevencao/inspecoes/${inspecao.id}`)
                              }
                              aria-label="Abrir inspeção"
                            />
                            {inspecao.status === StatusInspecao.Concluida && (
                              <BotaoAcao
                                tom="ver"
                                icon={<Eye24Regular />}
                                onClick={() => visualizarPdfInspecao(inspecao)}
                                aria-label="Visualizar PDF"
                              />
                            )}
                            {inspecao.status === StatusInspecao.Concluida && (
                              <BotaoAcao
                                tom="baixar"
                                icon={<ArrowDownload24Regular />}
                                disabled={baixandoPdfId === inspecao.id}
                                onClick={() => baixarPdfInspecao(inspecao)}
                                aria-label="Baixar PDF"
                              />
                            )}
                            {inspecao.status === StatusInspecao.Concluida && !inspecao.documentoAssinatura && (
                              <BotaoAcao
                                tom="ver"
                                icon={<Send24Regular />}
                                disabled={enviandoAssinaturaId === inspecao.id}
                                onClick={() => enviarParaAssinatura(inspecao)}
                                aria-label="Enviar para assinatura"
                              />
                            )}
                            {inspecao.status === StatusInspecao.Concluida && inspecao.documentoAssinatura?.temPdf && (
                              <BotaoAcao
                                tom="ver"
                                icon={<Eye24Regular />}
                                onClick={() => visualizarPdfAssinado(inspecao, inspecao.documentoAssinatura!)}
                                aria-label="Visualizar PDF assinado"
                              />
                            )}
                            {inspecao.status === StatusInspecao.Concluida && inspecao.documentoAssinatura?.temPdf && (
                              <BotaoAcao
                                tom="baixar"
                                icon={<ArrowDownload24Regular />}
                                disabled={baixandoAssinadoId === inspecao.documentoAssinatura.id}
                                onClick={() => baixarPdfAssinado(inspecao, inspecao.documentoAssinatura!)}
                                aria-label="Baixar PDF assinado"
                              />
                            )}
                          </div>
                        </div>
                      ))}
                    </div>
                  </div>
                )}
              </div>
            </Card>
          ))}
        </div>
      </div>
    );
  }

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <PageHeader titulo="Alojamento" subtitulo="Selecione uma obra para ver os alojamentos cadastrados nela." />

      {erro && <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>{erro}</FeedbackInline>}

      {!carregandoLista && obras.length === 0 && (
        <Card>
          <EstadoVazio
            titulo="Nenhuma obra cadastrada ainda."
            descricao="Cadastre uma obra em Obras para depois vincular alojamentos a ela."
          />
        </Card>
      )}

      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(280px, 1fr))', gap: 16 }}>
        {resumoPorObra.map(({ obra, alojamentos: doAlojamentos, totalMoradores, pendentes }) => (
          <Card key={obra.id}>
            <div
              role="button"
              tabIndex={0}
              onClick={() => abrirObra(obra.id)}
              onKeyDown={(e) => {
                if (e.key === 'Enter' || e.key === ' ') abrirObra(obra.id);
              }}
              style={{ cursor: 'pointer', display: 'flex', flexDirection: 'column', gap: 8 }}
            >
              <div style={{ fontWeight: 600 }}>{obra.nome}</div>
              <div style={{ fontSize: 12 }}>{obra.codigo}</div>
              <div style={{ display: 'flex', alignItems: 'center', gap: 6, fontSize: 12, marginTop: 4 }}>
                <Home24Regular />
                <span style={{ fontWeight: 600 }}>{doAlojamentos.length}</span>
                <span>{doAlojamentos.length === 1 ? 'alojamento' : 'alojamentos'}</span>
              </div>
              <div style={{ display: 'flex', gap: 6, flexWrap: 'wrap' }}>
                <StatusChip tom="info">{totalMoradores} morador(es)</StatusChip>
                {doAlojamentos.length > 0 && (
                  <StatusChip tom={pendentes === 0 ? 'ok' : 'alerta'}>
                    {pendentes === 0 ? 'Todos em dia' : `${pendentes} pendente(s)`}
                  </StatusChip>
                )}
              </div>
            </div>
          </Card>
        ))}
      </div>
    </div>
  );
}
