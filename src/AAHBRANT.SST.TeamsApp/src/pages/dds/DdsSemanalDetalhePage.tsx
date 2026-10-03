import { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import {
  BotaoAcao,
  Button,
  Field,
  Input,
  Card,
  PageHeader,
  StatusChip,
  FeedbackInline,
  Carregando,
  Legenda,
  FormSection,
  FormGrid,
  Campo,
  FormRodape,
  ListaSelecaoMultipla,
  Textarea,
  type Tom,
} from '@ui';
import {
  Add24Regular,
  ArrowDownload24Regular,
  CalendarCancel24Regular,
  ChevronRight24Regular,
  Dismiss16Regular,
  Eye24Regular,
  LockClosed24Regular,
  Signature24Regular,
} from '@fluentui/react-icons';
import {
  api,
  StatusDds,
  statusDdsLabel,
  StatusDdsSemanal,
  statusDdsSemanalLabel,
  tipoDdsSemanalLabel,
  TipoDdsSemanal,
  type Atividade,
  type CatalogoTemaDds,
  type DdsSemanalDetalhe,
} from '../../lib/api';
import { useVisualizadorPdf } from '../../components/useVisualizadorPdf';
import { SeletorTemaLivre } from './SeletorTemaLivre';

// A API devolve o instante em UTC (às vezes sem o "Z") — mostra no horário de Brasília.
function formatarDataHoraBrasilia(iso: string) {
  const utc = /[zZ]|[+-]\d{2}:\d{2}$/.test(iso) ? iso : `${iso}Z`;
  return new Date(utc).toLocaleString('pt-BR', { timeZone: 'America/Sao_Paulo', dateStyle: 'short', timeStyle: 'short' });
}

const NOMES_DIAS = ['Segunda-feira', 'Terça-feira', 'Quarta-feira', 'Quinta-feira', 'Sexta-feira'];

function novoDiaVazio() {
  return { atividadesIds: [] as string[], catalogoTemaDdsId: '' };
}

const tomStatusSemanal: Record<number, Tom> = {
  [StatusDdsSemanal.EmAndamento]: 'info',
  [StatusDdsSemanal.Concluida]: 'ok',
};

const tomStatusDia: Record<number, Tom> = {
  [StatusDds.EmAndamento]: 'atencao',
  [StatusDds.Concluido]: 'ok',
};

// Semana (contêiner) do DDS reformulado (31/08) — cada um dos 5 dias úteis é um registro diário
// próprio (DdsDetalhePage), feito e assinado no seu próprio dia. Esta tela mostra os 5 slots
// Seg-Sex: dia já registrado abre o detalhe; dia em aberto mostra o formulário de criação do
// registro daquele dia (seleção de atividades + origem do tema, ver CriarDdsCommand).
// Onda 2 (Task 14): conversões 2 (estilos.card/toolbar → Card + PageHeader), 3 (Text size=500 →
// PageHeader.titulo) e 4 (erro → FeedbackInline). Nada aqui usava <Table>/Badge de status com cor
// própria além do status geral (conversão 5 aplicada de passagem nos dois status). O checklist de
// atividades do dia usava useCheckboxChipStyles cru — vira ChipCheckboxGroup (o próprio componente
// já cita DDS como consumidor no seu comentário de origem). Sem WorkflowActions: a única transição
// real (encerrar semana) tem um único item condicional, sem ganho sobre um botão simples no rodapé
// do formulário — mesmo julgamento já registrado para PcmsoDetalhePage.tsx (Task 12).
export function DdsSemanalDetalhePage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const [detalhe, setDetalhe] = useState<DdsSemanalDetalhe | null>(null);
  const [atividades, setAtividades] = useState<Atividade[]>([]);
  const [catalogoTemas, setCatalogoTemas] = useState<CatalogoTemaDds[]>([]);
  const [diaEmCriacao, setDiaEmCriacao] = useState<string | null>(null);
  const [novoDia, setNovoDia] = useState(novoDiaVazio());
  const [diaMarcandoSemExpediente, setDiaMarcandoSemExpediente] = useState<string | null>(null);
  const [motivoSemExpediente, setMotivoSemExpediente] = useState('');
  const [responsavelTerceirizadaNome, setResponsavelTerceirizadaNome] = useState('');
  const [responsavelTerceirizadaFuncao, setResponsavelTerceirizadaFuncao] = useState('');
  const [erro, setErro] = useState<string | null>(null);
  const [processando, setProcessando] = useState(false);
  const [baixandoPdf, setBaixandoPdf] = useState(false);
  const [baixandoSemana, setBaixandoSemana] = useState(false);
  const [assinando, setAssinando] = useState<string | null>(null);
  const { visualizar, dialogoVisualizador } = useVisualizadorPdf();

  async function carregar() {
    if (!id) return;
    try {
      setErro(null);
      const det = await api.ddsSemanal.obterDetalhe(id);
      setDetalhe(det);
      const [listaAtividades, listaTemas] = await Promise.all([
        api.atividades.listar(det.semanal.obraId),
        api.catalogoTemasDds.listar(),
      ]);
      setAtividades(listaAtividades);
      setCatalogoTemas(listaTemas);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar a semana de DDS.');
    }
  }

  useEffect(() => {
    carregar();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [id]);

  function abrirCriacaoDia(data: string) {
    setDiaEmCriacao(data);
    setDiaMarcandoSemExpediente(null);
    setNovoDia(novoDiaVazio());
  }

  function abrirSemExpediente(data: string) {
    setDiaEmCriacao(null);
    setDiaMarcandoSemExpediente(data);
    setMotivoSemExpediente('');
  }

  async function confirmarSemExpediente() {
    if (!id || !diaMarcandoSemExpediente || !motivoSemExpediente.trim()) {
      setErro('Informe o motivo (feriado, folga, obra parada etc.).');
      return;
    }
    try {
      setProcessando(true);
      setErro(null);
      await api.dds.registrarSemExpediente(id, diaMarcandoSemExpediente, motivoSemExpediente.trim());
      setDiaMarcandoSemExpediente(null);
      setMotivoSemExpediente('');
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao registrar o dia sem expediente.');
    } finally {
      setProcessando(false);
    }
  }

  async function criarRegistroDia() {
    if (!id || !diaEmCriacao || novoDia.atividadesIds.length === 0) {
      setErro('Selecione ao menos uma atividade do dia.');
      return;
    }
    try {
      setProcessando(true);
      setErro(null);
      await api.dds.criar({
        ddsSemanalId: id,
        atividadesIds: novoDia.atividadesIds,
        data: diaEmCriacao,
        catalogoTemaDdsId: novoDia.catalogoTemaDdsId || null,
      });
      setDiaEmCriacao(null);
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao criar o registro do dia.');
    } finally {
      setProcessando(false);
    }
  }

  async function encerrarSemana() {
    if (!id) return;
    try {
      setProcessando(true);
      setErro(null);
      await api.ddsSemanal.encerrar(id, {
        responsavelEmpresaTerceirizadaNome: responsavelTerceirizadaNome || null,
        responsavelEmpresaTerceirizadaFuncao: responsavelTerceirizadaFuncao || null,
      });
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao encerrar a semana.');
    } finally {
      setProcessando(false);
    }
  }

  async function baixarPdf() {
    if (!id) return;
    try {
      setBaixandoPdf(true);
      setErro(null);
      const blob = await api.ddsSemanal.baixarPdf(id);
      const url = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = nomeArquivoPdf(id);
      link.click();
      URL.revokeObjectURL(url);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao gerar o PDF da semana.');
    } finally {
      setBaixandoPdf(false);
    }
  }

  // "Baixar semana": um PDF com o DDS semanal e todos os diários (listas de presença).
  async function baixarSemanaCompleta() {
    if (!id) return;
    try {
      setBaixandoSemana(true);
      setErro(null);
      const blob = await api.ddsSemanal.baixarSemanaCompleta(id);
      const url = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = `dds-semana-completa-${id}.pdf`;
      link.click();
      URL.revokeObjectURL(url);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao gerar o PDF da semana completa.');
    } finally {
      setBaixandoSemana(false);
    }
  }

  // Assinatura com um clique (sessão logada) de um dos dois campos do documento — o técnico assina
  // cada um no seu botão; o PDF só mostra assinado o campo que de fato foi assinado.
  async function assinarCampo(campo: 'responsavel-dds' | 'responsavel-obra-sst') {
    if (!id) return;
    try {
      setAssinando(campo);
      setErro(null);
      await api.ddsSemanal.assinar(id, campo);
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao registrar a assinatura.');
    } finally {
      setAssinando(null);
    }
  }

  function nomeArquivoPdf(semanalId: string) {
    return `dds-semanal-${semanalId}.pdf`;
  }

  // Abre o mesmo PDF do "Baixar PDF da semana" numa janela, sem baixar.
  // "Visualizar semana": mostra na tela o mesmo pacote de "Baixar semana" (semanal + diários).
  function visualizarSemanaCompleta() {
    if (!id) return;
    void visualizar({ titulo: 'Semana completa de DDS', nomeArquivo: `dds-semana-completa-${id}.pdf`, obter: () => api.ddsSemanal.baixarSemanaCompleta(id) });
  }

  function visualizarPdf() {
    if (!id) return;
    void visualizar({ titulo: 'PDF da semana de DDS', nomeArquivo: nomeArquivoPdf(id), obter: () => api.ddsSemanal.baixarPdf(id) });
  }

  if (!id) return <FeedbackInline tom="erro">Semana de DDS não encontrada.</FeedbackInline>;

  const semanal = detalhe?.semanal;

  // Erro na carga inicial precisa aparecer aqui: sem isso o skeleton ficaria para sempre (mesmo
  // padrão já revisado em NaoConformidadeDetalhePage.tsx/PgrDetalhePage.tsx).
  if (!detalhe || !semanal) {
    return erro ? (
      <FeedbackInline tom="erro" acao={{ rotulo: 'Tentar de novo', aoClicar: () => void carregar() }}>
        {erro}
      </FeedbackInline>
    ) : (
      <Carregando variante="detalhe" linhas={6} />
    );
  }

  const somenteLeitura = semanal.status !== StatusDdsSemanal.EmAndamento;
  const podeEncerrar =
    !somenteLeitura &&
    detalhe.dias.length === 5 &&
    detalhe.dias.every((d) => d.status === StatusDds.Concluido) &&
    (semanal.tipo !== TipoDdsSemanal.Terceirizados || (responsavelTerceirizadaNome.trim() && responsavelTerceirizadaFuncao.trim()));

  return (
    <div>
      <PageHeader
        titulo={`${semanal.numeroDocumento ? `${semanal.numeroDocumento} — ` : ''}${tipoDdsSemanalLabel[semanal.tipo]} — ${semanal.obraNome}`}
        subtitulo={[
          `Semana: ${semanal.dataInicioSemana?.slice(0, 10)} a ${semanal.dataFimSemana?.slice(0, 10)}`,
          `Responsável/Treinador: ${semanal.responsavelUsuarioNome}`,
          semanal.empresaTerceirizada ? `Empresa terceirizada: ${semanal.empresaTerceirizada}` : null,
          semanal.localFrenteServico ? `Local/Frente de serviço: ${semanal.localFrenteServico}` : null,
        ]
          .filter(Boolean)
          .join(' · ')}
        status={<StatusChip tom={tomStatusSemanal[semanal.status] ?? 'neutro'}>{statusDdsSemanalLabel[semanal.status]}</StatusChip>}
        voltarPara="/prevencao/dds"
        rotuloVoltar="Semanas de DDS"
        acoes={
          <>
            <BotaoAcao tom="ver" icon={<Eye24Regular />} onClick={visualizarPdf} aria-label="Visualizar PDF da semana">
              Visualizar PDF da semana
            </BotaoAcao>
            <BotaoAcao tom="baixar" icon={<ArrowDownload24Regular />} onClick={baixarPdf} disabled={baixandoPdf} aria-label="Baixar PDF da semana">
              Baixar PDF da semana
            </BotaoAcao>
            <BotaoAcao tom="ver" icon={<Eye24Regular />} onClick={visualizarSemanaCompleta} aria-label="Visualizar semana completa">
              Visualizar semana
            </BotaoAcao>
            <BotaoAcao tom="baixar" icon={<ArrowDownload24Regular />} onClick={baixarSemanaCompleta} disabled={baixandoSemana} aria-label="Baixar semana completa">
              {baixandoSemana ? 'Gerando…' : 'Baixar semana'}
            </BotaoAcao>
          </>
        }
      />
      {dialogoVisualizador}

      {erro && (
        <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>
          {erro}
        </FeedbackInline>
      )}

      <div style={{ marginBottom: 16 }}>
        <Card titulo="Assinaturas do documento" densidade="compacta">
          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(280px, 1fr))', gap: 16 }}>
            {(
              [
                { campo: 'responsavel-dds', rotulo: 'Responsável / Treinador pelo DDS', assinatura: detalhe.assinaturaResponsavelDds },
                { campo: 'responsavel-obra-sst', rotulo: 'Responsável da Obra / SST', assinatura: detalhe.assinaturaResponsavelObraSst },
              ] as const
            ).map(({ campo, rotulo, assinatura }) => (
              <div key={campo} style={{ display: 'grid', gap: 8, alignContent: 'start' }}>
                <strong>{rotulo}</strong>
                {assinatura ? (
                  <>
                    <StatusChip tom="ok">Assinado</StatusChip>
                    <Legenda>
                      {assinatura.nome} — {formatarDataHoraBrasilia(assinatura.assinadoEm)}
                    </Legenda>
                  </>
                ) : (
                  <Button
                    appearance="primary"
                    icon={<Signature24Regular />}
                    onClick={() => assinarCampo(campo)}
                    disabled={assinando !== null}
                  >
                    {assinando === campo ? 'Assinando…' : `Assinar como ${rotulo}`}
                  </Button>
                )}
              </div>
            ))}
          </div>
        </Card>
      </div>

      {!somenteLeitura && (
        <div style={{ marginBottom: 16 }}>
          <Card titulo="Encerramento da semana" densidade="compacta">
            {semanal.tipo === TipoDdsSemanal.Terceirizados && (
              <FormSection titulo="Responsável da Empresa Terceirizada" numero={1} primeira>
                <FormGrid>
                  <Campo span={6}>
                    <Field label="Nome do responsável da empresa terceirizada" required>
                      <Input value={responsavelTerceirizadaNome} onChange={(_, d) => setResponsavelTerceirizadaNome(d.value)} />
                    </Field>
                  </Campo>
                  <Campo span={6}>
                    <Field label="Função do responsável da empresa terceirizada" required>
                      <Input value={responsavelTerceirizadaFuncao} onChange={(_, d) => setResponsavelTerceirizadaFuncao(d.value)} />
                    </Field>
                  </Campo>
                </FormGrid>
              </FormSection>
            )}
            <FormRodape info="Só é possível encerrar quando os 5 dias úteis estiverem registrados e concluídos.">
              <Button appearance="primary" icon={<LockClosed24Regular />} onClick={encerrarSemana} disabled={processando || !podeEncerrar}>
                Encerrar semana
              </Button>
            </FormRodape>
          </Card>
        </div>
      )}

      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(260px, 1fr))', gap: 16 }}>
        {detalhe.dias.map((dia, indice) => (
          <Card key={dia.data} densidade="compacta" titulo={NOMES_DIAS[indice]} subtitulo={dia.data?.slice(0, 10)}>
            {dia.ddsId && dia.semExpediente ? (
              <>
                <div style={{ marginBottom: 8 }}>
                  <StatusChip tom="info" icone={<CalendarCancel24Regular />}>
                    Sem expediente
                  </StatusChip>
                </div>
                <Legenda>{dia.motivoSemExpediente}</Legenda>
              </>
            ) : dia.ddsId ? (
              <>
                <div style={{ marginBottom: 8 }}>
                  {dia.atividadesNomes.join(', ') || (dia.temaLivreNome ? '' : 'DDS do dia')}
                  {dia.temaLivreNome ? ` + ${dia.temaLivreNome}` : ''}
                </div>
                <div style={{ display: 'flex', gap: 8, alignItems: 'center', marginBottom: 8, flexWrap: 'wrap' }}>
                  {dia.status !== undefined && dia.status !== null && (
                    <StatusChip tom={tomStatusDia[dia.status] ?? 'neutro'}>{statusDdsLabel[dia.status]}</StatusChip>
                  )}
                  <Legenda>Fotos: {dia.totalFotosEvidencia}/3</Legenda>
                  <Legenda>Participantes: {dia.totalParticipantes}</Legenda>
                </div>
                <Button appearance="primary" icon={<ChevronRight24Regular />} onClick={() => navigate(`/prevencao/dds/dia/${dia.ddsId}`)}>
                  Abrir registro do dia
                </Button>
              </>
            ) : somenteLeitura ? (
              <Legenda>Nenhum registro criado para este dia.</Legenda>
            ) : diaMarcandoSemExpediente === dia.data ? (
              <FormSection titulo="Dia sem expediente" primeira>
                <FormGrid>
                  <Campo span={12}>
                    <Field label="Motivo (feriado, folga, obra parada etc.)">
                      <Textarea
                        value={motivoSemExpediente}
                        onChange={(_, d) => setMotivoSemExpediente(d.value)}
                      />
                    </Field>
                  </Campo>
                </FormGrid>
                <FormRodape>
                  <Button appearance="subtle" onClick={() => setDiaMarcandoSemExpediente(null)}>
                    Cancelar
                  </Button>
                  <Button appearance="primary" icon={<CalendarCancel24Regular />} onClick={confirmarSemExpediente} disabled={processando}>
                    Confirmar
                  </Button>
                </FormRodape>
              </FormSection>
            ) : diaEmCriacao === dia.data ? (
              <FormSection titulo="Registro do dia" primeira>
                <FormGrid>
                  <Campo span={12}>
                    <Field label="Atividades do dia" hint="Cada atividade marcada entra como um tema do dia, com perigo, consequência e controles da Matriz de Riscos.">
                      {atividades.length === 0 ? (
                        <Legenda>Nenhuma atividade cadastrada para esta obra.</Legenda>
                      ) : (
                        <div style={{ display: 'grid', gap: 8 }}>
                          {novoDia.atividadesIds.length > 0 && (
                            <div style={{ display: 'flex', gap: 6, flexWrap: 'wrap' }} aria-label="Atividades escolhidas">
                              {novoDia.atividadesIds.map((idAtividade) => (
                                <Button key={idAtividade} size="small" appearance="outline" icon={<Dismiss16Regular />} iconPosition="after"
                                  aria-label={`Remover ${atividades.find((a) => a.id === idAtividade)?.nome ?? 'atividade'}`}
                                  onClick={() => setNovoDia((atual) => ({ ...atual, atividadesIds: atual.atividadesIds.filter((x) => x !== idAtividade) }))}>
                                  {atividades.find((a) => a.id === idAtividade)?.nome ?? 'Atividade'}
                                </Button>
                              ))}
                            </div>
                          )}
                          <ListaSelecaoMultipla
                            aria-label="Atividades do dia"
                            placeholderBusca="Buscar atividade"
                            opcoes={atividades.map((a) => ({ id: a.id, rotulo: a.nome }))}
                            selecionados={novoDia.atividadesIds}
                            aoMudar={(atualizar) => setNovoDia((atual) => ({ ...atual, atividadesIds: atualizar(atual.atividadesIds) }))}
                          />
                        </div>
                      )}
                    </Field>
                  </Campo>
                  <Campo span={12}>
                    <Field label="Tema livre (opcional)" hint="Temas criados pela equipe, como campanhas do mês. Só um por registro.">
                      <SeletorTemaLivre
                        temas={catalogoTemas}
                        selecionadoId={novoDia.catalogoTemaDdsId}
                        aoSelecionar={(idTema) => setNovoDia((atual) => ({ ...atual, catalogoTemaDdsId: idTema }))}
                        aoCriar={(tema) => {
                          setCatalogoTemas((atuais) => [...atuais, tema].sort((a, b) => a.nome.localeCompare(b.nome, 'pt-BR')));
                          setNovoDia((atual) => ({ ...atual, catalogoTemaDdsId: tema.id }));
                        }}
                      />
                    </Field>
                  </Campo>
                </FormGrid>
                <FormRodape>
                  <Button appearance="subtle" onClick={() => setDiaEmCriacao(null)}>
                    Cancelar
                  </Button>
                  <Button appearance="primary" icon={<Add24Regular />} onClick={criarRegistroDia} disabled={processando}>
                    Criar registro do dia
                  </Button>
                </FormRodape>
              </FormSection>
            ) : (
              <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap' }}>
                <Button icon={<Add24Regular />} onClick={() => abrirCriacaoDia(dia.data)}>
                  Criar registro do dia
                </Button>
                <Button icon={<CalendarCancel24Regular />} onClick={() => abrirSemExpediente(dia.data)}>
                  Marcar sem expediente
                </Button>
              </div>
            )}
          </Card>
        ))}
      </div>
    </div>
  );
}
