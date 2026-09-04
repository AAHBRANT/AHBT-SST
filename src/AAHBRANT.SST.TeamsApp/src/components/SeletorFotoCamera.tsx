import { useEffect, useRef, useState } from 'react';
import {
  Button,
  Dialog,
  DialogActions,
  DialogBody,
  DialogContent,
  DialogSurface,
  DialogTitle,
  Spinner,
  Text,
} from '@fluentui/react-components';
import { Camera24Regular } from '@fluentui/react-icons';
import { comprimirImagem } from '../lib/imagem';

interface SeletorFotoCameraProps {
  aoSelecionarArquivo: (arquivo: File) => void | Promise<void>;
  // Sem isso, o usuário só descobria que o arquivo era grande demais depois do upload ir e voltar
  // do servidor com erro — o limite de negócio (5 MB pra foto, mais pra PDF/certificado) já existe
  // no backend, mas nunca era checado antes de gastar a requisição inteira.
  aoErroValidacao?: (mensagem: string) => void;
  tamanhoMaximoMb?: number;
  desabilitado?: boolean;
  rotulo?: string;
  tamanho?: 'small' | 'medium';
  tiposAceitos?: string;
  aparencia?: 'subtle' | 'secondary' | 'primary';
  apenasIcone?: boolean;
  // Câmera frontal ("user", ex.: reconhecimento facial — a pessoa fotografa o próprio rosto) ou
  // traseira ("environment", padrão — fotos de evidência/EPI/documento, apontando pra outra coisa).
  modoCamera?: 'user' | 'environment';
}

// Botão padrão de captura/seleção de foto em todo o sistema (pedido do usuário, 31/08 e 04/09): ao
// clicar, tenta abrir um preview ao vivo da câmera do dispositivo (getUserMedia) num diálogo, com
// botão "Capturar" — funciona tanto em notebook (webcam) quanto em celular/tablet. Se a câmera não
// existir, a permissão for negada, ou o navegador não suportar getUserMedia, cai automaticamente no
// seletor de arquivos nativo do sistema operacional (mesmo comportamento de antes).
export function SeletorFotoCamera({
  aoSelecionarArquivo,
  aoErroValidacao,
  tamanhoMaximoMb = 5,
  desabilitado,
  rotulo = 'Foto',
  tamanho = 'small',
  tiposAceitos = 'image/*',
  aparencia = 'subtle',
  apenasIcone = false,
  modoCamera = 'environment',
}: SeletorFotoCameraProps) {
  const inputRef = useRef<HTMLInputElement>(null);
  const videoRef = useRef<HTMLVideoElement>(null);
  const [processando, setProcessando] = useState(false);
  const [stream, setStream] = useState<MediaStream | null>(null);

  // Anexa o stream ao <video> só depois que o diálogo (e portanto o elemento) já está montado, e
  // para as tracks da câmera sempre que o stream muda ou o componente desmonta — sem isso a luz da
  // webcam ficava acesa mesmo depois de fechar o diálogo.
  useEffect(() => {
    if (!stream) return;
    if (videoRef.current) videoRef.current.srcObject = stream;
    return () => {
      stream.getTracks().forEach((track) => track.stop());
    };
  }, [stream]);

  async function tratarArquivo(arquivo: File | undefined) {
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
      aoErroValidacao?.(
        `O arquivo tem ${tamanhoMb.toFixed(1)} MB — o máximo permitido é ${tamanhoMaximoMb} MB. Tente uma foto com qualidade menor.`,
      );
      return;
    }

    try {
      setProcessando(true);
      await aoSelecionarArquivo(arquivoFinal);
    } finally {
      setProcessando(false);
    }
  }

  async function abrirCamera() {
    if (!navigator.mediaDevices?.getUserMedia) {
      inputRef.current?.click();
      return;
    }
    try {
      const novoStream = await navigator.mediaDevices.getUserMedia({ video: { facingMode: modoCamera } });
      setStream(novoStream);
    } catch {
      // Sem câmera, permissão negada, ou navegador sem suporte — cai no seletor de arquivos.
      inputRef.current?.click();
    }
  }

  function fecharCamera() {
    setStream(null);
  }

  function capturarFoto() {
    const video = videoRef.current;
    if (!video || !video.videoWidth) return;
    const canvas = document.createElement('canvas');
    canvas.width = video.videoWidth;
    canvas.height = video.videoHeight;
    canvas.getContext('2d')?.drawImage(video, 0, 0);
    fecharCamera();
    canvas.toBlob(
      (blob) => {
        if (!blob) return;
        tratarArquivo(new File([blob], `captura-${Date.now()}.jpg`, { type: 'image/jpeg' }));
      },
      'image/jpeg',
      0.92,
    );
  }

  return (
    <>
      <input
        ref={inputRef}
        type="file"
        accept={tiposAceitos}
        capture={modoCamera}
        style={{ display: 'none' }}
        onChange={(e) => {
          const arquivo = e.target.files?.[0];
          e.target.value = '';
          tratarArquivo(arquivo);
        }}
      />
      <Button
        appearance={aparencia}
        size={tamanho}
        icon={processando ? <Spinner size="tiny" /> : <Camera24Regular />}
        onClick={abrirCamera}
        disabled={desabilitado || processando}
        aria-label={rotulo}
        title={rotulo}
      >
        {apenasIcone ? undefined : rotulo}
      </Button>

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
                <a href="#" onClick={(e) => { e.preventDefault(); fecharCamera(); inputRef.current?.click(); }}>
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
    </>
  );
}
