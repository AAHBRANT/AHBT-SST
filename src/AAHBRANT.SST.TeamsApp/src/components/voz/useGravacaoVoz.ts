import { useCallback, useEffect, useRef, useState } from 'react';

export type EstadoGravacaoVoz = 'parado' | 'gravando' | 'transcrevendo';

const DURACAO_MAXIMA_SEGUNDOS = 600;
const QUANTIDADE_BARRAS = 15;

// Web Speech API: Chrome/Edge/Teams desktop expõem com prefixo webkit; o Safari (iPhone) também.
type ReconhecimentoFala = {
  lang: string;
  continuous: boolean;
  interimResults: boolean;
  onresult: ((e: { resultIndex: number; results: ArrayLike<ArrayLike<{ transcript: string }> & { isFinal: boolean }> }) => void) | null;
  onerror: ((e: { error: string }) => void) | null;
  onend: (() => void) | null;
  start: () => void;
  stop: () => void;
  abort: () => void;
};

function criarReconhecimento(): ReconhecimentoFala | null {
  const w = window as unknown as Record<string, (new () => ReconhecimentoFala) | undefined>;
  const Construtor = w.SpeechRecognition ?? w.webkitSpeechRecognition;
  return Construtor ? new Construtor() : null;
}

/**
 * Dita a fala do usuário pelo reconhecimento de voz do próprio navegador (sem custo, nenhum áudio
 * sai do aparelho para o SST) e entrega o texto a `enviar` (que, se for o caso, pede à IA o
 * preenchimento do formulário). Usado pelo "Relatar por voz" do Suporte IA e do registro de Ocorrências.
 */
export function useGravacaoVoz<T extends { transcricao: string }>(
  enviar: (texto: string) => Promise<T>,
  aoTranscrever: (resultado: T) => void,
  aoErro: (mensagem: string) => void,
) {
  const [estado, setEstado] = useState<EstadoGravacaoVoz>('parado');
  const [segundos, setSegundos] = useState(0);
  const [niveis, setNiveis] = useState<number[]>(() => Array(QUANTIDADE_BARRAS).fill(0));

  const reconhecimentoRef = useRef<ReconhecimentoFala | null>(null);
  const streamRef = useRef<MediaStream | null>(null);
  const audioContextRef = useRef<AudioContext | null>(null);
  const cronometroRef = useRef<number | null>(null);
  const animacaoRef = useRef<number | null>(null);
  // Callbacks mais recentes, lidos no onend do reconhecimento (que roda fora do render).
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
      // Saiu da tela no meio do ditado: descarta sem enviar.
      const r = reconhecimentoRef.current;
      if (r) {
        r.onend = null;
        r.onresult = null;
        r.abort();
      }
      liberarRecursos();
    },
    [liberarRecursos],
  );

  const parar = useCallback(() => {
    reconhecimentoRef.current?.stop();
  }, []);

  const iniciar = useCallback(async () => {
    const reconhecimento = criarReconhecimento();
    if (!reconhecimento) {
      callbacksRef.current.aoErro('Este navegador não tem ditado por voz. Use o Chrome/Edge ou escreva o relato.');
      return;
    }

    reconhecimento.lang = 'pt-BR';
    reconhecimento.continuous = true;
    reconhecimento.interimResults = false;

    const trechos: string[] = [];
    let erro: string | null = null;
    reconhecimento.onresult = (e) => {
      for (let i = e.resultIndex; i < e.results.length; i++) {
        const resultado = e.results[i];
        if (resultado.isFinal) trechos.push(resultado[0].transcript.trim());
      }
    };
    reconhecimento.onerror = (e) => {
      console.error('[ditado] erro do reconhecimento de voz:', e.error);
      if (e.error === 'not-allowed' || e.error === 'service-not-allowed') {
        erro = 'Não foi possível acessar o microfone. Libere a permissão no Teams ou no navegador, ou digite a descrição.';
      } else if (e.error !== 'no-speech' && e.error !== 'aborted') {
        erro = `O ditado por voz falhou (código: ${e.error}). Tente de novo ou digite a descrição.`;
      }
    };
    reconhecimento.onend = async () => {
      liberarRecursos();
      reconhecimentoRef.current = null;
      const texto = trechos.filter(Boolean).join(' ').trim();
      if (erro) {
        setEstado('parado');
        callbacksRef.current.aoErro(erro);
        return;
      }
      if (!texto) {
        setEstado('parado');
        callbacksRef.current.aoErro('Não deu para entender a fala. Tente falar mais perto do microfone.');
        return;
      }
      setEstado('transcrevendo');
      try {
        callbacksRef.current.aoTranscrever(await callbacksRef.current.enviar(texto));
      } catch (e) {
        callbacksRef.current.aoErro(e instanceof Error ? e.message : 'Falha ao analisar o relato.');
      } finally {
        setEstado('parado');
      }
    };

    try {
      reconhecimento.start();
    } catch {
      callbacksRef.current.aoErro('Não foi possível iniciar o ditado. Tente novamente ou digite a descrição.');
      return;
    }
    reconhecimentoRef.current = reconhecimento;

    // Medidor de volume só para dar retorno visual de que o microfone está captando.
    try {
      const stream = await navigator.mediaDevices.getUserMedia({ audio: true });
      streamRef.current = stream;
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
      // Sem medidor as barras ficam paradas; o ditado segue normal.
    }

    setSegundos(0);
    cronometroRef.current = window.setInterval(() => {
      setSegundos((s) => {
        if (s + 1 >= DURACAO_MAXIMA_SEGUNDOS) parar();
        return s + 1;
      });
    }, 1000);

    setEstado('gravando');
  }, [liberarRecursos, parar]);

  return { estado, segundos, niveis, iniciar, parar };
}

export function formatarTempoGravacao(segundos: number): string {
  const m = Math.floor(segundos / 60);
  const s = segundos % 60;
  return `${String(m).padStart(2, '0')}:${String(s).padStart(2, '0')}`;
}
