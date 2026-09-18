import { useState } from 'react';
import {
  makeStyles,
  mergeClasses,
  tokens,
  Dialog,
  DialogSurface,
  DialogTitle,
  DialogBody,
  DialogContent,
  DialogActions,
  Button,
} from '@fluentui/react-components';
import { Sparkle24Regular, Bug20Regular, Wrench20Regular, Add20Regular, ChevronDown20Regular } from '@fluentui/react-icons';
import { CategoriaNovidade, type NovidadeVersao } from '../../lib/api';

const useStyles = makeStyles({
  cabecalho: {
    display: 'flex',
    alignItems: 'center',
    gap: '10px',
  },
  icone: {
    width: '32px',
    height: '32px',
    borderRadius: tokens.borderRadiusMedium,
    backgroundColor: tokens.colorBrandBackground2,
    color: tokens.colorBrandForeground2,
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'center',
    flexShrink: 0,
  },
  subtitulo: {
    margin: 0,
    fontSize: tokens.fontSizeBase200,
    color: tokens.colorNeutralForeground3,
    paddingLeft: '42px',
  },
  instrucao: {
    margin: '16px 0 12px',
    fontSize: tokens.fontSizeBase200,
    fontWeight: tokens.fontWeightSemibold,
    color: tokens.colorNeutralForeground2,
  },
  lista: {
    display: 'flex',
    flexDirection: 'column',
    gap: '6px',
  },
  item: {
    border: `1px solid ${tokens.colorNeutralStroke2}`,
    borderRadius: tokens.borderRadiusMedium,
    overflow: 'hidden',
  },
  botaoItem: {
    width: '100%',
    display: 'flex',
    alignItems: 'flex-start',
    gap: '10px',
    padding: '10px 12px',
    background: 'transparent',
    border: 'none',
    cursor: 'pointer',
    textAlign: 'left',
  },
  marcadorCategoria: {
    width: '22px',
    height: '22px',
    borderRadius: tokens.borderRadiusCircular,
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'center',
    flexShrink: 0,
    marginTop: '1px',
  },
  marcadorCorrecao: {
    backgroundColor: tokens.colorPaletteGreenBackground2,
    color: tokens.colorPaletteGreenForeground2,
  },
  marcadorDestaque: {
    backgroundColor: tokens.colorBrandBackground2,
    color: tokens.colorBrandForeground2,
  },
  textoItem: {
    flex: 1,
    fontSize: tokens.fontSizeBase300,
    lineHeight: tokens.lineHeightBase300,
    color: tokens.colorNeutralForeground1,
  },
  chevron: {
    flexShrink: 0,
    marginTop: '3px',
    color: tokens.colorNeutralForeground3,
    transition: 'transform 0.15s',
  },
  chevronAberto: {
    transform: 'rotate(180deg)',
  },
  painel: {
    padding: '0 12px 14px 42px',
    display: 'flex',
    flexDirection: 'column',
    gap: '8px',
    fontSize: tokens.fontSizeBase200,
    lineHeight: tokens.lineHeightBase200,
  },
  rotuloAntes: {
    fontWeight: tokens.fontWeightSemibold,
    color: tokens.colorNeutralForeground3,
  },
  rotuloAgora: {
    fontWeight: tokens.fontWeightSemibold,
    color: tokens.colorBrandForeground1,
  },
  textoComparacao: {
    margin: '2px 0 0',
    color: tokens.colorNeutralForeground2,
  },
});

function IconeCategoria({ categoria, className }: { categoria: number; className: string }) {
  if (categoria === CategoriaNovidade.Correcao) return <Bug20Regular className={className} />;
  if (categoria === CategoriaNovidade.Novidade) return <Add20Regular className={className} />;
  return <Wrench20Regular className={className} />;
}

function ItemNovidade({ item }: { item: NovidadeVersao['itens'][number] }) {
  const estilos = useStyles();
  const [aberto, setAberto] = useState(false);
  const temComparacao = Boolean(item.antes || item.agora);

  return (
    <div className={estilos.item}>
      <button
        type="button"
        className={estilos.botaoItem}
        onClick={() => temComparacao && setAberto((atual) => !atual)}
        aria-expanded={aberto}
      >
        <span
          className={mergeClasses(
            estilos.marcadorCategoria,
            item.categoria === CategoriaNovidade.Correcao ? estilos.marcadorCorrecao : estilos.marcadorDestaque,
          )}
        >
          <IconeCategoria categoria={item.categoria} className="" />
        </span>
        <span className={estilos.textoItem}>{item.descricao}</span>
        {temComparacao && (
          <ChevronDown20Regular className={mergeClasses(estilos.chevron, aberto && estilos.chevronAberto)} />
        )}
      </button>
      {temComparacao && aberto && (
        <div className={estilos.painel}>
          {item.antes && (
            <div>
              <span className={estilos.rotuloAntes}>Antes</span>
              <p className={estilos.textoComparacao}>{item.antes}</p>
            </div>
          )}
          {item.agora && (
            <div>
              <span className={estilos.rotuloAgora}>Agora</span>
              <p className={estilos.textoComparacao}>{item.agora}</p>
            </div>
          )}
        </div>
      )}
    </div>
  );
}

function formatarData(dataIso: string) {
  return new Date(dataIso).toLocaleDateString('pt-BR');
}

export function NovidadesModal({
  novidade,
  nomeUsuario,
  aberto,
  aoFechar,
}: {
  novidade: NovidadeVersao;
  nomeUsuario: string;
  aberto: boolean;
  aoFechar: () => void;
}) {
  const estilos = useStyles();
  const primeiroNome = nomeUsuario.trim().split(' ')[0] || nomeUsuario;
  const temItemComComparacao = novidade.itens.some((item) => item.antes || item.agora);

  return (
    <Dialog open={aberto} onOpenChange={(_, data) => !data.open && aoFechar()}>
      <DialogSurface>
        <DialogBody>
          <DialogTitle>
            <div className={estilos.cabecalho}>
              <span className={estilos.icone}>
                <Sparkle24Regular />
              </span>
              Bem-vindo de volta, {primeiroNome}!
            </div>
          </DialogTitle>
          <DialogContent>
            <p className={estilos.subtitulo}>
              Novidades da versão {novidade.versao} &middot; publicado em {formatarData(novidade.dataPublicacao)}
            </p>

            <p className={estilos.instrucao}>
              {temItemComComparacao ? 'Clique em um item para ver o que mudou!' : 'O que mudou desde a última vez que você acessou:'}
            </p>
            <div className={estilos.lista}>
              {novidade.itens.map((item) => (
                <ItemNovidade key={item.id} item={item} />
              ))}
            </div>
          </DialogContent>

          <DialogActions>
            <Button appearance="primary" onClick={aoFechar}>
              Entendi
            </Button>
          </DialogActions>
        </DialogBody>
      </DialogSurface>
    </Dialog>
  );
}
