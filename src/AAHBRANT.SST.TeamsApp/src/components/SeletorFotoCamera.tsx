import { Button, Spinner, makeStyles } from '@fluentui/react-components';
import { Camera24Regular } from '@fluentui/react-icons';
import { useCapturaFoto, type UseCapturaFotoOptions } from './camera/useCapturaFoto';
import { DialogoCameraFoto } from './camera/DialogoCameraFoto';

interface SeletorFotoCameraProps extends UseCapturaFotoOptions {
  desabilitado?: boolean;
  rotulo?: string;
  tamanho?: 'small' | 'medium';
  tiposAceitos?: string;
  apenasIcone?: boolean;
  variante?: 'padrao' | 'facialAzure';
}

// Pedido do usuário (22/09): todo botão que abre a câmera (não os de só anexar arquivo/PDF, que têm
// permitirCamera=false) fica visualmente igual — mesmo verde do botão "Criar" do cabeçalho
// (AppShell.tsx, botaoCriarTopo), pra ficar óbvio de bater o olho qual ação abre a câmera.
const useEstilosBotaoCamera = makeStyles({
  botao: {
    backgroundColor: '#16a34a',
    color: '#ffffff',
    fontWeight: 600,
    minWidth: '128px',
    whiteSpace: 'nowrap',
    ':hover': {
      backgroundColor: '#15803d',
      color: '#ffffff',
    },
    ':hover:active': {
      backgroundColor: '#166534',
      color: '#ffffff',
    },
    ':disabled': {
      backgroundColor: 'var(--colorNeutralBackgroundDisabled)',
      color: 'var(--colorNeutralForegroundDisabled)',
    },
  },
  facialAzure: {
    minHeight: '52px',
    minWidth: '148px',
    border: '1px solid transparent',
    borderRadius: '5px',
    backgroundColor: '#0b5c32',
    color: '#ffffff',
    fontWeight: 800,
    boxShadow: 'inset 0 -3px 0 rgba(0, 0, 0, .14)',
    whiteSpace: 'nowrap',
    ':hover': {
      backgroundColor: '#084827',
      color: '#ffffff',
    },
    ':hover:active': {
      backgroundColor: '#06381f',
      color: '#ffffff',
    },
    ':disabled': {
      backgroundColor: 'var(--colorNeutralBackgroundDisabled)',
      color: 'var(--colorNeutralForegroundDisabled)',
      boxShadow: 'none',
    },
  },
});

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
  apenasIcone = false,
  modoCamera = 'environment',
  permitirCamera = true,
  exigirCamera = false,
  variante = 'padrao',
}: SeletorFotoCameraProps) {
  const captura = useCapturaFoto({ aoSelecionarArquivo, aoErroValidacao, tamanhoMaximoMb, modoCamera, permitirCamera, exigirCamera });
  const { inputRef, processando, abrirCamera, onInputChange } = captura;
  const estilosBotaoCamera = useEstilosBotaoCamera();
  const classeBotao = permitirCamera
    ? variante === 'facialAzure'
      ? estilosBotaoCamera.facialAzure
      : estilosBotaoCamera.botao
    : undefined;

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
        appearance={permitirCamera ? 'primary' : 'subtle'}
        className={classeBotao}
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
