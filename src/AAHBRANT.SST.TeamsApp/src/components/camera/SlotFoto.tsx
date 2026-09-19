import { useState } from 'react';
import {
  Button,
  Dialog,
  DialogSurface,
  Spinner,
  Text,
  makeStyles,
  mergeClasses,
  tokens,
} from '@fluentui/react-components';
import { Camera24Regular, Dismiss16Regular, Dismiss24Regular, Edit16Regular } from '@fluentui/react-icons';
import { designTokens } from '@ui';
import { useCapturaFoto, type UseCapturaFotoOptions } from './useCapturaFoto';
import { DialogoCameraFoto } from './DialogoCameraFoto';

const useEstilos = makeStyles({
  slotVazio: {
    aspectRatio: '1 / 1',
    border: `2px dashed ${designTokens.colorCardBorder}`,
    borderRadius: '8px',
    display: 'flex',
    flexDirection: 'column',
    alignItems: 'center',
    justifyContent: 'center',
    gap: '6px',
    cursor: 'pointer',
    backgroundColor: designTokens.colorNeutralLight,
    color: designTokens.colorNeutralMedium,
    transitionProperty: 'border-color, color',
    transitionDuration: '0.15s',
    ':hover': {
      border: `2px dashed ${designTokens.colorPrimary}`,
      color: designTokens.colorPrimary,
    },
    ':disabled': {
      cursor: 'not-allowed',
      opacity: 0.6,
    },
  },
  slotVazioCompacto: {
    width: '36px',
    height: '36px',
    aspectRatio: 'auto',
    borderRadius: '6px',
  },
  slotPreenchido: {
    position: 'relative',
    aspectRatio: '1 / 1',
    borderRadius: '8px',
    overflow: 'hidden',
    border: `1px solid ${designTokens.colorCardBorder}`,
  },
  slotPreenchidoCompacto: {
    width: '36px',
    height: '36px',
    aspectRatio: 'auto',
    borderRadius: '6px',
  },
  botaoOverlayCompacto: {
    top: '1px',
    right: '1px',
    minWidth: '20px',
    height: '20px',
    padding: 0,
  },
  imagemSlot: {
    width: '100%',
    height: '100%',
    objectFit: 'cover',
    display: 'block',
  },
  botaoImagem: {
    display: 'block',
    width: '100%',
    height: '100%',
    padding: 0,
    margin: 0,
    border: 'none',
    background: 'transparent',
    cursor: 'pointer',
  },
  imagemAmpliada: {
    display: 'block',
    maxWidth: '90vw',
    maxHeight: '85vh',
    objectFit: 'contain',
    margin: '0 auto',
  },
  superficieAmpliada: {
    position: 'relative',
    padding: 0,
    background: 'transparent',
    boxShadow: 'none',
    maxWidth: 'none',
  },
  botaoFecharAmpliada: {
    position: 'absolute',
    top: '8px',
    right: '8px',
    minWidth: 'auto',
    backgroundColor: 'rgba(0, 0, 0, 0.55)',
    color: tokens.colorNeutralBackground1,
    ':hover': {
      backgroundColor: 'rgba(0, 0, 0, 0.75)',
    },
  },
  botaoOverlay: {
    position: 'absolute',
    top: '4px',
    right: '4px',
    minWidth: 'auto',
    backgroundColor: 'rgba(0, 0, 0, 0.55)',
    color: tokens.colorNeutralBackground1,
    ':hover': {
      backgroundColor: 'rgba(0, 0, 0, 0.75)',
    },
  },
  overlayCarregando: {
    position: 'absolute',
    inset: 0,
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'center',
    backgroundColor: 'rgba(255, 255, 255, 0.6)',
  },
});

export interface SlotFotoProps extends UseCapturaFotoOptions {
  /** Texto mostrado dentro do quadro vazio (ex.: "Foto 1", "Antes", "Depois", "Funcionário"). */
  rotulo: string;
  /** URL da miniatura já carregada, se houver. */
  url?: string | null;
  /** Miniatura ainda sendo baixada (spinner substitui o quadro vazio/tracejado). */
  carregandoMiniatura?: boolean;
  somenteLeitura?: boolean;
  /**
   * 'substituir' (padrão): quadro preenchido tem um botão de lápis que reabre a câmera e troca a
   * foto no lugar — caso de uma foto única representando o registro (funcionário, obra, item de
   * catálogo, evidência antes/depois).
   * 'remover': quadro preenchido tem um X que chama `aoRemover` — caso de grade numerada com N fotos
   * obrigatórias (DDS, Turma de Treinamento), onde remover libera o número pra receber outra foto.
   */
  modo?: 'substituir' | 'remover';
  aoRemover?: () => void | Promise<void>;
  /**
   * 'padrao' (~96-160px, com o rótulo escrito no quadro vazio): evidências, uma foto grande por vez.
   * 'compacta' (~36px, só ícone, sem texto): dentro de linha de tabela — cadastro de catálogo, logo
   * de obra etc. `rotulo` continua usado como aria-label/title mesmo sem aparecer escrito.
   */
  tamanho?: 'padrao' | 'compacta';
}

// Visual de referência do sistema pra qualquer foto (pedido do usuário, 14/09): quadro com borda
// tracejada + ícone de câmera quando vazio; miniatura de verdade preenchendo o quadro quando já tem
// foto. Unifica o padrão que já existia só no DDS/Turma de Treinamento (GradeFotosEvidencia) com o
// diálogo de câmera ao vivo do SeletorFotoCamera (useCapturaFoto) — mesma captura, visual em quadro.
export function SlotFoto({
  rotulo,
  url,
  carregandoMiniatura = false,
  somenteLeitura = false,
  modo = 'substituir',
  aoRemover,
  tamanho = 'padrao',
  aoSelecionarArquivo,
  aoErroValidacao,
  tamanhoMaximoMb,
  modoCamera,
  permitirCamera,
}: SlotFotoProps) {
  const estilos = useEstilos();
  const captura = useCapturaFoto({ aoSelecionarArquivo, aoErroValidacao, tamanhoMaximoMb, modoCamera, permitirCamera });
  const { inputRef, processando, abrirCamera, onInputChange } = captura;
  const compacta = tamanho === 'compacta';
  const ocupado = processando || carregandoMiniatura;
  const [ampliada, setAmpliada] = useState(false);

  return (
    <div>
      <input ref={inputRef} type="file" accept="image/*" style={{ display: 'none' }} onChange={onInputChange} />

      {url ? (
        <div className={mergeClasses(estilos.slotPreenchido, compacta && estilos.slotPreenchidoCompacto)}>
          <button
            type="button"
            className={estilos.botaoImagem}
            onClick={() => setAmpliada(true)}
            aria-label={`Ampliar ${rotulo}`}
            title="Clique para ampliar"
          >
            <img src={url} alt={rotulo} className={estilos.imagemSlot} />
          </button>
          {!somenteLeitura && (
            <Button
              className={mergeClasses(estilos.botaoOverlay, compacta && estilos.botaoOverlayCompacto)}
              appearance="transparent"
              size="small"
              icon={modo === 'remover' ? <Dismiss16Regular /> : <Edit16Regular />}
              aria-label={modo === 'remover' ? `Remover ${rotulo}` : `Trocar ${rotulo}`}
              title={modo === 'remover' ? 'Remover/substituir foto' : 'Trocar foto'}
              onClick={modo === 'remover' ? aoRemover : abrirCamera}
              disabled={ocupado}
            />
          )}
          {ocupado && (
            <div className={estilos.overlayCarregando}>
              <Spinner size="tiny" />
            </div>
          )}
        </div>
      ) : (
        <button
          type="button"
          className={mergeClasses(estilos.slotVazio, compacta && estilos.slotVazioCompacto)}
          disabled={somenteLeitura || ocupado}
          onClick={abrirCamera}
          aria-label={rotulo}
          title={compacta ? rotulo : undefined}
        >
          {ocupado ? (
            <Spinner size="tiny" />
          ) : (
            <Camera24Regular fontSize={compacta ? 16 : undefined} />
          )}
          {!compacta && <Text size={200}>{rotulo}</Text>}
        </button>
      )}

      <DialogoCameraFoto captura={captura} />

      {url && (
        <Dialog open={ampliada} onOpenChange={(_, data) => setAmpliada(data.open)}>
          <DialogSurface className={estilos.superficieAmpliada}>
            <Button
              className={estilos.botaoFecharAmpliada}
              appearance="transparent"
              size="small"
              icon={<Dismiss24Regular />}
              aria-label="Fechar"
              onClick={() => setAmpliada(false)}
            />
            <img src={url} alt={rotulo} className={estilos.imagemAmpliada} />
          </DialogSurface>
        </Dialog>
      )}
    </div>
  );
}
