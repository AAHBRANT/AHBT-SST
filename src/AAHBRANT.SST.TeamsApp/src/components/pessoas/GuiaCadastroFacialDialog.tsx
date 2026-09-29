import { useState, type ReactNode } from 'react';
import { Dialog, DialogActions, DialogBody, DialogContent, DialogSurface, DialogTitle } from '@fluentui/react-components';
import {
  Checkmark20Filled,
  Eye20Regular,
  Lightbulb20Regular,
  Person20Regular,
  PersonBoard20Regular,
  Warning20Regular,
} from '@fluentui/react-icons';
import { Button, Text } from '@ui';

interface ItemGuia {
  icone: ReactNode;
  titulo: string;
  detalhe: string;
}

// Guia padrão exibido SEMPRE antes do cadastro facial (pedido do usuário, 29/09). A foto de cadastro é
// a referência de todo reconhecimento futuro; o servidor recusa foto fraca (ver ValidarQualidadeParaCadastro),
// mas é bem melhor o operador saber antes o que o sistema exige do que descobrir depois da recusa.
const ITENS: ItemGuia[] = [
  {
    icone: <Eye20Regular />,
    titulo: 'Sem óculos e sem acessórios no rosto',
    detalhe: 'Retire óculos (de grau ou de sol), boné, capacete, touca, máscara e protetor facial. Prenda o cabelo para não cobrir a testa nem os olhos.',
  },
  {
    icone: <Lightbulb20Regular />,
    titulo: 'Ambiente claro, com luz de frente',
    detalhe: 'A luz deve vir da frente do rosto. Nunca fotografe de costas para janela, porta aberta ou lâmpada, e evite sombras no rosto.',
  },
  {
    icone: <Person20Regular />,
    titulo: 'Rosto de frente, centralizado e parado',
    detalhe: 'Olhe direto para a câmera, com expressão neutra e boca fechada. O rosto deve ocupar boa parte do quadro, com a câmera na altura dos olhos. Fique parado até a captura.',
  },
  {
    icone: <PersonBoard20Regular />,
    titulo: 'Somente o trabalhador na foto',
    detalhe: 'Não deve haver outra pessoa no enquadramento. Prefira um fundo simples.',
  },
];

interface GuiaCadastroFacialDialogProps {
  aberto: boolean;
  aoConfirmar: () => void;
  aoCancelar: () => void;
}

export function GuiaCadastroFacialDialog({ aberto, aoConfirmar, aoCancelar }: GuiaCadastroFacialDialogProps) {
  return (
    <Dialog open={aberto} onOpenChange={(_, d) => !d.open && aoCancelar()}>
      <DialogSurface style={{ maxWidth: 560 }}>
        <DialogBody>
          <DialogTitle>Antes de fotografar — guia do cadastro facial</DialogTitle>
          <DialogContent style={{ display: 'flex', flexDirection: 'column', gap: 14 }}>
            <Text>
              Esta foto será a referência usada em todas as assinaturas por reconhecimento facial. Siga as orientações
              abaixo para que ela seja aceita na primeira tentativa.
            </Text>
            <ul style={{ listStyle: 'none', margin: 0, padding: 0, display: 'flex', flexDirection: 'column', gap: 12 }}>
              {ITENS.map((item) => (
                <li key={item.titulo} style={{ display: 'flex', gap: 12, alignItems: 'flex-start' }}>
                  <span
                    aria-hidden
                    style={{
                      flex: '0 0 32px',
                      height: 32,
                      borderRadius: 16,
                      background: '#670000',
                      color: '#ffffff',
                      display: 'flex',
                      alignItems: 'center',
                      justifyContent: 'center',
                    }}
                  >
                    {item.icone}
                  </span>
                  <span style={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
                    <Text weight="semibold">{item.titulo}</Text>
                    <Text>{item.detalhe}</Text>
                  </span>
                </li>
              ))}
            </ul>
            <div style={{ display: 'flex', gap: 8, alignItems: 'flex-start' }}>
              <Warning20Regular aria-hidden />
              <Text>
                Foto fora do padrão é recusada na hora, e você poderá repetir. A foto aceita fica guardada no perfil do
                funcionário, como registro do cadastro.
              </Text>
            </div>
          </DialogContent>
          <DialogActions>
            <Button appearance="secondary" onClick={aoCancelar}>
              Cancelar
            </Button>
            <Button appearance="primary" icon={<Checkmark20Filled />} onClick={aoConfirmar}>
              OK
            </Button>
          </DialogActions>
        </DialogBody>
      </DialogSurface>
    </Dialog>
  );
}

// Guarda a ação "continuar" enquanto o operador lê o guia: o botão da câmera chama pedirGuia(abrirCamera)
// e a câmera só abre depois do OK.
export function useGuiaCadastroFacial() {
  const [continuar, setContinuar] = useState<(() => void) | null>(null);

  return {
    pedirGuia: (aoConfirmar: () => void) => setContinuar(() => aoConfirmar),
    guia: (
      <GuiaCadastroFacialDialog
        aberto={continuar !== null}
        aoConfirmar={() => {
          const seguir = continuar;
          setContinuar(null);
          seguir?.();
        }}
        aoCancelar={() => setContinuar(null)}
      />
    ),
  };
}
