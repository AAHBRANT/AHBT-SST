import { Button, Dialog, DialogActions, DialogBody, DialogContent, DialogSurface, DialogTitle, Legenda } from '@ui';
import { BiometriaDigitalCartao } from '../../pessoas/BiometriaDigitalCartao';

interface Props {
  trabalhadorId: string;
  trabalhadorNome: string;
  aoFechar: () => void;
  /** Chamado a cada cadastro (digital ou facial) para a lista de assinaturas conferir de novo. */
  aoAlterar: () => void;
}

// "Cadastrar biometria agora" (mockup v4): quem está sem biometria cadastra na hora, sem sair da
// integração. Reaproveita o card de Biometria do perfil (duas digitais e a facial), que já pede o
// consentimento e guarda só o modelo, nunca a imagem.
export function CadastroBiometriaDialog({ trabalhadorId, trabalhadorNome, aoFechar, aoAlterar }: Props) {
  return (
    <Dialog open onOpenChange={(_, d) => !d.open && aoFechar()}>
      <DialogSurface style={{ maxWidth: 560 }}>
        <DialogBody>
          <DialogTitle>Cadastrar biometria — {trabalhadorNome}</DialogTitle>
          <DialogContent>
            <Legenda>Depois do cadastro, a pessoa já pode assinar nesta etapa pela digital ou pelo rosto.</Legenda>
            <div style={{ marginTop: 12 }}>
              <BiometriaDigitalCartao trabalhadorId={trabalhadorId} trabalhadorNome={trabalhadorNome} aoAlterar={aoAlterar} />
            </div>
          </DialogContent>
          <DialogActions>
            <Button appearance="primary" onClick={aoFechar}>
              Concluir
            </Button>
          </DialogActions>
        </DialogBody>
      </DialogSurface>
    </Dialog>
  );
}
