import { makeStyles } from '@fluentui/react-components';
import { designTokens } from '@ui';

type TipoEscopo = 'hoje' | 'periodo' | 'acumulado';

const TEXTOS: Record<TipoEscopo, { rotulo: string; ajuda: string }> = {
  hoje: { rotulo: 'hoje', ajuda: 'Situação de hoje. Não muda com o filtro de período.' },
  periodo: { rotulo: 'no período', ajuda: 'Segue o filtro de período escolhido no topo.' },
  acumulado: { rotulo: 'acumulado', ajuda: 'Soma de todo o histórico. Não muda com o filtro de período.' },
};

const useStyles = makeStyles({
  base: {
    display: 'inline-block',
    marginLeft: '8px',
    padding: '1px 7px',
    borderRadius: '99px',
    border: `1px solid ${designTokens.colorCardBorder}`,
    fontSize: '10.5px',
    fontWeight: 700,
    letterSpacing: '0.04em',
    textTransform: 'uppercase',
    verticalAlign: '2px',
    color: designTokens.colorNeutralMedium,
    backgroundColor: designTokens.colorNeutralLight,
  },
});

// Diz de relance se o card mostra a situação de hoje, o que segue o filtro de período ou o acumulado.
// Antes ficava ambíguo: o filtro "Semana/Mês/Ano" parecia valer para tudo, mas muitos cards são fotos de hoje.
export function RotuloEscopo({ tipo }: { tipo: TipoEscopo }) {
  const estilos = useStyles();
  const texto = TEXTOS[tipo];
  return (
    <span className={estilos.base} title={texto.ajuda}>
      {texto.rotulo}
    </span>
  );
}
