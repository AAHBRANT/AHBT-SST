import { useCallback, useEffect, useRef, useState } from 'react';
import { useNavigate, useParams, useSearchParams } from 'react-router-dom';
import { BotaoVoltar, Button, Card, Carregando, FeedbackInline, useEstilosEsmeralda } from '@ui';
import { ChevronRight16Regular, Info20Regular, LockClosed20Regular, Warning16Regular } from '@fluentui/react-icons';
import {
  api,
  type ConfiguracaoIntegracao,
  type EntradaIntegracao,
  type IntegracaoAtendimentoDetalhe,
  type Obra,
  type OsIntegracao,
  type SessaoTreinamentoDetalhe,
} from '../../lib/api';
import { useAgenteBiometricoLocal } from '../../components/treinamentos/useAgenteBiometricoLocal';
import { lerInstanteUtc } from '../../components/assinatura/assinaturaDigital';
import { useUsuarioLogado } from '../../lib/UsuarioLogadoContext';
import { StepperEtapas } from './componentes/StepperEtapas';
import { InicioIntegracao } from './componentes/InicioIntegracao';
import { EtapaCurso } from './componentes/EtapaCurso';
import { EtapaOrdemServico } from './componentes/EtapaOrdemServico';
import { IntegracaoConcluida } from './componentes/IntegracaoConcluida';
import { mergeClasses, useEstilosIntegracao } from './componentes/estilos';
import { useRelogio } from './componentes/useRelogio';
import { ROTULO_ALVO, alvoDaPendencia, bloqueioAvancar, fimCargaAutomaticaMs, situacaoBiometriaInstrutor, type AlvoPendencia } from './componentes/helpersInstrutor';
import {
  ehEtapaOs,
  elegiveisDaObra,
  etapaAtual,
  fraseFicaParaDepois,
  formatarCarga,
  MOTIVO_NENHUMA_PESSOA,
  mensagemEtapaConcluida,
  nomesCurtos,
  participantesAtivos,
  proximaEtapa,
  resumoPessoas,
  rotuloAvancar,
  rotuloEtapa,
  TOTAL_ETAPAS,
} from './componentes/helpers';

/** Linha da lista "Falta": com `alvo`, vira botão que rola até a seção que resolve a pendência. */
interface ItemFalta {
  texto: string;
  alvo: AlvoPendencia | null;
}

const semAlvo = (textos: string[]): ItemFalta[] => textos.map((texto) => ({ texto, alvo: null }));

interface Acao {
  rotulo: string;
  faltas: ItemFalta[];
  resumo: string;
  executar: () => void;
  desabilitada: boolean;
  /** Motivo escrito quando o botão está desabilitado por falta de escolha (ex.: nenhuma pessoa marcada). */
  motivo?: string;
}

/** dd/MM/aaaa no dia de Brasília (o slice do texto UTC erraria o dia depois das 21 h). */
function dataBrasilia(iso?: string | null): string {
  if (!iso) return '';
  const d = lerInstanteUtc(iso);
  return Number.isNaN(d.getTime()) ? '' : d.toLocaleDateString('pt-BR', { timeZone: 'America/Sao_Paulo' });
}

// O servidor devolve o UTC às vezes sem o "Z": lerInstanteUtc trata como UTC (new Date() leria como hora local e atrasaria 3 h).
function dataHoraCurta(iso?: string | null): string {
  if (!iso) return '';
  const d = lerInstanteUtc(iso);
  return Number.isNaN(d.getTime()) ? '' : d.toLocaleString('pt-BR', { dateStyle: 'short', timeStyle: 'short', timeZone: 'America/Sao_Paulo' });
}

// Realizar integração (11/11/2026, mockup v4 aprovado). O técnico conduz os admitidos de uma obra em 5
// etapas de ordem fixa: NR-06, NR-12, NR-18, NR-35 e Ordem de Serviço (NR-01). Cada etapa de curso exige
// carga mínima, 3 fotos próprias e a assinatura biométrica de cada participante no certificado daquele
// curso; a etapa só avança quando tudo isso está completo. Na última, cada pessoa lê e assina a própria
// OS. Rotas: /treinamentos/integracao/nova?obraId= (início) e /treinamentos/integracao/:id (andamento).
export function RealizarIntegracaoPage() {
  const { id } = useParams<{ id: string }>();
  const [busca] = useSearchParams();
  const navigate = useNavigate();
  const e = useEstilosIntegracao();
  const esm = useEstilosEsmeralda();
  const { agenteDisponivel, dispositivoLocal } = useAgenteBiometricoLocal();
  const { usuario } = useUsuarioLogado();

  const [obras, setObras] = useState<Obra[]>([]);
  const [obraId, setObraId] = useState(busca.get('obraId') ?? '');
  const [entrada, setEntrada] = useState<EntradaIntegracao | null>(null);
  const [instrutorId, setInstrutorId] = useState('');
  // Quem entra nesta integração: começa com todos os elegíveis da obra marcados; trocar de obra reinicia.
  const [marcados, setMarcados] = useState<Set<string>>(new Set());
  const [integracao, setIntegracao] = useState<IntegracaoAtendimentoDetalhe | null>(null);
  const [sessao, setSessao] = useState<SessaoTreinamentoDetalhe | null>(null);
  const [os, setOs] = useState<OsIntegracao | null>(null);
  const [erro, setErro] = useState<string | null>(null);
  const [erroCarga, setErroCarga] = useState<string | null>(null);
  const [confirmacao, setConfirmacao] = useState<string | null>(null);
  const [processando, setProcessando] = useState(false);
  const [carregando, setCarregando] = useState(true);
  // Intervalos das fotos automáticas (GET .../configuracao). Falhou: a tela usa o padrão (60 s / 5 min / 5 min).
  const [configuracao, setConfiguracao] = useState<ConfiguracaoIntegracao | null>(null);
  const iniciouAgora = useRef(false);
  // Obra hoje selecionada: respostas de outra obra (que chegam atrasadas) são descartadas.
  const obraSelecionadaRef = useRef(obraId);
  obraSelecionadaRef.current = obraId;
  // Trava de duplo clique (ref, vale antes do re-render): começar, avançar e concluir não repetem a chamada.
  const emAcao = useRef(false);
  // Versão do atendimento na tela: resposta de uma busca que saiu ANTES de uma ação (avançar, assinar) e chegou
  // depois dela é descartada, para a tela não voltar à etapa anterior.
  const versaoIntegracao = useRef(0);
  const aplicarIntegracao = useCallback((detalhe: IntegracaoAtendimentoDetalhe) => {
    versaoIntegracao.current += 1;
    setIntegracao(detalhe);
  }, []);
  // Aviso de erro das ações, para rolar até ele.
  const avisoErroRef = useRef<HTMLDivElement | null>(null);

  // ---- carga ----
  const carregarIntegracao = useCallback(async (alvo: string) => {
    try {
      setErroCarga(null);
      const versao = ++versaoIntegracao.current;
      const detalhe = await api.integracoesAtendimento.obter(alvo);
      if (versao === versaoIntegracao.current) setIntegracao(detalhe);
    } catch (err) {
      setErroCarga(err instanceof Error ? err.message : 'Não foi possível abrir a integração.');
    } finally {
      setCarregando(false);
    }
  }, []);

  const carregarEntrada = useCallback(
    async (alvo: string) => {
      if (!alvo) {
        setEntrada(null);
        setCarregando(false);
        return;
      }
      let obraDescartada = false;
      try {
        setErroCarga(null);
        setCarregando(true);
        const r = await api.integracoesAtendimento.entrada(alvo);
        if (obraSelecionadaRef.current !== alvo) {
          obraDescartada = true;
          return;
        }
        // Já existe atendimento aberto na obra: abre direto na etapa em que parou.
        if (r.atendimentoEmAndamento) {
          navigate(`/treinamentos/integracao/${r.atendimentoEmAndamento.id}`, { replace: true });
          return;
        }
        setEntrada(r);
        // Seleção inicial junto com a carga: a primeira pintura já vem com todos marcados.
        setMarcados(new Set(elegiveisDaObra(r.candidatos).map((c) => c.trabalhadorId)));
        setInstrutorId((atual) => (r.instrutores.some((i) => i.trabalhadorId === atual) ? atual : r.instrutores[0]?.trabalhadorId ?? ''));
      } catch (err) {
        if (obraSelecionadaRef.current === alvo) setErroCarga(err instanceof Error ? err.message : 'Não foi possível consultar a obra.');
      } finally {
        if (!obraDescartada && obraSelecionadaRef.current === alvo) setCarregando(false);
      }
    },
    [navigate],
  );

  // Depois de cadastrar a biometria do instrutor: busca a obra de novo SEM zerar quem está marcado nem a escolha do instrutor.
  const atualizarEntrada = useCallback(async (alvo: string) => {
    try {
      const r = await api.integracoesAtendimento.entrada(alvo);
      if (obraSelecionadaRef.current === alvo && !r.atendimentoEmAndamento) setEntrada(r);
    } catch {
      // A pré-checagem continua com o que já estava na tela.
    }
  }, []);

  useEffect(() => {
    if (id) return;
    void (async () => {
      try {
        const lista = await api.obras.listar();
        setObras(lista);
        setObraId((atual) => atual || lista[0]?.id || '');
      } catch (err) {
        setErroCarga(err instanceof Error ? err.message : 'Não foi possível carregar as obras.');
        setCarregando(false);
      }
    })();
  }, [id]);

  useEffect(() => {
    if (id) {
      if (integracao?.id === id) return;
      setCarregando(true);
      void carregarIntegracao(id);
    } else {
      setIntegracao(null);
      void carregarEntrada(obraId);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [id, obraId]);

  const elegiveis = elegiveisDaObra(entrada?.candidatos ?? []);
  const nMarcados = elegiveis.filter((c) => marcados.has(c.trabalhadorId)).length;
  const alternarPessoa = (trabalhadorId: string) =>
    setMarcados((atualSet) => {
      const novo = new Set(atualSet);
      if (!novo.delete(trabalhadorId)) novo.add(trabalhadorId);
      return novo;
    });
  const alternarTodos = () =>
    setMarcados(nMarcados === elegiveis.length ? new Set() : new Set(elegiveis.map((c) => c.trabalhadorId)));

  const atual = integracao ? etapaAtual(integracao) : null;
  const concluida = integracao?.status === 'concluido';
  const sessaoAlvoId = atual && !ehEtapaOs(atual) && atual.status === 'emAndamento' ? atual.sessaoTreinamentoId ?? null : null;
  const precisaOs = !!integracao && (concluida || (!!atual && ehEtapaOs(atual)));

  // Relógio da tela: a espera do fim da carga automática ("faltam X h YY min") e o Avançar recalculam por ele, e
  // o fim do período dispara a re-renderização na hora exata (e a busca do atendimento, que traz a regra do servidor).
  const fimCarga = atual && !ehEtapaOs(atual) ? fimCargaAutomaticaMs(atual.periodos) : null;
  const agoraMs = useRelogio(15_000, [fimCarga]);
  const integracaoIdParaFim = integracao?.id;
  const buscouAposFim = useRef<number | null>(null);
  useEffect(() => {
    if (fimCarga == null || agoraMs < fimCarga || buscouAposFim.current === fimCarga || !integracaoIdParaFim) return;
    buscouAposFim.current = fimCarga;
    void carregarIntegracao(integracaoIdParaFim);
  }, [agoraMs, fimCarga, integracaoIdParaFim, carregarIntegracao]);

  useEffect(() => {
    if (!integracaoIdParaFim) return;
    let cancelado = false;
    api.integracoesAtendimento
      .configuracao()
      .then((c) => {
        if (!cancelado) setConfiguracao(c);
      })
      .catch(() => undefined);
    return () => {
      cancelado = true;
    };
  }, [integracaoIdParaFim]);

  /** Rola até a seção que resolve a pendência, põe o foco nela (leitor de tela) e a destaca por um instante. */
  function irParaAlvo(alvo: AlvoPendencia) {
    const secao = document.querySelector<HTMLElement>(`[data-alvo="${alvo}"]`);
    if (!secao) return;
    const reduzir = window.matchMedia?.('(prefers-reduced-motion: reduce)').matches;
    secao.scrollIntoView({ behavior: reduzir ? 'auto' : 'smooth', block: 'center' });
    secao.focus({ preventScroll: true });
    secao.classList.remove(e.destaqueBreve);
    void secao.offsetWidth;
    secao.classList.add(e.destaqueBreve);
    window.setTimeout(() => secao.classList.remove(e.destaqueBreve), 1800);
  }

  const sessaoAlvoRef = useRef(sessaoAlvoId);
  sessaoAlvoRef.current = sessaoAlvoId;
  const recarregarSessao = useCallback(async () => {
    if (!sessaoAlvoId) return;
    try {
      const detalhe = await api.sessoesTreinamento.obterDetalhe(sessaoAlvoId);
      // Trocou de etapa enquanto a turma carregava: a resposta da etapa anterior não entra.
      if (sessaoAlvoRef.current === sessaoAlvoId) setSessao(detalhe);
    } catch (err) {
      setErro(err instanceof Error ? err.message : 'Não foi possível carregar a turma da etapa.');
    }
  }, [sessaoAlvoId]);

  useEffect(() => {
    setSessao(null);
    if (sessaoAlvoId) void recarregarSessao();
  }, [sessaoAlvoId, recarregarSessao]);

  const integracaoId = integracao?.id;
  // Carga, fotos e assinaturas mudam o que falta para avançar: recarrega a turma e a integração juntas.
  const recarregarEtapa = useCallback(async () => {
    await Promise.all([recarregarSessao(), integracaoId ? carregarIntegracao(integracaoId) : Promise.resolve()]);
  }, [recarregarSessao, carregarIntegracao, integracaoId]);
  const recarregarOs = useCallback(async () => {
    if (!integracaoId) return;
    try {
      setOs(await api.integracoesAtendimento.ordensServico(integracaoId));
    } catch (err) {
      setErro(err instanceof Error ? err.message : 'Não foi possível carregar as Ordens de Serviço.');
    }
  }, [integracaoId]);

  useEffect(() => {
    if (precisaOs) void recarregarOs();
  }, [precisaOs, recarregarOs]);

  // Erro de uma ação lá embaixo (carga, assinatura, avançar) aparece no topo: leva o olhar até ele.
  useEffect(() => {
    if (erro) avisoErroRef.current?.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
  }, [erro]);

  // ---- ações ----
  async function comecar() {
    if (!entrada || emAcao.current) return;
    emAcao.current = true;
    const pessoas = elegiveis.filter((c) => marcados.has(c.trabalhadorId)).map((c) => c.trabalhadorId);
    if (pessoas.length === 0) {
      emAcao.current = false;
      return;
    }
    try {
      setProcessando(true);
      setErro(null);
      const novo = await api.integracoesAtendimento.iniciar(entrada.obraId, pessoas, instrutorId);
      iniciouAgora.current = true;
      aplicarIntegracao(novo);
      setConfirmacao(null);
      navigate(`/treinamentos/integracao/${novo.id}`, { replace: true });
    } catch (err) {
      setErro(err instanceof Error ? err.message : 'Não foi possível iniciar a integração.');
    } finally {
      emAcao.current = false;
      setProcessando(false);
    }
  }

  async function avancar() {
    if (!integracao || !atual || emAcao.current) return;
    emAcao.current = true;
    try {
      setProcessando(true);
      setErro(null);
      const mensagem = mensagemEtapaConcluida(atual);
      const novo = await api.integracoesAtendimento.avancar(integracao.id, atual.ordem);
      aplicarIntegracao(novo);
      setConfirmacao(mensagem);
    } catch (err) {
      // 409 explica o que falta; recarrega para pegar a versão atual do atendimento.
      setErro(err instanceof Error ? err.message : 'Não foi possível avançar a etapa.');
      void recarregarEtapa();
    } finally {
      emAcao.current = false;
      setProcessando(false);
    }
  }

  async function concluir() {
    if (!integracao || emAcao.current) return;
    emAcao.current = true;
    try {
      setProcessando(true);
      setErro(null);
      const novo = await api.integracoesAtendimento.concluir(integracao.id);
      aplicarIntegracao(novo);
      setConfirmacao(null);
      await recarregarOs();
    } catch (err) {
      setErro(err instanceof Error ? err.message : 'Não foi possível concluir a integração.');
      void carregarIntegracao(integracao.id);
    } finally {
      emAcao.current = false;
      setProcessando(false);
    }
  }

  function voltar() {
    const unico = integracao?.participantes.length === 1 ? integracao.participantes[0] : null;
    navigate(unico ? `/pessoas/${unico.trabalhadorId}` : '/pessoas');
  }

  // ---- ação principal (botão grande da lateral, do rodapé e da barra fixa) ----
  let acao: Acao;
  if (!integracao) {
    const n = nMarcados;
    const total = elegiveis.length;
    const bloqueio = entrada?.bloqueios[0]?.mensagem;
    const semMarcar = total > 0 && n === 0;
    // Pré-checagem (decisão 2, 16/11): instrutor sem digital nem facial bloqueia o Iniciar; ASO não entra aqui.
    const instrutorSemBiometria = situacaoBiometriaInstrutor(entrada?.instrutores.find((i) => i.trabalhadorId === instrutorId)) === 'sem_biometria';
    const motivoInstrutor = 'Disponível quando o instrutor tiver biometria cadastrada.';
    acao = {
      rotulo: 'Começar pela NR-06',
      faltas: [],
      resumo:
        bloqueio ??
        (instrutorSemBiometria
          ? motivoInstrutor
          : total === 0
            ? 'Nenhum admitido para integrar'
            : semMarcar
              ? MOTIVO_NENHUMA_PESSOA
              : n === total
                ? `${n} pessoa${n > 1 ? 's' : ''} selecionada${n > 1 ? 's' : ''}`
                : `${n} de ${total} pessoas selecionadas`),
      executar: () => void comecar(),
      desabilitada: !entrada?.podeIniciar || !instrutorId || processando || n === 0 || instrutorSemBiometria,
      motivo: instrutorSemBiometria ? motivoInstrutor : semMarcar ? MOTIVO_NENHUMA_PESSOA : undefined,
    };
  } else if (concluida) {
    acao = { rotulo: 'Voltar ao perfil', faltas: [], resumo: 'Integração concluída', executar: voltar, desabilitada: false };
  } else if (atual && ehEtapaOs(atual)) {
    const pessoasOs = os?.pessoas ?? [];
    // "pendente" (RH, ex.: sem ASO) não segura a conclusão; "bloqueada" (PGR/engenheiro) segura.
    const faltas = nomesCurtos(pessoasOs.filter((p) => !['pendente', 'assinada', 'bloqueada'].includes(p.situacao)).map((p) => p.nome));
    const bloqueadas = nomesCurtos(pessoasOs.filter((p) => p.situacao === 'bloqueada').map((p) => p.nome));
    const lista = !os
      ? ['carregando as Ordens de Serviço']
      : [
          ...(faltas.length ? [`assinatura da OS de ${faltas.join(', ')}`] : []),
          ...(bloqueadas.length ? [`OS bloqueada por pendência do PGR (resolve: SST): ${bloqueadas.join(', ')}`] : []),
        ];
    acao = {
      rotulo: 'Concluir integração',
      faltas: semAlvo(lista),
      resumo: lista.length ? `Falta: ${lista.join('; ')}` : 'Tudo pronto para concluir',
      executar: () => void concluir(),
      desabilitada: lista.length > 0 || processando,
    };
  } else if (atual) {
    // Avançar fica desabilitado enquanto a etapa tiver QUALQUER pendência (servidor: etapa.pendencias), com o motivo
    // e, na carga, o tempo que falta ("Carga horária fecha às 13:00 — faltam 2 h 48 min"). A trava real é do servidor (409).
    const bloqueio = bloqueioAvancar(atual, integracao.instrutorTrabalhadorId, agoraMs);
    acao = {
      rotulo: rotuloAvancar(proximaEtapa(integracao, atual)),
      faltas: bloqueio.pendencias.map((p) => ({ texto: p.texto, alvo: alvoDaPendencia(p.codigo) })),
      resumo: bloqueio.motivo ? `Falta: ${bloqueio.pendencias.map((p) => p.texto).join('; ')}` : 'Tudo pronto para avançar',
      executar: () => void avancar(),
      desabilitada: bloqueio.bloqueado || processando,
    };
  } else {
    acao = { rotulo: 'Voltar', faltas: [], resumo: '', executar: voltar, desabilitada: false };
  }

  const botaoAcao = (
    <>
      <Button
        appearance="primary"
        size="large"
        className={mergeClasses(e.btnGrande, e.btnCheio)}
        icon={acao.faltas.length > 0 ? <LockClosed20Regular /> : undefined}
        disabled={acao.desabilitada}
        onClick={acao.executar}
      >
        {acao.rotulo}
      </Button>
      {acao.motivo && (
        <div className={e.motivo}>
          <Warning16Regular aria-hidden="true" />
          <span>{acao.motivo}</span>
        </div>
      )}
      {acao.faltas.length > 0 && (
        <>
          <div className={e.mudo} style={{ marginTop: 10 }}>
            Falta{acao.faltas.some((f) => f.alvo) ? ' (toque para ir direto)' : ''}:
          </div>
          {acao.faltas.some((f) => f.alvo) ? (
            <ul className={e.faltaBotoes}>
              {acao.faltas.map((f) => (
                <li key={f.texto}>
                  {f.alvo ? (
                    <button
                      type="button"
                      className={e.faltaBotao}
                      aria-label={`${f.texto}. Ir para ${ROTULO_ALVO[f.alvo]}`}
                      onClick={() => irParaAlvo(f.alvo!)}
                    >
                      <Warning16Regular aria-hidden="true" />
                      <span>{f.texto}</span>
                      <span className={e.faltaIr} aria-hidden="true">
                        Ir <ChevronRight16Regular />
                      </span>
                    </button>
                  ) : (
                    <div className={mergeClasses(e.faltaBotao, e.faltaBotaoSemAcao)}>
                      <Warning16Regular aria-hidden="true" />
                      <span>{f.texto}</span>
                    </div>
                  )}
                </li>
              ))}
            </ul>
          ) : (
            <ul className={e.faltaLista}>
              {acao.faltas.map((f) => (
                <li key={f.texto}>{f.texto}</li>
              ))}
            </ul>
          )}
        </>
      )}
    </>
  );

  // ---- resumo lateral ----
  function linhasResumo(): Array<[string, string]> {
    if (!integracao) {
      return [['Pessoas', resumoPessoas(nMarcados, elegiveis.length)], ['Etapas', '5 (NR-12 e NR-35 só para quem exige)'], ['Primeira etapa', 'NR-06']];
    }
    if (concluida) {
      const certificados = integracao.etapas.filter((x) => !ehEtapaOs(x)).reduce((t, x) => t + participantesAtivos(x).length, 0);
      const assinadas = os?.assinadas ?? 0;
      const pendentes = os?.pendentes ?? 0;
      const linhas: Array<[string, string]> = [['Etapas', `${TOTAL_ETAPAS} de ${TOTAL_ETAPAS}`], ['Certificados', String(certificados)], ['OS assinadas', String(assinadas)]];
      if (pendentes > 0) linhas.push(['Pendência', `${pendentes} com OS pendente`]);
      return linhas;
    }
    if (!atual) return [];
    const rot = rotuloEtapa(atual.tipo);
    if (ehEtapaOs(atual)) {
      const bloqueadas = os?.pendentes ?? 0;
      const prontas = (os?.totalPessoas ?? 0) - bloqueadas;
      return [
        ['Etapa', `${atual.ordem} de ${TOTAL_ETAPAS} · ${rot.curto}`],
        ['OS assinadas', `${os?.assinadas ?? 0}/${prontas}`],
        ['Bloqueadas', String(bloqueadas)],
      ];
    }
    const min = Math.round(atual.cargaMinimaHoras * 60);
    const ativos = participantesAtivos(atual);
    const assinaram = ativos.filter((p) => p.presencaConfirmada).length;
    const total = ativos.length;
    if (atual.participantes.length === 0) return [['Etapa', `${atual.ordem} de ${TOTAL_ETAPAS} · ${rot.codigo}`], ['Situação', 'Ninguém precisa']];
    return [
      ['Etapa', `${atual.ordem} de ${TOTAL_ETAPAS} · ${rot.codigo}`],
      ['Carga', `${formatarCarga(atual.cargaLancadaMinutos)}${min ? ` de ${atual.cargaMinimaHoras} h` : ''}`],
      ['Fotos', `${atual.fotosRegistradas}/${atual.fotosNecessarias || 3}`],
      ['Assinaram', `${assinaram}/${total}`],
    ];
  }

  // ---- render ----
  if (carregando) return <Carregando variante="detalhe" linhas={8} />;
  if (erroCarga && !integracao && !entrada) {
    return (
      <FeedbackInline tom="erro" acao={{ rotulo: 'Tentar de novo', aoClicar: () => (id ? void carregarIntegracao(id) : void carregarEntrada(obraId)) }}>
        {erroCarga}
      </FeedbackInline>
    );
  }

  const nParticipantes = integracao ? integracao.participantes.length : elegiveis.length;
  const entradaFotosAutomaticas = entrada?.fotosAutomaticas ?? null;
  const obraNome = integracao?.obraNome ?? entrada?.obraNome ?? obras.find((o) => o.id === obraId)?.nome ?? '—';
  const periodo = !integracao
    ? 'A iniciar'
    : concluida && integracao.concluidaEmUtc
      ? `${dataBrasilia(integracao.iniciadaEmUtc)} a ${dataBrasilia(integracao.concluidaEmUtc)}`
      : `Desde ${dataBrasilia(integracao.iniciadaEmUtc)}`;
  const retomado = !!integracao && !concluida && !iniciouAgora.current;

  return (
    <div className={e.pagina}>
      <div className={e.topo}>
        <BotaoVoltar rotulo="Voltar" onClick={() => (integracao ? voltar() : navigate(-1))} />
        <span className={e.migalha}>Pessoas · Funcionário novo · <b>Realizar integração</b></span>
      </div>

      <div className={e.layout}>
        <div className={e.principal}>
          <section className={mergeClasses(e.cab, esm.vidro)} aria-label="Dados da integração">
            <div className={e.cabLinha}>
              <div>
                <div className={e.cabEyebrow}>Integração de admitidos</div>
                <h1 className={e.cabTitulo}>Realizar integração</h1>
              </div>
              <span className={mergeClasses(e.cabChip, esm.vidroChip)}>
                {concluida ? 'Concluída' : integracao ? 'Em andamento' : 'Não iniciada'}
              </span>
            </div>
            <div className={e.cabMeta}>
              <div><span className={e.cabMetaRotulo}>Obra</span><span className={e.cabMetaValor}>{obraNome}</span></div>
              <div><span className={e.cabMetaRotulo}>Período</span><span className={e.cabMetaValor}>{periodo}</span></div>
              <div><span className={e.cabMetaRotulo}>Técnico responsável</span><span className={e.cabMetaValor}>{integracao?.responsavelNome ?? usuario?.nome ?? '—'}</span></div>
              <div><span className={e.cabMetaRotulo}>Admitidos</span><span className={e.cabMetaValor}>{nParticipantes} {nParticipantes === 1 ? 'pessoa' : 'pessoas'}</span></div>
            </div>
            {retomado && integracao && (
              <div className={mergeClasses(e.cabAviso, esm.vidroChip)}>
                <Info20Regular />
                <div>
                  <b>Atendimento retomado.</b> Este atendimento já estava aberto desde {dataHoraCurta(integracao.iniciadaEmUtc).replace(', ', ' às ')}. Etapas
                  concluídas, fotos e assinaturas já registradas foram mantidas.
                </div>
              </div>
            )}
          </section>

          {erro && (
            <div ref={avisoErroRef}>
              <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>
                {erro}
              </FeedbackInline>
            </div>
          )}
          {erroCarga && (integracao || entrada) && (
            <FeedbackInline
              tom="erro"
              aoFechar={() => setErroCarga(null)}
              acao={integracao ? { rotulo: 'Atualizar', aoClicar: () => void carregarIntegracao(integracao.id) } : undefined}
            >
              {erroCarga}
            </FeedbackInline>
          )}
          {confirmacao && (
            <FeedbackInline tom="sucesso" aoFechar={() => setConfirmacao(null)}>
              <b>{confirmacao}</b>
            </FeedbackInline>
          )}

          <StepperEtapas
            previa={!integracao}
            etapas={integracao?.etapas ?? []}
            etapaAtualOrdem={atual?.ordem ?? null}
            concluida={concluida}
          />

          {!integracao && (
            <InicioIntegracao
              obras={obras}
              obraId={obraId}
              aoMudarObra={(v) => {
                setObraId(v);
                setEntrada(null);
              }}
              entrada={entrada}
              marcados={marcados}
              aoAlternarPessoa={alternarPessoa}
              aoAlternarTodos={alternarTodos}
              instrutorId={instrutorId}
              aoMudarInstrutor={setInstrutorId}
              aoAtualizarInstrutores={() => void atualizarEntrada(obraId)}
            />
          )}

          {integracao && concluida && <IntegracaoConcluida integracao={integracao} os={os} />}

          {integracao && !concluida && atual && !ehEtapaOs(atual) && (
            <EtapaCurso
              key={atual.id}
              integracao={integracao}
              etapa={atual}
              sessao={sessao}
              recarregar={recarregarEtapa}
              aoAtualizarIntegracao={aplicarIntegracao}
              aoErro={setErro}
              agenteDisponivel={agenteDisponivel}
              dispositivoLocal={dispositivoLocal}
              somenteLeitura={false}
              agoraMs={agoraMs}
              fotosAutomaticas={configuracao?.fotosAutomaticas ?? entradaFotosAutomaticas}
            />
          )}

          {integracao && !concluida && atual && ehEtapaOs(atual) && (
            <EtapaOrdemServico key={atual.id} ordem={atual.ordem} os={os} somenteLeitura={false} recarregarOs={recarregarOs} aoErro={setErro} instrutorNome={integracao.instrutorNome} />
          )}

          {concluida && (
            <div className={e.rodapePrincipal}>
              <Button appearance="primary" size="large" className={mergeClasses(e.btnGrande, e.btnCheio)} onClick={voltar}>
                Voltar ao perfil
              </Button>
            </div>
          )}
        </div>

        <aside className={e.lateral} aria-label="Resumo da etapa">
          <Card titulo="Resumo da etapa" densidade="compacta">
            {linhasResumo().map(([rotulo, valor]) => (
              <div key={rotulo} className={e.resumoLinha}>
                <span>{rotulo}</span>
                <b>{valor}</b>
              </div>
            ))}
            <div style={{ marginTop: 12 }}>{botaoAcao}</div>
            {!integracao && entrada && elegiveis.length > nMarcados && nMarcados > 0 && (
              <div className={e.mudo} style={{ marginTop: 10 }}>{fraseFicaParaDepois(elegiveis.length - nMarcados)}</div>
            )}
          </Card>
        </aside>
      </div>

      {!concluida && (
        <div className={e.barra}>
          <span className={e.barraResumo}>{acao.resumo}</span>
          <Button
            appearance="primary"
            size="large"
            className={e.btnGrande}
            icon={acao.faltas.length > 0 ? <LockClosed20Regular /> : undefined}
            disabled={acao.desabilitada}
            onClick={acao.executar}
          >
            {acao.rotulo}
          </Button>
        </div>
      )}
    </div>
  );
}
