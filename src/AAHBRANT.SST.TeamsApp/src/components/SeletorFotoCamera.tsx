import { useRef, useState } from 'react';
import { Button, Spinner } from '@fluentui/react-components';
import { Camera24Regular } from '@fluentui/react-icons';

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
}

const LADO_MAXIMO_PX = 1600;
const QUALIDADE_JPEG = 0.8;

// Fotos de câmera de celular vêm em resolução plena (3-8 MB) mas são exibidas em miniaturas
// pequenas nas telas do sistema — sem isso, cada upload gasta banda/tempo à toa e engorda o banco
// (todo arquivo é gravado como byte[] direto numa tabela, não há storage próprio). Redesenha a
// imagem num <canvas> reduzindo pro lado maior de 1600px e reexporta como JPEG 80% de qualidade
// antes de checar o limite de tamanho. Se falhar (formato exótico, navegador sem suporte), segue
// com o arquivo original — a checagem de tamanho abaixo continua valendo como rede de segurança.
async function comprimirImagem(arquivo: File): Promise<File> {
  const bitmap = await createImageBitmap(arquivo);
  try {
    const escala = Math.min(1, LADO_MAXIMO_PX / Math.max(bitmap.width, bitmap.height));
    const largura = Math.round(bitmap.width * escala);
    const altura = Math.round(bitmap.height * escala);

    const canvas = document.createElement('canvas');
    canvas.width = largura;
    canvas.height = altura;
    const contexto = canvas.getContext('2d');
    if (!contexto) return arquivo;
    contexto.drawImage(bitmap, 0, 0, largura, altura);

    const blob = await new Promise<Blob | null>((resolve) => canvas.toBlob(resolve, 'image/jpeg', QUALIDADE_JPEG));
    if (!blob) return arquivo;

    const novoNome = arquivo.name.replace(/\.[^./]+$/, '') + '.jpg';
    return new File([blob], novoNome, { type: 'image/jpeg' });
  } finally {
    bitmap.close();
  }
}

// Botão padrão de captura/seleção de foto em todo o sistema (pedido do usuário, 31/08): ícone de
// câmera no lugar do input de arquivo nativo ("Escolher Arquivo", feio e sem padrão visual), com
// capture="environment" para abrir a câmera traseira do celular direto — em vez do seletor de
// galeria/arquivos do sistema operacional. Mostra um spinner enquanto aoSelecionarArquivo roda
// (upload direto) ou instantâneo (seleção que só alimenta estado do formulário pai).
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
}: SeletorFotoCameraProps) {
  const inputRef = useRef<HTMLInputElement>(null);
  const [processando, setProcessando] = useState(false);

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

  return (
    <>
      <input
        ref={inputRef}
        type="file"
        accept={tiposAceitos}
        capture="environment"
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
        onClick={() => inputRef.current?.click()}
        disabled={desabilitado || processando}
        aria-label={rotulo}
        title={rotulo}
      >
        {apenasIcone ? undefined : rotulo}
      </Button>
    </>
  );
}
