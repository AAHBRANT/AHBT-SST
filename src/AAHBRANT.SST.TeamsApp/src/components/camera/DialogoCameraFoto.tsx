import { useEffect, useState } from 'react';
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
import { molduraEmPorcentagem } from '../../lib/rosto/moldura';
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
  // quadrado acompanha o rosto e a mensagem diz o que ajustar (aproximar, centralizar, luz). É só ajuda
  // ao operador: a validação real continua sendo a do Azure Face API.
  // Correção (11/10): o detector NÃO bloqueia mais o botão Capturar. Com o bloqueio, quem tinha webcam
  // de baixa resolução, pouca luz no canteiro ou o rosto um pouco fora do centro nunca chegava ao verde
  // e não conseguia assinar — e, como a foto nem saía do aparelho, essas tentativas não apareciam no
  // registro de falhas do servidor. Agora a pessoa sempre pode capturar; se a foto não servir, o Azure
  // recusa com a mensagem do motivo e ela tenta de novo.
  // 04/11: o oval virou uma moldura 3×4 em pé. No cadastro o Capturar só libera com o rosto inteiro
  // dentro dela; na assinatura ela é guia. Nos dois a foto sai em 3×4 centralizada no rosto.
  const mostrarGuiaRosto = modoCamera === 'user';
  const modoAssinatura = captura.modoCaptura === 'assinatura';
  const [urlPrevia, setUrlPrevia] = useState<string>();
  useEffect(() => {
    if (!captura.previa) { setUrlPrevia(undefined); return; }
    const url = URL.createObjectURL(captura.previa.arquivo);
    setUrlPrevia(url);
    return () => URL.revokeObjectURL(url);
  }, [captura.previa]);
  const faltas = pendenciasFoto(captura.previa?.dados);
  const detector = useDetectorRosto(videoRef, mostrarGuiaRosto && !!stream, captura.modoCaptura);
  const avaliacao = detector.situacao === 'ativo' ? detector.avaliacao : null;
  const corQuadrado = avaliacao ? CORES_DETECTOR[avaliacao.tom] : CORES_DETECTOR.ok;
  // Captura sozinha depois de 1,5 s com o rosto aprovado (verde) e estável. Só onde a tela permite
  // (treinamento e DDS) e só quando o detector está ativo; começa desligada a cada abertura.
  // O botão Capturar só libera com o rosto INTEIRO dentro da moldura (pedido do usuário, 03/11): foto
  // com rosto cortado ou fora do guia é a que o Azure recusa. Revê a decisão de 11/10 só no que é
  // geometria — luz e contraluz continuam apenas orientando. Se o detector não carregar
  // ('indisponivel'), não há como verificar e o botão fica liberado, para o cadastro não travar.
  const [dimensoesVideo, setDimensoesVideo] = useState<{ largura: number; altura: number } | null>(null);
  const aguardandoEnquadramento =
    mostrarGuiaRosto && (detector.situacao === 'carregando' || (detector.situacao === 'ativo' && !avaliacao?.enquadrado));
  const oferecerAutomatica = mostrarGuiaRosto && captura.permitirCapturaAutomatica;
  const automatica = useCapturaAutomatica(
    stream,
    oferecerAutomatica && !!avaliacao?.liberaCaptura && !captura.processando,
    () => void capturarFoto(detector.caixa),
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
              <StatusLocalizacao captura={captura} />
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
                ref={aoMontarVideo}
                onLoadedMetadata={(e) => setDimensoesVideo({ largura: e.currentTarget.videoWidth, altura: e.currentTarget.videoHeight })}
                autoPlay
                playsInline
                muted
                style={{
                  width: '100%',
                  borderRadius: 8,
                  display: 'block',
                  transform: captura.espelharVideo ? 'scaleX(-1)' : undefined,
                }}
              />
              {mostrarGuiaRosto && dimensoesVideo && (
                <div
                  aria-hidden
                  style={{ position: 'absolute', inset: 0, borderRadius: 8, overflow: 'hidden', pointerEvents: 'none' }}
                >
                  {/* Moldura 3×4 em pé (04/11), no lugar do oval: mesma conta da verificação e do recorte.
                      Verde quando o rosto está inteiro dentro dela (é quando o Capturar libera no cadastro). */}
                  <div
                    data-testid="moldura-rosto"
                    style={{
                      position: 'absolute',
                      ...molduraEmPorcentagem(dimensoesVideo.largura, dimensoesVideo.altura),
                      borderRadius: 6,
                      border: `3px solid ${avaliacao?.enquadrado ? CORES_DETECTOR.ok : 'rgba(255, 255, 255, 0.9)'}`,
                      boxShadow: '0 0 0 2000px rgba(0, 0, 0, 0.45)',
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
                      ? (modoAssinatura ? 'Preparando o detector de rosto… Olhe para a câmera.' : 'Preparando o detector de rosto… O botão libera quando o rosto estiver inteiro dentro da moldura.')
                      : (modoAssinatura ? 'Olhe para a câmera antes de capturar.' : 'Coloque o rosto inteiro dentro da moldura antes de capturar.')}
                  </Text>
                )}
              </div>
            )}
            {/* DIAGNÓSTICO TEMPORÁRIO (03/11): resolução real do vídeo e tamanho do rosto, para calibrar a
                distância mínima no tablet do campo. Remover depois da calibração. */}
            {mostrarGuiaRosto && stream && (
              <Text size={100} style={{ display: 'block', textAlign: 'center', marginTop: 4, opacity: 0.7 }}>
                Diagnóstico: vídeo {videoRef.current?.videoWidth ?? '?'}×{videoRef.current?.videoHeight ?? '?'}
                {detector.caixa && videoRef.current?.videoHeight
                  ? ` · rosto ${Math.round((Math.min(detector.caixa.largura * videoRef.current.videoWidth, detector.caixa.altura * videoRef.current.videoHeight) / videoRef.current.videoHeight) * 100)}% da altura do vídeo (liberado entre 33% e ~48%)`
                  : ' · sem rosto'}
              </Text>
            )}
            {oferecerAutomatica && detector.situacao === 'ativo' && (
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
            <Button appearance="primary" icon={<Camera24Regular />} onClick={() => void capturarFoto(detector.caixa)} disabled={captura.processando || aguardandoEnquadramento}>
              Capturar
            </Button>
          </DialogActions>
        </DialogBody>
      </DialogSurface>
    </Dialog>
    <Dialog open={!!captura.previa} onOpenChange={(_, d) => { if (!d.open && !captura.processando) captura.descartarPrevia(); }}>
      <DialogSurface>
        <DialogBody>
          <DialogTitle>Conferir foto</DialogTitle>
          <DialogContent>
            {urlPrevia && <img src={urlPrevia} alt="Foto capturada" style={{ width: '100%', maxHeight: '40vh', objectFit: 'contain' }} />}
            {!!faltas.length && <FeedbackInline tom="aviso">
              Falta registrar {faltas.join(', ')}. {captura.previa?.dados.motivoLocalizacao} Confira os dados e refaça a foto no local.
            </FeedbackInline>}
            <ResumoDadosFoto dados={captura.previa?.dados} />
          </DialogContent>
          <DialogActions>
            <Button disabled={captura.processando} onClick={captura.descartarPrevia}>Cancelar</Button>
            <Button disabled={captura.processando} onClick={() => { captura.descartarPrevia(); void captura.abrirCamera(); }}>Refazer foto</Button>
            <Button appearance="primary" disabled={captura.processando || !!faltas.length} onClick={() => void captura.confirmarFoto()}>
              {captura.processando ? 'Salvando…' : 'Confirmar foto'}
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
  return <FeedbackInline tom="erro">{e.motivo ?? 'Permita o acesso à localização e tente novamente.'}</FeedbackInline>;
}
