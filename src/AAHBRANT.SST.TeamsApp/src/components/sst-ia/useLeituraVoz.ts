import { useCallback, useEffect, useRef, useState } from 'react';

// Resposta falada do SST IA com a voz do próprio aparelho (speechSynthesis), decisão do usuário em
// 18/11/2026: sem custo e o texto não sai do aparelho. A API do Claude só devolve texto. A qualidade
// depende das vozes instaladas (Teams no Windows tem vozes Microsoft em pt-BR; no celular varia).

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
  const suportado = typeof window !== 'undefined' && 'speechSynthesis' in window;
  // Id da mensagem que está sendo lida (null = em silêncio).
  const [lendoId, setLendoId] = useState<string | null>(null);
  const atualRef = useRef<SpeechSynthesisUtterance | null>(null);

  useEffect(() => {
    if (!suportado) return;
    // Chrome/Edge carregam a lista de vozes depois do primeiro getVoices().
    window.speechSynthesis.getVoices();
    return () => window.speechSynthesis.cancel();
  }, [suportado]);

  const parar = useCallback(() => {
    if (!suportado) return;
    atualRef.current = null;
    window.speechSynthesis.cancel();
    setLendoId(null);
  }, [suportado]);

  const ler = useCallback(
    (id: string, texto: string) => {
      if (!suportado) return;
      const fala = textoParaFala(texto);
      if (!fala) return;
      window.speechSynthesis.cancel();
      const frase = new SpeechSynthesisUtterance(fala);
      const voz = escolherVoz();
      if (voz) frase.voice = voz;
      frase.lang = voz?.lang ?? 'pt-BR';
      frase.rate = 1.03;
      const terminou = () => {
        if (atualRef.current === frase) {
          atualRef.current = null;
          setLendoId(null);
        }
      };
      frase.onend = terminou;
      frase.onerror = terminou;
      atualRef.current = frase;
      setLendoId(id);
      window.speechSynthesis.speak(frase);
    },
    [suportado],
  );

  return { suportado, lendoId, ler, parar };
}
