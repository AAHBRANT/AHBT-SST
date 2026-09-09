import { useRef, useState } from 'react';
import { Button, Spinner, Text, makeStyles, tokens } from '@fluentui/react-components';
import { Camera24Regular, Dismiss16Regular } from '@fluentui/react-icons';
import { comprimirImagem } from '../lib/imagem';
import { designTokens } from '../theme';

const useEstilos = makeStyles({
  grade: {
    display: 'grid',
    gridTemplateColumns: 'repeat(3, minmax(96px, 160px))',
    gap: '12px',
  },
  slotVazio: {
    aspectRatio: '1 / 1',
    border: `2px dashed ${designTokens.colorCardBorder}`,
    borderRadius: '8px',
    display: 'flex',
    flexDirection: 'column',
    alignItems: 'center',
    justifyContent: 'center',
    gap: '6px',
    cursor: 'pointer',
    backgroundColor: designTokens.colorNeutralLight,
    color: designTokens.colorNeutralMedium,
    transition: 'border-color 0.15s, color 0.15s',
    ':hover': {
      border: `2px dashed ${designTokens.colorPrimary}`,
      color: designTokens.colorPrimary,
    },
    ':disabled': {
      cursor: 'not-allowed',
      opacity: 0.6,
    },
  },
  slotPreenchido: {
    position: 'relative',
    aspectRatio: '1 / 1',
    borderRadius: '8px',
    overflow: 'hidden',
    border: `1px solid ${designTokens.colorCardBorder}`,
  },
  imagemSlot: {
    width: '100%',
    height: '100%',
    objectFit: 'cover',
    display: 'block',
  },
  botaoRemover: {
    position: 'absolute',
    top: '4px',
    right: '4px',
    minWidth: 'auto',
    backgroundColor: 'rgba(0, 0, 0, 0.55)',
    color: tokens.colorNeutralBackground1,
    ':hover': {
      backgroundColor: 'rgba(0, 0, 0, 0.75)',
    },
  },
  overlayCarregando: {
    position: 'absolute',
    inset: 0,
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'center',
    backgroundColor: 'rgba(255, 255, 255, 0.6)',
  },
});

export interface FotoEvidenciaSlot {
  ordem: number;
  id: string;
  url: string;
}

interface GradeFotosEvidenciaProps {
  titulo: string;
  subtitulo?: string;
  total: number;
  fotos: FotoEvidenciaSlot[];
  somenteLeitura?: boolean;
  onSelecionarFoto: (ordem: number, arquivo: File) => void | Promise<void>;
  onRemoverFoto: (fotoId: string) => void | Promise<void>;
  onErroValidacao?: (mensagem: string) => void;
  tamanhoMaximoMb?: number;
}

// Grade de evidências fotográficas em quadros individuais (pedido do usuário, 04/09 — modelo visual
// de referência em quadros/slots numerados, cada um substituível). Reaproveitada por DdsDetalhePage
// e SessaoTreinamentoDetalhePage — os dois únicos fluxos do sistema com "N fotos obrigatórias
// travando uma ação de encerramento" (mesma checagem de negócio, cliente + servidor).
export function GradeFotosEvidencia({
  titulo,
  subtitulo,
  total,
  fotos,
  somenteLeitura = false,
  onSelecionarFoto,
  onRemoverFoto,
  onErroValidacao,
  tamanhoMaximoMb = 5,
}: GradeFotosEvidenciaProps) {
  const estilos = useEstilos();
  const inputsRef = useRef<Record<number, HTMLInputElement | null>>({});
  const [ordemProcessando, setOrdemProcessando] = useState<number | null>(null);

  async function tratarArquivo(ordem: number, arquivo: File | undefined) {
    if (!arquivo) return;

    let arquivoFinal = arquivo;
    if (arquivo.type.startsWith('image/')) {
      try {
        arquivoFinal = await comprimirImagem(arquivo);
      } catch {
        arquivoFinal = arquivo;
      }
    }

    const tamanhoMb = arquivoFinal.size / (1024 * 1024);
    if (tamanhoMb > tamanhoMaximoMb) {
      onErroValidacao?.(
        `O arquivo tem ${tamanhoMb.toFixed(1)} MB — o máximo permitido é ${tamanhoMaximoMb} MB. Tente uma foto com qualidade menor.`,
      );
      return;
    }

    try {
      setOrdemProcessando(ordem);
      await onSelecionarFoto(ordem, arquivoFinal);
    } finally {
      setOrdemProcessando(null);
    }
  }

  const porOrdem = new Map(fotos.map((f) => [f.ordem, f]));

  return (
    <div>
      <Text weight="semibold" style={{ display: 'block' }}>
        {titulo} ({fotos.length}/{total})
      </Text>
      {subtitulo && (
        <Text size={200} style={{ display: 'block', marginBottom: 8 }}>
          {subtitulo}
        </Text>
      )}

      <div className={estilos.grade}>
        {Array.from({ length: total }, (_, i) => i + 1).map((ordem) => {
          const foto = porOrdem.get(ordem);
          const processando = ordemProcessando === ordem;

          if (foto) {
            return (
              <div key={ordem} className={estilos.slotPreenchido}>
                <img src={foto.url} alt={`Evidência ${ordem}`} className={estilos.imagemSlot} />
                {!somenteLeitura && (
                  <Button
                    className={estilos.botaoRemover}
                    appearance="transparent"
                    size="small"
                    icon={<Dismiss16Regular />}
                    aria-label={`Remover foto ${ordem}`}
                    title="Remover/substituir foto"
                    onClick={() => onRemoverFoto(foto.id)}
                  />
                )}
                {processando && (
                  <div className={estilos.overlayCarregando}>
                    <Spinner size="tiny" />
                  </div>
                )}
              </div>
            );
          }

          return (
            <button
              key={ordem}
              type="button"
              className={estilos.slotVazio}
              disabled={somenteLeitura || processando}
              onClick={() => inputsRef.current[ordem]?.click()}
            >
              <input
                ref={(el) => {
                  inputsRef.current[ordem] = el;
                }}
                type="file"
                accept="image/*"
                capture="environment"
                style={{ display: 'none' }}
                onChange={(e) => {
                  const arquivo = e.target.files?.[0];
                  e.target.value = '';
                  tratarArquivo(ordem, arquivo);
                }}
              />
              {processando ? <Spinner size="tiny" /> : <Camera24Regular />}
              <Text size={200}>Foto {ordem}</Text>
            </button>
          );
        })}
      </div>
    </div>
  );
}
