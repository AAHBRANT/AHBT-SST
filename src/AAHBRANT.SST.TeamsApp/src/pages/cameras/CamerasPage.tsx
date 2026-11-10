import { Abas, PageHeader, useAbaNaUrl } from '@ui';
import { Camera24Regular, Warning24Regular } from '@fluentui/react-icons';
import { MosaicoCamerasTab } from './MosaicoCamerasTab';
import { AlertasEpiTab } from './AlertasEpiTab';

type AbaCameras = 'cameras' | 'alertas';

const ABAS_VALIDAS: readonly AbaCameras[] = ['cameras', 'alertas'];

export function CamerasPage() {
  // O parâmetro ?aba=alertas é usado por links de outras telas (Task 3).
  const [aba, setAba] = useAbaNaUrl<AbaCameras>('aba', ABAS_VALIDAS, 'cameras');

  return (
    <div>
      <PageHeader
        titulo="Câmeras"
        subtitulo="Monitoramento de capacete e colete por imagem, com alerta no Telegram."
      />
      <Abas
        nivel="pilar"
        valor={aba}
        aoMudar={setAba}
        aria-label="Seções de Câmeras"
        abas={[
          { valor: 'cameras', rotulo: 'Câmeras', icone: <Camera24Regular /> },
          { valor: 'alertas', rotulo: 'Alertas de EPI', icone: <Warning24Regular /> },
        ]}
      />
      {aba === 'cameras' && <MosaicoCamerasTab />}
      {aba === 'alertas' && <AlertasEpiTab />}
    </div>
  );
}
