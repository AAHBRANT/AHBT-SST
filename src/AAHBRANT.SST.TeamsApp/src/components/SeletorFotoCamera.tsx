import { useRef, useState } from 'react';
import { Button, Spinner } from '@fluentui/react-components';
import { Camera24Regular } from '@fluentui/react-icons';

interface SeletorFotoCameraProps {
  aoSelecionarArquivo: (arquivo: File) => void | Promise<void>;
  desabilitado?: boolean;
  rotulo?: string;
  tamanho?: 'small' | 'medium';
  tiposAceitos?: string;
  aparencia?: 'subtle' | 'secondary' | 'primary';
  apenasIcone?: boolean;
}

// Botão padrão de captura/seleção de foto em todo o sistema (pedido do usuário, 31/08): ícone de
// câmera no lugar do input de arquivo nativo ("Escolher Arquivo", feio e sem padrão visual), com
// capture="environment" para abrir a câmera traseira do celular direto — em vez do seletor de
// galeria/arquivos do sistema operacional. Mostra um spinner enquanto aoSelecionarArquivo roda
// (upload direto) ou instantâneo (seleção que só alimenta estado do formulário pai).
export function SeletorFotoCamera({
  aoSelecionarArquivo,
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
    try {
      setProcessando(true);
      await aoSelecionarArquivo(arquivo);
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
