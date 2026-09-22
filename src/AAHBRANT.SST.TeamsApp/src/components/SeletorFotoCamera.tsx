import { Button, Spinner } from '@fluentui/react-components';
import { Camera24Regular } from '@fluentui/react-icons';
import { useCapturaFoto, type UseCapturaFotoOptions } from './camera/useCapturaFoto';
import { DialogoCameraFoto } from './camera/DialogoCameraFoto';

interface SeletorFotoCameraProps extends UseCapturaFotoOptions {
  desabilitado?: boolean;
  rotulo?: string;
  tamanho?: 'small' | 'medium';
  tiposAceitos?: string;
  aparencia?: 'subtle' | 'secondary' | 'primary';
  apenasIcone?: boolean;
}

// Botão padrão de captura/seleção de foto em todo o sistema (pedido do usuário, 31/08 e 04/09): ao
// clicar, tenta abrir um preview ao vivo da câmera do dispositivo (getUserMedia) num diálogo, com
// botão "Capturar" — funciona tanto em notebook (webcam) quanto em celular/tablet. Se a câmera não
// existir, a permissão for negada, ou o navegador não suportar getUserMedia, cai automaticamente no
// seletor de arquivos nativo do sistema operacional (mesmo comportamento de antes).
//
// Lógica de câmera mora em useCapturaFoto (14/09) — SlotFoto reaproveita o mesmo hook e diálogo
// pro visual de quadro/miniatura (ver components/camera/).
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
  permitirCamera = true,
  exigirCamera = false,
}: SeletorFotoCameraProps) {
  const captura = useCapturaFoto({ aoSelecionarArquivo, aoErroValidacao, tamanhoMaximoMb, modoCamera, permitirCamera, exigirCamera });
  const { inputRef, processando, abrirCamera, onInputChange } = captura;

  return (
    <>
      <input
        ref={inputRef}
        type="file"
        accept={tiposAceitos}
        capture={permitirCamera ? modoCamera : undefined}
        style={{ display: 'none' }}
        onChange={onInputChange}
      />
      <Button
        appearance={aparencia}
        size={tamanho}
        icon={processando ? <Spinner size="tiny" /> : <Camera24Regular />}
        onClick={permitirCamera ? abrirCamera : () => inputRef.current?.click()}
        disabled={desabilitado || processando}
        aria-label={rotulo}
        title={rotulo}
      >
        {apenasIcone ? undefined : rotulo}
      </Button>

      <DialogoCameraFoto captura={captura} />
    </>
  );
}
