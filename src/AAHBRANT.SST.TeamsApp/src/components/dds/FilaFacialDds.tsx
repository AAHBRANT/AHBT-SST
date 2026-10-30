import { useCallback, useEffect, useRef, useState } from 'react';
import { Dialog, DialogSurface } from '@fluentui/react-components';
import { Checkmark24Filled, Dismiss24Regular, Info24Regular, Person24Regular, ScanCamera24Regular } from '@fluentui/react-icons';
import { api } from '../../lib/api';
import { tocarBipeAssinaturaAceita, tocarBipeNegado } from '../../lib/bipeAssinatura';
import { useDetectorRosto } from '../camera/useDetectorRosto';

export interface ResultadoFilaFacial {
  trabalhadorNome: string;
  jaConfirmado: boolean;
}

interface FilaFacialDdsProps {
  ddsId?: string;
  // Envio próprio da foto (19/10): a turma de treinamento reaproveita esta mesma fila em tela cheia.
  // Sem ele, usa o endpoint de fila do DDS.
  enviarFoto?: (foto: File) => Promise<ResultadoFilaFacial>;
  // Cabeçalho do pop-up (30/10): nome do DDS/turma, obra e quantos faltavam confirmar ao abrir a fila
  // (o "X de Y"; o valor é congelado na abertura, porque a tela recarrega a cada confirmação).
  titulo?: string;
  subtitulo?: string;
  totalInscritos?: number;
  aberto: boolean;
  aoFechar: () => void;
  // Chamado a cada presença nova confirmada, para a tela do DDS recarregar a lista de participantes.
  aoPresencaConfirmada: () => void;
}

type Resultado = { tom: 'ok' | 'info' | 'erro'; titulo: string; detalhe: string };
type Confirmado = { nome: string; hora: string };

// Paleta AAHBRANT do pop-up (mockup aprovado em 30/10).
const MARSALA = '#670000';
const BEGE = '#ebe9ad';
const VERDE = '#1f7a3a';

function iniciais(nome: string): string {
  const partes = nome.trim().split(/\s+/);
  return ((partes[0]?.[0] ?? '') + (partes.length > 1 ? partes[partes.length - 1][0] : '')).toUpperCase();
}

// Quantos ciclos seguidos (~120 ms cada) o rosto precisa estar no quadro antes da captura automática.
const CICLOS_ESTAVEL = 6;
// Lado mínimo do rosto, em pixels do vídeo, para valer a pena mandar ao Azure. Bem abaixo dos 200 px do
// cadastro: aqui só se identifica, e o usuário (29/10) não quer exigir posição nem luz na fila.
const LADO_MINIMO_ROSTO_FILA_PX = 120;
// Quanto o resultado fica na tela antes de a fila aceitar a próxima pessoa.
const RESULTADO_MS = { ok: 2200, info: 2200, erro: 3200 } as const;
// Depois do resultado, a próxima captura só arma quando o rosto sai do quadro (ciclos sem rosto)...
const CICLOS_SEM_ROSTO_PARA_ARMAR = 6;
// ...ou depois deste tempo, para a fila não travar se a próxima pessoa já estiver no quadro.
const REARMAR_APOS_MS = 5000;

// Na fila basta um rosto só, inteiro no quadro e não muito longe: sem oval, sem checagem de luz ou de
// centro (pedido do usuário, 29/10). Quem decide se é a pessoa é o Azure, no servidor.
function rostoServeParaFila(
  estado: string | undefined,
  caixa: { largura: number; altura: number } | null,
  video: HTMLVideoElement | null,
): boolean {
  if (!estado || estado === 'nenhum' || estado === 'varios' || estado === 'fora' || !caixa || !video?.videoWidth) return false;
  const lado = Math.min(caixa.largura * video.videoWidth, caixa.altura * video.videoHeight);
  return lado >= LADO_MINIMO_ROSTO_FILA_PX;
}

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

// "Modo fila" do DDS por reconhecimento facial, em pop-up (tela cheia até 30/10): a câmera fica aberta, cada
// funcionário só olha para ela e a presença é confirmada sozinha, com bipe (igual à fila da digital).
// O detector de rosto decide quando capturar (um rosto bom e estável); quem identifica é o Azure, no
// servidor, que recusa leituras ambíguas. Ver RegistrarParticipanteFacialFilaCommand.
export function FilaFacialDds({ ddsId, enviarFoto, titulo, subtitulo, totalInscritos, aberto, aoFechar, aoPresencaConfirmada }: FilaFacialDdsProps) {
  const videoRef = useRef<HTMLVideoElement>(null);
  // Guardado em ref para que a identidade de capturarEEnviar não mude a cada render da tela do DDS (o
  // efeito do detector contaria um "ciclo bom" a mais a cada render).
  const aoConfirmadaRef = useRef(aoPresencaConfirmada);
  aoConfirmadaRef.current = aoPresencaConfirmada;
  const enviarRef = useRef(enviarFoto);
  enviarRef.current = enviarFoto;
  const [stream, setStream] = useState<MediaStream | null>(null);
  const [erroCamera, setErroCamera] = useState<string | null>(null);
  const [resultado, setResultado] = useState<Resultado | null>(null);
  const [confirmados, setConfirmados] = useState<Confirmado[]>([]);
  const confirmadas = confirmados.length;
  const [totalNaAbertura, setTotalNaAbertura] = useState<number | undefined>(undefined);
  // Proporção real do vídeo da câmera (largura/altura), para a imagem ocupar a área sem faixas pretas.
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
    setConfirmados([]);
    setTotalNaAbertura(totalInscritos);
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
      const r = enviarRef.current
        ? await enviarRef.current(foto)
        : await api.dds.registrarParticipanteFacialFila(ddsId ?? '', foto);
      tocarBipeAssinaturaAceita();
      if (r.jaConfirmado) {
        novo = { tom: 'info', titulo: r.trabalhadorNome, detalhe: 'Presença já estava confirmada.' };
      } else {
        const hora = new Date().toLocaleTimeString('pt-BR', { hour: '2-digit', minute: '2-digit' });
        setConfirmados((lista) => [{ nome: r.trabalhadorNome, hora }, ...lista]);
        aoConfirmadaRef.current();
        novo = { tom: 'ok', titulo: r.trabalhadorNome, detalhe: `Presença confirmada · ${hora}` };
      }
    } catch (e) {
      tocarBipeNegado();
      novo = { tom: 'erro', titulo: 'Rosto não reconhecido', detalhe: `${mensagemDoErro(e)} Tente de novo ou use a digital.` };
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
    ciclosBons.current = rostoServeParaFila(avaliacao.estado, detector.caixa, videoRef.current) ? ciclosBons.current + 1 : 0;
    if (ciclosBons.current >= CICLOS_ESTAVEL) void capturarEEnviar();
  }, [avaliacao, aberto, detector.situacao, detector.caixa, capturarEEnviar]);

  const corOval = resultado ? { ok: '#2fb35a', info: BEGE, erro: '#e5484d' }[resultado.tom] : 'rgba(235, 233, 173, 0.9)';
  const fundoCartao = resultado ? { ok: VERDE, info: '#3a3a3a', erro: MARSALA }[resultado.tom] : MARSALA;
  const mensagemGuia = erroCamera
    ?? (detector.situacao === 'indisponivel'
      ? 'O detector de rosto não carregou. Use a fila da digital ou recarregue a página.'
      : detector.situacao === 'carregando' || !stream
        ? 'Preparando a câmera…'
        : null);
  const totalInscritosFila = totalNaAbertura || undefined;
  const progresso = totalInscritosFila ? Math.min(100, Math.round((confirmadas / totalInscritosFila) * 100)) : 0;

  // Pop-up (mockup aprovado em 30/10): faixa marsala no topo, câmera com guia oval à esquerda e a lista de
  // quem já foi confirmado nesta fila à direita. Em tela estreita a lista desce para baixo da câmera.
  return (
    <Dialog open={aberto} onOpenChange={(_, d) => !d.open && aoFechar()}>
      <DialogSurface
        aria-label="Fila facial"
        style={{
          width: 'min(1000px, calc(100vw - 32px))', maxWidth: 'none', height: 'min(680px, calc(100vh - 32px))', maxHeight: 'none',
          padding: 0, borderRadius: 20, overflow: 'hidden', display: 'flex', flexDirection: 'column', background: '#fff',
        }}
      >
        <div style={{ display: 'flex', alignItems: 'center', gap: 16, padding: '16px 20px', background: MARSALA, color: '#fff', flexWrap: 'wrap' }}>
          <div style={{ width: 44, height: 44, borderRadius: 12, background: 'rgba(235,233,173,0.18)', display: 'grid', placeItems: 'center', color: BEGE }}>
            <ScanCamera24Regular />
          </div>
          <div style={{ display: 'flex', flexDirection: 'column', gap: 2, flex: '1 1 200px', minWidth: 0 }}>
            <span style={{ fontSize: 20, fontWeight: 700 }}>{titulo ?? 'Fila facial'}</span>
            {subtitulo && <span style={{ fontSize: 13, color: BEGE }}>{subtitulo}</span>}
          </div>
          <div style={{ display: 'flex', alignItems: 'baseline', gap: 6, padding: '8px 16px', borderRadius: 999, background: 'rgba(255,255,255,0.12)' }}>
            <span style={{ fontSize: 22, fontWeight: 800 }}>{confirmadas}</span>
            <span style={{ fontSize: 14, color: BEGE }}>
              {totalInscritosFila ? `de ${totalInscritosFila} confirmados` : `confirmado${confirmadas === 1 ? '' : 's'}`}
            </span>
          </div>
          <button
            type="button"
            aria-label="Fechar fila"
            onClick={aoFechar}
            style={{ width: 44, height: 44, border: 0, borderRadius: 12, background: 'rgba(255,255,255,0.12)', color: '#fff', display: 'grid', placeItems: 'center', cursor: 'pointer' }}
          >
            <Dismiss24Regular />
          </button>
        </div>

        <div style={{ display: 'flex', flexWrap: 'wrap', flex: 1, minHeight: 0, overflow: 'auto' }}>
          <div style={{ flex: '999 1 420px', minWidth: 0, padding: 20, display: 'flex', flexDirection: 'column', gap: 14 }}>
            <div style={{ position: 'relative', flex: 1, minHeight: 280, borderRadius: 16, overflow: 'hidden', background: '#1b1414', display: 'grid', placeItems: 'center' }}>
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
                  style={{ width: '100%', height: '100%', display: 'block', objectFit: 'cover', transform: 'scaleX(-1)' }}
                />
              </div>
              <div
                aria-hidden
                style={{
                  position: 'absolute', left: '50%', top: '46%', height: '62%', aspectRatio: '3 / 4', transform: 'translate(-50%, -50%)',
                  borderRadius: '50%', border: `${resultado ? 4 : 3}px ${resultado ? 'solid' : 'dashed'} ${corOval}`,
                  boxShadow: '0 0 0 2000px rgba(0,0,0,0.35)', transition: 'border-color 150ms', pointerEvents: 'none',
                }}
              />
              {stream && (
                <div style={{ position: 'absolute', left: 14, top: 14, display: 'flex', alignItems: 'center', gap: 8, padding: '6px 12px', borderRadius: 999, background: 'rgba(0,0,0,0.55)', color: '#fff', fontSize: 13, fontWeight: 600 }}>
                  <span style={{ width: 8, height: 8, borderRadius: '50%', background: '#e5484d' }} />
                  Câmera ao vivo
                </div>
              )}
              {resultado && (
                <div
                  role="status"
                  aria-live="assertive"
                  style={{ position: 'absolute', left: 16, right: 16, bottom: 16, padding: 18, borderRadius: 16, background: fundoCartao, color: '#fff', display: 'flex', alignItems: 'center', gap: 16 }}
                >
                  <div style={{ width: 52, height: 52, flexShrink: 0, borderRadius: '50%', background: resultado.tom === 'erro' ? BEGE : '#fff', color: fundoCartao, display: 'grid', placeItems: 'center' }}>
                    {resultado.tom === 'ok' ? <Checkmark24Filled /> : resultado.tom === 'info' ? <Info24Regular /> : <Dismiss24Regular />}
                  </div>
                  <div style={{ display: 'flex', flexDirection: 'column', gap: 4, minWidth: 0 }}>
                    <span style={{ fontSize: 22, fontWeight: 800 }}>{resultado.titulo}</span>
                    <span style={{ fontSize: 15, color: resultado.tom === 'erro' ? BEGE : '#fff' }}>{resultado.detalhe}</span>
                  </div>
                </div>
              )}
            </div>
            <div role="status" aria-live="polite" style={{ display: 'flex', alignItems: 'center', gap: 14, padding: '12px 16px', borderRadius: 14, background: '#f7f6ea', border: '1px solid #e3e0b8', color: MARSALA }}>
              <Person24Regular />
              <div style={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
                <span style={{ fontSize: 18, fontWeight: 700 }}>{mensagemGuia ?? 'Próximo, por favor'}</span>
                {!mensagemGuia && (
                  <span style={{ fontSize: 14, color: '#4a4a4a' }}>Olhe para a câmera com o rosto dentro do oval. A presença é confirmada sozinha.</span>
                )}
              </div>
            </div>
          </div>

          <div style={{ flex: '1 1 260px', minWidth: 0, borderLeft: '1px solid #ecebe4', background: '#fbfbf8', display: 'flex', flexDirection: 'column' }}>
            <div style={{ padding: '18px 18px 10px', display: 'flex', flexDirection: 'column', gap: 8 }}>
              <span style={{ fontSize: 12, fontWeight: 700, letterSpacing: '0.06em', textTransform: 'uppercase', color: '#5f5f5f' }}>Confirmados nesta fila</span>
              {totalInscritosFila ? (
                <div style={{ height: 8, borderRadius: 999, background: '#ecebe4', overflow: 'hidden' }}>
                  <div style={{ width: `${progresso}%`, height: '100%', background: MARSALA, transition: 'width 300ms' }} />
                </div>
              ) : null}
            </div>
            <div style={{ flex: 1, minHeight: 0, overflowY: 'auto', padding: '0 12px 12px', display: 'flex', flexDirection: 'column', gap: 6 }}>
              {confirmados.length === 0 && (
                <span style={{ padding: '8px 6px', fontSize: 14, color: '#5f5f5f' }}>Ninguém confirmado ainda nesta fila.</span>
              )}
              {confirmados.map((c, i) => (
                <div key={`${c.nome}-${i}`} style={{ display: 'flex', alignItems: 'center', gap: 12, padding: 10, borderRadius: 12, background: '#fff', border: '1px solid #ecebe4' }}>
                  <div style={{ width: 38, height: 38, flexShrink: 0, borderRadius: '50%', background: BEGE, color: MARSALA, fontSize: 14, fontWeight: 700, display: 'grid', placeItems: 'center' }}>
                    {iniciais(c.nome)}
                  </div>
                  <div style={{ display: 'flex', flexDirection: 'column', flex: 1, minWidth: 0 }}>
                    <span style={{ fontSize: 14, fontWeight: 600, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>{c.nome}</span>
                    <span style={{ fontSize: 12, color: '#5f5f5f' }}>{c.hora}</span>
                  </div>
                  <span style={{ color: VERDE, display: 'grid' }}><Checkmark24Filled /></span>
                </div>
              ))}
            </div>
            <div style={{ padding: '14px 18px', borderTop: '1px solid #ecebe4' }}>
              <button
                type="button"
                onClick={aoFechar}
                style={{ width: '100%', height: 48, border: 0, borderRadius: 12, background: '#1a1a1a', color: '#fff', fontFamily: 'inherit', fontSize: 15, fontWeight: 600, cursor: 'pointer' }}
              >
                Encerrar fila
              </button>
            </div>
          </div>
        </div>
      </DialogSurface>
    </Dialog>
  );
}
