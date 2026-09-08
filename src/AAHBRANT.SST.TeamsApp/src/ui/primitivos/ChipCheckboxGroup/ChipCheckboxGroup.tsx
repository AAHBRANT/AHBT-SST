import { Checkbox, makeStyles, shorthands } from '@fluentui/react-components';
import { designTokens, tokensUi } from '../../tokens/tokens';

const useStyles = makeStyles({
  grupo: { display: 'flex', flexWrap: 'wrap', gap: tokensUi.espaco.sm },
  chip: {
    display: 'inline-flex', alignItems: 'center', backgroundColor: designTokens.colorNeutralLight,
    ...shorthands.borderRadius(tokensUi.raio.full), ...shorthands.border('1px', 'solid', designTokens.colorCardBorder),
    ...shorthands.padding('6px', '14px', '6px', '10px'), cursor: 'pointer',
    transitionProperty: 'background-color, border-color', transitionDuration: tokensUi.duracao.rapido,
    ':hover': { backgroundColor: designTokens.colorSurface, ...shorthands.borderColor(tokensUi.chrome.ativoFundo) },
  },
});

export interface ChipCheckboxGroupProps {
  opcoes: { id: string; rotulo: string }[];
  selecionados: string[];
  aoMudar: (ids: string[]) => void;
  'aria-label'?: string;
}

// Seleção múltipla em chips clicáveis inteiros (pedido do usuário 02/09; spec §3). Formaliza
// useCheckboxChipStyles, usado em matrizes, PT e DDS.
export function ChipCheckboxGroup({ opcoes, selecionados, aoMudar, 'aria-label': ariaLabel }: ChipCheckboxGroupProps) {
  const e = useStyles();
  function alternar(id: string, marcado: boolean) {
    aoMudar(marcado ? [...selecionados, id] : selecionados.filter((s) => s !== id));
  }
  return (
    <div role="group" aria-label={ariaLabel} className={e.grupo}>
      {opcoes.map((o) => (
        <Checkbox key={o.id} className={e.chip} label={o.rotulo} checked={selecionados.includes(o.id)} onChange={(_, d) => alternar(o.id, !!d.checked)} />
      ))}
    </div>
  );
}
