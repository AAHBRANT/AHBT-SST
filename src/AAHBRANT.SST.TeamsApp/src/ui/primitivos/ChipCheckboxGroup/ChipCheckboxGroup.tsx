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
  /** Recebe uma função de atualização (padrão `setState(prev => ...)`), não a lista já calculada. */
  aoMudar: (atualizar: (atuais: string[]) => string[]) => void;
  'aria-label'?: string;
}

// Seleção múltipla em chips clicáveis inteiros (pedido do usuário 02/09; spec §3). Formaliza
// useCheckboxChipStyles, usado em matrizes, PT e DDS.
//
// `aoMudar` recebe uma função de atualização, não a lista pronta — dois cliques em sequência rápida
// disparam dois `onChange` antes deste componente re-renderizar com a prop `selecionados`
// atualizada; se a lista fosse calculada aqui (a partir da prop, que nesse instante ainda está
// desatualizada), o segundo clique sobrescrevia o resultado do primeiro (achado do usuário, 10/09:
// "clico em mais de um e o primeiro desseleciona", reproduzido em Responsáveis da APR e
// Participantes da Turma de treinamento). Repassando a atualização pro `set` funcional do
// componente-pai, o React garante que cada clique enfileirado usa o estado realmente mais recente.
export function ChipCheckboxGroup({ opcoes, selecionados, aoMudar, 'aria-label': ariaLabel }: ChipCheckboxGroupProps) {
  const e = useStyles();
  function alternar(id: string, marcado: boolean) {
    aoMudar((atuais) => (marcado ? [...atuais, id] : atuais.filter((s) => s !== id)));
  }
  return (
    <div role="group" aria-label={ariaLabel} className={e.grupo}>
      {opcoes.map((o) => (
        <Checkbox key={o.id} className={e.chip} label={o.rotulo} checked={selecionados.includes(o.id)} onChange={(_, d) => alternar(o.id, !!d.checked)} />
      ))}
    </div>
  );
}
