import { useMemo, useState } from 'react';
import { Button, Checkbox, Input, makeStyles, mergeClasses, shorthands } from '@fluentui/react-components';
import { Search20Regular } from '@fluentui/react-icons';
import { designTokens, tokensUi } from '../../tokens/tokens';

const useStyles = makeStyles({
  raiz: {
    display: 'flex', flexDirection: 'column', width: '100%', minWidth: 0,
    ...shorthands.border('1px', 'solid', designTokens.colorCardBorder),
    ...shorthands.borderRadius(tokensUi.raio.sm), backgroundColor: designTokens.colorSurface, overflow: 'hidden',
  },
  topo: {
    display: 'flex', flexWrap: 'wrap', alignItems: 'center', gap: tokensUi.espaco.sm,
    ...shorthands.padding(tokensUi.espaco.sm, tokensUi.espaco.md),
    borderBottom: `1px solid ${designTokens.colorCardBorder}`, backgroundColor: designTokens.colorNeutralLight,
  },
  busca: { flexGrow: 1, minWidth: '200px' },
  contador: { fontSize: '12px', fontWeight: 600, color: designTokens.colorNeutralMedium, whiteSpace: 'nowrap' },
  lista: { maxHeight: '260px', overflowY: 'auto', display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(280px, 1fr))', alignContent: 'start' },
  item: {
    ...shorthands.padding('2px', tokensUi.espaco.md),
    borderBottom: `1px solid ${designTokens.colorCardBorder}`,
    transitionProperty: 'background-color', transitionDuration: tokensUi.duracao.rapido,
    ':hover': { backgroundColor: designTokens.colorNeutralLight },
  },
  checkbox: { width: '100%', cursor: 'pointer' },
  itemMarcado: { backgroundColor: designTokens.colorNeutralLight },
  vazio: { ...shorthands.padding(tokensUi.espaco.lg), textAlign: 'center', fontSize: '13px', color: designTokens.colorNeutralMedium },
});

export interface ListaSelecaoMultiplaProps {
  opcoes: { id: string; rotulo: string }[];
  selecionados: string[];
  /** Mesmo contrato do ChipCheckboxGroup: recebe função de atualização (`setState(prev => ...)`). */
  aoMudar: (atualizar: (atuais: string[]) => string[]) => void;
  placeholderBusca?: string;
  'aria-label'?: string;
}

function normalizar(s: string) {
  return s.normalize('NFD').replace(/[̀-ͯ]/g, '').toLowerCase();
}

// Seleção múltipla em lista rolável com busca (pedido do usuário 29/09: chips não escalam para
// ~200 funcionários). Busca ignora acentos/caixa; "Marcar todos" atua só sobre os resultados
// filtrados; selecionados que saem do filtro continuam selecionados.
export function ListaSelecaoMultipla({ opcoes, selecionados, aoMudar, placeholderBusca = 'Buscar por nome...', 'aria-label': ariaLabel }: ListaSelecaoMultiplaProps) {
  const e = useStyles();
  const [busca, setBusca] = useState('');
  const filtradas = useMemo(() => {
    const q = normalizar(busca.trim());
    return q ? opcoes.filter((o) => normalizar(o.rotulo).includes(q)) : opcoes;
  }, [busca, opcoes]);
  const setSel = useMemo(() => new Set(selecionados), [selecionados]);
  const todosVisiveisMarcados = filtradas.length > 0 && filtradas.every((o) => setSel.has(o.id));

  function alternar(id: string, marcado: boolean) {
    aoMudar((atuais) => (marcado ? (atuais.includes(id) ? atuais : [...atuais, id]) : atuais.filter((s) => s !== id)));
  }
  function alternarVisiveis() {
    const ids = filtradas.map((o) => o.id);
    aoMudar((atuais) => (todosVisiveisMarcados
      ? atuais.filter((s) => !ids.includes(s))
      : [...atuais, ...ids.filter((id) => !atuais.includes(id))]));
  }

  return (
    <div className={e.raiz}>
      <div className={e.topo}>
        <Input
          className={e.busca}
          contentBefore={<Search20Regular />}
          placeholder={placeholderBusca}
          aria-label={ariaLabel ? `Buscar em ${ariaLabel}` : 'Buscar'}
          value={busca}
          onChange={(_, d) => setBusca(d.value)}
        />
        <Button size="small" appearance="subtle" onClick={alternarVisiveis} disabled={filtradas.length === 0}>
          {todosVisiveisMarcados ? 'Desmarcar' : 'Marcar'} {busca.trim() ? 'resultados' : 'todos'}
        </Button>
        {selecionados.length > 0 && (
          <Button size="small" appearance="subtle" onClick={() => aoMudar(() => [])}>Limpar seleção</Button>
        )}
        <span className={e.contador} aria-live="polite">{selecionados.length} selecionado(s) de {opcoes.length}</span>
      </div>
      <div role="group" aria-label={ariaLabel} className={e.lista}>
        {filtradas.length === 0 ? (
          <div className={e.vazio}>Nenhum resultado para “{busca}”.</div>
        ) : filtradas.map((o) => (
          <div key={o.id} className={mergeClasses(e.item, setSel.has(o.id) && e.itemMarcado)}>
            <Checkbox className={e.checkbox} label={o.rotulo} checked={setSel.has(o.id)} onChange={(_, d) => alternar(o.id, !!d.checked)} />
          </div>
        ))}
      </div>
    </div>
  );
}
