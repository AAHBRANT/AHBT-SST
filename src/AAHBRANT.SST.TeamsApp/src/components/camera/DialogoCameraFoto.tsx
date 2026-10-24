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
    aoMontarVideo,
    modoCamera,
    dispositivosVideo,
    dispositivoAtualId,
    fecharCamera,
    capturarFoto,
    trocarDispositivo,
  } = captura;

  // Câmera simples (23/10): o usuário pediu para voltar ao "clica e captura". Saíram a guia oval, o
  // detector de rosto ao vivo (quadrado verde e mensagens de luz/posição), a captura automática e a
  // fila facial do DDS/Treinamento. useDetectorRosto, useCapturaAutomatica, QuadradoRosto e
  // FilaFacialDds continuam no projeto, só sem uso, caso se queira religar. A validação real segue
  // sendo a do Azure Face API depois da foto.

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
              <StatusLocalizacao captura={captura} />
              <Button onClick={() => void captura.tentarLocalizacao()} disabled={captura.localizando}>Tentar localização novamente</Button>
              {!captura.exigirCamera && <Button appearance="subtle" onClick={captura.anexarDaGaleria}>
                Anexar foto da galeria (sem geolocalização)
              </Button>}
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
                ref={aoMontarVideo}
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
            </div>

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

// Mensagem do rastreio contínuo de localização (08/10) — um texto por estado, ver rastreadorLocalizacao.ts.
function StatusLocalizacao({ captura }: { captura: UseCapturaFoto }) {
  const e = captura.estadoLocalizacao;
  const precisao = e?.leitura?.precisaoMetros;
  if (captura.aguardandoLocalizacao) {
    return <FeedbackInline tom="info">Foto tirada. Aguardando a localização (até 15 s)…</FeedbackInline>;
  }
  if (!e || e.status === 'buscando') {
    return <FeedbackInline tom="info">Buscando localização… Pode enquadrar a foto. Se ela não chegar até o clique, a captura espera até 15 s.</FeedbackInline>;
  }
  if (e.status === 'imprecisa') {
    return <FeedbackInline tom="aviso">
      Melhorando a precisão{precisao != null ? `: ${Math.round(precisao)} m` : ''}. O limite é 100 m. Aguarde alguns segundos, de preferência em área aberta.
    </FeedbackInline>;
  }
  if (e.status === 'pronta') {
    return <FeedbackInline tom="sucesso">
      Localização obtida{precisao != null ? ` · precisão ${Math.round(precisao)} m` : ' · precisão não informada pelo aparelho'}. Atualizando automaticamente.
    </FeedbackInline>;
  }
  return <FeedbackInline tom="erro">{e.motivo ?? 'Não foi possível obter a localização. A foto ficará com pendência.'}</FeedbackInline>;
}
