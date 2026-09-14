import {
  Button,
  Dialog,
  DialogActions,
  DialogBody,
  DialogContent,
  DialogSurface,
  DialogTitle,
  Text,
} from '@fluentui/react-components';
import { Camera24Regular } from '@fluentui/react-icons';
import type { UseCapturaFoto } from './useCapturaFoto';

interface DialogoCameraFotoProps {
  captura: UseCapturaFoto;
}

// Diálogo de preview ao vivo da câmera (getUserMedia), compartilhado entre SeletorFotoCamera e
// SlotFoto — mesmo markup extraído de SeletorFotoCamera (14/09), sem mudança visual.
export function DialogoCameraFoto({ captura }: DialogoCameraFotoProps) {
  const { stream, videoRef, modoCamera, fecharCamera, capturarFoto, inputRef } = captura;

  return (
    <Dialog open={!!stream} onOpenChange={(_, data) => !data.open && fecharCamera()}>
      <DialogSurface>
        <DialogBody>
          <DialogTitle>Tirar foto</DialogTitle>
          <DialogContent>
            <video
              ref={videoRef}
              autoPlay
              playsInline
              muted
              style={{
                width: '100%',
                borderRadius: 8,
                transform: modoCamera === 'user' ? 'scaleX(-1)' : undefined,
              }}
            />
            <Text size={200} style={{ display: 'block', marginTop: 8 }}>
              Não consegue usar a câmera?{' '}
              <a
                href="#"
                onClick={(e) => {
                  e.preventDefault();
                  fecharCamera();
                  inputRef.current?.click();
                }}
              >
                Selecionar um arquivo
              </a>
              .
            </Text>
          </DialogContent>
          <DialogActions>
            <Button appearance="secondary" onClick={fecharCamera}>
              Cancelar
            </Button>
            <Button appearance="primary" icon={<Camera24Regular />} onClick={capturarFoto}>
              Capturar
            </Button>
          </DialogActions>
        </DialogBody>
      </DialogSurface>
    </Dialog>
  );
}
