import { useCallback, useEffect, useRef, useState } from 'react';
import { Button, FeedbackInline } from '@ui';
import { Camera24Regular, Checkmark24Regular, Dismiss24Regular, Pause24Regular, Play24Regular, Warning20Regular } from '@fluentui/react-icons';
import { api, type FotosAutomaticasIntegracao, type SessaoTreinamentoDetalhe } from '../../../lib/api';
import { prepararDadosEvidencia } from '../../../lib/capturaEvidencia';
import { vincularDadosFoto } from '../../../lib/dadosFoto';
import { criarRastreadorFoto } from '../../../lib/fontesLocalizacao';
import { observarEndereco, type ObservadorEndereco } from '../../../lib/carimboFoto';
import { comprimirImagem } from '../../../lib/imagem';
import type { Rastreador } from '../../../lib/rastreadorLocalizacao';
import {
  TOTAL_FOTOS_AUTOMATICAS,
  agendaAoAtivar,
  classificarFalhaCamera,
  configuracaoFotosSegura,
  formatarContagem,
  ordinalFoto,
  pausaParaVencerEm,
  proximaOrdemLivre,
  resumoIntervalos,
  situacaoAgenda,
  textoAtraso,
  type EstadoAgendaFotos,
} from './agendaFotos';
import { formatarHoraBrasil } from './helpersInstrutor';
import { useEstilosIntegracao } from './estilos';

interface Props {
  sessao: SessaoTreinamentoDetalhe;
  /** "NR-18": entra no carimbo da foto ("NR-18 · Foto exclusiva desta etapa"). */
  codigoEtapa: string;
  /** Intervalos vindos do servidor (configuracao / entrada.fotosAutomaticas). Ausente ou inválido: padrão 60 s / 5 min / 5 min. */
  configuracao?: FotosAutomaticasIntegracao | null;
  somenteLeitura: boolean;
  /** Recarrega a turma e a integração depois de cada foto gravada (a miniatura aparece na grade). */
  aoAtualizar: () => Promise<void> | void;
  aoErro: (mensagem: string | null) => void;
}

type Fase = 'pronto' | 'ativa' | 'pausada' | 'negada' | 'indisponivel' | 'dormiu' | 'erro_envio' | 'concluido';

const DELAY_RETOMADA_MS = 5_000;
const DELAY_NOVA_TENTATIVA_MS = 15_000;
const TENTATIVAS_AUTOMATICAS = 2;
const TAMANHO_MAXIMO_MB = 5;

// Fotos automáticas da etapa (16/11/2026, decisão do usuário). O técnico deixa o tablet virado para a turma e
// toca em "Fotografar automaticamente": a 1ª foto sai `primeiraAposSegundos` depois, a 2ª e a 3ª depois da
// anterior (intervalos do servidor). Só câmera ao vivo (nada de upload). Cada foto passa pelo MESMO caminho da
// foto manual: carimbo, geolocalização e metadados (lib/capturaEvidencia.ts), compressão e envio com o próprio
// token de captura, pedido no momento de capturar (api.sessoesTreinamento.anexarFotoEvidencia). O hash único
// entre etapas e o limite de 3 fotos continuam sendo do servidor.
//
// Timers: tudo por HORÁRIO ABSOLUTO (agendaFotos.ts). O tick só pergunta "o que é devido agora?"; tela
// apagada ou aba em segundo plano atrasam o tick, nunca a conta. Foto vencida com a aba oculta não é tirada
// (o vídeo não renderiza): ao voltar, se passou da tolerância, a tela avisa e oferece retomar. A tela
// também pede o Wake Lock para o aparelho não dormir, quando o navegador permite.
export function FotosAutomaticasEtapa({ sessao, codigoEtapa, configuracao, somenteLeitura, aoAtualizar, aoErro }: Props) {
  const e = useEstilosIntegracao();
  const cfg = configuracaoFotosSegura(configuracao);
  const sessaoId = sessao.sessao.id;
  const nFotos = sessao.fotosEvidencia.length;

  const [fase, setFase] = useState<Fase>(nFotos >= TOTAL_FOTOS_AUTOMATICAS ? 'concluido' : 'pronto');
  const [agora, setAgora] = useState(() => Date.now());
  const [capturando, setCapturando] = useState(false);
  const [tirandoAgora, setTirandoAgora] = useState(false);
  const [mensagemErro, setMensagemErro] = useState<string | null>(null);
  const [aviso, setAviso] = useState<string | null>(null);
  const [pausouAs, setPausouAs] = useState<number | null>(null);

  const videoRef = useRef<HTMLVideoElement | null>(null);
  const streamRef = useRef<MediaStream | null>(null);
  const rastreadorRef = useRef<Rastreador | null>(null);
  const enderecoRef = useRef<ObservadorEndereco | null>(null);
  const wakeLockRef = useRef<{ release: () => Promise<void> } | null>(null);
  const agendaRef = useRef<EstadoAgendaFotos>({ ativadoEm: 0, feitasEm: [], pausaMs: 0 });
  const faseRef = useRef<Fase>(fase);
  const ocupadoRef = useRef(false);
  const pausadoEmRef = useRef<number | null>(null);
  const falhasSeguidasRef = useRef(0);
  const ultimoTickRef = useRef(Date.now());
  // Sobe a cada pararTudo(): pedidos assíncronos (câmera, Wake Lock) que voltam depois de um encerramento/desmontar se desfazem sozinhos.
  const geracaoRef = useRef(0);
  const ordensRef = useRef<number[]>([]);
  const nFotosRef = useRef(nFotos);
  const cfgRef = useRef(cfg);
  cfgRef.current = cfg;
  ordensRef.current = sessao.fotosEvidencia.map((f) => f.ordem);
  faseRef.current = fase;

  const definirFase = useCallback((nova: Fase) => {
    faseRef.current = nova;
    setFase(nova);
  }, []);

  // ---- câmera, rastreio de posição e Wake Lock ----
  const pararTudo = useCallback(() => {
    geracaoRef.current += 1;
    streamRef.current?.getTracks().forEach((t) => t.stop());
    streamRef.current = null;
    if (videoRef.current) videoRef.current.srcObject = null;
    rastreadorRef.current?.parar();
    rastreadorRef.current = null;
    enderecoRef.current?.parar();
    enderecoRef.current = null;
    void wakeLockRef.current?.release().catch(() => undefined);
    wakeLockRef.current = null;
  }, []);

  const pedirWakeLock = useCallback(async () => {
    const geracao = geracaoRef.current;
    try {
      if ('wakeLock' in navigator) {
        const trava = await navigator.wakeLock.request('screen');
        // Encerrou (ou desmontou) enquanto o navegador respondia: solta a trava em vez de guardá-la.
        if (geracao !== geracaoRef.current) void trava.release().catch(() => undefined);
        else wakeLockRef.current = trava;
      }
    } catch {
      // Sem Wake Lock (navegador sem suporte ou bateria baixa): o aviso na tela cobre o caso.
    }
  }, []);

  /** Abre a câmera traseira ao vivo e liga o rastreio da posição. Devolve false e põe a tela no estado de erro certo se falhar. */
  const abrirCamera = useCallback(async (): Promise<boolean> => {
    pararTudo();
    const geracao = geracaoRef.current;
    try {
      if (!navigator.mediaDevices?.getUserMedia) throw new DOMException('Sem câmera', 'NotFoundError');
      let stream: MediaStream;
      try {
        stream = await navigator.mediaDevices.getUserMedia({ video: { facingMode: { ideal: 'environment' } }, audio: false });
      } catch (err) {
        // Restrição de câmera não atendida: tenta qualquer câmera. Permissão negada não tem segunda chance.
        if (err instanceof DOMException && (err.name === 'OverconstrainedError' || err.name === 'NotFoundError')) {
          stream = await navigator.mediaDevices.getUserMedia({ video: true, audio: false });
        } else {
          throw err;
        }
      }
      if (geracao !== geracaoRef.current) {
        // Parou ou desmontou enquanto o navegador abria a câmera: desliga a luz e desiste.
        stream.getTracks().forEach((t) => t.stop());
        return false;
      }
      streamRef.current = stream;
      const video = videoRef.current;
      if (video) {
        video.srcObject = stream;
        await video.play().catch(() => undefined);
      }
      if (geracao !== geracaoRef.current) {
        stream.getTracks().forEach((t) => t.stop());
        return false;
      }
      const rastreador = criarRastreadorFoto();
      rastreadorRef.current = rastreador;
      const obs = observarEndereco();
      enderecoRef.current = obs;
      rastreador.assinar((est) => {
        if (rastreadorRef.current === rastreador && est.leitura) obs.atualizar(est.leitura.latitude, est.leitura.longitude);
      });
      void pedirWakeLock();
      return true;
    } catch (err) {
      pararTudo();
      definirFase(classificarFalhaCamera(err instanceof Error ? err.name : null) === 'negada' ? 'negada' : 'indisponivel');
      return false;
    }
  }, [definirFase, pararTudo, pedirWakeLock]);

  // ---- captura e envio (mesmo pipeline da foto manual) ----
  const registrarFeita = useCallback(() => {
    agendaRef.current = { ...agendaRef.current, feitasEm: [...agendaRef.current.feitasEm, Date.now()], pausaMs: 0 };
    falhasSeguidasRef.current = 0;
  }, []);

  const capturar = useCallback(
    async (origem: 'auto' | 'manual') => {
      if (ocupadoRef.current) return;
      ocupadoRef.current = true;
      setCapturando(true);
      setMensagemErro(null);
      try {
        const video = videoRef.current;
        const trilha = streamRef.current?.getVideoTracks()[0];
        if (!video || !trilha || trilha.readyState !== 'live' || !video.videoWidth) {
          throw Object.assign(new Error('A câmera parou de responder. Toque em "Tentar de novo".'), { camera: true });
        }
        const rastreador = rastreadorRef.current;
        if (!rastreador) throw Object.assign(new Error('A localização não está ligada. Toque em "Tentar de novo".'), { camera: true });
        const ordem = proximaOrdemLivre(ordensRef.current);
        if (ordem == null) return;

        const capturadaEm = new Date();
        const canvas = document.createElement('canvas');
        canvas.width = video.videoWidth;
        canvas.height = video.videoHeight;
        canvas.getContext('2d')?.drawImage(video, 0, 0);
        // Carimbo + posição + metadados: o mesmo pipeline da foto manual (lib/capturaEvidencia.ts).
        const dados = await prepararDadosEvidencia({
          canvas,
          contextoFoto: { registroId: sessaoId, obraId: sessao.sessao.obraId, obraNome: sessao.sessao.obraNome },
          localFoto: `${codigoEtapa} · Foto exclusiva desta etapa`,
          capturadaEm,
          rastreador,
          endereco: enderecoRef.current,
          carimbar: true,
        });
        const blob = await new Promise<Blob | null>((resolve) => canvas.toBlob(resolve, 'image/jpeg', 0.92));
        if (!blob) throw new Error('Não foi possível capturar a imagem. Tente de novo.');
        let arquivo = new File([blob], `captura-${Date.now()}.jpg`, { type: 'image/jpeg' });
        try {
          arquivo = await comprimirImagem(arquivo);
        } catch {
          // Segue com o original: o limite de tamanho é conferido abaixo.
        }
        if (arquivo.size / (1024 * 1024) > TAMANHO_MAXIMO_MB) throw new Error(`A foto passou de ${TAMANHO_MAXIMO_MB} MB. Tente de novo.`);
        vincularDadosFoto(arquivo, dados);

        // Token próprio, pedido AGORA (uso único, 5 min). Se vencer ou já estiver usado, pede outro e reenvia a mesma foto uma vez.
        const enviar = async () => api.sessoesTreinamento.anexarFotoEvidencia(sessaoId, ordem, arquivo, (await api.sessoesTreinamento.obterTokenCapturaFoto(sessaoId)).token);
        try {
          await enviar();
        } catch (err) {
          if (!(err instanceof Error) || !/token/i.test(err.message)) throw err;
          await enviar();
        }
        registrarFeita();
        setAviso(`${ordinalFoto(agendaRef.current.feitasEm.length - 1)} foto registrada às ${formatarHoraBrasil(Date.now())}.`);
        await aoAtualizar();
      } catch (err) {
        const texto = err instanceof Error ? err.message : 'Não foi possível registrar a foto.';
        const daCamera = !!(err as { camera?: boolean } | null)?.camera;
        setMensagemErro(texto);
        if (origem === 'auto' && faseRef.current === 'ativa') {
          falhasSeguidasRef.current += 1;
          if (daCamera) {
            pararTudo();
            definirFase('indisponivel');
          } else if (falhasSeguidasRef.current <= TENTATIVAS_AUTOMATICAS) {
            agendaRef.current = { ...agendaRef.current, pausaMs: pausaParaVencerEm(agendaRef.current, cfgRef.current, Date.now() + DELAY_NOVA_TENTATIVA_MS) };
          } else {
            pararTudo();
            definirFase('erro_envio');
          }
        }
      } finally {
        ocupadoRef.current = false;
        setCapturando(false);
      }
    },
    [aoAtualizar, codigoEtapa, definirFase, pararTudo, registrarFeita, sessao.sessao.obraId, sessao.sessao.obraNome, sessaoId],
  );

  // ---- tick por horário absoluto ----
  const tick = useCallback(() => {
    const agoraMs = Date.now();
    const ultimo = ultimoTickRef.current;
    ultimoTickRef.current = agoraMs;
    setAgora(agoraMs);
    if (faseRef.current !== 'ativa' || ocupadoRef.current) return;
    const sit = situacaoAgenda(agendaRef.current, cfgRef.current, agoraMs);
    if (sit.fase === 'concluida') return;
    if (sit.fase === 'atrasada') {
      // Dormiu ou ficou em segundo plano: avisa em vez de disparar às cegas.
      // Encerramento completo: câmera, rastreio de posição, observador de endereço e Wake Lock. Ao retomar,
      // retomarDepoisDeDormir() reabre tudo pelo abrirCamera().
      setPausouAs(ultimo);
      pararTudo();
      definirFase('dormiu');
      return;
    }
    if (sit.fase === 'devida' && document.visibilityState === 'visible') void capturar('auto');
  }, [capturar, definirFase, pararTudo]);

  useEffect(() => {
    if (fase !== 'ativa') return;
    ultimoTickRef.current = Date.now();
    const id = window.setInterval(tick, 500);
    const aoVoltar = () => {
      if (document.visibilityState === 'visible') {
        if (!wakeLockRef.current && streamRef.current) void pedirWakeLock();
        tick();
      }
    };
    document.addEventListener('visibilitychange', aoVoltar);
    return () => {
      window.clearInterval(id);
      document.removeEventListener('visibilitychange', aoVoltar);
    };
  }, [fase, tick, pedirWakeLock]);

  // Desmontar a tela desliga câmera, rastreio e Wake Lock (a luz da câmera não pode ficar acesa).
  useEffect(() => () => pararTudo(), [pararTudo]);

  // Fotos que mudam por fora (manual, remoção): a agenda acompanha o que está gravado.
  useEffect(() => {
    nFotosRef.current = nFotos;
    const feitas = agendaRef.current.feitasEm;
    if (nFotos > feitas.length) {
      // Foto que não foi desta tela (manual): conta como feita agora, e a próxima automática se recalcula dela.
      agendaRef.current = { ...agendaRef.current, feitasEm: [...feitas, ...Array(nFotos - feitas.length).fill(Date.now())], pausaMs: 0 };
    } else if (nFotos < feitas.length) {
      agendaRef.current = { ...agendaRef.current, feitasEm: feitas.slice(0, nFotos), pausaMs: 0 };
    }
    if (nFotos >= TOTAL_FOTOS_AUTOMATICAS) {
      pararTudo();
      definirFase('concluido');
    } else if (faseRef.current === 'concluido') {
      definirFase('pronto');
    }
  }, [nFotos, definirFase, pararTudo]);

  // ---- ações dos botões ----
  async function ativar() {
    aoErro(null);
    setMensagemErro(null);
    setAviso(null);
    if (!(await abrirCamera())) return;
    const inicio = Date.now();
    agendaRef.current = agendaAoAtivar(nFotos, inicio);
    falhasSeguidasRef.current = 0;
    setAgora(inicio);
    definirFase('ativa');
  }

  async function retomarDepoisDeDormir() {
    setMensagemErro(null);
    if (!(await abrirCamera())) return;
    const inicio = Date.now();
    agendaRef.current = { ...agendaRef.current, pausaMs: pausaParaVencerEm(agendaRef.current, cfgRef.current, inicio + DELAY_RETOMADA_MS) };
    falhasSeguidasRef.current = 0;
    setAgora(inicio);
    ultimoTickRef.current = inicio;
    definirFase('ativa');
  }

  function pausar() {
    pausadoEmRef.current = Date.now();
    definirFase('pausada');
  }

  function continuar() {
    const parouEm = pausadoEmRef.current ?? Date.now();
    agendaRef.current = { ...agendaRef.current, pausaMs: (agendaRef.current.pausaMs ?? 0) + (Date.now() - parouEm) };
    pausadoEmRef.current = null;
    ultimoTickRef.current = Date.now();
    definirFase('ativa');
  }

  function parar() {
    pararTudo();
    setMensagemErro(null);
    definirFase(nFotos >= TOTAL_FOTOS_AUTOMATICAS ? 'concluido' : 'pronto');
  }

  /** Plano B: tira uma foto agora, ao vivo, com o mesmo pipeline. Se a câmera automática está ligada, usa a mesma câmera. */
  async function tirarAgora() {
    aoErro(null);
    setMensagemErro(null);
    const jaLigada = (fase === 'ativa' || fase === 'pausada') && !!streamRef.current;
    if (jaLigada) {
      await capturar('manual');
      return;
    }
    setTirandoAgora(true);
    let abriu = false;
    try {
      abriu = await abrirCamera();
      if (abriu) {
        // Dá um instante para a câmera entregar o primeiro quadro (e a posição, que já está sendo buscada).
        await new Promise((r) => window.setTimeout(r, 1200));
        await capturar('manual');
      }
    } finally {
      pararTudo();
      setTirandoAgora(false);
    }
    // Falha de câmera (negada/indisponível) mantém o estado de erro; senão volta ao "pronto" (ou "concluído").
    if (abriu) definirFase(nFotosRef.current >= TOTAL_FOTOS_AUTOMATICAS ? 'concluido' : 'pronto');
  }

  if (somenteLeitura) return null;

  const cameraLigada = fase === 'ativa' || fase === 'pausada' || tirandoAgora;
  const sit = situacaoAgenda(agendaRef.current, cfg, agora);
  const contagem =
    fase === 'pausada'
      ? { rotulo: 'Pausado', valor: '—' }
      : capturando || sit.fase === 'devida'
        ? { rotulo: `Capturando foto ${Math.min(sit.indice + 1, TOTAL_FOTOS_AUTOMATICAS)} de ${TOTAL_FOTOS_AUTOMATICAS}`, valor: '…' }
        : tirandoAgora
          ? { rotulo: 'Foto agora', valor: '…' }
          : { rotulo: `${ordinalFoto(sit.indice)} foto em`, valor: formatarContagem(sit.restanteSeg) };

  const erroTela: { titulo: string; texto: string } | null =
    fase === 'negada'
      ? { titulo: 'Permissão da câmera negada.', texto: 'O navegador ou o Teams bloqueou a câmera. Permita a câmera (cadeado ao lado do endereço, ou as configurações do Teams) e tente de novo.' }
      : fase === 'indisponivel'
        ? { titulo: 'Câmera indisponível.', texto: 'Outro aplicativo está usando a câmera ou ela não respondeu. Feche os outros apps e tente de novo.' }
        : fase === 'erro_envio'
          ? { titulo: 'Não consegui registrar a foto.', texto: mensagemErro ?? 'Confira a internet e a localização e tente de novo.' }
          : fase === 'dormiu'
            ? {
                titulo: `A tela dormiu e a contagem pausou${pausouAs ? ` às ${formatarHoraBrasil(pausouAs)}` : ''}.`,
                texto: `Foram feitas ${agendaRef.current.feitasEm.length} de ${TOTAL_FOTOS_AUTOMATICAS} fotos; a ${ordinalFoto(sit.indice)} está atrasada em ${textoAtraso(sit.atrasoMs)}. Retome para continuar.`,
              }
            : null;

  return (
    <div>
      {fase !== 'concluido' && (
        <div className={e.regraTela}>
          <Warning20Regular aria-hidden="true" />
          <div>
            <b>Deixe o aparelho com a tela ligada e esta aba aberta.</b> Se a tela apagar ou você trocar de aba, a contagem pausa e as fotos atrasam. Ligue o
            tablet na tomada e desative o descanso de tela.
          </div>
        </div>
      )}

      <div style={{ height: 12 }} />

      {/* O <video> fica sempre no DOM (oculto sem câmera) para a captura ter de onde ler o quadro. */}
      <div className={cameraLigada ? e.cam : undefined}>
        {cameraLigada && (
          <span className={e.camSelo} role="status">
            <span className={e.camPonto} aria-hidden="true" /> CÂMERA ATIVA: FOTOGRAFANDO A TURMA
          </span>
        )}
        <video ref={videoRef} className={cameraLigada ? e.camVideo : e.camVideoOculto} muted playsInline autoPlay aria-hidden="true" />
        {capturando && cameraLigada && <div className={e.camFlash} aria-hidden="true" />}
        {cameraLigada && (
          <div className={e.camContagem}>
            <div style={{ fontSize: 13 }}>{contagem.rotulo}</div>
            <div className={e.camNumero}>{contagem.valor}</div>
          </div>
        )}
      </div>

      <div role="status" aria-live="polite" className={e.apenasLeitorTela}>
        {aviso}
      </div>

      {fase === 'pronto' && (
        <div style={{ marginTop: 0 }}>
          <div className={e.acoesLivres} style={{ marginTop: 0 }}>
            <Button appearance="primary" size="large" className={e.btnGrande} icon={<Camera24Regular />} onClick={() => void ativar()}>
              Fotografar automaticamente
            </Button>
            <Button size="large" className={e.btnGrande} disabled={capturando} onClick={() => void tirarAgora()}>
              Tirar foto agora
            </Button>
          </div>
          <div className={e.periodoAuto} style={{ marginTop: 12 }}>
            <div>
              <div className={e.mudo}>Intervalos padrão</div>
              <b>{resumoIntervalos(cfg)}</b>
            </div>
          </div>
        </div>
      )}

      {(fase === 'ativa' || fase === 'pausada') && (
        <>
          <div className={e.acoesLivres}>
            {fase === 'pausada' ? (
              <Button size="large" className={e.btnGrande} icon={<Play24Regular />} onClick={continuar}>
                Continuar
              </Button>
            ) : (
              <Button size="large" className={e.btnGrande} icon={<Pause24Regular />} onClick={pausar}>
                Pausar
              </Button>
            )}
            <Button size="large" className={e.btnGrande} disabled={capturando} onClick={() => void tirarAgora()}>
              Tirar foto agora
            </Button>
            <Button size="large" className={e.btnGrande} icon={<Dismiss24Regular />} onClick={parar}>
              Parar
            </Button>
          </div>
          <div className={e.lgpd}>
            <b>Aviso à turma:</b> a câmera deste tablet está fotografando a turma {TOTAL_FOTOS_AUTOMATICAS} vezes para o registro desta etapa da integração. As fotos ficam no
            registro da integração e não são usadas para outra finalidade (LGPD).
          </div>
        </>
      )}

      {erroTela && (
        <div style={{ marginTop: 12 }}>
          <FeedbackInline tom="erro">
            <b>{erroTela.titulo}</b> {erroTela.texto}
          </FeedbackInline>
          <div className={e.acoesLivres}>
            <Button appearance="primary" size="large" className={e.btnGrande} onClick={() => void (fase === 'dormiu' ? retomarDepoisDeDormir() : ativar())}>
              {fase === 'dormiu' ? 'Retomar fotos automáticas' : 'Tentar de novo'}
            </Button>
            <Button size="large" className={e.btnGrande} icon={<Camera24Regular />} disabled={capturando || tirandoAgora} onClick={() => void tirarAgora()}>
              Tirar foto agora
            </Button>
          </div>
          <div className={e.mudo} style={{ marginTop: 8 }}>"Tirar foto agora" abre a câmera ao vivo e conta como uma das {TOTAL_FOTOS_AUTOMATICAS} fotos.</div>
        </div>
      )}

      {mensagemErro && fase !== 'erro_envio' && (
        <div style={{ marginTop: 12 }}>
          <FeedbackInline tom="erro" aoFechar={() => setMensagemErro(null)}>
            <b>Foto recusada.</b> {mensagemErro}
          </FeedbackInline>
        </div>
      )}

      {fase === 'concluido' && (
        <div className={e.okCaixa}>
          <span className={e.okCaixaIcone}>
            <Checkmark24Regular />
          </span>
          <div>
            <b style={{ display: 'block', fontSize: 15 }}>As {TOTAL_FOTOS_AUTOMATICAS} fotos da etapa foram feitas</b>
            <span style={{ fontSize: 12 }}>Câmera desligada.</span>
          </div>
        </div>
      )}
    </div>
  );
}
