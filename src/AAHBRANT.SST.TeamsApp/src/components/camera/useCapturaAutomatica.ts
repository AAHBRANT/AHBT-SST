import { useEffect, useRef, useState } from 'react';

export const DURACAO_CAPTURA_AUTOMATICA_MS = 1500;

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
// Pedido do usuário (11/10): a captura automática começa DESLIGADA a cada abertura da câmera (não
// lembra a escolha anterior) e só é oferecida no treinamento e no DDS — ver permitirCapturaAutomatica
// em useCapturaFoto. O operador liga na chave quando quiser.
export function useCapturaAutomatica(sessao: unknown, aprovado: boolean, aoCompletar: () => void): CapturaAutomatica {
  // Guarda em qual abertura a chave foi ligada: numa abertura nova (`sessao` diferente) ela já nasce
  // desligada, sem precisar zerar estado num efeito.
  const [ligadaNaSessao, setLigadaNaSessao] = useState<unknown>(null);
  const ligada = ligadaNaSessao !== null && ligadaNaSessao === sessao;
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
    alternar: (valor) => setLigadaNaSessao(valor ? sessao : null),
    contando,
  };
}
