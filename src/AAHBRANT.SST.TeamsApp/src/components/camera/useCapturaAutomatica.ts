import { useEffect, useRef, useState } from 'react';

export const DURACAO_CAPTURA_AUTOMATICA_MS = 1500;

const CHAVE_PREFERENCIA = 'sst.camera.capturaAutomatica';

function lerPreferencia(): boolean {
  try {
    return localStorage.getItem(CHAVE_PREFERENCIA) !== 'desligada';
  } catch {
    return true;
  }
}

function gravarPreferencia(ligada: boolean) {
  try {
    localStorage.setItem(CHAVE_PREFERENCIA, ligada ? 'ligada' : 'desligada');
  } catch {
    // Sem armazenamento (janela anônima, Teams restrito): a escolha vale só até fechar a câmera.
  }
}

export interface CapturaAutomatica {
  ligada: boolean;
  alternar: (ligada: boolean) => void;
  // true durante a contagem; a tela usa para mostrar o anel e a mensagem "fique parado".
  contando: boolean;
}

// Captura sozinha quando o detector mantém o rosto aprovado por DURACAO_CAPTURA_AUTOMATICA_MS
// seguidos. Se o rosto sai do verde (mexeu, saiu do quadro, luz ruim), a contagem zera. Dispara uma
// vez só por câmera aberta (`sessao` muda a cada abertura): depois da foto o diálogo fecha, e uma nova
// abertura recomeça do zero. O botão Capturar continua valendo para quem preferir clicar.
export function useCapturaAutomatica(sessao: unknown, aprovado: boolean, aoCompletar: () => void): CapturaAutomatica {
  const [ligada, setLigada] = useState(lerPreferencia);
  const [contando, setContando] = useState(false);
  const jaDisparou = useRef(false);
  const aoCompletarRef = useRef(aoCompletar);
  useEffect(() => {
    aoCompletarRef.current = aoCompletar;
  });

  useEffect(() => {
    jaDisparou.current = false;
  }, [sessao]);

  useEffect(() => {
    if (!ligada || !aprovado || jaDisparou.current) {
      setContando(false);
      return;
    }
    setContando(true);
    const temporizador = window.setTimeout(() => {
      jaDisparou.current = true;
      setContando(false);
      aoCompletarRef.current();
    }, DURACAO_CAPTURA_AUTOMATICA_MS);
    return () => {
      window.clearTimeout(temporizador);
      setContando(false);
    };
  }, [ligada, aprovado, sessao]);

  return {
    ligada,
    alternar: (valor) => {
      setLigada(valor);
      gravarPreferencia(valor);
    },
    contando,
  };
}
