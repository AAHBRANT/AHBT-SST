import {
  Button,
  Dialog,
  DialogActions,
  DialogBody,
  DialogContent,
  DialogSurface,
  DialogTitle,
  Field,
  Select,
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
  const {
    stream,
    videoRef,
    modoCamera,
    exigirCamera,
    dispositivosVideo,
    dispositivoAtualId,
    fecharCamera,
    capturarFoto,
    trocarDispositivo,
    inputRef,
  } = captura;

  // Reconhecimento facial (câmera frontal) ganha uma guia oval sobreposta ao vídeo — pedido do
  // usuário (22/09): ajuda a pessoa a centralizar o rosto no quadro antes de capturar, melhorando a
  // qualidade da foto enviada ao Azure Face API. É só uma guia visual (máscara + contorno), não faz
  // detecção de rosto em tempo real — isso exigiria uma biblioteca de IA extra no navegador; a
  // validação de qualidade do rosto em si já é feita pelo Azure Face API no cadastro/autenticação.
  const mostrarGuiaRosto = modoCamera === 'user';

  return (
    <Dialog open={!!stream} onOpenChange={(_, data) => !data.open && fecharCamera()}>
      <DialogSurface>
        <DialogBody>
          <DialogTitle>Tirar foto</DialogTitle>
          <DialogContent>
            {dispositivosVideo.length > 1 && (
              <Field label="Câmera" style={{ marginBottom: 8 }}>
                <Select
                  value={dispositivoAtualId ?? ''}
                  onChange={(_, d) => trocarDispositivo(d.value)}
                >
                  {dispositivosVideo.map((d, indice) => (
                    <option key={d.deviceId} value={d.deviceId}>
                      {d.label || `Câmera ${indice + 1}`}
                    </option>
                  ))}
                </Select>
              </Field>
            )}

            <div style={{ position: 'relative' }}>
              <video
                ref={videoRef}
                autoPlay
                playsInline
                muted
                style={{
                  width: '100%',
                  borderRadius: 8,
                  display: 'block',
                  transform: modoCamera === 'user' ? 'scaleX(-1)' : undefined,
                }}
              />
              {mostrarGuiaRosto && (
                <div
                  aria-hidden
                  style={{
                    position: 'absolute',
                    inset: 0,
                    borderRadius: 8,
                    pointerEvents: 'none',
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'center',
                  }}
                >
                  <div
                    style={{
                      width: '55%',
                      aspectRatio: '3 / 4',
                      borderRadius: '50%',
                      border: '3px solid rgba(255, 255, 255, 0.85)',
                      boxShadow: '0 0 0 2000px rgba(0, 0, 0, 0.35)',
                    }}
                  />
                </div>
              )}
            </div>
            {mostrarGuiaRosto && (
              <Text size={200} style={{ display: 'block', marginTop: 8, textAlign: 'center' }}>
                Centralize o rosto dentro do círculo antes de capturar.
              </Text>
            )}

            {!exigirCamera && (
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
            )}
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
