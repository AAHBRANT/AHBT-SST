import { useRef } from 'react';
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
//
// Mantém uma cópia dos selecionados num ref, atualizada de forma otimista a cada clique, em vez de
// calcular a próxima lista direto da prop `selecionados`: dois cliques em sequência rápida disparam
// dois `onChange` antes deste componente re-renderizar com a prop atualizada, e calcular a partir
// dela (que nesse instante ainda está desatualizada) fazia o segundo clique sobrescrever o resultado
// do primeiro (achado do usuário, 10/09: "clico em mais de um e o primeiro desseleciona",
// reproduzido em Responsáveis da APR e Participantes da Turma de treinamento). O ref garante que o
// segundo clique enxergue o resultado do primeiro mesmo sem essa re-renderização — sem exigir que
// nenhum dos consumidores (Responsáveis, matriz de EPI, Atividades do DDS, Equipe da PT etc.) troque
// o `setState` que já usa por uma forma funcional.
export function ChipCheckboxGroup({ opcoes, selecionados, aoMudar, 'aria-label': ariaLabel }: ChipCheckboxGroupProps) {
  const e = useStyles();
  const selecionadosRef = useRef(selecionados);
  selecionadosRef.current = selecionados;
  function alternar(id: string, marcado: boolean) {
    const proximos = marcado ? [...selecionadosRef.current, id] : selecionadosRef.current.filter((s) => s !== id);
    selecionadosRef.current = proximos;
    aoMudar(proximos);
  }
  return (
    <div role="group" aria-label={ariaLabel} className={e.grupo}>
      {opcoes.map((o) => (
        <Checkbox key={o.id} className={e.chip} label={o.rotulo} checked={selecionados.includes(o.id)} onChange={(_, d) => alternar(o.id, !!d.checked)} />
      ))}
    </div>
  );
}
