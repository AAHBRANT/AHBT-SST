import { useCallback, useEffect, useRef, useState } from 'react';

export type EstadoGravacaoVoz = 'parado' | 'gravando' | 'transcrevendo';

// gpt-4o-transcribe aceita até 25 MB; 10 min de opus fica bem abaixo disso.
const DURACAO_MAXIMA_SEGUNDOS = 600;
const QUANTIDADE_BARRAS = 15;

// Chrome/Edge/Teams desktop gravam webm/opus; Safari (Teams no iPhone) só grava mp4.
function escolherFormato(): { mimeType: string; extensao: string } | null {
  if (typeof MediaRecorder === 'undefined') return null;
  const candidatos = [
    { mimeType: 'audio/webm;codecs=opus', extensao: 'webm' },
    { mimeType: 'audio/webm', extensao: 'webm' },
    { mimeType: 'audio/mp4', extensao: 'mp4' },
  ];
  return candidatos.find((c) => MediaRecorder.isTypeSupported(c.mimeType)) ?? { mimeType: '', extensao: 'webm' };
}

/**
 * Grava a fala do usuário e entrega o áudio a `enviar` (que transcreve e, se for o caso, pede à IA
 * o preenchimento do formulário). O áudio fica só em memória; nada é salvo no servidor. Usado pelo
 * "Relatar por voz" do Suporte IA e do registro de Ocorrências.
 */
export function useGravacaoVoz<T extends { transcricao: string }>(
  enviar: (audio: Blob, nomeArquivo: string) => Promise<T>,
  aoTranscrever: (resultado: T) => void,
  aoErro: (mensagem: string) => void,
) {
  const [estado, setEstado] = useState<EstadoGravacaoVoz>('parado');
  const [segundos, setSegundos] = useState(0);
  const [niveis, setNiveis] = useState<number[]>(() => Array(QUANTIDADE_BARRAS).fill(0));

  const gravadorRef = useRef<MediaRecorder | null>(null);
  const streamRef = useRef<MediaStream | null>(null);
  const audioContextRef = useRef<AudioContext | null>(null);
  const cronometroRef = useRef<number | null>(null);
  const animacaoRef = useRef<number | null>(null);
  // Callbacks mais recentes, lidos no onstop do MediaRecorder (que roda fora do render).
  const callbacksRef = useRef({ enviar, aoTranscrever, aoErro });
  useEffect(() => {
    callbacksRef.current = { enviar, aoTranscrever, aoErro };
  }, [enviar, aoTranscrever, aoErro]);

  const liberarRecursos = useCallback(() => {
    if (cronometroRef.current !== null) window.clearInterval(cronometroRef.current);
    if (animacaoRef.current !== null) cancelAnimationFrame(animacaoRef.current);
    cronometroRef.current = null;
    animacaoRef.current = null;
    streamRef.current?.getTracks().forEach((t) => t.stop());
    streamRef.current = null;
    audioContextRef.current?.close().catch(() => undefined);
    audioContextRef.current = null;
    setNiveis(Array(QUANTIDADE_BARRAS).fill(0));
  }, []);

  useEffect(
    () => () => {
      // Saiu da tela no meio da gravação: descarta sem transcrever.
      if (gravadorRef.current) gravadorRef.current.ondataavailable = null;
      if (gravadorRef.current?.state === 'recording') {
        gravadorRef.current.onstop = null;
        gravadorRef.current.stop();
      }
      liberarRecursos();
    },
    [liberarRecursos],
  );

  const parar = useCallback(() => {
    if (gravadorRef.current?.state === 'recording') gravadorRef.current.stop();
  }, []);

  const iniciar = useCallback(async () => {
    const formato = escolherFormato();
    if (!formato || !navigator.mediaDevices?.getUserMedia) {
      callbacksRef.current.aoErro('Este navegador não permite gravar áudio. Escreva o relato.');
      return;
    }

    let stream: MediaStream;
    try {
      stream = await navigator.mediaDevices.getUserMedia({ audio: true });
    } catch {
      callbacksRef.current.aoErro(
        'Não foi possível acessar o microfone. Libere a permissão no Teams ou no navegador, ou digite a descrição.',
      );
      return;
    }
    streamRef.current = stream;

    const gravador = new MediaRecorder(stream, formato.mimeType ? { mimeType: formato.mimeType } : undefined);
    const partes: Blob[] = [];
    gravador.ondataavailable = (e) => {
      if (e.data.size > 0) partes.push(e.data);
    };
    gravador.onstop = async () => {
      liberarRecursos();
      gravadorRef.current = null;
      const audio = new Blob(partes, { type: gravador.mimeType || formato.mimeType || 'audio/webm' });
      if (audio.size === 0) {
        setEstado('parado');
        return;
      }
      setEstado('transcrevendo');
      try {
        const resultado = await callbacksRef.current.enviar(audio, `relato.${formato.extensao}`);
        if (resultado.transcricao.trim()) {
          callbacksRef.current.aoTranscrever(resultado);
        } else {
          callbacksRef.current.aoErro('Não deu para entender o áudio. Tente falar mais perto do microfone.');
        }
      } catch (e) {
        callbacksRef.current.aoErro(e instanceof Error ? e.message : 'Falha ao transcrever o áudio.');
      } finally {
        setEstado('parado');
      }
    };
    gravadorRef.current = gravador;

    // Medidor de volume só para dar retorno visual de que o microfone está captando.
    try {
      const contexto = new AudioContext();
      const analisador = contexto.createAnalyser();
      analisador.fftSize = 64;
      contexto.createMediaStreamSource(stream).connect(analisador);
      audioContextRef.current = contexto;
      const dados = new Uint8Array(analisador.frequencyBinCount);
      const desenhar = () => {
        analisador.getByteFrequencyData(dados);
        const passo = Math.max(1, Math.floor(dados.length / QUANTIDADE_BARRAS));
        setNiveis(Array.from({ length: QUANTIDADE_BARRAS }, (_, i) => (dados[i * passo] ?? 0) / 255));
        animacaoRef.current = requestAnimationFrame(desenhar);
      };
      desenhar();
    } catch {
      // Sem AudioContext o medidor fica parado; a gravação segue normal.
    }

    setSegundos(0);
    cronometroRef.current = window.setInterval(() => {
      setSegundos((s) => {
        if (s + 1 >= DURACAO_MAXIMA_SEGUNDOS) parar();
        return s + 1;
      });
    }, 1000);

    gravador.start(1000);
    setEstado('gravando');
  }, [liberarRecursos, parar]);

  return { estado, segundos, niveis, iniciar, parar };
}

export function formatarTempoGravacao(segundos: number): string {
  const m = Math.floor(segundos / 60);
  const s = segundos % 60;
  return `${String(m).padStart(2, '0')}:${String(s).padStart(2, '0')}`;
}
