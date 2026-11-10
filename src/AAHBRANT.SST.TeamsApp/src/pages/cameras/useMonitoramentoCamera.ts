import { useCallback, useEffect, useRef, useState } from 'react';
import { api, type ResultadoQuadro } from '../../lib/api';
import { ALTURA_AMOSTRA, LARGURA_AMOSTRA, houveMovimento, paraCinza } from './detectorMovimento';

// Sem intervalo entre análises (decisão do usuário em 09/11/2026): havendo movimento, o próximo quadro sai
// assim que a análise anterior termina. Só existe uma análise em andamento por vez.
export const INTERVALO_ENVIO_MS = 0;
const INTERVALO_AMOSTRA_MS = 1_000;
const LADO_MAXIMO_FOTO = 1280;
const QUALIDADE_JPEG = 0.8;
const TEMPO_LIMITE_ENVIO_MS = 60_000;
const SEM_AMOSTRA_MS = 5_000;
const AMOSTRAS_IGNORADAS = 2; // primeiros quadros após abrir a câmera (exposição ainda ajustando)

// Último envio por câmera, fora do hook: base do intervalo entre envios, se INTERVALO_ENVIO_MS voltar a ser usado.
const ultimoEnvioPorCamera = new Map<string, number>();

export type EstadoMonitoramento = 'parado' | 'iniciando' | 'monitorando' | 'erro';

// Qual câmera do dispositivo filma: traseira (padrão, tablet apontado para a área) ou frontal.
export type LadoCamera = 'traseira' | 'frontal';
const CHAVE_LADO = 'sst.cameras.lado';

// A escolha vale para o aparelho (fica no navegador), não para o cadastro da câmera.
function lerLado(): LadoCamera {
  try {
    return window.localStorage.getItem(CHAVE_LADO) === 'frontal' ? 'frontal' : 'traseira';
  } catch {
    return 'traseira';
  }
}

function gravarLado(lado: LadoCamera) {
  try {
    window.localStorage.setItem(CHAVE_LADO, lado);
  } catch {
    // sem armazenamento (aba privada): a escolha vale só enquanto a tela estiver aberta.
  }
}

export interface AnaliseSessao extends ResultadoQuadro {
  em: Date;
}

export interface ContadoresSessao {
  analises: number;
  ocorrencias: number;
  alertas: number;
  falhas: number;
}

const CONTADORES_ZERADOS: ContadoresSessao = { analises: 0, ocorrencias: 0, alertas: 0, falhas: 0 };

function mensagemDeCamera(erro: unknown): string {
  const nome = erro instanceof DOMException ? erro.name : '';
  if (nome === 'NotAllowedError') return 'A câmera está bloqueada. Libere a permissão de câmera para este site e tente de novo.';
  if (nome === 'NotFoundError') return 'Nenhuma câmera foi encontrada neste dispositivo.';
  if (nome === 'NotReadableError') return 'A câmera está em uso por outro aplicativo.';
  return 'Não foi possível abrir a câmera.';
}

export function useMonitoramentoCamera(cameraId: string) {
  const videoRef = useRef<HTMLVideoElement | null>(null);
  const streamRef = useRef<MediaStream | null>(null);
  const timerRef = useRef<number | null>(null);
  const wakeLockRef = useRef<WakeLockSentinel | null>(null);
  const amostraAnteriorRef = useRef<Uint8Array | null>(null);
  const canvasAmostraRef = useRef<HTMLCanvasElement | null>(null);
  const quadroAnteriorRef = useRef<number | null>(null);
  const amostrasRef = useRef(0);
  const ultimaAmostraRef = useRef(0);
  const abortRef = useRef<AbortController | null>(null);
  const limpezaEventosRef = useRef<(() => void) | null>(null);
  const enviandoRef = useRef(false);
  const ativoRef = useRef(false);
  // Evita duas aberturas de câmera simultâneas (iniciar chamado duas vezes antes de a primeira terminar).
  const iniciandoRef = useRef(false);
  // Sobe a cada parar(): uma abertura ou análise em andamento de uma sessão anterior se descarta sozinha.
  const geracaoRef = useRef(0);

  const [estado, setEstado] = useState<EstadoMonitoramento>('parado');
  const [erro, setErro] = useState<string | null>(null);
  const [movimento, setMovimento] = useState(false);
  const [enviando, setEnviando] = useState(false);
  const [ultimaAnalise, setUltimaAnalise] = useState<AnaliseSessao | null>(null);
  const [contadores, setContadores] = useState<ContadoresSessao>(CONTADORES_ZERADOS);
  const [ocorrenciasSessao, setOcorrenciasSessao] = useState<AnaliseSessao[]>([]);
  const [segundosParaProxima, setSegundosParaProxima] = useState(0);
  const [semAmostraRecente, setSemAmostraRecente] = useState(false);
  const [lado, setLado] = useState<LadoCamera>(lerLado);
  const ladoRef = useRef<LadoCamera>(lado);

  const parar = useCallback(() => {
    geracaoRef.current += 1;
    ativoRef.current = false;
    iniciandoRef.current = false;
    enviandoRef.current = false;
    abortRef.current?.abort();
    abortRef.current = null;
    limpezaEventosRef.current?.();
    limpezaEventosRef.current = null;
    if (timerRef.current !== null) window.clearInterval(timerRef.current);
    timerRef.current = null;
    streamRef.current?.getTracks().forEach((t) => t.stop());
    streamRef.current = null;
    if (videoRef.current) videoRef.current.srcObject = null;
    void wakeLockRef.current?.release().catch(() => undefined);
    wakeLockRef.current = null;
    amostraAnteriorRef.current = null;
    canvasAmostraRef.current = null;
    quadroAnteriorRef.current = null;
    setEnviando(false);
    setMovimento(false);
    setSegundosParaProxima(0);
    setSemAmostraRecente(false);
    setEstado((atual) => (atual === 'erro' ? atual : 'parado'));
  }, []);

  const capturarFoto = useCallback(async (video: HTMLVideoElement): Promise<Blob | null> => {
    const escala = Math.min(1, LADO_MAXIMO_FOTO / Math.max(video.videoWidth, video.videoHeight));
    const canvas = document.createElement('canvas');
    canvas.width = Math.round(video.videoWidth * escala);
    canvas.height = Math.round(video.videoHeight * escala);
    canvas.getContext('2d')?.drawImage(video, 0, 0, canvas.width, canvas.height);
    return new Promise((resolve) => canvas.toBlob(resolve, 'image/jpeg', QUALIDADE_JPEG));
  }, []);

  const enviar = useCallback(async (video: HTMLVideoElement) => {
    const geracao = geracaoRef.current;
    const vigente = () => ativoRef.current && geracaoRef.current === geracao;
    const controle = new AbortController();
    abortRef.current = controle;
    let expirou = false;
    const relogio = window.setTimeout(() => {
      expirou = true;
      controle.abort();
    }, TEMPO_LIMITE_ENVIO_MS);
    enviandoRef.current = true;
    ultimoEnvioPorCamera.set(cameraId, Date.now());
    setEnviando(true);
    try {
      const foto = await capturarFoto(video);
      if (!foto || !vigente()) return;
      const resultado = await api.cameras.analisarQuadro(cameraId, foto, new Date(), controle.signal);
      if (!vigente()) return;
      setErro(null);
      if (resultado.situacao === 'ignorado') return;
      const analise: AnaliseSessao = { ...resultado, em: new Date() };
      setUltimaAnalise(analise);
      setContadores((c) => ({
        analises: c.analises + 1,
        // Repetição somada a uma ocorrência já aberta não conta como ocorrência nova.
        ocorrencias: c.ocorrencias + (resultado.situacao === 'ocorrencia' && !resultado.repetida ? 1 : 0),
        alertas: c.alertas + (resultado.alertaEnviado ? 1 : 0),
        falhas: c.falhas + (resultado.situacao === 'falha' ? 1 : 0),
      }));
      // A mesma ocorrência vista de novo sobe para o topo da lista em vez de aparecer duas vezes.
      if (resultado.situacao === 'ocorrencia')
        setOcorrenciasSessao((lista) =>
          [analise, ...lista.filter((o) => !analise.ocorrenciaId || o.ocorrenciaId !== analise.ocorrenciaId)].slice(0, 20),
        );
    } catch (e) {
      // Erro de rede ou câmera desligada no cadastro: mostra o aviso e segue tentando no próximo ciclo.
      // Cancelamento vindo de parar() não é erro (a sessão deixou de ser vigente).
      if (vigente()) {
        if (expirou) setErro('A análise demorou demais e foi cancelada. A próxima tentativa sai com o próximo movimento.');
        else setErro(e instanceof Error ? e.message : 'Não foi possível enviar a foto para análise.');
      }
    } finally {
      window.clearTimeout(relogio);
      if (abortRef.current === controle) abortRef.current = null;
      if (geracaoRef.current === geracao) {
        enviandoRef.current = false;
        setEnviando(false);
      }
    }
  }, [cameraId, capturarFoto]);

  const amostrar = useCallback(() => {
    const video = videoRef.current;
    if (!video || !ativoRef.current) return;
    // Se o intervalo foi atrasado (aba em segundo plano) ou o vídeo não entrega quadros, avisa a tela.
    setSemAmostraRecente(Date.now() - ultimaAmostraRef.current > SEM_AMOSTRA_MS);
    if (video.readyState < 2 || video.videoWidth === 0) return;
    const quadro = video.getVideoPlaybackQuality?.().totalVideoFrames || video.currentTime;
    if (quadro === quadroAnteriorRef.current) return;
    quadroAnteriorRef.current = quadro;
    if (!canvasAmostraRef.current) {
      const novo = document.createElement('canvas');
      novo.width = LARGURA_AMOSTRA;
      novo.height = ALTURA_AMOSTRA;
      canvasAmostraRef.current = novo;
    }
    const canvas = canvasAmostraRef.current;
    const ctx = canvas.getContext('2d', { willReadFrequently: true });
    if (!ctx) return;
    ctx.drawImage(video, 0, 0, LARGURA_AMOSTRA, ALTURA_AMOSTRA);
    const atual = paraCinza(ctx.getImageData(0, 0, LARGURA_AMOSTRA, ALTURA_AMOSTRA).data);

    ultimaAmostraRef.current = Date.now();
    setSemAmostraRecente(false);

    amostrasRef.current += 1;
    // Os 2 primeiros quadros podem estar mal expostos: descartados. O 3º vira só a referência.
    if (amostrasRef.current <= AMOSTRAS_IGNORADAS) return;
    if (amostrasRef.current === AMOSTRAS_IGNORADAS + 1) {
      amostraAnteriorRef.current = atual;
      return;
    }

    const detectado = houveMovimento(amostraAnteriorRef.current, atual);
    amostraAnteriorRef.current = atual;
    setMovimento(detectado);

    const restante = INTERVALO_ENVIO_MS - (Date.now() - (ultimoEnvioPorCamera.get(cameraId) ?? 0));
    setSegundosParaProxima(Math.max(0, Math.ceil(restante / 1000)));

    if (detectado && restante <= 0 && !enviandoRef.current) void enviar(video);
  }, [cameraId, enviar]);

  // manterSessao: reabertura da mesma sessão (virar a câmera) não zera contadores nem a lista.
  const iniciar = useCallback(async (manterSessao = false) => {
    if (ativoRef.current || iniciandoRef.current) return;
    iniciandoRef.current = true;
    const geracao = geracaoRef.current;
    setErro(null);
    setEstado('iniciando');
    try {
      const stream = await navigator.mediaDevices.getUserMedia({
        video: {
          facingMode: { ideal: ladoRef.current === 'frontal' ? 'user' : 'environment' },
          width: { ideal: 1280 },
          height: { ideal: 720 },
        },
        audio: false,
      });
      if (geracaoRef.current !== geracao) {
        // parar() (ou saída da tela) aconteceu enquanto a câmera abria: libera sem ligar nada.
        stream.getTracks().forEach((t) => t.stop());
        return;
      }
      const video = videoRef.current;
      if (!video) {
        stream.getTracks().forEach((t) => t.stop());
        iniciandoRef.current = false;
        setErro('A tela de monitoramento não está pronta. Volte para a lista de câmeras e abra de novo.');
        setEstado('erro');
        return;
      }
      streamRef.current = stream;
      video.srcObject = stream;
      await video.play();
      if (geracaoRef.current !== geracao) return;
      ativoRef.current = true;
      iniciandoRef.current = false;
      amostrasRef.current = 0;
      ultimaAmostraRef.current = Date.now();
      setSemAmostraRecente(false);

      // Câmera desligada, bloqueada ou desconectada: o vídeo para sem aviso. Encerra tudo e mostra o motivo.
      const faixa = stream.getVideoTracks()[0];
      const aoEncerrar = () => {
        if (geracaoRef.current !== geracao) return;
        setErro('A câmera foi desligada ou bloqueada. Toque em Iniciar para retomar.');
        setEstado('erro');
        parar();
      };
      // O navegador solta a trava de tela quando a aba some: pede de novo ao voltar.
      const aoMudarVisibilidade = () => {
        if (document.visibilityState !== 'visible' || !ativoRef.current || geracaoRef.current !== geracao) return;
        setSemAmostraRecente(Date.now() - ultimaAmostraRef.current > SEM_AMOSTRA_MS);
        if (wakeLockRef.current && !wakeLockRef.current.released) return;
        void (async () => {
          try {
            const nova = (await navigator.wakeLock?.request('screen')) ?? null;
            if (!nova) return;
            if (geracaoRef.current !== geracao || (wakeLockRef.current && !wakeLockRef.current.released)) {
              void nova.release().catch(() => undefined);
              return;
            }
            wakeLockRef.current = nova;
          } catch {
            // sem trava: a tela pode apagar, mas o monitoramento segue.
          }
        })();
      };
      faixa?.addEventListener('ended', aoEncerrar);
      document.addEventListener('visibilitychange', aoMudarVisibilidade);
      limpezaEventosRef.current = () => {
        faixa?.removeEventListener('ended', aoEncerrar);
        document.removeEventListener('visibilitychange', aoMudarVisibilidade);
      };
      if (!faixa || faixa.readyState === 'ended') {
        aoEncerrar();
        return;
      }
      aoMudarVisibilidade();
      if (!manterSessao) {
        setContadores(CONTADORES_ZERADOS);
        setOcorrenciasSessao([]);
        setUltimaAnalise(null);
      }
      timerRef.current = window.setInterval(amostrar, INTERVALO_AMOSTRA_MS);
      setEstado('monitorando');
    } catch (e) {
      if (geracaoRef.current !== geracao) return;
      setErro(mensagemDeCamera(e));
      setEstado('erro');
      parar();
    }
  }, [amostrar, parar]);

  // Troca entre a câmera traseira e a frontal do dispositivo. Monitorando, reabre a câmera do outro lado
  // sem perder a sessão; parado, só guarda a escolha para o próximo início.
  const virarCamera = useCallback(() => {
    const novo: LadoCamera = ladoRef.current === 'frontal' ? 'traseira' : 'frontal';
    ladoRef.current = novo;
    setLado(novo);
    gravarLado(novo);
    if (!ativoRef.current) return;
    parar();
    void iniciar(true);
  }, [iniciar, parar]);

  // Sai da página ou troca de câmera: libera a câmera do dispositivo e zera o que era da câmera anterior.
  useEffect(
    () => () => {
      parar();
      setEstado('parado');
      setErro(null);
      setUltimaAnalise(null);
      setContadores(CONTADORES_ZERADOS);
      setOcorrenciasSessao([]);
    },
    [parar, cameraId],
  );

  return { estado, erro, videoRef, iniciar, parar, ultimaAnalise, contadores, ocorrenciasSessao, segundosParaProxima, movimento, enviando, semAmostraRecente, lado, virarCamera };
}
