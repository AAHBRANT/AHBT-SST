import { useCallback, useEffect, useRef, useState } from 'react';
import { api, type ResultadoQuadro } from '../../lib/api';
import { ALTURA_AMOSTRA, LARGURA_AMOSTRA, houveMovimento, paraCinza } from './detectorMovimento';

export const INTERVALO_ENVIO_MS = 30_000; // teto do navegador; o servidor ignora abaixo de 25 s
const INTERVALO_AMOSTRA_MS = 1_000;
const LADO_MAXIMO_FOTO = 1280;
const QUALIDADE_JPEG = 0.8;

export type EstadoMonitoramento = 'parado' | 'iniciando' | 'monitorando' | 'erro';

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
  const movimentoPendenteRef = useRef(false);
  const ultimoEnvioRef = useRef(0);
  const enviandoRef = useRef(false);
  const ativoRef = useRef(false);
  // Evita duas aberturas de câmera simultâneas (iniciar chamado duas vezes antes de a primeira terminar).
  const iniciandoRef = useRef(false);
  // Sobe a cada parar(): uma abertura ou análise em andamento de uma sessão anterior se descarta sozinha.
  const geracaoRef = useRef(0);

  const [estado, setEstado] = useState<EstadoMonitoramento>('parado');
  const [erro, setErro] = useState<string | null>(null);
  const [movimento, setMovimento] = useState(false);
  const [ultimaAnalise, setUltimaAnalise] = useState<AnaliseSessao | null>(null);
  const [contadores, setContadores] = useState<ContadoresSessao>(CONTADORES_ZERADOS);
  const [ocorrenciasSessao, setOcorrenciasSessao] = useState<AnaliseSessao[]>([]);
  const [segundosParaProxima, setSegundosParaProxima] = useState(0);

  const parar = useCallback(() => {
    geracaoRef.current += 1;
    ativoRef.current = false;
    iniciandoRef.current = false;
    if (timerRef.current !== null) window.clearInterval(timerRef.current);
    timerRef.current = null;
    streamRef.current?.getTracks().forEach((t) => t.stop());
    streamRef.current = null;
    if (videoRef.current) videoRef.current.srcObject = null;
    void wakeLockRef.current?.release().catch(() => undefined);
    wakeLockRef.current = null;
    amostraAnteriorRef.current = null;
    movimentoPendenteRef.current = false;
    setMovimento(false);
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
    enviandoRef.current = true;
    ultimoEnvioRef.current = Date.now();
    movimentoPendenteRef.current = false;
    try {
      const foto = await capturarFoto(video);
      if (!foto || !vigente()) return;
      const resultado = await api.cameras.analisarQuadro(cameraId, foto, new Date());
      if (!vigente()) return;
      if (resultado.situacao === 'ignorado') return;
      const analise: AnaliseSessao = { ...resultado, em: new Date() };
      setUltimaAnalise(analise);
      setContadores((c) => ({
        analises: c.analises + 1,
        ocorrencias: c.ocorrencias + (resultado.situacao === 'ocorrencia' ? 1 : 0),
        alertas: c.alertas + (resultado.alertaEnviado ? 1 : 0),
        falhas: c.falhas + (resultado.situacao === 'falha' ? 1 : 0),
      }));
      if (resultado.situacao === 'ocorrencia') setOcorrenciasSessao((lista) => [analise, ...lista].slice(0, 20));
      setErro(null);
    } catch (e) {
      // Erro de rede ou câmera desligada no cadastro: mostra o aviso e segue tentando no próximo ciclo.
      if (vigente()) setErro(e instanceof Error ? e.message : 'Não foi possível enviar a foto para análise.');
    } finally {
      enviandoRef.current = false;
    }
  }, [cameraId, capturarFoto]);

  const amostrar = useCallback(() => {
    const video = videoRef.current;
    if (!video || !ativoRef.current || video.readyState < 2 || video.videoWidth === 0) return;

    const canvas = document.createElement('canvas');
    canvas.width = LARGURA_AMOSTRA;
    canvas.height = ALTURA_AMOSTRA;
    const ctx = canvas.getContext('2d', { willReadFrequently: true });
    if (!ctx) return;
    ctx.drawImage(video, 0, 0, LARGURA_AMOSTRA, ALTURA_AMOSTRA);
    const atual = paraCinza(ctx.getImageData(0, 0, LARGURA_AMOSTRA, ALTURA_AMOSTRA).data);

    if (houveMovimento(amostraAnteriorRef.current, atual)) movimentoPendenteRef.current = true;
    amostraAnteriorRef.current = atual;
    setMovimento(movimentoPendenteRef.current);

    const restante = INTERVALO_ENVIO_MS - (Date.now() - ultimoEnvioRef.current);
    setSegundosParaProxima(Math.max(0, Math.ceil(restante / 1000)));

    if (movimentoPendenteRef.current && restante <= 0 && !enviandoRef.current) void enviar(video);
  }, [enviar]);

  const iniciar = useCallback(async () => {
    if (ativoRef.current || iniciandoRef.current) return;
    iniciandoRef.current = true;
    const geracao = geracaoRef.current;
    setErro(null);
    setEstado('iniciando');
    try {
      const stream = await navigator.mediaDevices.getUserMedia({
        video: { facingMode: { ideal: 'environment' }, width: { ideal: 1280 }, height: { ideal: 720 } },
        audio: false,
      });
      if (geracaoRef.current !== geracao) {
        // parar() (ou saída da tela) aconteceu enquanto a câmera abria: libera sem ligar nada.
        stream.getTracks().forEach((t) => t.stop());
        return;
      }
      streamRef.current = stream;
      if (videoRef.current) {
        videoRef.current.srcObject = stream;
        await videoRef.current.play();
      }
      // Mantém a tela do tablet acesa quando o navegador suporta; sem suporte, o aviso da tela cobre.
      let trava: WakeLockSentinel | null = null;
      try {
        trava = (await navigator.wakeLock?.request('screen')) ?? null;
      } catch {
        trava = null;
      }
      if (geracaoRef.current !== geracao) {
        void trava?.release().catch(() => undefined);
        return;
      }
      wakeLockRef.current = trava;
      ativoRef.current = true;
      iniciandoRef.current = false;
      ultimoEnvioRef.current = 0;
      setContadores(CONTADORES_ZERADOS);
      setOcorrenciasSessao([]);
      setUltimaAnalise(null);
      timerRef.current = window.setInterval(amostrar, INTERVALO_AMOSTRA_MS);
      setEstado('monitorando');
    } catch (e) {
      if (geracaoRef.current !== geracao) return;
      setErro(mensagemDeCamera(e));
      setEstado('erro');
      parar();
    }
  }, [amostrar, parar]);

  // Sai da página ou troca de câmera: libera a câmera do dispositivo.
  useEffect(() => parar, [parar, cameraId]);

  return { estado, erro, videoRef, iniciar, parar, ultimaAnalise, contadores, ocorrenciasSessao, segundosParaProxima, movimento };
}
