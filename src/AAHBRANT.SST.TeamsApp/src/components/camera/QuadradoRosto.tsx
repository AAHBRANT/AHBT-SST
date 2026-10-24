import type { EstadoDetectorRosto } from './useDetectorRosto';

interface QuadradoRostoProps {
  caixa: EstadoDetectorRosto['caixa'];
  cor: string;
  // O vídeo da câmera frontal é espelhado (scaleX(-1)): o quadrado espelha junto.
  espelhado?: boolean;
}

// Quadrado que acompanha o rosto sobre o vídeo, posicionado por porcentagem (o vídeo ocupa a largura
// toda, sem recorte). Compartilhado entre o diálogo da câmera e a fila facial do DDS.
export function QuadradoRosto({ caixa, cor, espelhado = true }: QuadradoRostoProps) {
  if (!caixa) return null;
  const esquerda = espelhado ? 1 - caixa.x - caixa.largura : caixa.x;
  return (
    <div
      aria-hidden
      data-testid="quadrado-rosto"
      style={{
        position: 'absolute',
        left: `${esquerda * 100}%`,
        top: `${caixa.y * 100}%`,
        width: `${caixa.largura * 100}%`,
        height: `${caixa.altura * 100}%`,
        border: `3px solid ${cor}`,
        borderRadius: 6,
        pointerEvents: 'none',
        transition: 'all 120ms linear',
      }}
    />
  );
}

export const CORES_DETECTOR = { ok: '#1f8a4c', atencao: '#d98a00', alerta: '#d13438' } as const;
