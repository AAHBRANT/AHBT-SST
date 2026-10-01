import {
  Button,
  Dialog,
  DialogActions,
  DialogBody,
  DialogContent,
  DialogSurface,
  DialogTitle,
  Field,
  Input,
  Select,
  Text,
} from '@fluentui/react-components';
import { Camera24Regular } from '@fluentui/react-icons';
import type { UseCapturaFoto } from './useCapturaFoto';
import { ResumoDadosFoto } from './ResumoDadosFoto';
import { pendenciasFoto } from '../../lib/dadosFoto';
import { FeedbackInline } from '@ui';

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
    dispositivosVideo,
    dispositivoAtualId,
    fecharCamera,
    capturarFoto,
    trocarDispositivo,
  } = captura;

  // Reconhecimento facial (câmera frontal) ganha uma guia oval sobreposta ao vídeo — pedido do
  // usuário (22/09): ajuda a pessoa a centralizar o rosto no quadro antes de capturar, melhorando a
  // qualidade da foto enviada ao Azure Face API. É só uma guia visual (máscara + contorno), não faz
  // detecção de rosto em tempo real — isso exigiria uma biblioteca de IA extra no navegador; a
  // validação de qualidade do rosto em si já é feita pelo Azure Face API no cadastro/autenticação.
  const mostrarGuiaRosto = modoCamera === 'user';

  return (
    <>
    <Dialog open={!!stream} onOpenChange={(_, data) => !data.open && fecharCamera()}>
      <DialogSurface>
        <DialogBody>
          <DialogTitle>Tirar foto</DialogTitle>
          <DialogContent>
            {captura.contextoFoto && <div style={{ display: 'grid', gridTemplateColumns: 'minmax(0, 1fr)', gap: 12, marginBottom: 16, overflowWrap: 'anywhere' }}>
              <Text weight="semibold">Obra: {captura.contextoFoto.obraNome || 'não identificada'}</Text>
              <Text size={200}>A data e a hora serão registradas ao capturar.</Text>
              <Field label="Local da foto" required>
                <Input value={captura.localFoto} maxLength={200} placeholder="Ex.: galpão 2, área de montagem"
                  onChange={(_, d) => captura.setLocalFoto(d.value)} />
              </Field>
              <FeedbackInline tom={captura.localizando ? 'info' : captura.localizacao.latitude != null ? 'info' : 'aviso'}>
                {captura.localizando ? 'Obtendo localização… aguarde antes de capturar.'
                  : captura.localizacao.motivoLocalizacao || (captura.localizacao.latitude != null
                    ? `Localização obtida${captura.localizacao.precisaoMetros != null ? ` · precisão ${Math.round(captura.localizacao.precisaoMetros)} m` : ' · precisão não informada pelo aparelho'}.`
                    : 'Localização ainda não obtida. A foto ficará com pendência.')}
              </FeedbackInline>
              <Button onClick={() => void captura.tentarLocalizacao()} disabled={captura.localizando}>Tentar localização novamente</Button>
            </div>}
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

          </DialogContent>
          <DialogActions>
            <Button appearance="secondary" onClick={fecharCamera}>
              Cancelar
            </Button>
            <Button appearance="primary" icon={<Camera24Regular />} onClick={capturarFoto} disabled={captura.processando}>
              Capturar
            </Button>
          </DialogActions>
        </DialogBody>
      </DialogSurface>
    </Dialog>
    <Dialog open={!!captura.fotoPendente} onOpenChange={(_, d) => { if (!d.open && !captura.processando) captura.cancelarFotoPendente(); }}>
      <DialogSurface aria-describedby="aviso-pendencia-foto">
        <DialogBody>
          <DialogTitle>Foto com dados pendentes</DialogTitle>
          <DialogContent>
            <div id="aviso-pendencia-foto" role="alert">
              <FeedbackInline tom="aviso">
                Falta registrar: {pendenciasFoto(captura.fotoPendente?.dados).join(', ')}.
                Você pode guardar a foto com pendência. Confira os dados antes de sair do local; poderá ser necessária uma nova captura para finalizar o registro.
              </FeedbackInline>
              <ResumoDadosFoto dados={captura.fotoPendente?.dados} />
            </div>
          </DialogContent>
          <DialogActions>
            <Button disabled={captura.processando} onClick={() => { captura.cancelarFotoPendente(); void captura.abrirCamera(); }}>Refazer foto</Button>
            <Button appearance="primary" disabled={captura.processando} onClick={() => void captura.salvarFotoPendente()}>
              {captura.processando ? 'Salvando…' : 'Salvar foto com pendência'}
            </Button>
          </DialogActions>
        </DialogBody>
      </DialogSurface>
    </Dialog>
    </>
  );
}
