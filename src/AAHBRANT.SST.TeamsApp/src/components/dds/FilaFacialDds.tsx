import { useCallback, useEffect, useRef, useState } from 'react';
import { Button, Dialog, DialogBody, DialogSurface, Text } from '@fluentui/react-components';
import { Stop24Regular } from '@fluentui/react-icons';
import { api } from '../../lib/api';
import { tocarBipeAssinaturaAceita } from '../../lib/bipeAssinatura';
import { QuadradoRosto, CORES_DETECTOR } from '../camera/QuadradoRosto';
import { useDetectorRosto } from '../camera/useDetectorRosto';

interface FilaFacialDdsProps {
  ddsId: string;
  aberto: boolean;
  aoFechar: () => void;
  // Chamado a cada presença nova confirmada, para a tela do DDS recarregar a lista de participantes.
  aoPresencaConfirmada: () => void;
}

type Resultado = { tom: 'ok' | 'info' | 'erro'; texto: string };

// Quantos ciclos seguidos (~120 ms cada) o rosto precisa estar bom e parado antes da captura automática.
const CICLOS_ESTAVEL = 8;
// Quanto o resultado fica na tela antes de a fila aceitar a próxima pessoa.
const RESULTADO_MS = { ok: 2200, info: 2200, erro: 3200 } as const;
// Depois do resultado, a próxima captura só arma quando o rosto sai do quadro (ciclos sem rosto)...
const CICLOS_SEM_ROSTO_PARA_ARMAR = 6;
// ...ou depois deste tempo, para a fila não travar se a próxima pessoa já estiver no quadro.
const REARMAR_APOS_MS = 5000;

function mensagemDoErro(e: unknown): string {
  if (!(e instanceof Error)) return 'Não foi possível confirmar a presença. Tente de novo.';
  const trecho = e.message.match(/\{.*\}$/);
  if (trecho) {
    try {
      const corpo = JSON.parse(trecho[0]) as { erro?: string };
      if (typeof corpo.erro === 'string') return corpo.erro;
    } catch {
      // corpo não era JSON
    }
  }
  return e instanceof TypeError ? 'Sem conexão com o servidor. Verifique a internet.' : e.message;
}

// "Modo fila" do DDS por reconhecimento facial, em tela cheia para tablet: a câmera fica aberta, cada
// funcionário só olha para ela e a presença é confirmada sozinha, com bipe (igual à fila da digital).
// O detector de rosto decide quando capturar (um rosto bom e estável); quem identifica é o Azure, no
// servidor, que recusa leituras ambíguas. Ver RegistrarParticipanteFacialFilaCommand.
export function FilaFacialDds({ ddsId, aberto, aoFechar, aoPresencaConfirmada }: FilaFacialDdsProps) {
  const videoRef = useRef<HTMLVideoElement>(null);
  // Guardado em ref para que a identidade de capturarEEnviar não mude a cada render da tela do DDS (o
  // efeito do detector contaria um "ciclo bom" a mais a cada render).
  const aoConfirmadaRef = useRef(aoPresencaConfirmada);
  aoConfirmadaRef.current = aoPresencaConfirmada;
  const [stream, setStream] = useState<MediaStream | null>(null);
  const [erroCamera, setErroCamera] = useState<string | null>(null);
  const [resultado, setResultado] = useState<Resultado | null>(null);
  const [confirmadas, setConfirmadas] = useState(0);
  // Proporção real do vídeo da câmera (largura/altura): o quadrado do rosto é posicionado por porcentagem
  // e só alinha se a caixa do vídeo tiver exatamente essa proporção, sem faixas pretas.
  const [proporcao, setProporcao] = useState('16 / 9');

  const detector = useDetectorRosto(videoRef, aberto && !!stream);
  const avaliacao = detector.situacao === 'ativo' ? detector.avaliacao : null;

  // Estado da fila fora do React (muda a cada ciclo do detector): "ocupada" enquanto envia/mostra o
  // resultado, e "armada" quando já pode capturar a próxima pessoa.
  const ocupada = useRef(false);
  const armada = useRef(true);
  const ciclosBons = useRef(0);
  const ciclosSemRosto = useRef(0);

  useEffect(() => {
    if (!aberto) return undefined;
    let cancelado = false;
    let aberta: MediaStream | null = null;
    setErroCamera(null);
    setResultado(null);
    setConfirmadas(0);
    ocupada.current = false;
    armada.current = true;
    ciclosBons.current = 0;
    ciclosSemRosto.current = 0;
    if (!navigator.mediaDevices?.getUserMedia) {
      setErroCamera('Este aparelho não permite usar a câmera.');
      return undefined;
    }
    navigator.mediaDevices
      .getUserMedia({ video: { facingMode: 'user', width: { ideal: 1280 }, height: { ideal: 720 } } })
      .then((s) => {
        if (cancelado) {
          s.getTracks().forEach((t) => t.stop());
          return;
        }
        aberta = s;
        setStream(s);
      })
      .catch(() => !cancelado && setErroCamera('Não consegui abrir a câmera. Permita o uso da câmera e tente de novo.'));
    return () => {
      cancelado = true;
      aberta?.getTracks().forEach((t) => t.stop());
      setStream(null);
    };
  }, [aberto]);

  useEffect(() => {
    if (videoRef.current && stream) videoRef.current.srcObject = stream;
  }, [stream, aberto]);

  const capturarEEnviar = useCallback(async () => {
    const video = videoRef.current;
    if (!video || !video.videoWidth) return;
    ocupada.current = true;
    armada.current = false;
    ciclosBons.current = 0;
    ciclosSemRosto.current = 0;

    const canvas = document.createElement('canvas');
    canvas.width = video.videoWidth;
    canvas.height = video.videoHeight;
    canvas.getContext('2d')?.drawImage(video, 0, 0);
    let novo: Resultado;
    try {
      const blob = await new Promise<Blob | null>((r) => canvas.toBlob(r, 'image/jpeg', 0.92));
      if (!blob) throw new Error('Não consegui capturar a foto.');
      const foto = new File([blob], 'fila-facial.jpg', { type: 'image/jpeg' });
      const r = await api.dds.registrarParticipanteFacialFila(ddsId, foto);
      if (r.jaConfirmado) {
        novo = { tom: 'info', texto: `${r.trabalhadorNome} já teve a presença confirmada.` };
      } else {
        tocarBipeAssinaturaAceita();
        setConfirmadas((n) => n + 1);
        aoConfirmadaRef.current();
        novo = { tom: 'ok', texto: `Presença de ${r.trabalhadorNome} confirmada.` };
      }
    } catch (e) {
      novo = { tom: 'erro', texto: mensagemDoErro(e) };
    }
    setResultado(novo);
    window.setTimeout(() => {
      ocupada.current = false;
      setResultado(null);
      // A próxima captura só arma quando o rosto sair do quadro ou passar o tempo de espera.
      window.setTimeout(() => { armada.current = true; }, REARMAR_APOS_MS);
    }, RESULTADO_MS[novo.tom]);
  }, [ddsId]);

  // A cada leitura do detector: conta ciclos bons seguidos (para capturar) e ciclos sem rosto (para armar).
  useEffect(() => {
    if (!aberto || detector.situacao !== 'ativo' || !avaliacao) return;
    if (avaliacao.estado === 'nenhum') {
      ciclosSemRosto.current++;
      if (ciclosSemRosto.current >= CICLOS_SEM_ROSTO_PARA_ARMAR && !ocupada.current) armada.current = true;
    } else {
      ciclosSemRosto.current = 0;
    }
    if (ocupada.current || !armada.current) {
      ciclosBons.current = 0;
      return;
    }
    ciclosBons.current = avaliacao.liberaCaptura ? ciclosBons.current + 1 : 0;
    if (ciclosBons.current >= CICLOS_ESTAVEL) void capturarEEnviar();
  }, [avaliacao, aberto, detector.situacao, capturarEEnviar]);

  const cor = avaliacao ? CORES_DETECTOR[avaliacao.tom] : CORES_DETECTOR.ok;
  const corResultado = resultado ? { ok: '#1f8a4c', info: '#1b6ec2', erro: '#d13438' }[resultado.tom] : undefined;
  const mensagemGuia = erroCamera
    ?? (detector.situacao === 'indisponivel'
      ? 'O detector de rosto não carregou. Use a fila da digital ou recarregue a página.'
      : detector.situacao === 'carregando' || !stream
        ? 'Preparando a câmera…'
        : avaliacao?.liberaCaptura
          ? 'Fique parado, vou capturar…'
          : avaliacao?.mensagem ?? 'Próximo funcionário: olhe para a câmera.');

  return (
    <Dialog open={aberto} onOpenChange={(_, d) => !d.open && aoFechar()}>
      <DialogSurface style={{ width: '100vw', maxWidth: '100vw', height: '100vh', maxHeight: '100vh', borderRadius: 0, padding: 0 }}>
        <DialogBody style={{ display: 'grid', gridTemplateRows: 'auto minmax(0, 1fr) auto', height: '100%', gap: 12, padding: 16 }}>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', gap: 12, flexWrap: 'wrap' }}>
            <Text size={600} weight="semibold">Fila facial · {confirmadas} confirmada{confirmadas === 1 ? '' : 's'} nesta fila</Text>
            <Button appearance="primary" size="large" icon={<Stop24Regular />} onClick={aoFechar}>Fechar fila</Button>
          </div>

          <div style={{ position: 'relative', minHeight: 0, display: 'grid', placeItems: 'center', background: '#111', borderRadius: 8, overflow: 'hidden' }}>
            <div style={{ position: 'relative', height: '100%', maxWidth: '100%', aspectRatio: proporcao }}>
              <video
                ref={videoRef}
                autoPlay
                playsInline
                muted
                onLoadedMetadata={(e) => {
                  const v = e.currentTarget;
                  if (v.videoWidth && v.videoHeight) setProporcao(`${v.videoWidth} / ${v.videoHeight}`);
                }}
                style={{ width: '100%', height: '100%', display: 'block', transform: 'scaleX(-1)' }}
              />
              <QuadradoRosto caixa={detector.caixa} cor={cor} />
            </div>
            {resultado && (
              <div
                role="status"
                aria-live="assertive"
                style={{
                  position: 'absolute', inset: 0, display: 'grid', placeItems: 'center', padding: 24,
                  background: `${corResultado}e6`, color: '#fff', textAlign: 'center',
                }}
              >
                <span style={{ fontSize: 'clamp(28px, 6vw, 56px)', fontWeight: 700, lineHeight: 1.2 }}>{resultado.texto}</span>
              </div>
            )}
          </div>

          <div role="status" aria-live="polite" style={{ textAlign: 'center', minHeight: 48 }}>
            <span style={{ fontSize: 'clamp(20px, 3.4vw, 32px)', fontWeight: 600, color: avaliacao && !avaliacao.liberaCaptura ? cor : undefined }}>
              {mensagemGuia}
            </span>
          </div>
        </DialogBody>
      </DialogSurface>
    </Dialog>
  );
}
