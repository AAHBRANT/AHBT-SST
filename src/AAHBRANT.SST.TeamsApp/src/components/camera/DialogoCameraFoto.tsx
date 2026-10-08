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
  Switch,
  Text,
} from '@fluentui/react-components';
import { Camera24Regular } from '@fluentui/react-icons';
import type { UseCapturaFoto } from './useCapturaFoto';
import { ResumoDadosFoto } from './ResumoDadosFoto';
import { useDetectorRosto } from './useDetectorRosto';
import { DURACAO_CAPTURA_AUTOMATICA_MS, useCapturaAutomatica } from './useCapturaAutomatica';
import { QuadradoRosto, CORES_DETECTOR } from './QuadradoRosto';
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
    aoMontarVideo,
    modoCamera,
    dispositivosVideo,
    dispositivoAtualId,
    fecharCamera,
    capturarFoto,
    trocarDispositivo,
  } = captura;

  // Reconhecimento facial (câmera frontal) ganha uma guia oval sobreposta ao vídeo — pedido do
  // usuário (22/09): ajuda a pessoa a centralizar o rosto no quadro antes de capturar.
  // Desde 07/10 há também um detector de rosto ao vivo (useDetectorRosto, roda no aparelho): um
  // quadrado acompanha o rosto e o botão Capturar só libera com 1 rosto de frente, grande, centrado e
  // com luz boa. É só ajuda ao operador: a validação real continua sendo a do Azure Face API. Se o
  // detector não carregar, a câmera funciona como antes e a captura não é bloqueada.
  const mostrarGuiaRosto = modoCamera === 'user';
  const detector = useDetectorRosto(videoRef, mostrarGuiaRosto && !!stream);
  const avaliacao = detector.situacao === 'ativo' ? detector.avaliacao : null;
  const capturaBloqueadaPeloDetector = detector.situacao === 'ativo' && !avaliacao?.liberaCaptura;
  const corQuadrado = avaliacao ? CORES_DETECTOR[avaliacao.tom] : CORES_DETECTOR.ok;
  // Captura sozinha depois de 1,5 s com o rosto aprovado (verde) e estável. Só quando o detector está
  // ativo: sem detector não há como saber se o rosto está bom, e aí vale o botão Capturar.
  const automatica = useCapturaAutomatica(
    stream,
    mostrarGuiaRosto && !!avaliacao?.liberaCaptura && !captura.processando,
    () => void capturarFoto(),
  );

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
              {!captura.exigirCamera && <Button appearance="subtle" onClick={captura.anexarDaGaleria} disabled={!captura.localFoto.trim()}>
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
              {mostrarGuiaRosto && <QuadradoRosto caixa={detector.caixa} cor={corQuadrado} />}
              {automatica.contando && (
                <svg
                  aria-hidden
                  viewBox="0 0 90 90"
                  style={{ position: 'absolute', left: '50%', top: '50%', width: 88, height: 88, marginLeft: -44, marginTop: -44, transform: 'rotate(-90deg)', pointerEvents: 'none' }}
                >
                  <circle cx="45" cy="45" r="40" fill="rgba(0,0,0,0.35)" stroke="rgba(255,255,255,0.35)" strokeWidth="8" />
                  <circle cx="45" cy="45" r="40" fill="none" stroke={CORES_DETECTOR.ok} strokeWidth="8" strokeLinecap="round" strokeDasharray="251" strokeDashoffset="251">
                    <animate attributeName="stroke-dashoffset" from="251" to="0" dur={`${DURACAO_CAPTURA_AUTOMATICA_MS}ms`} fill="freeze" />
                  </circle>
                </svg>
              )}
            </div>
            {mostrarGuiaRosto && (
              <div role="status" aria-live="polite" style={{ marginTop: 8, textAlign: 'center' }}>
                {avaliacao ? (
                  <Text size={300} weight="semibold" style={{ color: corQuadrado }}>
                    {automatica.contando ? 'Fique parado… capturando' : avaliacao.mensagem}
                  </Text>
                ) : (
                  <Text size={200}>
                    {detector.situacao === 'carregando'
                      ? 'Preparando o detector de rosto… Centralize o rosto dentro do círculo.'
                      : 'Centralize o rosto dentro do círculo antes de capturar.'}
                  </Text>
                )}
              </div>
            )}
            {mostrarGuiaRosto && detector.situacao === 'ativo' && (
              <Switch
                style={{ marginTop: 4 }}
                checked={automatica.ligada}
                onChange={(_, d) => automatica.alternar(d.checked)}
                label="Captura automática"
              />
            )}

          </DialogContent>
          <DialogActions>
            <Button appearance="secondary" onClick={fecharCamera}>
              Cancelar
            </Button>
            <Button appearance="primary" icon={<Camera24Regular />} onClick={capturarFoto} disabled={captura.processando || capturaBloqueadaPeloDetector}>
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
