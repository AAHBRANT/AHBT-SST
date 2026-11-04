import { useEffect, useState, type RefObject } from 'react';
import { avaliarPresencaRosto, avaliarRostos, type AvaliacaoRosto, type CaixaRosto, type ModoCapturaRosto } from '../../lib/rosto/avaliarRosto';
import { detectarRostos, obterDetectorRosto } from '../../lib/rosto/detectorRosto';
import { medirLuz } from '../../lib/rosto/medirLuz';

export interface EstadoDetectorRosto {
  // 'carregando' enquanto o modelo baixa; 'indisponivel' se não deu para carregar (a câmera segue sem
  // detector e a captura não é bloqueada); 'ativo' quando está avaliando o quadro.
  situacao: 'carregando' | 'indisponivel' | 'ativo';
  avaliacao: AvaliacaoRosto | null;
  // Caixa do rosto em fração do quadro (0 a 1), para desenhar o quadrado; null sem rosto único.
  caixa: { x: number; y: number; largura: number; altura: number } | null;
}

const INTERVALO_MS = 120;
const INICIAL: EstadoDetectorRosto = { situacao: 'carregando', avaliacao: null, caixa: null };

// Roda o detector sobre o vídeo da câmera enquanto `ativo`. Ele só orienta o operador: o Azure Face
// continua sendo quem valida a foto no servidor.
// `modo` 'assinatura' só exige 1 rosto no quadro (ver avaliarPresencaRosto); 'cadastro' (padrão)
// mantém todas as regras de enquadramento e luz.
export function useDetectorRosto(
  videoRef: RefObject<HTMLVideoElement | null>,
  ativo: boolean,
  modo: ModoCapturaRosto = 'cadastro',
): EstadoDetectorRosto {
  const [estado, setEstado] = useState<EstadoDetectorRosto>(INICIAL);

  useEffect(() => {
    if (!ativo) return;
    let parar = false;
    let temporizador: number | undefined;

    void obterDetectorRosto().then((detector) => {
      if (parar) return;
      if (!detector) {
        setEstado({ situacao: 'indisponivel', avaliacao: null, caixa: null });
        return;
      }
      const ciclo = () => {
        if (parar) return;
        const video = videoRef.current;
        if (video && video.readyState >= 2 && video.videoWidth > 0) {
          try {
            const caixas: CaixaRosto[] = detectarRostos(detector, video, performance.now());
            const avaliacao = modo === 'assinatura'
              ? avaliarPresencaRosto(caixas)
              : avaliarRostos(caixas, video.videoWidth, video.videoHeight, caixas.length === 1 ? medirLuz(video, caixas[0]) : null);
            const c = caixas.length === 1 ? caixas[0] : null;
            setEstado({
              situacao: 'ativo',
              avaliacao,
              caixa: c && {
                x: c.x / video.videoWidth,
                y: c.y / video.videoHeight,
                largura: c.largura / video.videoWidth,
                altura: c.altura / video.videoHeight,
              },
            });
          } catch {
            // Falha pontual de um quadro: tenta o próximo.
          }
        }
        temporizador = window.setTimeout(ciclo, INTERVALO_MS);
      };
      ciclo();
    });

    return () => {
      parar = true;
      if (temporizador) window.clearTimeout(temporizador);
      // Zera ao fechar a câmera, para a próxima abertura não mostrar o quadro da vez anterior.
      setEstado(INICIAL);
    };
  }, [videoRef, ativo, modo]);

  return ativo ? estado : INICIAL;
}
