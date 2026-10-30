import { pendenciasFoto } from '../../lib/dadosFoto';
import { useCallback, useEffect, useRef, useState } from 'react';
import { useLocation, useNavigate, useParams } from 'react-router-dom';
import {
  BotaoAcao,
  Button,
  Campo,
  Card,
  CampoData,
  Carregando,
  DetailPageLayout,
  Field,
  FeedbackInline,
  FormGrid,
  Input,
  Select,
  StatusChip,
  Text,
  Textarea,
  WorkflowActions,
  type AcaoWorkflow,
  type Tom,
} from '@ui';
import {
  ArrowDownload24Regular,
  Eye24Regular,
  Open24Regular,
  Save24Regular,
  Signature24Regular,
  Warning24Regular,
} from '@fluentui/react-icons';
import {
  api,
  StatusInspecao,
  StatusItemChecklist,
  statusInspecaoLabel,
  statusItemChecklistLabel,
  TipoInspecao,
  tipoInspecaoLabel,
  tipoVeiculoLabel,
  type InspecaoDetalhe,
  type Usuario,
} from '../../lib/api';
import { SlotFoto } from '../../components/camera/SlotFoto';
import { SeletorStatusItemChecklist } from '../../components/inspecoes/SeletorStatusItemChecklist';
import { useRegistrosTravados } from '../../hooks/useRegistrosTravados';
import { useVisualizadorPdf } from '../../components/useVisualizadorPdf';
import { assinarMudancasSync, estaOnline } from '../../lib/offline/syncEngine';
import { obterInspecaoLocal, salvarRascunhoInspecao, salvarRespostaInspecao, salvarFotoInspecao,
  finalizarInspecaoLocal, estadoSyncInspecao, baixarFotoInspecaoLocal, edicaoDaResposta } from '../../lib/offline/inspecoes';

interface EdicaoResposta {
  descricao: string;
  statusItem: string;
  observacao: string;
  local: string;
  planoDeAcao: string;
  responsavelUsuarioId: string;
  prazo: string;
}

function edicaoInicial(): EdicaoResposta {
  return { descricao: '', statusItem: '', observacao: '', local: '', planoDeAcao: '', responsavelUsuarioId: '', prazo: '' };
}

// EmAndamento/Concluida — mesmo mapeamento de InspecoesTab.tsx (Onda 2 ruling: mesmos tons entre
// lista e detalhe do módulo).
const tomPorStatusInspecao: Record<number, Tom> = {
  [StatusInspecao.EmAndamento]: 'info',
  [StatusInspecao.Concluida]: 'ok',
};

// Cor do status do ponto verificado — mesmo esquema verde/amarelo da planilha "Patrulha de Segurança do
// Trabalho" (pendente = ainda não conforme, resolvido = já corrigido e reavaliado como conforme).
const tomPorStatusItem: Record<number, Tom> = {
  [StatusItemChecklist.Conforme]: 'ok',
  [StatusItemChecklist.NaoConforme]: 'atencao',
  [StatusItemChecklist.NaoAplicavel]: 'info',
};

function formatarDataIso(data?: string | null) {
  if (!data) return '';
  const [ano, mes, dia] = data.slice(0, 10).split('-');
  return dia && mes && ano ? `${dia}/${mes}/${ano}` : data;
}

function DadoResumo({ rotulo, valor }: { rotulo: string; valor: string }) {
  return (
    <div style={{ borderBottom: '1px solid rgba(0, 0, 0, 0.08)', padding: '0 0 10px' }}>
      <Text size={200} style={{ display: 'block', color: '#667085', marginBottom: 4 }}>
        {rotulo}
      </Text>
      <Text weight="semibold">{valor}</Text>
    </div>
  );
}

// Onda 2 Task 10 (camada ui/): detalhe de uma execução de inspeção. DetailPageLayout com resumo e a
// única ação de fluxo (encerrar) na lateral — "Baixar PDF" e "Assinar inspeção" ficam no cabeçalho
// por serem navegação/exportação, não transição de estado (mesmo critério de AprDetalhePage.tsx: só
// aprovar/reprovar entram em WorkflowActions, exportar fica em cabecalho.acoes). Retorno antecipado
// com OU erro OU skeleton na carga inicial, nunca cabeçalho+skeleton+erro empilhados (padrão já
// revisado em AprDetalhePage.tsx/PgrDetalhePage.tsx).
export function InspecaoDetalhePage() {
  const { id } = useParams<{ id: string }>();
  // Inspeção assinada não aceita mais respostas nem fotos (o servidor recusa): os campos ficam só para leitura.
  const inspecaoAssinada = useRegistrosTravados('Inspecao', id ? [id] : []).has(id ?? '');
  const navigate = useNavigate();
  const location = useLocation();
  // Task 9 (2026-09-15): quando AlojamentoTab retoma (em vez de criar) a inspeção do dia, o aviso
  // vem via location.state — lido só na primeira renderização; navegar de novo pra cá sem esse
  // estado (ex. F5) não deve reexibir um aviso obsoleto.
  const [avisoRetomada, setAvisoRetomada] = useState<string | null>(
    (location.state as { avisoRetomada?: string } | null)?.avisoRetomada ?? null,
  );
  const [detalhe, setDetalhe] = useState<InspecaoDetalhe | null>(null);
  const [usuarios, setUsuarios] = useState<Usuario[]>([]);
  const [edicoes, setEdicoes] = useState<Record<string, EdicaoResposta>>({});
  const [erro, setErro] = useState<string | null>(null);
  const [processando, setProcessando] = useState(false);
  const [gerandoOcorrenciaId, setGerandoOcorrenciaId] = useState<string | null>(null);
  const [baixandoPdf, setBaixandoPdf] = useState(false);
  const { visualizar, dialogoVisualizador } = useVisualizadorPdf();
  const [fotoUrls, setFotoUrls] = useState<Record<string, string>>({});
  const [fotoDepoisUrls, setFotoDepoisUrls] = useState<Record<string, string>>({});
  const [fotoAdicionalUrls, setFotoAdicionalUrls] = useState<Record<string, string>>({});
  const [sync, setSync] = useState({ pendentes: 0, conflitos: 0, criacaoPendente: false, finalizacaoPendente: false, itensPendentes: [] as string[] });
  const [rascunhos, setRascunhos] = useState<string[]>([]);
  const [salvandoRascunho, setSalvandoRascunho] = useState(false);
  const gravacoes = useRef(0);
  const escritaRascunhos = useRef<Promise<void>>(Promise.resolve());
  const versaoEdicao = useRef(0);
  const versaoCarga = useRef(0);
  const urlsCriadas = useRef(new Set<string>());
  const criarUrl = (blob: Blob) => { const url = URL.createObjectURL(blob); urlsCriadas.current.add(url); return url; };

  const carregar = useCallback(async () => {
    if (!id) return;
    const versao = ++versaoCarga.current;
    const edicaoNoInicio = versaoEdicao.current;
    try {
      setErro(null);
      await escritaRascunhos.current;
      const [local, listaUsuarios] = await Promise.all([obterInspecaoLocal(id), api.usuarios.listar().catch(() => [] as Usuario[])]);
      const estado = await estadoSyncInspecao(id);
      if (versao !== versaoCarga.current) return;
      setDetalhe(local.detalhe);
      setUsuarios(listaUsuarios);
      setSync(estado);
      // Uma atualização de rede iniciada antes do toque não pode apagar o que acabou de ser digitado.
      if (versaoEdicao.current === edicaoNoInicio) {
        setRascunhos(Object.keys(local.rascunhos));
        setEdicoes(Object.fromEntries(local.detalhe.respostas.map(r => [r.id, local.rascunhos[r.id] ?? edicaoDaResposta(r)])));
      }
    } catch (e) {
      if (versao === versaoCarga.current) setErro(e instanceof Error ? e.message : 'Falha ao carregar inspeção.');
    }
  }, [id]);

  useEffect(() => {
    void carregar();
    const cancelar = assinarMudancasSync(() => void carregar());
    return () => { cancelar(); versaoCarga.current += 1; };
  }, [carregar]);

  // Miniaturas das evidências (antes/depois) são baixadas sob demanda (só para respostas com
  // temFoto/temFotoDepois) e mantidas como object URL até a página ser desmontada — mesmo padrão de
  // TrabalhadoresTab.baixarFoto/ObrasPage.baixarLogo.
  useEffect(() => {
    let cancelado = false;
    (async () => {
      if (!detalhe) return;
      for (const resposta of detalhe.respostas) {
        if (resposta.temFoto && !fotoUrls[resposta.id]) {
          try {
            const blob = await baixarFotoInspecaoLocal(detalhe.inspecao.id, resposta.id);
            if (cancelado) return;
            setFotoUrls((atual) => ({ ...atual, [resposta.id]: criarUrl(blob) }));
          } catch {
            // Falha ao carregar miniatura não impede o uso da página; o slot fica vazio.
          }
        }
        for (const extra of resposta.fotosAdicionais ?? []) {
          if (fotoAdicionalUrls[extra.id]) continue;
          try {
            const blob = await api.inspecoes.baixarFotoAdicional(extra.id);
            if (cancelado) return;
            setFotoAdicionalUrls((atual) => ({ ...atual, [extra.id]: criarUrl(blob) }));
          } catch {
            // Falha ao carregar miniatura não impede o uso da página; o slot fica vazio.
          }
        }
        if (resposta.temFotoDepois && !fotoDepoisUrls[resposta.id]) {
          try {
            const blob = await baixarFotoInspecaoLocal(detalhe.inspecao.id, resposta.id, true);
            if (cancelado) return;
            setFotoDepoisUrls((atual) => ({ ...atual, [resposta.id]: criarUrl(blob) }));
          } catch {
            // Falha ao carregar miniatura não impede o uso da página; o slot fica vazio.
          }
        }
      }
    })();
    return () => {
      cancelado = true;
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [detalhe]);

  useEffect(() => {
    const urls = urlsCriadas.current;
    return () => {
      urls.forEach((url) => URL.revokeObjectURL(url));
      urls.clear();
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  function atualizarEdicao(respostaId: string, campos: Partial<EdicaoResposta>) {
    versaoEdicao.current += 1;
    setEdicoes((atual) => ({
      ...atual,
      [respostaId]: { ...(atual[respostaId] ?? edicaoInicial()), ...campos },
    }));
    if (!id) return;
    gravacoes.current += 1;
    setSalvandoRascunho(true);
    setRascunhos(atual => atual.includes(respostaId) ? atual : [...atual, respostaId]);
    escritaRascunhos.current = escritaRascunhos.current.then(() => salvarRascunhoInspecao(id, respostaId, campos)).catch((e) => {
      setErro(`O rascunho não foi salvo: ${e instanceof Error ? e.message : 'verifique o armazenamento do dispositivo'}`);
    }).finally(() => {
      gravacoes.current -= 1;
      if (gravacoes.current === 0) setSalvandoRascunho(false);
    });
  }

  async function salvarResposta(respostaId: string) {
    const edicao = edicoes[respostaId];
    if (!edicao?.statusItem) {
      setErro('Selecione o status do ponto verificado antes de salvar.');
      return;
    }
    try {
      setProcessando(true);
      setErro(null);
      if (!id) return;
      await escritaRascunhos.current;
      await salvarRespostaInspecao(id, respostaId, edicao);
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao salvar o ponto verificado.');
    } finally {
      setProcessando(false);
    }
  }

  async function enviarFoto(respostaId: string, arquivo: File) {
    try {
      setErro(null);
      if (!id) return;
      await salvarFotoInspecao(id, respostaId, arquivo);
      setFotoUrls(atual => ({ ...atual, [respostaId]: criarUrl(arquivo) }));
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao enviar a evidência anterior.');
    }
  }

  async function enviarFotoAdicional(respostaId: string, arquivo: File) {
    try {
      setErro(null);
      await api.inspecoes.anexarFotoAdicional(respostaId, arquivo);
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao enviar a 2ª foto do item.');
    }
  }

  async function removerFotoAdicional(fotoId: string) {
    try {
      setErro(null);
      await api.inspecoes.removerFotoAdicional(fotoId);
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao remover a 2ª foto do item.');
    }
  }

  async function enviarFotoDepois(respostaId: string, arquivo: File) {
    try {
      setErro(null);
      if (!id) return;
      await salvarFotoInspecao(id, respostaId, arquivo, true);
      setFotoDepoisUrls(atual => ({ ...atual, [respostaId]: criarUrl(arquivo) }));
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao enviar a evidência posterior.');
    }
  }

  // Quem abre a inspeção nem sempre é o responsável (30/10/2026): troca liberada até a assinatura,
  // mesmo depois de concluída — o servidor recusa se o documento já estiver assinado.
  async function alterarResponsavel(responsavelUsuarioId: string) {
    if (!id || !responsavelUsuarioId) return;
    try {
      setErro(null);
      await api.inspecoes.alterarResponsavel(id, responsavelUsuarioId);
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao alterar o responsável da inspeção.');
    }
  }

  async function gerarOcorrencia(respostaId: string) {
    if (!navigator.onLine || !estaOnline()) {
      setErro('Gerar ocorrência exige conexão. As não conformidades também serão geradas pelo servidor ao finalizar a inspeção.');
      return;
    }
    try {
      setGerandoOcorrenciaId(respostaId);
      setErro(null);
      const { id: ncId } = await api.inspecoes.gerarOcorrencia(respostaId, {});
      await carregar();
      navigate(`/nao-conformidades/${ncId}`);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao gerar ocorrência a partir do item.');
    } finally {
      setGerandoOcorrenciaId(null);
    }
  }

  // Entra em WorkflowActions: retornar `false` mantém o formulário aberto (aqui não há formulário —
  // a ação executa direto — mas o contrato pede erro tratado sem lançar, mesmo padrão de
  // AprDetalhePage.tsx).
  async function encerrar() {
    if (!id) return false;
    try {
      setProcessando(true);
      setErro(null);
      await finalizarInspecaoLocal(id);
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao encerrar inspeção. Confira se todos os itens foram respondidos.');
      return false;
    } finally {
      setProcessando(false);
    }
  }

  async function baixarPdf() {
    if (!id || !detalhe) return;
    try {
      setBaixandoPdf(true);
      setErro(null);
      const blob = await api.inspecoes.baixarPdf(id);
      const url = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = nomeArquivoPdf(detalhe);
      link.click();
      URL.revokeObjectURL(url);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao gerar o PDF da inspeção.');
    } finally {
      setBaixandoPdf(false);
    }
  }

  function nomeArquivoPdf(det: InspecaoDetalhe) {
    return `inspecao-${det.inspecao.data?.slice(0, 10)}.pdf`;
  }

  // Abre o mesmo PDF do "Baixar PDF" numa janela, sem baixar.
  function visualizarPdf() {
    if (!id || !detalhe) return;
    void visualizar({ titulo: 'PDF da inspeção', nomeArquivo: nomeArquivoPdf(detalhe), obter: () => api.inspecoes.baixarPdf(id) });
  }

  if (!id) return <FeedbackInline tom="erro">Inspeção não encontrada.</FeedbackInline>;

  // Erro na carga inicial precisa aparecer aqui: sem isso o skeleton ficaria para sempre e a falha
  // (API fora, id inexistente) não teria onde ser lida.
  if (!detalhe) {
    return erro ? (
      <FeedbackInline tom="erro" acao={{ rotulo: 'Tentar de novo', aoClicar: () => void carregar() }}>
        {erro}
      </FeedbackInline>
    ) : (
      <Carregando variante="detalhe" linhas={8} />
    );
  }

  const inspecao = detalhe.inspecao;
  const ehInspecaoAlojamento = inspecao.tipoInspecao === TipoInspecao.Alojamento;
  const ehInspecaoVeiculo = inspecao.tipoInspecao === TipoInspecao.Veiculo;
  const ehInspecaoTema = inspecao.tipoInspecao === TipoInspecao.Tema;
  // Veículos e temas criados pelo usuário: 1 foto por item, obrigatória só quando o item é Não Conforme
  // (decisão de 02/10/2026).
  const usaLayoutCompacto = ehInspecaoVeiculo || ehInspecaoTema;
  // Volta para o card de onde a inspeção foi aberta (Alojamento, Veículos ou o tema do item).
  const voltarPara = ehInspecaoAlojamento
    ? '/operacao?secao=inspecoes&cartao=alojamento'
    : ehInspecaoVeiculo
      ? '/operacao?secao=inspecoes&cartao=veiculos'
      : ehInspecaoTema && inspecao.temaInspecaoId
        ? `/operacao?secao=inspecoes&cartao=tema:${inspecao.temaInspecaoId}`
        : '/prevencao/inspecoes';

  const campoResponsavel = (
    <Field label="Responsável">
      <Select
        value={inspecao.responsavelUsuarioId}
        onChange={(_, d) => void alterarResponsavel(d.value)}
        disabled={inspecaoAssinada || processando || sync.criacaoPendente}
        style={{ width: '100%' }}
      >
        {!usuarios.some((u) => u.id === inspecao.responsavelUsuarioId) && (
          <option value={inspecao.responsavelUsuarioId}>{inspecao.responsavelUsuarioNome}</option>
        )}
        {usuarios.map((usuario) => (
          <option key={usuario.id} value={usuario.id}>
            {usuario.nome}
          </option>
        ))}
      </Select>
    </Field>
  );

  const acoes: AcaoWorkflow[] = [];
  if (inspecao.status === StatusInspecao.EmAndamento && !sync.finalizacaoPendente) {
    acoes.push({
      chave: 'encerrar',
      rotulo: 'Finalizar inspeção',
      finalizacao: true,
      pendencias: [...(rascunhos.length ? ['Salve os itens em rascunho antes de finalizar.'] : []), ...detalhe.respostas.flatMap(r => {
        const p: string[] = [];
        if (r.statusItem == null) p.push('resposta do checklist');
        if (r.exigeFotografia && !r.temFoto) p.push('foto anterior obrigatória');
        if (ehInspecaoTema && !r.temFoto) p.push('foto do item obrigatória');
        else if (ehInspecaoVeiculo && r.statusItem === StatusItemChecklist.NaoConforme && !r.temFoto) p.push('foto da não conformidade obrigatória');
        if (r.temFoto && r.dadosFoto) p.push(...pendenciasFoto(r.dadosFoto).map(d => 'foto anterior: ' + d));
        if (r.temFotoDepois && r.dadosFotoDepois) p.push(...pendenciasFoto(r.dadosFotoDepois).map(d => 'foto posterior: ' + d));
        return p.length ? [r.descricao + ': ' + p.join('; ')] : [];
      })],
      descricao: 'Confira se todos os itens foram respondidos antes de encerrar.',
      tom: 'primario',
      habilitada: true,
      aoExecutar: encerrar,
    });
  }

  return (
    <DetailPageLayout
      cabecalho={{
        titulo: ehInspecaoTema
          ? `${inspecao.temaInspecaoNome ?? 'Tema'} — ${inspecao.itemInspecionavelIdentificacao ?? inspecao.checklistModeloNome}`
          : `${tipoInspecaoLabel[inspecao.tipoInspecao]} — ${inspecao.checklistModeloNome} (v${inspecao.checklistModeloVersao})`,
        status: (
          <StatusChip tom={tomPorStatusInspecao[inspecao.status] ?? 'neutro'}>
            {sync.finalizacaoPendente ? 'Finalização pendente' : sync.criacaoPendente ? 'Criada no tablet' : statusInspecaoLabel[inspecao.status]}
          </StatusChip>
        ),
        voltarPara,
        rotuloVoltar: 'Voltar para Inspeções',
        acoes: (
          <>
            <BotaoAcao tom="ver" icon={<Eye24Regular />} onClick={visualizarPdf} aria-label="Visualizar PDF">
              Visualizar PDF
            </BotaoAcao>
            <BotaoAcao tom="baixar" icon={<ArrowDownload24Regular />} onClick={baixarPdf} disabled={baixandoPdf} aria-label="Baixar PDF">
              Baixar PDF
            </BotaoAcao>
            {dialogoVisualizador}
            {inspecao.status === StatusInspecao.Concluida && (
              <Button
                appearance="primary"
                icon={<Signature24Regular />}
                onClick={() => navigate(`/prevencao/inspecoes/${id}/assinar`)}
              >
                Assinar inspeção
              </Button>
            )}
          </>
        ),
      }}
      lateral={
        <>
          <Card densidade="compacta" titulo="Resumo">
            <DadoResumo rotulo="Protocolo" valor={inspecao.numeroDocumento ?? (sync.criacaoPendente ? 'Aguardando confirmação do servidor' : '—')} />
            {ehInspecaoTema ? (
              <div style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
                <DadoResumo rotulo="Tema" valor={inspecao.temaInspecaoNome ?? '—'} />
                <DadoResumo rotulo="Item" valor={inspecao.itemInspecionavelIdentificacao ?? '—'} />
                <DadoResumo rotulo="Obra" valor={inspecao.obraNome} />
                <DadoResumo rotulo="Data do preenchimento" valor={formatarDataIso(inspecao.data)} />
                {campoResponsavel}
                <DadoResumo rotulo="Itens respondidos" valor={`${inspecao.itensRespondidos}/${inspecao.totalItens}`} />
              </div>
            ) : ehInspecaoVeiculo ? (
              <div style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
                <DadoResumo rotulo="Placa/Prefixo" valor={inspecao.veiculoPlacaPrefixo ?? '—'} />
                <DadoResumo rotulo="Tipo de veículo" valor={inspecao.veiculoTipo ? tipoVeiculoLabel[inspecao.veiculoTipo] : '—'} />
                {inspecao.veiculoMarcaModelo && <DadoResumo rotulo="Marca/Modelo" valor={inspecao.veiculoMarcaModelo} />}
                {inspecao.veiculoEmpresa && <DadoResumo rotulo="Subcontratada" valor={inspecao.veiculoEmpresa} />}
                <DadoResumo rotulo="Obra" valor={inspecao.obraNome} />
                <DadoResumo rotulo="Data do preenchimento" valor={formatarDataIso(inspecao.data)} />
                {campoResponsavel}
                <DadoResumo rotulo="Itens respondidos" valor={`${inspecao.itensRespondidos}/${inspecao.totalItens}`} />
              </div>
            ) : ehInspecaoAlojamento ? (
              <div style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
                <DadoResumo rotulo="Obra vinculada ao alojamento" valor={inspecao.obraNome} />
                <DadoResumo rotulo="Data do preenchimento" valor={formatarDataIso(inspecao.data)} />
                {campoResponsavel}
                <DadoResumo rotulo="Itens respondidos" valor={`${inspecao.itensRespondidos}/${inspecao.totalItens}`} />
              </div>
            ) : (
              <FormGrid>
                <Campo span={12}>
                  <Field label="Obra">
                    <Input value={inspecao.obraNome} readOnly />
                  </Field>
                </Campo>
                {inspecao.atividadeNome && (
                  <Campo span={12}>
                    <Field label="Atividade">
                      <Input value={inspecao.atividadeNome} readOnly />
                    </Field>
                  </Campo>
                )}
                <Campo span={12}>
                  <Field label="Data">
                    <Input value={inspecao.data?.slice(0, 10) ?? ''} readOnly />
                  </Field>
                </Campo>
                <Campo span={12}>
                  {campoResponsavel}
                </Campo>
                <Campo span={12}>
                  <Field label="Pontos verificados respondidos">
                    <Input value={`${inspecao.itensRespondidos}/${inspecao.totalItens}`} readOnly />
                  </Field>
                </Campo>
              </FormGrid>
            )}
            {inspecao.itensNaoConformes > 0 && (
              <div style={{ marginTop: 12 }}>
                <StatusChip tom="atencao">{inspecao.itensNaoConformes} pendente(s)</StatusChip>
              </div>
            )}
          </Card>
          {acoes.length > 0 && (
            <Card densidade="compacta" titulo="Ações disponíveis">
              <WorkflowActions acoes={acoes} processando={processando} erro={erro} />
            </Card>
          )}
        </>
      }
    >
      {avisoRetomada && (
        <FeedbackInline tom="info" aoFechar={() => setAvisoRetomada(null)}>
          {avisoRetomada}
        </FeedbackInline>
      )}

      {erro && (
        <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>
          {erro}
        </FeedbackInline>
      )}

      {sync.pendentes > 0 && (
        <FeedbackInline tom="info">
          Salvo no tablet — aguardando sincronização. {sync.pendentes} ação(ões) pendente(s).
          {sync.finalizacaoPendente && ' A inspeção só aparecerá como concluída após a confirmação do servidor.'}
        </FeedbackInline>
      )}
      {sync.criacaoPendente && <FeedbackInline tom="info">Inspeção criada neste tablet. Você pode preencher os itens e guardar as fotos sem internet. O protocolo será gerado quando o servidor confirmar a criação.</FeedbackInline>}
      {sync.conflitos > 0 && (
        <FeedbackInline tom="aviso">Há envios recusados desta inspeção. Abra o indicador de sincronização para conferir o motivo e tentar novamente ou descartar. A finalização aguarda a resolução dessas pendências.</FeedbackInline>
      )}
      {rascunhos.length > 0 && (
        <FeedbackInline tom="info">{salvandoRascunho ? 'Salvando rascunho no tablet…' : 'Rascunho salvo no tablet. Use Salvar item para incluir as respostas na sincronização.'}</FeedbackInline>
      )}

      <Text weight="semibold">Pontos verificados</Text>

      <div style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
        {detalhe.respostas.map((resposta, indice) => {
          const edicao = edicoes[resposta.id] ?? edicaoInicial();
          const somenteLeitura = inspecao.status !== StatusInspecao.EmAndamento || inspecaoAssinada || processando || (sync.finalizacaoPendente && sync.conflitos === 0);
          // Cabeçalho de seção só aparece na fronteira entre seções diferentes — checklists sem
          // Secao preenchida (todos os anteriores ao campo existir) continuam em lista corrida.
          const secaoAnterior = indice > 0 ? detalhe.respostas[indice - 1].secao : undefined;
          const mostrarCabecalhoSecao = !!resposta.secao && resposta.secao !== secaoAnterior;
          return (
            <div key={resposta.id}>
              {mostrarCabecalhoSecao && (
                <Text weight="semibold" style={{ display: 'block', margin: '16px 0 8px' }}>
                  {resposta.secao}
                </Text>
              )}
              <Card densidade="compacta">
              {sync.itensPendentes.includes(resposta.id) && <StatusChip tom="atencao">Item salvo no tablet — envio pendente</StatusChip>}
              {usaLayoutCompacto ? (
                <>
                  <div style={{ display: 'grid', gridTemplateColumns: 'minmax(260px, 1fr) minmax(220px, 320px)', gap: 16, alignItems: 'start' }}>
                    <div>
                      <Text weight="semibold" style={{ display: 'block', marginBottom: 8 }}>
                        Item {resposta.ordem}
                      </Text>
                      <Text style={{ display: 'block', lineHeight: 1.45 }}>
                        {resposta.descricao}
                      </Text>
                    </div>
                    <Field label="Status do item">
                      <SeletorStatusItemChecklist
                        value={edicao.statusItem ? Number(edicao.statusItem) : null}
                        onChange={(valor) => atualizarEdicao(resposta.id, { statusItem: String(valor) })}
                        disabled={somenteLeitura}
                      />
                    </Field>
                  </div>

                  <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(220px, 1fr))', gap: 16, marginTop: 16, alignItems: 'start' }}>
                    <Field label="Observação (opcional)">
                      <Textarea
                        value={edicao.observacao}
                        onChange={(_, d) => atualizarEdicao(resposta.id, { observacao: d.value })}
                        disabled={somenteLeitura}
                        rows={2}
                      />
                    </Field>

                    {(ehInspecaoTema || edicao.statusItem === String(StatusItemChecklist.NaoConforme)) && (
                      <Field label={ehInspecaoTema ? 'Fotos do item (1ª obrigatória, até 2)' : 'Fotos da não conformidade (1ª obrigatória, até 2)'}>
                        <div style={{ display: 'flex', gap: 12, flexWrap: 'wrap' }}>
                        <div style={{ width: 180 }}>
                          <SlotFoto
                            rotulo={ehInspecaoTema ? 'Foto do item' : 'Foto da não conformidade'}
                            contextoFoto={{ obraId: inspecao.obraId, obraNome: inspecao.obraNome, local: inspecao.veiculoPlacaPrefixo }}
                            dadosFoto={resposta.dadosFoto}
                            url={fotoUrls[resposta.id]}
                            carregandoMiniatura={resposta.temFoto && !fotoUrls[resposta.id]}
                            somenteLeitura={somenteLeitura}
                            aoSelecionarArquivo={(arquivo) => enviarFoto(resposta.id, arquivo)}
                            aoErroValidacao={setErro}
                          />
                        </div>
                        {(() => {
                          // 2ª foto (opcional): liberada depois da 1ª; online só.
                          const extra = resposta.fotosAdicionais?.find((f) => f.ordem === 2);
                          if (!extra && (!resposta.temFoto || somenteLeitura)) return null;
                          return (
                            <div style={{ width: 180 }}>
                              <SlotFoto
                                rotulo="2ª foto"
                                contextoFoto={{ obraId: inspecao.obraId, obraNome: inspecao.obraNome, local: inspecao.veiculoPlacaPrefixo }}
                                dadosFoto={extra?.dadosFoto}
                                url={extra ? fotoAdicionalUrls[extra.id] : null}
                                carregandoMiniatura={!!extra && !fotoAdicionalUrls[extra.id]}
                                somenteLeitura={somenteLeitura}
                                modo="remover"
                                aoRemover={extra ? () => removerFotoAdicional(extra.id) : undefined}
                                aoSelecionarArquivo={(arquivo) => enviarFotoAdicional(resposta.id, arquivo)}
                                aoErroValidacao={setErro}
                              />
                            </div>
                          );
                        })()}
                        </div>
                      </Field>
                    )}
                  </div>

                  {!somenteLeitura && (
                    <div style={{ display: 'flex', justifyContent: 'flex-end', marginTop: 16 }}>
                      <Button
                        appearance="primary"
                        icon={<Save24Regular />}
                        onClick={() => salvarResposta(resposta.id)}
                        disabled={processando || salvandoRascunho}
                      >
                        Salvar item
                      </Button>
                    </div>
                  )}
                </>
              ) : ehInspecaoAlojamento ? (
                <>
                  <div style={{ display: 'grid', gridTemplateColumns: 'minmax(260px, 1fr) minmax(220px, 320px)', gap: 16, alignItems: 'start' }}>
                    <div>
                      <Text weight="semibold" style={{ display: 'block', marginBottom: 8 }}>
                        Item {resposta.ordem}
                      </Text>
                      <Text style={{ display: 'block', lineHeight: 1.45 }}>
                        {resposta.descricao}
                      </Text>
                    </div>
                    <Field label="Status do item">
                      <SeletorStatusItemChecklist
                        value={edicao.statusItem ? Number(edicao.statusItem) : null}
                        onChange={(valor) => atualizarEdicao(resposta.id, { statusItem: String(valor) })}
                        disabled={somenteLeitura}
                      />
                    </Field>
                  </div>

                  <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(180px, 1fr))', gap: 16, marginTop: 16 }}>
                    <Field label="Foto do item (obrigatória)">
                      <div style={{ maxWidth: 180 }}>
                        <SlotFoto
                          rotulo="Foto do item"
                          contextoFoto={{ obraId: inspecao.obraId, obraNome: inspecao.obraNome, local: resposta.local }}
                          dadosFoto={resposta.dadosFoto}
                          url={fotoUrls[resposta.id]}
                          carregandoMiniatura={resposta.temFoto && !fotoUrls[resposta.id]}
                          somenteLeitura={somenteLeitura}
                          aoSelecionarArquivo={(arquivo) => enviarFoto(resposta.id, arquivo)}
                          aoErroValidacao={setErro}
                        />
                      </div>
                    </Field>

{/* Alojamento: 1 foto por item (30/10). A posterior só aparece em inspeções antigas que já a têm. */}
                    {resposta.temFotoDepois && (
                    <Field label="Evidência posterior (registro antigo)">
                      <div style={{ maxWidth: 180 }}>
                        <SlotFoto
                          rotulo="Evidência posterior"
                          contextoFoto={{ obraId: inspecao.obraId, obraNome: inspecao.obraNome, local: resposta.local }}
                          dadosFoto={resposta.dadosFotoDepois}
                          url={fotoDepoisUrls[resposta.id]}
                          carregandoMiniatura={resposta.temFotoDepois && !fotoDepoisUrls[resposta.id]}
                          somenteLeitura={somenteLeitura}
                          aoSelecionarArquivo={(arquivo) => enviarFotoDepois(resposta.id, arquivo)}
                          aoErroValidacao={setErro}
                        />
                      </div>
                    </Field>
                    )}
                  </div>

                  {!somenteLeitura && (
                    <div style={{ display: 'flex', justifyContent: 'flex-end', marginTop: 16 }}>
                      <Button
                        appearance="primary"
                        icon={<Save24Regular />}
                        onClick={() => salvarResposta(resposta.id)}
                        disabled={processando || salvandoRascunho}
                      >
                        Salvar item
                      </Button>
                    </div>
                  )}
                </>
              ) : (
                <>
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', gap: 12, flexWrap: 'wrap' }}>
                <div style={{ flex: 1, minWidth: 260 }}>
                  <Text weight="semibold" style={{ display: 'block', marginBottom: 8 }}>
                    Ponto verificado {resposta.ordem}
                  </Text>
                  <Field label="Descrição do ponto verificado">
                    <Input
                      value={edicao.descricao}
                      onChange={(_, d) => atualizarEdicao(resposta.id, { descricao: d.value })}
                      disabled={somenteLeitura}
                      placeholder="Descreva a irregularidade encontrada"
                    />
                  </Field>
                </div>
                <div style={{ minWidth: 180 }}>
                  <Field label="Status do ponto verificado">
                    <Select
                      value={edicao.statusItem}
                      onChange={(_, d) => atualizarEdicao(resposta.id, { statusItem: d.value })}
                      disabled={somenteLeitura}
                      style={{ width: '100%' }}
                    >
                      <option value="">Selecione o status</option>
                      {Object.entries(statusItemChecklistLabel).map(([valor, rotulo]) => (
                        <option key={valor} value={valor}>
                          {rotulo}
                        </option>
                      ))}
                    </Select>
                  </Field>
                </div>
                {resposta.statusItem != null && (
                  <StatusChip tom={tomPorStatusItem[resposta.statusItem] ?? 'neutro'}>
                    {statusItemChecklistLabel[resposta.statusItem]}
                  </StatusChip>
                )}
              </div>

              <div style={{ display: 'flex', gap: 12, marginTop: 12, flexWrap: 'wrap' }}>
                <div style={{ flex: 1, minWidth: 200 }}>
                  <Field label="Local">
                    <Input
                      value={edicao.local}
                      onChange={(_, d) => atualizarEdicao(resposta.id, { local: d.value })}
                      disabled={somenteLeitura}
                    />
                  </Field>
                </div>
                <div style={{ flex: 1, minWidth: 200 }}>
                  <Field label="Responsável">
                    <Select
                      value={edicao.responsavelUsuarioId}
                      onChange={(_, d) => atualizarEdicao(resposta.id, { responsavelUsuarioId: d.value })}
                      disabled={somenteLeitura}
                      style={{ width: '100%' }}
                    >
                      <option value="">Selecione</option>
                      {usuarios.map((usuario) => (
                        <option key={usuario.id} value={usuario.id}>
                          {usuario.nome}
                        </option>
                      ))}
                    </Select>
                  </Field>
                </div>
                <div style={{ minWidth: 160 }}>
                  <Field label="Prazo">
                    <CampoData
                      value={edicao.prazo}
                      onChange={(_, d) => atualizarEdicao(resposta.id, { prazo: d.value })}
                      disabled={somenteLeitura}
                    />
                  </Field>
                </div>
              </div>

              <div style={{ marginTop: 12 }}>
                <Field label="Plano de ação">
                  <Textarea
                    value={edicao.planoDeAcao}
                    onChange={(_, d) => atualizarEdicao(resposta.id, { planoDeAcao: d.value })}
                    disabled={somenteLeitura}
                    resize="vertical"
                  />
                </Field>
              </div>

              <div style={{ marginTop: 12 }}>
                <Field label="Observação">
                  <Textarea
                    value={edicao.observacao}
                    onChange={(_, d) => atualizarEdicao(resposta.id, { observacao: d.value })}
                    disabled={somenteLeitura}
                    resize="vertical"
                  />
                </Field>
              </div>

              {/* Evidências no mesmo padrão de quadro/miniatura usado em todo o sistema (pedido do
                  usuário, 14/09) — ver components/camera/SlotFoto.tsx. */}
              <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(240px, 1fr))', gap: 16, marginTop: 12 }}>
                <Field label="Evidência anterior">
                  <div style={{ maxWidth: 160 }}>
                    <SlotFoto
                      rotulo="Evidência anterior"
                      contextoFoto={{ obraId: inspecao.obraId, obraNome: inspecao.obraNome, local: resposta.local }}
                      dadosFoto={resposta.dadosFoto}
                      url={fotoUrls[resposta.id]}
                      carregandoMiniatura={resposta.temFoto && !fotoUrls[resposta.id]}
                      somenteLeitura={somenteLeitura}
                      aoSelecionarArquivo={(arquivo) => enviarFoto(resposta.id, arquivo)}
                      aoErroValidacao={setErro}
                    />
                  </div>
                </Field>

                <Field label="Evidência posterior">
                  <div style={{ maxWidth: 160 }}>
                    <SlotFoto
                      rotulo="Evidência posterior"
                      contextoFoto={{ obraId: inspecao.obraId, obraNome: inspecao.obraNome, local: resposta.local }}
                      dadosFoto={resposta.dadosFotoDepois}
                      url={fotoDepoisUrls[resposta.id]}
                      carregandoMiniatura={resposta.temFotoDepois && !fotoDepoisUrls[resposta.id]}
                      somenteLeitura={somenteLeitura}
                      aoSelecionarArquivo={(arquivo) => enviarFotoDepois(resposta.id, arquivo)}
                      aoErroValidacao={setErro}
                    />
                  </div>
                </Field>
              </div>

              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', gap: 12, flexWrap: 'wrap', marginTop: 16 }}>
                <div>
                  {resposta.naoConformidadeId ? (
                    <Button
                      appearance="secondary"
                      icon={<Open24Regular />}
                      onClick={() => navigate(`/nao-conformidades/${resposta.naoConformidadeId}`)}
                    >
                      Ver ocorrência
                    </Button>
                  ) : (
                    resposta.statusItem === StatusItemChecklist.NaoConforme && (
                      <Button
                        appearance="secondary"
                        icon={<Warning24Regular />}
                        onClick={() => gerarOcorrencia(resposta.id)}
                        disabled={gerandoOcorrenciaId === resposta.id}
                      >
                        Gerar ocorrência
                      </Button>
                    )
                  )}
                </div>
                {!somenteLeitura && (
                  <Button
                    appearance="primary"
                    icon={<Save24Regular />}
                    onClick={() => salvarResposta(resposta.id)}
                    disabled={processando || salvandoRascunho}
                  >
                    Salvar ponto verificado
                  </Button>
                )}
              </div>
              </>
              )}
              </Card>
            </div>
          );
        })}
      </div>
    </DetailPageLayout>
  );
}
