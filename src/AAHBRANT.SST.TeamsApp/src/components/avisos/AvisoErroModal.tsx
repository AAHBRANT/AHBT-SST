import { useEffect, useState } from 'react';
import {
  makeStyles,
  mergeClasses,
  tokens,
  Dialog,
  DialogSurface,
  DialogBody,
  DialogTitle,
  DialogContent,
  DialogActions,
  Button,
} from '@fluentui/react-components';
import { ErrorCircle24Filled, Warning24Filled, ChevronRight16Regular } from '@fluentui/react-icons';
import { irParaCampo, ouvirAvisoErro, resolverAlvo, type AvisoErro } from '../../lib/avisoErro';

const useStyles = makeStyles({
  cabecalho: { display: 'flex', alignItems: 'center', gap: '12px' },
  icone: {
    width: '40px',
    height: '40px',
    borderRadius: '50%',
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'center',
    flexShrink: 0,
  },
  iconeErro: { backgroundColor: tokens.colorPaletteRedBackground1, color: tokens.colorPaletteRedForeground1 },
  iconePendencia: {
    backgroundColor: tokens.colorPaletteMarigoldBackground1,
    color: tokens.colorPaletteMarigoldForeground1,
  },
  descricao: { margin: '2px 0 0', fontSize: tokens.fontSizeBase200, color: tokens.colorNeutralForeground3, fontWeight: 400 },
  lista: { listStyle: 'none', margin: 0, padding: 0, maxHeight: '320px', overflowY: 'auto' },
  item: {
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'space-between',
    gap: '12px',
    padding: '10px 8px',
    borderRadius: tokens.borderRadiusMedium,
    ':hover': { backgroundColor: tokens.colorNeutralBackground1Hover },
  },
  nome: { display: 'block', fontWeight: 600, fontSize: tokens.fontSizeBase300 },
  dica: { display: 'block', fontSize: tokens.fontSizeBase200, color: tokens.colorNeutralForeground3 },
  botaoIr: { backgroundColor: '#ebe9ad', color: '#670000', flexShrink: 0, ':hover': { backgroundColor: '#e1de94', color: '#670000' } },
});

// Montado uma única vez no AppShell; abre quando alguém chama mostrarAvisoErro (lib/avisoErro.ts).
export function AvisoErroModal() {
  const s = useStyles();
  const [aviso, setAviso] = useState<AvisoErro | null>(null);

  useEffect(() => ouvirAvisoErro(setAviso), []);

  if (!aviso) return null;
  const itens = aviso.itens ?? [];
  const comCampo = itens.filter((item) => resolverAlvo(item.alvo));
  const pendencia = aviso.tipo === 'pendencia';

  const ir = (alvo: (typeof itens)[number]['alvo']) => {
    setAviso(null);
    // Espera o modal fechar para o foco não voltar ao botão Salvar.
    window.setTimeout(() => irParaCampo(alvo), 150);
  };

  return (
    <Dialog open onOpenChange={(_, dados) => !dados.open && setAviso(null)}>
      <DialogSurface style={{ maxWidth: 480 }}>
        <DialogBody>
          <DialogTitle>
            <div className={s.cabecalho}>
              <div className={mergeClasses(s.icone, pendencia ? s.iconePendencia : s.iconeErro)}>
                {pendencia ? <Warning24Filled /> : <ErrorCircle24Filled />}
              </div>
              <div>
                {aviso.titulo}
                {aviso.descricao && <p className={s.descricao}>{aviso.descricao}</p>}
              </div>
            </div>
          </DialogTitle>
          <DialogContent>
            {itens.length > 0 && (
              <ul className={s.lista}>
                {itens.map((item, i) => (
                  <li key={i} className={s.item}>
                    <div>
                      <span className={s.nome}>{item.nome}</span>
                      {item.dica && <span className={s.dica}>{item.dica}</span>}
                    </div>
                    {resolverAlvo(item.alvo) && (
                      <Button
                        size="small"
                        appearance="primary"
                        className={s.botaoIr}
                        icon={<ChevronRight16Regular />}
                        iconPosition="after"
                        onClick={() => ir(item.alvo)}
                      >
                        Ir
                      </Button>
                    )}
                  </li>
                ))}
              </ul>
            )}
          </DialogContent>
          <DialogActions>
            <Button appearance="secondary" onClick={() => setAviso(null)}>
              Fechar
            </Button>
            {comCampo.length > 0 && (
              <Button appearance="primary" style={{ whiteSpace: 'nowrap' }} onClick={() => ir(comCampo[0].alvo)}>
                Ir para o primeiro
              </Button>
            )}
          </DialogActions>
        </DialogBody>
      </DialogSurface>
    </Dialog>
  );
}
