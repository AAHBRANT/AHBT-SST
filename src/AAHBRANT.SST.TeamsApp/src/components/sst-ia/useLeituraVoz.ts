import { useCallback, useEffect, useRef, useState } from 'react';
import { api } from '../../lib/api';

// Resposta falada do SST IA. Desde 19/11/2026 usa a voz neural do Azure (pedido do usuário: as vozes
// do aparelho no Teams soam robóticas). Se o servidor não tiver a voz neural (503), falhar ou o
// navegador bloquear o áudio, cai na voz do próprio aparelho (speechSynthesis).

/** Tira a marcação que o agente usa (negrito, listas, links) para a voz não ler símbolos. */
export function textoParaFala(texto: string): string {
  return texto
    .replace(/```[\s\S]*?```/g, ' ')
    .replace(/\[([^\]]+)\]\([^)]+\)/g, '$1')
    .replace(/[*_`#>]+/g, '')
    .replace(/^\s*[-•]\s+/gm, '')
    .replace(/›/g, ', ')
    .replace(/\s+/g, ' ')
    .trim();
}

function escolherVoz(): SpeechSynthesisVoice | null {
  const vozes = window.speechSynthesis.getVoices();
  const ptBr = vozes.filter((v) => v.lang?.toLowerCase().replace('_', '-').startsWith('pt-br'));
  // Vozes neurais "Online (Natural)" do Edge/Teams soam bem melhor; depois qualquer pt-BR; depois pt.
  return (
    ptBr.find((v) => /natural|online/i.test(v.name)) ??
    ptBr[0] ??
    vozes.find((v) => v.lang?.toLowerCase().startsWith('pt')) ??
    null
  );
}

export function useLeituraVoz() {
  const temVozAparelho = typeof window !== 'undefined' && 'speechSynthesis' in window;
  const suportado = typeof window !== 'undefined' && (typeof Audio !== 'undefined' || temVozAparelho);
  // Id da mensagem que está sendo lida (null = em silêncio). Fica marcado já enquanto o áudio é gerado.
  const [lendoId, setLendoId] = useState<string | null>(null);
  const atualRef = useRef<string | null>(null);
  const audioRef = useRef<HTMLAudioElement | null>(null);
  const abortRef = useRef<AbortController | null>(null);
  // Áudio já gerado por mensagem: "Ouvir" de novo não chama o servidor outra vez.
  const cacheRef = useRef(new Map<string, string>());
  // Depois de um 503 a voz neural não existe neste ambiente: não insiste a cada clique.
  const semNeuralRef = useRef(false);

  useEffect(() => {
    // Chrome/Edge carregam a lista de vozes depois do primeiro getVoices().
    if (temVozAparelho) window.speechSynthesis.getVoices();
    const cache = cacheRef.current;
    return () => {
      abortRef.current?.abort();
      audioRef.current?.pause();
      if (temVozAparelho) window.speechSynthesis.cancel();
      cache.forEach((url) => URL.revokeObjectURL(url));
      cache.clear();
    };
  }, [temVozAparelho]);

  const pararTudo = useCallback(() => {
    abortRef.current?.abort();
    abortRef.current = null;
    if (audioRef.current) {
      audioRef.current.onended = null;
      audioRef.current.onerror = null;
      audioRef.current.pause();
      audioRef.current = null;
    }
    if (temVozAparelho) window.speechSynthesis.cancel();
  }, [temVozAparelho]);

  const parar = useCallback(() => {
    atualRef.current = null;
    pararTudo();
    setLendoId(null);
  }, [pararTudo]);

  const lerNoAparelho = useCallback(
    (id: string, fala: string) => {
      if (!temVozAparelho || atualRef.current !== id) {
        if (atualRef.current === id) {
          atualRef.current = null;
          setLendoId(null);
        }
        return;
      }
      const frase = new SpeechSynthesisUtterance(fala);
      const voz = escolherVoz();
      if (voz) frase.voice = voz;
      frase.lang = voz?.lang ?? 'pt-BR';
      frase.rate = 1.03;
      const terminou = () => {
        if (atualRef.current === id) {
          atualRef.current = null;
          setLendoId(null);
        }
      };
      frase.onend = terminou;
      frase.onerror = terminou;
      window.speechSynthesis.speak(frase);
    },
    [temVozAparelho],
  );

  const ler = useCallback(
    async (id: string, texto: string) => {
      if (!suportado) return;
      const fala = textoParaFala(texto);
      if (!fala) return;
      pararTudo();
      atualRef.current = id;
      setLendoId(id);

      if (semNeuralRef.current || typeof Audio === 'undefined') {
        lerNoAparelho(id, fala);
        return;
      }

      try {
        let url = cacheRef.current.get(id);
        if (!url) {
          const controle = new AbortController();
          abortRef.current = controle;
          const blob = await api.agenteIa.falar(fala, controle.signal);
          abortRef.current = null;
          url = URL.createObjectURL(blob);
          cacheRef.current.set(id, url);
        }
        if (atualRef.current !== id) return; // parou ou trocou de mensagem enquanto gerava
        const audio = new Audio(url);
        audioRef.current = audio;
        const terminou = () => {
          if (audioRef.current === audio) audioRef.current = null;
          if (atualRef.current === id) {
            atualRef.current = null;
            setLendoId(null);
          }
        };
        audio.onended = terminou;
        audio.onerror = terminou;
        await audio.play();
      } catch (e) {
        if (atualRef.current !== id) return; // cancelado pelo usuário
        if ((e as { status?: number })?.status === 503) semNeuralRef.current = true;
        // Sem voz neural, falha de rede ou áudio bloqueado: lê com a voz do aparelho.
        audioRef.current = null;
        lerNoAparelho(id, fala);
      }
    },
    [suportado, pararTudo, lerNoAparelho],
  );

  return { suportado, lendoId, ler, parar };
}
