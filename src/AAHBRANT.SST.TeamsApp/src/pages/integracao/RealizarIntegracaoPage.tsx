import { useCallback, useEffect, useRef, useState } from 'react';
import { useNavigate, useParams, useSearchParams } from 'react-router-dom';
import { BotaoVoltar, Button, Card, Carregando, FeedbackInline } from '@ui';
import { Info20Regular, LockClosed20Regular } from '@fluentui/react-icons';
import {
  api,
  type EntradaIntegracao,
  type IntegracaoAtendimentoDetalhe,
  type Obra,
  type OsIntegracao,
  type SessaoTreinamentoDetalhe,
} from '../../lib/api';
import { useAgenteBiometricoLocal } from '../../components/treinamentos/useAgenteBiometricoLocal';
import { useUsuarioLogado } from '../../lib/UsuarioLogadoContext';
import { StepperEtapas } from './componentes/StepperEtapas';
import { InicioIntegracao } from './componentes/InicioIntegracao';
import { EtapaCurso } from './componentes/EtapaCurso';
import { EtapaOrdemServico } from './componentes/EtapaOrdemServico';
import { IntegracaoConcluida } from './componentes/IntegracaoConcluida';
import { mergeClasses, useEstilosIntegracao } from './componentes/estilos';
import {
  ehEtapaOs,
  etapaAtual,
  faltasDaEtapa,
  formatarCarga,
  mensagemEtapaConcluida,
  nomesCurtos,
  participantesAtivos,
  proximaEtapa,
  rotuloAvancar,
  rotuloEtapa,
  TOTAL_ETAPAS,
} from './componentes/helpers';

interface Acao {
  rotulo: string;
  faltas: string[];
  resumo: string;
  executar: () => void;
  desabilitada: boolean;
}

function dataHoraCurta(iso?: string | null): string {
  if (!iso) return '';
  const d = new Date(iso);
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
  const { agenteDisponivel, dispositivoLocal } = useAgenteBiometricoLocal();
  const { usuario } = useUsuarioLogado();

  const [obras, setObras] = useState<Obra[]>([]);
  const [obraId, setObraId] = useState(busca.get('obraId') ?? '');
  const [entrada, setEntrada] = useState<EntradaIntegracao | null>(null);
  const [instrutorId, setInstrutorId] = useState('');
  const [integracao, setIntegracao] = useState<IntegracaoAtendimentoDetalhe | null>(null);
  const [sessao, setSessao] = useState<SessaoTreinamentoDetalhe | null>(null);
  const [os, setOs] = useState<OsIntegracao | null>(null);
  const [erro, setErro] = useState<string | null>(null);
  const [erroCarga, setErroCarga] = useState<string | null>(null);
  const [confirmacao, setConfirmacao] = useState<string | null>(null);
  const [processando, setProcessando] = useState(false);
  const [carregando, setCarregando] = useState(true);
  const iniciouAgora = useRef(false);
  // Trava de duplo clique (ref, vale antes do re-render): começar, avançar e concluir não repetem a chamada.
  const emAcao = useRef(false);

  // ---- carga ----
  const carregarIntegracao = useCallback(async (alvo: string) => {
    try {
      setErroCarga(null);
      setIntegracao(await api.integracoesAtendimento.obter(alvo));
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
      try {
        setErroCarga(null);
        setCarregando(true);
        const r = await api.integracoesAtendimento.entrada(alvo);
        // Já existe atendimento aberto na obra: abre direto na etapa em que parou.
        if (r.atendimentoEmAndamento) {
          navigate(`/treinamentos/integracao/${r.atendimentoEmAndamento.id}`, { replace: true });
          return;
        }
        setEntrada(r);
        setInstrutorId((atual) => (r.instrutores.some((i) => i.trabalhadorId === atual) ? atual : r.instrutores[0]?.trabalhadorId ?? ''));
      } catch (err) {
        setErroCarga(err instanceof Error ? err.message : 'Não foi possível consultar a obra.');
      } finally {
        setCarregando(false);
      }
    },
    [navigate],
  );

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

  const atual = integracao ? etapaAtual(integracao) : null;
  const concluida = integracao?.status === 'concluido';
  const sessaoAlvoId = atual && !ehEtapaOs(atual) && atual.status === 'emAndamento' ? atual.sessaoTreinamentoId ?? null : null;
  const precisaOs = !!integracao && (concluida || (!!atual && ehEtapaOs(atual)));

  const recarregarSessao = useCallback(async () => {
    if (!sessaoAlvoId) return;
    try {
      setSessao(await api.sessoesTreinamento.obterDetalhe(sessaoAlvoId));
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

  // ---- ações ----
  async function comecar() {
    if (!entrada || emAcao.current) return;
    emAcao.current = true;
    const pessoas = entrada.candidatos.filter((c) => !c.jaParticipa).map((c) => c.trabalhadorId);
    try {
      setProcessando(true);
      setErro(null);
      const novo = await api.integracoesAtendimento.iniciar(entrada.obraId, pessoas, instrutorId);
      iniciouAgora.current = true;
      setIntegracao(novo);
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
      setIntegracao(novo);
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
      setIntegracao(novo);
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
    const n = entrada?.candidatos.filter((c) => !c.jaParticipa).length ?? 0;
    const bloqueio = entrada?.bloqueios[0]?.mensagem;
    acao = {
      rotulo: 'Começar pela NR-06',
      faltas: [],
      resumo: bloqueio ?? (n === 0 ? 'Nenhum admitido para integrar' : `${n} pessoa${n > 1 ? 's' : ''} selecionada${n > 1 ? 's' : ''}`),
      executar: () => void comecar(),
      desabilitada: !entrada?.podeIniciar || !instrutorId || processando,
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
      faltas: lista,
      resumo: lista.length ? `Falta: ${lista.join('; ')}` : 'Tudo pronto para concluir',
      executar: () => void concluir(),
      desabilitada: lista.length > 0 || processando,
    };
  } else if (atual) {
    const todas = faltasDaEtapa(atual, integracao.instrutorTrabalhadorId);
    acao = {
      rotulo: rotuloAvancar(proximaEtapa(integracao, atual)),
      faltas: todas,
      resumo: todas.length ? `Falta: ${todas.join('; ')}` : 'Tudo pronto para avançar',
      executar: () => void avancar(),
      desabilitada: todas.length > 0 || processando,
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
      {acao.faltas.length > 0 && (
        <>
          <div className={e.mudo} style={{ marginTop: 10 }}>Falta:</div>
          <ul className={e.faltaLista}>
            {acao.faltas.map((f) => (
              <li key={f}>{f}</li>
            ))}
          </ul>
        </>
      )}
    </>
  );

  // ---- resumo lateral ----
  function linhasResumo(): Array<[string, string]> {
    if (!integracao) {
      const c = entrada?.candidatos.filter((x) => !x.jaParticipa).length ?? 0;
      return [['Pessoas', String(c)], ['Etapas', '5 (NR-12 e NR-35 só para quem exige)'], ['Primeira etapa', 'NR-06']];
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

  const nParticipantes = integracao ? integracao.participantes.length : entrada?.candidatos.filter((c) => !c.jaParticipa).length ?? 0;
  const obraNome = integracao?.obraNome ?? entrada?.obraNome ?? obras.find((o) => o.id === obraId)?.nome ?? '—';
  const periodo = !integracao
    ? 'A iniciar'
    : concluida && integracao.concluidaEmUtc
      ? `${integracao.iniciadaEmUtc.slice(0, 10).split('-').reverse().join('/')} a ${integracao.concluidaEmUtc.slice(0, 10).split('-').reverse().join('/')}`
      : `Desde ${integracao.iniciadaEmUtc.slice(0, 10).split('-').reverse().join('/')}`;
  const retomado = !!integracao && !concluida && !iniciouAgora.current;

  return (
    <div className={e.pagina}>
      <div className={e.topo}>
        <BotaoVoltar rotulo="Voltar" onClick={() => (integracao ? voltar() : navigate(-1))} />
        <span className={e.migalha}>Pessoas · Funcionário novo · <b>Realizar integração</b></span>
      </div>

      <div className={e.layout}>
        <div className={e.principal}>
          <section className={e.cab} aria-label="Dados da integração">
            <div className={e.cabLinha}>
              <div>
                <div className={e.cabEyebrow}>Integração de admitidos</div>
                <h1 className={e.cabTitulo}>Realizar integração</h1>
              </div>
              <span className={mergeClasses(e.cabChip, concluida && e.cabChipFinal)}>
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
              <div className={e.cabAviso}>
                <Info20Regular />
                <div>
                  <b>Atendimento retomado.</b> Este atendimento já estava aberto desde {dataHoraCurta(integracao.iniciadaEmUtc).replace(', ', ' às ')}. Etapas
                  concluídas, fotos e assinaturas já registradas foram mantidas.
                </div>
              </div>
            )}
          </section>

          {erro && (
            <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>
              {erro}
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
              instrutorId={instrutorId}
              aoMudarInstrutor={setInstrutorId}
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
              aoAtualizarIntegracao={setIntegracao}
              aoErro={setErro}
              agenteDisponivel={agenteDisponivel}
              dispositivoLocal={dispositivoLocal}
              somenteLeitura={false}
            />
          )}

          {integracao && !concluida && atual && ehEtapaOs(atual) && (
            <EtapaOrdemServico key={atual.id} ordem={atual.ordem} os={os} somenteLeitura={false} recarregarOs={recarregarOs} aoErro={setErro} />
          )}

          {!concluida && (
            <div className={e.rodapePrincipal}>
              <Card titulo="Para avançar" densidade="compacta">
                {botaoAcao}
              </Card>
            </div>
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
