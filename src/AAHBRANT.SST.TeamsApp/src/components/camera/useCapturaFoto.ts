import { useEffect, useRef, useState, type ChangeEvent } from 'react';
import { comprimirImagem } from '../../lib/imagem';

export interface UseCapturaFotoOptions {
  aoSelecionarArquivo: (arquivo: File) => void | Promise<void>;
  // Sem isso, o usuário só descobria que o arquivo era grande demais depois do upload ir e voltar
  // do servidor com erro — o limite de negócio (5 MB pra foto, mais pra PDF/certificado) já existe
  // no backend, mas nunca era checado antes de gastar a requisição inteira.
  aoErroValidacao?: (mensagem: string) => void;
  tamanhoMaximoMb?: number;
  // Câmera frontal ("user", ex.: reconhecimento facial — a pessoa fotografa o próprio rosto) ou
  // traseira ("environment", padrão — fotos de evidência/EPI/documento, apontando pra outra coisa).
  modoCamera?: 'user' | 'environment';
  // false para pontos de anexo que nunca são foto (ex.: documento PDF) — pula o diálogo de câmera
  // e vai direto pro seletor de arquivos nativo, já que a câmera só pode produzir JPEG.
  permitirCamera?: boolean;
}

// Lógica de captura/seleção de foto extraída de SeletorFotoCamera (14/09) para ser reaproveitada
// também pelo visual de slot em quadro (SlotFoto/GradeFotosEvidencia) — mesmo diálogo de câmera ao
// vivo (getUserMedia) nos dois lugares, em vez de um <input capture> "burro" que o Chrome/Edge de
// desktop costuma ignorar (só funciona de verdade em navegador mobile).
export function useCapturaFoto({
  aoSelecionarArquivo,
  aoErroValidacao,
  tamanhoMaximoMb = 5,
  modoCamera = 'environment',
  permitirCamera = true,
}: UseCapturaFotoOptions) {
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
      const sugestao = arquivoFinal.type.startsWith('image/') ? ' Tente uma foto com qualidade menor.' : '';
      aoErroValidacao?.(
        `O arquivo tem ${tamanhoMb.toFixed(1)} MB — o máximo permitido é ${tamanhoMaximoMb} MB.${sugestao}`,
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
    if (!permitirCamera || !navigator.mediaDevices?.getUserMedia) {
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

  function onInputChange(evento: ChangeEvent<HTMLInputElement>) {
    const arquivo = evento.target.files?.[0];
    evento.target.value = '';
    tratarArquivo(arquivo);
  }

  return {
    inputRef,
    videoRef,
    processando,
    stream,
    modoCamera,
    permitirCamera,
    abrirCamera,
    fecharCamera,
    capturarFoto,
    onInputChange,
  };
}

export type UseCapturaFoto = ReturnType<typeof useCapturaFoto>;
