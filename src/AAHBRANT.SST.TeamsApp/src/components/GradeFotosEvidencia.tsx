import { Text, makeStyles } from '@fluentui/react-components';
import { SlotFoto } from './camera/SlotFoto';

const useEstilos = makeStyles({
  grade: {
    display: 'grid',
    gridTemplateColumns: 'repeat(3, minmax(96px, 160px))',
    gap: '12px',
  },
});

export interface FotoEvidenciaSlot {
  ordem: number;
  id: string;
  url: string;
}

interface GradeFotosEvidenciaProps {
  titulo: string;
  subtitulo?: string;
  total: number;
  fotos: FotoEvidenciaSlot[];
  somenteLeitura?: boolean;
  onSelecionarFoto: (ordem: number, arquivo: File) => void | Promise<void>;
  onRemoverFoto: (fotoId: string) => void | Promise<void>;
  onErroValidacao?: (mensagem: string) => void;
  tamanhoMaximoMb?: number;
}

// Grade de evidências fotográficas em quadros individuais (pedido do usuário, 04/09 — modelo visual
// de referência em quadros/slots numerados, cada um substituível). Reaproveitada por DdsDetalhePage
// e SessaoTreinamentoDetalhePage — os dois únicos fluxos do sistema com "N fotos obrigatórias
// travando uma ação de encerramento" (mesma checagem de negócio, cliente + servidor).
//
// Virou o padrão de foto do sistema inteiro (14/09): o quadro/miniatura de cada slot agora mora em
// SlotFoto (components/camera/), reaproveitado aqui e em qualquer outro lugar com foto única.
export function GradeFotosEvidencia({
  titulo,
  subtitulo,
  total,
  fotos,
  somenteLeitura = false,
  onSelecionarFoto,
  onRemoverFoto,
  onErroValidacao,
  tamanhoMaximoMb = 5,
}: GradeFotosEvidenciaProps) {
  const estilos = useEstilos();
  const porOrdem = new Map(fotos.map((f) => [f.ordem, f]));

  return (
    <div>
      <Text weight="semibold" style={{ display: 'block' }}>
        {titulo} ({fotos.length}/{total})
      </Text>
      {subtitulo && (
        <Text size={200} style={{ display: 'block', marginBottom: 8 }}>
          {subtitulo}
        </Text>
      )}

      <div className={estilos.grade}>
        {Array.from({ length: total }, (_, i) => i + 1).map((ordem) => {
          const foto = porOrdem.get(ordem);
          return (
            <SlotFoto
              key={ordem}
              rotulo={`Foto ${ordem}`}
              url={foto?.url}
              somenteLeitura={somenteLeitura}
              modo="remover"
              aoRemover={foto ? () => onRemoverFoto(foto.id) : undefined}
              aoSelecionarArquivo={(arquivo) => onSelecionarFoto(ordem, arquivo)}
              aoErroValidacao={onErroValidacao}
              tamanhoMaximoMb={tamanhoMaximoMb}
            />
          );
        })}
      </div>
    </div>
  );
}
